using BililiveStreamCrawler.Common;
using FluentScheduler;
using MihaZupan;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Telegram.Bot;
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

        private static void Main(string[] args)
        {
            Config = JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText("config.json"));

            SetupTelegram();
            SetupScheduler();

            using (var server = new WebServer(Config.Url))
            {
                server.RegisterModule(new WebSocketsModule());

                WebsocketServer = new WorkDispatcherServer();
                server.Module<WebSocketsModule>().RegisterWebSocketsServer("/", WebsocketServer);

                var cts = new CancellationTokenSource();
                var task = server.RunAsync(cts.Token);

                Console.CancelKeyPress += (sender, e) =>
                {
                    cts.Cancel();
                };

                while (!cts.IsCancellationRequested) { }

                Console.WriteLine("Exiting!");
                Environment.Exit(0);
            }
        }

        private static void SetupScheduler()
        {
            var reg = new Registry();

            reg.Schedule(() => FetchNewRoom()).WithName("Fetch New Room").ToRunNow().AndEvery(2).Minutes();
            reg.Schedule(() => ReassignTimedoutTasks()).WithName("re-assign timed-out tasks").ToRunEvery(1).Minutes();

            JobManager.InitializeWithoutStarting(reg);
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
                    // client.SendJob(room);
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
                    // client.SendJob(room);
                }
                else
                {
                    RoomQueue.Enqueue(room);
                }
            }
        }

        public static void NewClient(IWebSocketContext webSocket)
        {
            ConnectedClient.Add(new CrawlerClient { Name = webSocket.RequestUri.Query, WebSocketContext = webSocket });
        }

        public static void RemoveClient(IWebSocketContext webSocket)
        {
            var client = ConnectedClient.FirstOrDefault(x => x.WebSocketContext == webSocket);
            if (client != null)
            {
                ClientQueue.Remove(client);
                ConnectedClient.Remove(client);
                client.CurrentJobs.ForEach(RetryRoom);
            }
        }

        public static void ReceivedMessage(IWebSocketContext webSocketContext, string data)
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
                            ProcessClient(client);
                            break;
                        case CommandType.CompleteSuccess:
                            // TODO
                            break;
                        case CommandType.CompleteFailed:
                            // TODO
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

        private static void RetryRoom(StreamRoom room)
        {

        }

        private static void ReassignTimedoutTasks()
        {

        }

        private static void FetchNewRoom()
        {

        }

        private static void SetupTelegram()
        {
            HttpToSocks5Proxy proxy = new HttpToSocks5Proxy(Config.Telegram.ProxyHostname, Config.Telegram.ProxyPort);
            Telegram = new TelegramBotClient(Config.Telegram.Token, proxy);
            TelegramChannelId = new ChatId(Config.Telegram.TargetId);
        }

        private static void SendTelegramMessage(string message)
        {

        }
    }
}
