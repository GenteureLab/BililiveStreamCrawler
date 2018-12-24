using BililiveRecorder.FlvProcessor;
using BililiveStreamCrawler.Common;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace BililiveStreamCrawler.Client
{
    internal static class StreamParser
    {
        public static async Task<StreamMetadata> Parse(StreamRoom room)
        {
            StreamMetadata streamMetadata = new StreamMetadata();

            IFlvStreamProcessor Processor = new FlvStreamProcessor(null, (byte[] data) => new FlvMetadata(data), () => new FlvTag())
                .Initialize(null, null, EnabledFeature.ClipOnly, AutoCuttingMode.Disabled);

            Stream _stream = null;
            HttpResponseMessage _response = null;

            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;


            bool avc = false;
            Processor.TagProcessed += (sender, e) =>
            {
                IFlvTag t = e.Tag;

                if (t.TimeStamp > 10 * 1000)
                {
                    try
                    {
                        cancellationTokenSource.Cancel();
                    }
                    catch (Exception) { }
                    return;
                }

                if (!avc && t.IsVideoKeyframe && t.Profile != -1)
                {
                    avc = true;
                    streamMetadata.Profile = t.Profile;
                    streamMetadata.Level = t.Level;
                    streamMetadata.AVCDecoderConfigurationRecord = t.Data;
                }
                if (t.TagType == TagType.VIDEO)
                {
                    streamMetadata.TotalSize += t.TagSize;
                }
            };


            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36");
                    client.DefaultRequestHeaders.Referrer = new Uri("https://live.bilibili.com");
                    client.DefaultRequestHeaders.Add("Origin", "https://live.bilibili.com");

                    string flv_path = GetPlayUrl(room.Roomid);

                    streamMetadata.FlvHost = new Uri(flv_path).Host;

                    _response = await client.GetAsync(flv_path, HttpCompletionOption.ResponseHeadersRead);
                }

                if (_response.StatusCode != HttpStatusCode.OK)
                {
                    _CleanupFlvRequest();
                    throw new Exception("StatusCode: " + _response.StatusCode);
                }
                else
                {
                    // Processor = newIFlvStreamProcessor().Initialize(GetStreamFilePath, GetClipFilePath, _config.EnabledFeature, _config.CuttingMode);
                    // Processor.ClipLengthFuture = _config.ClipLengthFuture;
                    // Processor.ClipLengthPast = _config.ClipLengthPast;
                    // Processor.CuttingNumber = _config.CuttingNumber;

                    _stream = await _response.Content.ReadAsStreamAsync();
                    _stream.ReadTimeout = 3 * 1000;

                    await _ReadStreamLoop();

                    {
                        var metadata = Processor.Metadata.Meta.ToDictionary(
                                x => x.Key.Replace("\0", ""),
                                x => (x.Value is string str) ? str.Replace("\0", "") : x.Value
                            );

                        streamMetadata.Width = (metadata["width"] != null)
                            ? ((metadata["width"] is int w)
                                ? w
                                : int.Parse(metadata["width"].ToString()))
                            : ((metadata["displayWidth"] is int dw)
                                ? dw
                                : int.Parse(metadata["displayWidth"].ToString()));

                        streamMetadata.Height = (metadata["height"] != null)
                        ? ((metadata["height"] is int h)
                            ? h
                            : int.Parse(metadata["height"].ToString()))
                        : ((metadata["displayHeight"] is int dh)
                            ? dh
                            : int.Parse(metadata["displayHeight"].ToString()));

                        streamMetadata.Fps = (metadata["framerate"] != null)
                        ? ((metadata["framerate"] is int f)
                            ? f
                            : int.Parse(metadata["framerate"].ToString()))
                        : ((metadata["fps"] is int df)
                            ? df
                            : int.Parse(metadata["fps"].ToString()));

                        streamMetadata.VideoDatarate = (metadata["videodatarate"] is int vdr) ? vdr : int.Parse(metadata["videodatarate"].ToString());
                        streamMetadata.AudioDatarate = (metadata["audiodatarate"] is int adr) ? adr : int.Parse(metadata["audiodatarate"].ToString());
                        streamMetadata.Encoder = metadata["encoder"].ToString();

                        streamMetadata.FlvMetadata = metadata;
                    }
                }
            }
            finally
            {
                _CleanupFlvRequest();
            }
            return streamMetadata;

            async Task _ReadStreamLoop()
            {
                try
                {
                    const int BUF_SIZE = 1024 * 8;
                    byte[] buffer = new byte[BUF_SIZE];
                    while (!token.IsCancellationRequested)
                    {
                        int bytesRead = await _stream.ReadAsync(buffer, 0, BUF_SIZE, token);
                        if (bytesRead != 0)
                        {
                            if (bytesRead != BUF_SIZE)
                            {
                                Processor.AddBytes(buffer.Take(bytesRead).ToArray());
                            }
                            else
                            {
                                Processor.AddBytes(buffer);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is ObjectDisposedException && token.IsCancellationRequested) { return; }

                    throw;
                }
            }
            void _CleanupFlvRequest()
            {
                if (Processor != null)
                {
                    Processor.FinallizeFile();
                    Processor.Dispose();
                    Processor = null;
                }
                _stream?.Dispose();
                _stream = null;
                _response?.Dispose();
                _response = null;

            }
        }

        private static string GetPlayUrl(int roomid)
        {
            string url = $@"https://api.live.bilibili.com/room/v1/Room/playUrl?cid={roomid}&quality=0&platform=web";
            if (HttpGetJson(url)?["data"]?["durl"] is JArray array)
            {
                List<string> urls = new List<string>();
                for (int i = 0; i < array.Count; i++)
                {
                    urls.Add(array[i]?["url"]?.ToObject<string>());
                }
                var distinct = urls.Distinct().ToArray();
                if (distinct.Length > 0)
                {
                    return distinct[random.Next(0, distinct.Count() - 1)];
                }
            }
            throw new Exception("没有直播播放地址");
        }

        public static JObject HttpGetJson(string url)
        {
            var c = new WebClient();
            c.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36");
            c.Headers.Add(HttpRequestHeader.Accept, "application/json, text/javascript, */*; q=0.01");
            c.Headers.Add(HttpRequestHeader.Referer, "https://live.bilibili.com/");
            var s = c.DownloadString(url);
            var j = JObject.Parse(s);
            return j;
        }

        private static readonly Random random = new Random();
    }
}
