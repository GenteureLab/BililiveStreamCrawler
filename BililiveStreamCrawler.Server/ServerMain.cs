using BililiveStreamCrawler.Common;
using Dapper;
using FluentScheduler;
using MihaZupan;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using ChatId = Telegram.Bot.Types.ChatId;

namespace BililiveStreamCrawler.Server
{
    internal static class ServerMain
    {
        private static readonly object lockObject = new object();

        private static TelegramBotClient Telegram;

        private static ChatId TelegramChannelId;

        private static WorkDispatcherServer WebsocketServer;

        private static ServerConfig Config;

        /// <summary>
        /// 排队等待分析的直播间
        /// </summary>
        private static readonly LinkedListQueue<StreamRoom> RoomQueue = new LinkedListQueue<StreamRoom>();

        /// <summary>
        /// 空闲可以分配任务的 Client
        /// </summary>
        private static readonly LinkedListQueue<CrawlerClient> ClientQueue = new LinkedListQueue<CrawlerClient>();

        /// <summary>
        /// 连接上了的所有 Client
        /// </summary>
        private static readonly List<CrawlerClient> ConnectedClient = new List<CrawlerClient>();

        private static readonly EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private static void Main(string[] args)
        {
            Config = JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText("config.json"));

            SetupTelegram();
            SetupScheduler();

            ProcessRoom(new StreamRoom
            {
                Roomid = 1017,
                ShortId = 0,
                StreamId = 0,
                Title = "测试",
                Uid = 0,
                UserName = "测试用户名"
            });

            using (var server = new WebServer(Config.Url))
            {
                server.RegisterModule(new WebSocketsModule());

                WebsocketServer = new WorkDispatcherServer();
                server.Module<WebSocketsModule>().RegisterWebSocketsServer("/", WebsocketServer);

                var cts = new CancellationTokenSource();
                var task = server.RunAsync(cts.Token);

                Console.CancelKeyPress += (sender, e) => { Environment.Exit(-1); };

                while (true)
                {
                    Console.ReadKey(true);
                }
            }
        }

        /// <summary>
        /// 初始化定时任务
        /// </summary>
        private static void SetupScheduler()
        {
            var reg = new Registry();

            reg.Schedule(() => FetchNewRoom()).WithName("Fetch New Room").ToRunNow().AndEvery(2).Minutes();
            reg.Schedule(() => ReassignTimedoutTasks()).WithName("re-assign timed-out tasks").ToRunEvery(1).Minutes();
            reg.Schedule(() => RemoveOldTasks()).WithName("remove old tasks").ToRunEvery(5).Minutes();

            JobManager.Initialize(reg);
        }

        /// <summary>
        /// 给 client 找活干
        /// </summary>
        /// <param name="client"></param>
        private static void ProcessClient(CrawlerClient client)
        {
            lock (lockObject)
            {
                if (ClientQueue.Contains(client))
                {
                    // 已经在排队中
                    return;
                }
                else if (RoomQueue.Count > 0)
                {
                    var room = RoomQueue.Dequeue();
                    SendTask(client, room);
                }
                else
                {
                    ClientQueue.Enqueue(client);
                }
            }
        }

        /// <summary>
        /// 给直播间分配 client
        /// </summary>
        /// <param name="room"></param>
        private static void ProcessRoom(StreamRoom room)
        {
            lock (lockObject)
            {
                if (ClientQueue.Count > 0)
                {
                    var client = ClientQueue.Dequeue();
                    SendTask(client, room);
                }
                else
                {
                    RoomQueue.Enqueue(room);
                }
            }
        }

        /// <summary>
        /// 处理新 websocket 连接
        /// </summary>
        /// <param name="webSocket"></param>
        public static void NewClient(IWebSocketContext webSocket)
        {
            lock (lockObject)
            {
                SendTelegramMessage($"{webSocket.RequestUri.Query} 连接到了服务器\n#connect");
                ConnectedClient.Add(new CrawlerClient { Name = webSocket.RequestUri.Query, WebSocketContext = webSocket });
            }
        }

        /// <summary>
        /// 处理断开的 websocket 连接
        /// </summary>
        /// <param name="webSocket"></param>
        public static void RemoveClient(IWebSocketContext webSocket)
        {
            lock (lockObject)
            {
                var client = ConnectedClient.FirstOrDefault(x => x.WebSocketContext == webSocket);
                if (client != null)
                {
                    var sb = new StringBuilder();
                    sb.Append(client.Name);
                    sb.AppendLine(" 从服务器断开");
                    if (client.CurrentJobs.Count != 0)
                    {
                        sb.AppendLine("重新分配以下任务");
                        client.CurrentJobs.ForEach(x => sb.AppendLine(x.Roomid.ToString()));
                    }
                    sb.Append("#disconnect");
                    SendTelegramMessage(sb.ToString());

                    ClientQueue.Remove(client);
                    ConnectedClient.Remove(client);
                    client.CurrentJobs.ForEach(RetryRoom);
                }
            }
        }

        /// <summary>
        /// websocket 收到了消息
        /// </summary>
        /// <param name="webSocketContext"></param>
        /// <param name="data"></param>
        public static void ReceivedMessage(IWebSocketContext webSocketContext, string data)
        {
            lock (lockObject)
            {
                var client = ConnectedClient.FirstOrDefault(x => x.WebSocketContext == webSocketContext);
                if (client != null)
                {
                    try
                    {
                        var command = JsonConvert.DeserializeObject<Command>(data);

                        switch (command.Type)
                        {
                            case CommandType.Request:
                                Console.WriteLine("收到 Request: " + client.Name);
                                ProcessClient(client);
                                break;
                            case CommandType.CompleteSuccess:
                                {
                                    Console.WriteLine("收到 CompleteSuccess: " + client.Name);
                                    // TODO: Telegram
                                    var room = client.CurrentJobs.FirstOrDefault(x => x.Roomid == command.Room?.Roomid);
                                    if (room != null)
                                    {
                                        client.CurrentJobs.Remove(room);
                                        WriteResult(client.Name, room, command.Metadata);
                                    }
                                }
                                break;
                            case CommandType.CompleteFailed:
                                {
                                    Console.WriteLine("收到 CompleteFailed: " + client.Name);
                                    // TODO: Telegram
                                    var room = client.CurrentJobs.FirstOrDefault(x => x.Roomid == command.Room?.Roomid);
                                    if (room != null)
                                    {
                                        client.CurrentJobs.Remove(room);
                                        RetryRoom(room);
                                    }
                                }
                                break;
                            default:
                                webSocketContext.WebSocket.CloseAsync();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        SendTelegramMessage("ReceivedMessage Error\n" + ex.ToString());
                        webSocketContext.WebSocket.CloseAsync();
                    }
                }
                else
                {
                    webSocketContext.WebSocket.CloseAsync();
                }
            }
        }

        private static void WriteResult(string name, StreamRoom room, StreamMetadata metadata)
        {
            Exception exception = null;
            try
            {
                var roominfo = JsonConvert.SerializeObject(room);
                var onmetadata = JsonConvert.SerializeObject(metadata.FlvMetadata);
                using (var connection = new MySqlConnection(Config.MySql))
                {
                    int result = connection.Execute("INSERT INTO data(`roomid`,`clientname`,`roominfo`," +
                           "`flvhost`,`height`,`width`,`fps`,`encoder`,`video_datarate`,`audio_datarate`," +
                           "`profile`,`level`,`size`,`onmetadata`,`avc_dcr`) VALUES " +
                           "(@Roomid,@name,@roominfo,@FlvHost,@Height,@Width,@Fps,@Encoder," +
                           "@VideoDatarate,@AudioDatarate,@Profile,@Level,@TotalSize,@onmetadata,@AVCDecoderConfigurationRecord)",
                           new
                           {
                               room.Roomid,
                               name,
                               roominfo,
                               metadata.FlvHost,
                               metadata.Height,
                               metadata.Width,
                               metadata.Fps,
                               metadata.Encoder,
                               metadata.VideoDatarate,
                               metadata.AudioDatarate,
                               metadata.Profile,
                               metadata.Level,
                               metadata.TotalSize,
                               onmetadata,
                               metadata.AVCDecoderConfigurationRecord
                           });
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            var sb = new StringBuilder();
            sb.Append(name)
                .Append(" 提交的数据\n")
                .Append("房间号: ")
                .Append(room.Roomid)
                .Append("\n宽: ")
                .Append(metadata.Width)
                .Append(" 高: ")
                .Append(metadata.Height)
                .Append("\nFPS: ")
                .Append(metadata.Fps)
                .Append("\n视频码率: ")
                .Append(metadata.VideoDatarate)
                .Append(" 音频码率: ")
                .Append(metadata.AudioDatarate)
                .Append("\nProfile: ")
                .Append(metadata.Profile)
                .Append(" Level: ")
                .Append(metadata.Level)
                .Append("\n")
                .Append(metadata.Encoder);

            if (exception != null)
            {
                sb.Append("\n\n写数据库错误\n")
                    .Append(exception.ToString());
            }

            SendTelegramMessage(sb.ToString());
        }

        /// <summary>
        /// 向 client 发送要处理的直播间信息
        /// </summary>
        /// <param name="client"></param>
        /// <param name="room"></param>
        private static void SendTask(CrawlerClient client, StreamRoom room)
        {
            room.StartTime = DateTime.Now;
            client.CurrentJobs.Add(room);
            WebsocketServer.SendString(client.WebSocketContext, JsonConvert.SerializeObject(new Command { Type = CommandType.Issue, Room = room }));
        }

        /// <summary>
        /// 重新给直播间分配一个新 client
        /// </summary>
        /// <param name="room"></param>
        private static void RetryRoom(StreamRoom room)
        {
            if (ClientQueue.Count > 0)
            {
                var client = ClientQueue.Dequeue();
                SendTask(client, room);
            }
            else
            {
                RoomQueue.AddFirst(room);
            }
        }

        /// <summary>
        /// 移除长时间未处理的直播间任务
        /// </summary>
        private static void RemoveOldTasks()
        {
            lock (lockObject)
            {
                var now = DateTime.Now;
                var diff = TimeSpan.FromMinutes(15);
                List<StreamRoom> temp = new List<StreamRoom>();
                foreach (var room in RoomQueue.Rawlist)
                {
                    if (room.FetchTime + diff < now)
                    {
                        temp.Add(room);
                    }
                }
                temp.ForEach(x => RoomQueue.Rawlist.Remove(x));

                var sb = new StringBuilder();


                SendTelegramMessage("移除了旧任务"); // TODO telegram


            }
        }

        /// <summary>
        /// 重新分配超时的直播间任务
        /// </summary>
        private static void ReassignTimedoutTasks()
        {
            lock (lockObject)
            {
                var now = DateTime.Now;
                var diff = TimeSpan.FromMinutes(2);

                foreach (var client in ConnectedClient)
                {
                    client.CurrentJobs.Where(x => x.StartTime + diff < now).ToList().ForEach(x =>
                    {
                        client.CurrentJobs.Remove(x);
                        RetryRoom(x);
                    });

                }
            }
        }

        /// <summary>
        /// 从B站获取最新开播的直播间
        /// </summary>
        private static void FetchNewRoom()
        {

        }

        /// <summary>
        /// 初始化 telegram
        /// </summary>
        private static void SetupTelegram()
        {
            HttpToSocks5Proxy proxy = new HttpToSocks5Proxy(Config.Telegram.ProxyHostname, Config.Telegram.ProxyPort);
            Telegram = new TelegramBotClient(Config.Telegram.Token, proxy);
            TelegramChannelId = new ChatId(Config.Telegram.TargetId);
        }

        /// <summary>
        /// 向 telegram 发送消息
        /// </summary>
        /// <param name="message"></param>
        private static void SendTelegramMessage(string message) => Telegram.SendTextMessageAsync(TelegramChannelId, message, ParseMode.Default, true, true);
    }
}
