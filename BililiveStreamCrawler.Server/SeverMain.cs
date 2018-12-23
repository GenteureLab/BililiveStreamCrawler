using FluentScheduler;
using MihaZupan;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using Telegram.Bot;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using ChatId = Telegram.Bot.Types.ChatId;

namespace BililiveStreamCrawler.Server
{
    internal class SeverMain
    {

        private static TelegramBotClient Telegram;

        private static ChatId TelegramChannelId;

        private static ServerConfig Config;

        private static void Main(string[] args)
        {
            Config = JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText("config.json"));

            SetupTelegram();

            SetupScheduler();

            using (var server = new WebServer(Config.Url))
            {
                server.RegisterModule(new WebSocketsModule());

                var dispatcher = new WorkDispatcherServer();
                server.Module<WebSocketsModule>().RegisterWebSocketsServer("/", dispatcher);

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
    }
}
