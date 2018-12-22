using Newtonsoft.Json;
using System;

namespace BililiveStreamCrawler.Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StreamRoom
    {
        /// <summary>
        /// 主站用户 ID
        /// </summary>
        [JsonProperty("uid")]
        public int Uid { get; set; }

        /// <summary>
        /// 直播间原始房间号
        /// </summary>
        [JsonProperty("roomid")]
        public int Roomid { get; set; }

        /// <summary>
        /// 直播间标题
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// 不知道什么神奇的东西
        /// </summary>
        [JsonProperty("stream_id")]
        public int StreamId { get; set; }

        /// <summary>
        /// 主播用户名
        /// </summary>
        [JsonProperty("uname")]
        public string UserName { get; set; }

        /// <summary>
        /// 直播间短号，无则为0
        /// </summary>
        [JsonProperty("short_id")]
        public int ShortId { get; set; }

        /// <summary>
        /// 任务开始执行的时间
        /// </summary>
        public DateTime? StartTime { get; set; }

    }
}
