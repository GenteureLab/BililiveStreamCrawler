using BililiveStreamCrawler.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using WebSocketSharp;

namespace BililiveStreamCrawler.Client
{
    internal class ClientMain
    {
        private static readonly string requestPayload = JsonConvert.SerializeObject(new Command { Type = CommandType.Request });

        private static readonly List<StreamRoom> StreamRooms = new List<StreamRoom>();

        private static ClientConfig Config;

        private static WebSocket WebSocket;

        private static void Main(string[] args)
        {
            Config = JsonConvert.DeserializeObject<ClientConfig>(File.ReadAllText("config.json"));

            var ub = new UriBuilder(Config.Url)
            {
                Query = "name=" + Uri.EscapeDataString(Config.Name)
            };

            Console.WriteLine("Connecting: " + ub.Uri.AbsoluteUri);
            WebSocket = new WebSocket(ub.Uri.AbsoluteUri);
            if (WebSocket.IsSecure)
            {
                WebSocket.SslConfiguration.CheckCertificateRevocation = false;
                WebSocket.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
                WebSocket.SslConfiguration.ServerCertificateValidationCallback =
                    (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                    {
                        if (certificate is X509Certificate2 cert)
                        {
                            if (cert.Thumbprint == Config.Thumbprint)
                            {
                                return true;
                            }
                            else
                            {
                                Console.WriteLine("Server certificate thumbprint doesn't match!");
                                Console.WriteLine("Expected: " + Config.Thumbprint);
                                Console.WriteLine("Received: " + cert.Thumbprint);
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    };
            }

            WebSocket.OnOpen += Ws_OnOpen;
            WebSocket.OnMessage += Ws_OnMessage;
            WebSocket.OnError += Ws_OnError;
            WebSocket.OnClose += Ws_OnClose;

            WebSocket.Connect();

            Console.CancelKeyPress += (sender, e) => { WebSocket.Close(); Environment.Exit(0); };

            while (true)
            {
                Console.ReadKey(true);
            }
        }

        private static void Ws_OnClose(object sender, CloseEventArgs e)
        {
            Environment.Exit(1);
        }

        private static void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Environment.Exit(2);
        }

        private static void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            if (!e.IsText)
            {
                return;
            }

            var command = JsonConvert.DeserializeObject<Command>(e.Data);
            if (command.Type != CommandType.Issue) { return; }

            StreamRoom streamRoom = command.Room;

            StreamRooms.Add(streamRoom);
            Task.Run(() => StreamParser.Parse(streamRoom))
                .ContinueWith((task) =>
                {
                    StreamRooms.Remove(streamRoom);
                    if (!task.IsFaulted)
                    {
                        StreamMetadata data = task.Result;
                        WebSocket.Send(JsonConvert.SerializeObject(new Command { Type = CommandType.CompleteSuccess, Room = streamRoom, Metadata = data }));
                    }
                    else
                    {
                        string error = task.Exception.ToString();
                        WebSocket.Send(JsonConvert.SerializeObject(new Command { Type = CommandType.CompleteFailed, Room = streamRoom, Error = error }));
                    }
                    TryRequestNewTask();
                });
            TryRequestNewTask();
        }

        private static void Ws_OnOpen(object sender, EventArgs e)
        {
            TryRequestNewTask();
        }

        private static void TryRequestNewTask()
        {
            if (StreamRooms.Count < Config.MaxParallelTask)
            {
                RequestNewTask();
            }
        }

        private static void RequestNewTask()
        {
            WebSocket.Send(requestPayload);
        }
    }
}
