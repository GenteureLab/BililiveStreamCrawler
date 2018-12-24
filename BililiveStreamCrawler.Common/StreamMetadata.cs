using Newtonsoft.Json;
using System.Collections.Generic;

namespace BililiveStreamCrawler.Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StreamMetadata
    {
        /// <summary>
        /// 直播流服务器域名
        /// </summary>
        [JsonProperty("flvhost")]
        public string FlvHost { get; set; }

        /// <summary>
        /// 视频高（FLV数据）
        /// </summary>
        [JsonProperty("height")]
        public int Height { get; set; }

        /// <summary>
        /// 视频宽（FLV数据）
        /// </summary>
        [JsonProperty("width")]
        public int Width { get; set; }

        /// <summary>
        /// FPS（FLV数据）
        /// </summary>
        [JsonProperty("fps")]
        public int Fps { get; set; }

        /// <summary>
        /// 编码器（FLV数据）
        /// </summary>
        [JsonProperty("encoder")]
        public string Encoder { get; set; }

        /// <summary>
        /// 视频码率（FLV数据）
        /// </summary>
        [JsonProperty("video_datarate")]
        public int VideoDatarate { get; set; }

        /// <summary>
        /// 音频码率（FLV数据）
        /// </summary>
        [JsonProperty("audio_datarate")]
        public int AudioDatarate { get; set; }

        /// <summary>
        /// Profile（H264数据）
        /// </summary>
        [JsonProperty("profile")]
        public int Profile { get; set; }

        /// <summary>
        /// Level（H264数据）
        /// </summary>
        [JsonProperty("level")]
        public int Level { get; set; }

        /// <summary>
        /// 十秒直播流中视频部分的大小
        /// </summary>
        [JsonProperty("size")]
        public int TotalSize { get; set; }

        /// <summary>
        /// FLV Script Tag OnMetaData
        /// </summary>
        [JsonProperty("onmetadata")]
        public Dictionary<string, object> FlvMetadata { get; set; }

        /// <summary>
        /// 含有 AVCDecoderConfigurationRecord 的 FLV VIDEODATA （PDF 第9页）
        /// </summary>
        [JsonProperty("avc_dcr")]
        public byte[] AVCDecoderConfigurationRecord { get; set; }

    }
}
