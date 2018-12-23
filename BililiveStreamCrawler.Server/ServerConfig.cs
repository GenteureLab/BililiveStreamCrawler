using Newtonsoft.Json;

namespace BililiveStreamCrawler.Server
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class ServerConfig
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        // Mysql connection string

        // Telegram bot token, channel id, proxy address

    }
}
