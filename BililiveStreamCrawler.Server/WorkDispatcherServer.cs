using BililiveStreamCrawler.Common;
using System.Collections.Generic;
using System.Net;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace BililiveStreamCrawler.Server
{
    internal class WorkDispatcherServer : WebSocketsServer
    {
        /// <summary>
        /// 排队等待分析的直播间
        /// </summary>
        private readonly LinkedListQueue<StreamRoom> RoomQueue = new LinkedListQueue<StreamRoom>();

        /// <summary>
        /// 空闲可以分配任务的 Client
        /// </summary>
        private readonly LinkedListQueue<CrawlerClient> ClientQueue = new LinkedListQueue<CrawlerClient>();

        /// <summary>
        /// 连接上了的所有 Client
        /// </summary>
        private readonly List<CrawlerClient> ConnectedClient = new List<CrawlerClient>();


        public WorkDispatcherServer() : base(true)
        {

        }

        public override string ServerName => "Work Dispatcher";

        protected override void OnClientConnected(IWebSocketContext context, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnClientDisconnected(IWebSocketContext context)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnFrameReceived(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnMessageReceived(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            throw new System.NotImplementedException();
        }
    }
}
