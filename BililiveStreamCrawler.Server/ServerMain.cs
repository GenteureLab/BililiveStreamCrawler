using BililiveStreamCrawler.Common;
using Dapper;
using FluentScheduler;
using MihaZupan;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
        // private static readonly LinkedListQueue<CrawlerClient> ClientQueue = new LinkedListQueue<CrawlerClient>();

        /// <summary>
        /// 连接上了的所有 Client
        /// </summary>
        private static readonly List<CrawlerClient> ConnectedClient = new List<CrawlerClient>();

        /// <summary>
        /// 刚才已经处理过的直播间
        /// </summary>
        private static readonly LinkedList<int> ProcessedRoom = new LinkedList<int>();

        /// <summary>
        /// 等待重试的直播间
        /// </summary>
        private static readonly List<StreamRoom> RetryQueue = new List<StreamRoom>();

        /// <summary>
        /// telegram 消息
        /// </summary>
        private static readonly StringBuilder TelegramMessage = new StringBuilder();

        private static void Main(string[] args)
        {
            Config = JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText("config.json"));

            SetupTelegram();
            SetupScheduler();
            FetchNewRoom(true);

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

            reg.Schedule(() => SendTelegramMessage()).WithName("send telegram message").ToRunEvery(6).Seconds();
            reg.Schedule(() => IssueTasks()).WithName("issue tasks").ToRunEvery(2).Seconds();
            reg.Schedule(() => FetchNewRoom()).WithName("Fetch New Room").ToRunEvery(90).Seconds();
            reg.Schedule(() => ReassignTimedoutTasks()).WithName("re-assign timed-out tasks").ToRunEvery(10).Seconds();
            reg.Schedule(() => RemoveOldTasks()).WithName("remove old tasks").ToRunEvery(3).Minutes();

            JobManager.Initialize(reg);
        }

        /// <summary>
        /// 任务下发逻辑
        /// </summary>
        private static void IssueTasks()
        {
            lock (lockObject)
            {
                var now = DateTime.Now;
                RetryQueue.RemoveAll(room =>
                {
                    if (room.RetryAfter < now)
                    {
                        var client = ConnectedClient.FirstOrDefault(x => x.MaxParallelTask > x.CurrentJobs.Count && !room.Clients.Contains(x.Name));
                        if (client == default(CrawlerClient))
                        {
                            client = ConnectedClient.FirstOrDefault(x => x.MaxParallelTask > x.CurrentJobs.Count);
                        }
                        if (client == default(CrawlerClient))
                        {
                            return false;
                        }

                        ConnectedClient.Remove(client);
                        ConnectedClient.Add(client);
                        SendTask(client, room);

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
                while (RoomQueue.Count > 0)
                {
                    var client = ConnectedClient.FirstOrDefault(x => x.MaxParallelTask > x.CurrentJobs.Count);
                    if (client == null)
                    {
                        return;
                    }
                    else
                    {
                        var room = RoomQueue.Dequeue();
                        ConnectedClient.Remove(client);
                        ConnectedClient.Add(client);
                        SendTask(client, room);
                    }
                }
            }
        }

        /// <summary>
        /// 处理新 websocket 连接
        /// </summary>
        /// <param name="webSocket"></param>
        public static void NewClient(IWebSocketContext webSocket)
        {
            var query = webSocket.RequestUri.Query
                .TrimStart('?').Split('&')
                .Select(x => x.Split('='))
                .Where(x => x.Length == 2)
                .ToDictionary(x => Uri.UnescapeDataString(x[0]), x => Uri.UnescapeDataString(x[1]));

            if (!query.TryGetValue("version", out string version) || version != Static.VERSION)
            {
                webSocket.WebSocket.CloseAsync();
                return;
            }

            if (!query.ContainsKey("max"))
            {
                webSocket.WebSocket.CloseAsync();
                return;
            }

            string name = (webSocket as Unosquare.Net.WebSocketContext).Headers.Get("CertSdn").Remove(0, 3);

            if ((!int.TryParse(query["max"], out int max)) || name.Length < 3 || name.Length > 20 || (!Regex.IsMatch(name, "[A-Za-z0-9]+")))
            {
                webSocket.WebSocket.CloseAsync();
                return;
            }

            if (ConnectedClient.Any(x => x.Name == name))
            {
                webSocket.WebSocket.CloseAsync();
                return;
            }

            CrawlerClient newclient = new CrawlerClient { Name = name, MaxParallelTask = max, WebSocketContext = webSocket };
            lock (lockObject)
            {
                ConnectedClient.Add(newclient);
                TelegramMessage.Append(DateTime.Now.ToString("HH:mm:ss.f")).Append("\n").Append(newclient.Name).Append(" #connect\n\n");
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

                    TelegramMessage
                        .Append(DateTime.Now.ToString("HH:mm:ss.f"))
                        .Append("\n")
                        .Append(client.Name)
                        .Append(" #disconnect");
                    if (client.CurrentJobs.Count != 0)
                    {
                        TelegramMessage.Append("\n重分配任务 ");
                        client.CurrentJobs.ForEach(x => TelegramMessage.Append(x.Roomid.ToString()).Append(" "));
                    }
                    TelegramMessage.Append("\n\n");

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
                                // Console.WriteLine("收到 Request: " + client.Name);
                                // ProcessClient(client);
                                break;
                            case CommandType.CompleteSuccess:
                                {
                                    Console.WriteLine("收到 CompleteSuccess: " + client.Name);
                                    var room = client.CurrentJobs.FirstOrDefault(x => x.Roomid == command.Room?.Roomid);
                                    if (room != null)
                                    {
                                        client.CurrentJobs.Remove(room);
                                        WriteResult(client.Name, room, command.Metadata);
                                    }
                                    else
                                    {
                                        TelegramMessage
                                            .Append(DateTime.Now.ToString("HH:mm:ss.f"))
                                            .Append("\n")
                                            .Append(client.Name)
                                            .Append(" 尝试提交 ")
                                            .Append(command.Room?.Roomid)
                                            .Append(" #rejected\n\n");
                                    }
                                }
                                break;
                            case CommandType.CompleteFailed:
                                {
                                    Console.WriteLine("收到 CompleteFailed: " + client.Name);
                                    var room = client.CurrentJobs.FirstOrDefault(x => x.Roomid == command.Room?.Roomid);
                                    if (room != null)
                                    {
                                        client.CurrentJobs.Remove(room);
                                        if (command.Error.Contains("System.Exception: StatusCode: NotFound"))
                                        {
                                            RetryRoomAfter(TimeSpan.FromMinutes(3), room);
                                        }
                                        else
                                        {
                                            RetryRoom(room);
                                        }
                                        Console.WriteLine(client.Name + " 处理 " + room.Roomid + " 时出错，将重新分配任务 #failed\n\n" + command.Error);
                                    }
                                    else
                                    {
                                        TelegramMessage
                                            .Append(DateTime.Now.ToString("HH:mm:ss.f"))
                                            .Append("\n")
                                            .Append(client.Name)
                                            .Append(" 尝试报告 ")
                                            .Append(command.Room?.Roomid)
                                            .Append(" #rejected\n\n");
                                        Console.WriteLine(client.Name + " 房间号:" + command.Room?.Roomid);
                                        Console.WriteLine(command.Error);
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
                        Console.WriteLine("ReceivedMessage Error\n" + ex.ToString());
                        webSocketContext.WebSocket.CloseAsync();
                    }
                }
                else
                {
                    webSocketContext.WebSocket.CloseAsync();
                }
            }
        }

        /// <summary>
        /// 把收集到的数据写入数据库
        /// </summary>
        /// <param name="name"></param>
        /// <param name="room"></param>
        /// <param name="metadata"></param>
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

            TelegramMessage
                .Append(DateTime.Now.ToString("HH:mm:ss.f"))
                .Append("\n")
                .Append(name)
                .Append(" #success ")
                .Append(room.Roomid)
                .Append("\nW: ")
                .Append(metadata.Width)
                .Append(" H: ")
                .Append(metadata.Height)
                .Append(" F: ")
                .Append(metadata.Fps)
                .Append("\nV: ")
                .Append(metadata.VideoDatarate)
                .Append(" A: ")
                .Append(metadata.AudioDatarate)
                .Append("\nP: ")
                .Append(metadata.Profile)
                .Append(" L: ")
                .Append(metadata.Level)
                .Append("\nE: ")
                .Append(metadata.Encoder);

            if (exception != null)
            {
                TelegramMessage
                    .Append("\n写数据库错误");
                Console.WriteLine("写数据库错误 " + exception.ToString());
            }

            TelegramMessage.Append("\n\n");
        }

        /// <summary>
        /// 向 client 发送要处理的直播间信息
        /// </summary>
        /// <param name="client"></param>
        /// <param name="room"></param>
        private static void SendTask(CrawlerClient client, StreamRoom room)
        {
            room.StartTime = DateTime.Now;
            room.Clients.Add(client.Name);
            client.CurrentJobs.Add(room);
            WebsocketServer.SendString(client.WebSocketContext, JsonConvert.SerializeObject(new Command { Type = CommandType.Issue, Room = room }));
            Console.WriteLine($@"下发 {room.Roomid} 给 {client.Name}");
        }

        /// <summary>
        /// 重新给直播间分配一个新 client
        /// </summary>
        /// <param name="room"></param>
        private static void RetryRoom(StreamRoom room)
        {
            if (++room.RetryTime >= 3)
            {
                return;
            }

            var client = ConnectedClient.FirstOrDefault(x => x.MaxParallelTask > x.CurrentJobs.Count);
            if (client == null)
            {
                RoomQueue.AddFirst(room);
            }
            else
            {
                ConnectedClient.Remove(client);
                ConnectedClient.Add(client);
                SendTask(client, room);
            }
        }

        /// <summary>
        /// 在一定时间后重试
        /// </summary>
        /// <param name="time"></param>
        /// <param name="room"></param>
        private static void RetryRoomAfter(TimeSpan time, StreamRoom room)
        {
            if (++room.RetryTime >= 3)
            {
                return;
            }

            room.RetryAfter = DateTime.Now + time;

            RetryQueue.Add(room);
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

                if (temp.Count > 0)
                {
                    TelegramMessage
                        .Append(DateTime.Now.ToString("HH:mm:ss.f"))
                        .Append("\n")
                        .Append("#remove 处理速度跟不上\n移除了 ")
                        .Append(temp.Count)
                        .Append(" 个任务\n\n");
                }
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
                var diff = TimeSpan.FromMinutes(5);

                foreach (var client in ConnectedClient)
                {
                    client.CurrentJobs.Where(x => x.StartTime + diff < now).ToList().ForEach(x =>
                    {
                        TelegramMessage.Append(DateTime.Now.ToString("HH:mm:ss.f")).Append("\n")
                            .Append(client.Name).Append(" #timeout ").Append(x.Roomid).Append("\n\n");

                        client.CurrentJobs.Remove(x);
                        RetryRoom(x);
                    });

                }
            }
        }

        /// <summary>
        /// 从B站获取最新开播的直播间
        /// </summary>
        private static void FetchNewRoom(bool dry = false)
        {
            var c = new WebClient();
            c.Headers.Add(HttpRequestHeader.Accept, "application/json, text/plain, */*");
            c.Headers.Add(HttpRequestHeader.Referer, "https://live.bilibili.com/all");
            c.Headers.Add("Origin", "https://live.bilibili.com");
            c.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36");

            string rawdata = c.DownloadString("https://api.live.bilibili.com/room/v1/Area/getListByAreaID?areaId=0&sort=livetime&pageSize=300&page=1");
            JObject j = JObject.Parse(rawdata);
            JArray rawlist = j["data"] as JArray;

            lock (lockObject)
            {
                foreach (JToken rawroom in rawlist)
                {
                    try
                    {
                        int roomid = rawroom["roomid"].ToObject<int>();
                        if (!ProcessedRoom.Contains(roomid))
                        {
                            ProcessedRoom.AddLast(roomid);
                            if (!dry)
                            {
                                RoomQueue.Enqueue(rawroom.ToObject<StreamRoom>());
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }

            while (ProcessedRoom.Count > 600)
            {
                ProcessedRoom.RemoveFirst();
            }
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
        private static void SendTelegramMessage()
        {
            lock (lockObject)
            {
                if (TelegramMessage.Length > 0)
                {
                    TelegramMessage
                        .Append(DateTime.Now.ToString("HH:mm:ss.f"))
                        .Append("\n排队中: ")
                        .Append(RoomQueue.Count)
                        .Append("\n处理中: ")
                        .Append(ConnectedClient.Aggregate(0, (c, x) => c += x.CurrentJobs.Count))
                        .Append("\n等待重试: ")
                        .Append(RetryQueue.Count)
                        .Append("\nWorker: ")
                        .Append(ConnectedClient.Count)
                        .Append("\n总并发: ")
                        .Append(ConnectedClient.Aggregate(0, (c, x) => c += x.MaxParallelTask));

                    Telegram.SendTextMessageAsync(TelegramChannelId, TelegramMessage.ToString(), ParseMode.Default, true, true);

                    TelegramMessage.Clear();
                }
            }
        }
    }
}
