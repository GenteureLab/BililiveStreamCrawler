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
        private static readonly object lockObject = new object();

        private static readonly string requestPayload = JsonConvert.SerializeObject(new Command { Type = CommandType.Request });

        private static readonly List<StreamRoom> StreamRooms = new List<StreamRoom>();

        private static ClientConfig Config;

        private static WebSocket WebSocket;

        private static void Main(string[] args)
        {
            Config = JsonConvert.DeserializeObject<ClientConfig>(File.ReadAllText("config.json"));

            var ub = new UriBuilder(Config.Url)
            {
                Query = "max=" + Config.MaxParallelTask + "&version=" + Static.VERSION
            };

            Console.WriteLine("Connecting: " + ub.Uri.AbsoluteUri);
            WebSocket = new WebSocket(ub.Uri.AbsoluteUri);
            if (WebSocket.IsSecure)
            {
                WebSocket.SslConfiguration.CheckCertificateRevocation = false;
                WebSocket.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
                WebSocket.SslConfiguration.ClientCertificates = new X509CertificateCollection(new[] { new X509Certificate2("client.pfx") });
                WebSocket.SslConfiguration.ClientCertificateSelectionCallback =
                    (object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers) =>
                    {
                        return localCertificates[0];
                    };
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

            WebSocket.OnMessage += Ws_OnMessage;
            WebSocket.OnError += Ws_OnError;
            WebSocket.OnClose += Ws_OnClose;

            WebSocket.Connect();

            Console.WriteLine("Connected!");

            Console.CancelKeyPress += (sender, e) => { WebSocket.Close(); Environment.Exit(0); };

            while (true)
            {
                Console.ReadKey(true);
            }
        }

        private static void Ws_OnClose(object sender, CloseEventArgs e)
        {
            Console.WriteLine("Connection Closed!");
            Console.WriteLine(e.Reason);
            Environment.Exit(1);
        }

        private static void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Console.WriteLine("Connection Error!");
            Console.WriteLine(e.Message);
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

            Console.WriteLine("New task: " + streamRoom.Roomid);

            StreamRooms.Add(streamRoom);
            Task.Run(() => StreamParser.Parse(streamRoom))
                .ContinueWith((task) =>
                {
                    StreamRooms.Remove(streamRoom);
                    if (task.IsFaulted)
                    {
                        string error = task.Exception.ToString();
                        Console.WriteLine("ERROR: " + streamRoom.Roomid + " " + task.Exception.InnerException.Message);
                        WebSocket.Send(JsonConvert.SerializeObject(new Command { Type = CommandType.CompleteFailed, Room = streamRoom, Error = error }));
                    }
                    else if (task.IsCanceled)
                    {
                        Console.WriteLine("ERROR: GetAsync Timed Out");
                        WebSocket.Send(JsonConvert.SerializeObject(new Command { Type = CommandType.CompleteFailed, Room = streamRoom, Error = "GetAsync Timed Out" }));
                    }
                    else
                    {
                        StreamMetadata data = task.Result;
                        Console.WriteLine("Success: " + streamRoom.Roomid);
                        WebSocket.Send(JsonConvert.SerializeObject(new Command { Type = CommandType.CompleteSuccess, Room = streamRoom, Metadata = data }));
                    }
                });
        }
    }
}
