using BililiveStreamCrawler.Common;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace BililiveStreamCrawler.Client
{
    internal class ClientMain
    {
        private static readonly List<StreamRoom> StreamRooms = new List<StreamRoom>();

        private static readonly int MaxParallelTask = 3;

        private static WebSocket WebSocket;

        private static void Main(string[] args)
        {
            // TODO: parse config file

            WebSocket = new WebSocket("wss://echo.websocket.org");

            WebSocket.SslConfiguration.CheckCertificateRevocation = false;
            WebSocket.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
            WebSocket.SslConfiguration.ServerCertificateValidationCallback =
                (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                {
                    if (certificate is X509Certificate2 cert)
                    {
                        Console.WriteLine("Server certificate thumbprint: " + cert.Thumbprint);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                };

            WebSocket.OnOpen += Ws_OnOpen;
            WebSocket.OnMessage += Ws_OnMessage;
            WebSocket.OnError += Ws_OnError;
            WebSocket.OnClose += Ws_OnClose;

            WebSocket.Connect();


            while (WebSocket.IsAlive)
            {
                Thread.Sleep(500);
            }

            try
            {
                WebSocket.Close();
            }
            catch (Exception) { }
        }

        private static void Ws_OnClose(object sender, CloseEventArgs e)
        {

        }

        private static void Ws_OnError(object sender, ErrorEventArgs e)
        {

        }

        private static void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            StreamRoom streamRoom = null; // TODO: parse data from websocket

            StreamRooms.Add(streamRoom);
            Task.Run(() => StreamParser.Parse(streamRoom))
                .ContinueWith((task) =>
                {
                    StreamRooms.Remove(streamRoom);
                    if (!task.IsFaulted)
                    {
                        StreamMetadata data = task.Result;
                        // ws.Send(data);
                        // TODO: report success
                    }
                    else
                    {
                        string error = task.Exception.ToString();
                        // ws.Send(error);
                        // TODO: report error
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
            if (StreamRooms.Count < MaxParallelTask)
            {
                RequestNewTask();
            }
        }

        private static void RequestNewTask()
        {
            // TODO
        }
    }
}
