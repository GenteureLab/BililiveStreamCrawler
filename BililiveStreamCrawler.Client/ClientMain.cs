using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp;

namespace BililiveStreamCrawler.Client
{
    internal class ClientMain
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var ws = new WebSocket("wss://echo.websocket.org");

            ws.SslConfiguration.CheckCertificateRevocation = false;
            ws.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
            ws.SslConfiguration.ServerCertificateValidationCallback =
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

            ws.Connect();

            ws.Close();

        }
    }
}
