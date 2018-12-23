using System.Net;
using System.Text;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace BililiveStreamCrawler.Server
{
    internal class WorkDispatcherServer : WebSocketsServer
    {
        public override string ServerName => "Work Dispatcher";

        public WorkDispatcherServer() : base(true)
        {
            Encoding = Encoding.UTF8;
        }

        public void SendString(IWebSocketContext webSocketContext, string text) => Send(webSocketContext, text);

        protected override void OnClientConnected(IWebSocketContext context, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            ServerMain.NewClient(context);
        }

        protected override void OnClientDisconnected(IWebSocketContext context)
        {
            ServerMain.RemoveClient(context);
        }

        protected override void OnFrameReceived(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {

        }

        protected override void OnMessageReceived(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            // 0 = Text message
            // 1 = Binary message
            // 2 = Close
            if (result.MessageType == 0)
            {
                ServerMain.ReceivedMessage(context, Encoding.UTF8.GetString(buffer));
            }
        }
    }
}
