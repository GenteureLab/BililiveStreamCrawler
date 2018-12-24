using BililiveStreamCrawler.Common;
using System.Collections.Generic;
using Unosquare.Labs.EmbedIO;

namespace BililiveStreamCrawler.Server
{
    public class CrawlerClient
    {
        public string Name { get; set; }

        public int MaxParallelTask { get; set; }

        public IWebSocketContext WebSocketContext { get; set; }

        public List<StreamRoom> CurrentJobs { get; } = new List<StreamRoom>();

    }
}
