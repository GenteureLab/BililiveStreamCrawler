using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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
        /// 子分区 id
        /// </summary>
        [JsonProperty("area_v2_id")]
        public int AreaV2Id { get; set; }

        /// <summary>
        /// 子分区名
        /// </summary>
        [JsonProperty("area_v2_name")]
        public string AreaV2Name { get; set; }

        /// <summary>
        /// 父分区 id
        /// </summary>
        [JsonProperty("area_v2_parent_id")]
        public int AreaV2ParentId { get; set; }

        /// <summary>
        /// 父分区名
        /// </summary>
        [JsonProperty("area_v2_parent_name")]
        public string AreaV2ParentName { get; set; }

        /// <summary>
        /// 任务开始执行的时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 在此时间后重试请求
        /// </summary>
        public DateTime RetryAfter { get; set; } = DateTime.Now;

        /// <summary>
        /// 此任务生成时间（Object 创建时间）
        /// </summary>
        public DateTime FetchTime { get; } = DateTime.Now;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryTime { get; set; } = 0;

        /// <summary>
        /// 尝试过此直播间的 client 名字
        /// </summary>
        public List<string> Clients { get; set; } = new List<string>();
    }
}
