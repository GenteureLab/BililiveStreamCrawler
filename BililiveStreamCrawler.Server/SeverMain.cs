using System;
using System.Threading;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace BililiveStreamCrawler.Server
{
    internal class SeverMain
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            using (var server = new WebServer("http://127.0.0.1:9696/"))
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
    }
}
