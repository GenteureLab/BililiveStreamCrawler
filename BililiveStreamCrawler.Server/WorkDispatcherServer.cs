using BililiveStreamCrawler.Common;
using System.Collections.Generic;
using System.Net;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace BililiveStreamCrawler.Server
{
    internal class WorkDispatcherServer : WebSocketsServer
    {
        private readonly object lockObject = new object();

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

        public override string ServerName => "Work Dispatcher";

        public WorkDispatcherServer() : base(true)
        {

        }

        private void ProcessClient(CrawlerClient client)
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

        private void ProcessRoom(StreamRoom room)
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
