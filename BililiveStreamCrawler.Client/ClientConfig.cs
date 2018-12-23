using Newtonsoft.Json;

namespace BililiveStreamCrawler.Client
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class ClientConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("thumbprint")]
        public string Thumbprint { get; set; }

        [JsonProperty("max_parallel_task")]
        public int MaxParallelTask { get; set; }
    }
}
