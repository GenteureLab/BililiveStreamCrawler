using Newtonsoft.Json;

namespace BililiveStreamCrawler.Server
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class ServerConfig
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        // Mysql connection string

        [JsonProperty("telegram")]
        public TelegramConfig Telegram { get; set; }

        [JsonObject(MemberSerialization.OptIn)]
        internal class TelegramConfig
        {
            [JsonProperty("token")]
            public string Token { get; set; }

            [JsonProperty("id")]
            public long TargetId { get; set; }

            [JsonProperty("host")]
            public string ProxyHostname { get; set; }

            [JsonProperty("port")]
            public int ProxyPort { get; set; }
        }
    }
}
