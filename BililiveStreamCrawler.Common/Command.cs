using Newtonsoft.Json;

namespace BililiveStreamCrawler.Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Command
    {
        /// <summary>
        /// 命令类型
        /// </summary>
        [JsonProperty("type")]
        public CommandType Type { get; set; }

        /// <summary>
        /// 下发任务时使用的直播间信息
        /// </summary>
        [JsonProperty("room")]
        public StreamRoom Room { get; set; }

        /// <summary>
        /// 任务完成时的回报信息
        /// </summary>
        [JsonProperty("metadata")]
        public StreamMetadata Metadata { get; set; }

        /// <summary>
        /// 任务出错时的错误信息
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
