using System;
using System.Collections.Generic;
using System.IO;

namespace BililiveRecorder.FlvProcessor
{
    public class FlvClipProcessor : IFlvClipProcessor
    {
        public IFlvMetadata Header { get; private set; }
        public List<IFlvTag> HTags { get; private set; }
        public List<IFlvTag> Tags { get; private set; }
        private int target = -1;
        private string path;

        public FlvClipProcessor()
        {
        }

        public IFlvClipProcessor Initialize(string path, IFlvMetadata metadata, List<IFlvTag> head, List<IFlvTag> data, uint seconds)
        {
            this.path = path;
            Header = metadata; // TODO: Copy a copy, do not share
            HTags = head;
            Tags = data;
            target = Tags[Tags.Count - 1].TimeStamp + (int)(seconds * FlvStreamProcessor.SEC_TO_MS);

            return this;
        }

        public void AddTag(IFlvTag tag)
        {
            Tags.Add(tag);
            if (tag.TimeStamp >= target)
            {
                FinallizeFile();
            }
        }

        public void FinallizeFile()
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite))
                {
                    fs.Write(FlvStreamProcessor.FLV_HEADER_BYTES, 0, FlvStreamProcessor.FLV_HEADER_BYTES.Length);
                    fs.Write(new byte[] { 0, 0, 0, 0, }, 0, 4);

                    Header.Meta["duration"] = (Tags[Tags.Count - 1].TimeStamp - Tags[0].TimeStamp) / 1000d;
                    Header.Meta["lasttimestamp"] = (Tags[Tags.Count - 1].TimeStamp - Tags[0].TimeStamp);

                    var t = new FlvTag
                    {
                        TagType = TagType.DATA,
                        Data = Header.ToBytes()
                    };
                    t.WriteTo(fs);

                    int offset = Tags[0].TimeStamp;

                    HTags.ForEach(tag => tag.WriteTo(fs));
                    Tags.ForEach(tag => tag.WriteTo(fs, offset));

                    fs.Close();
                }
                Tags.Clear();


            }
            catch (Exception)
            {

            }
            ClipFinalized?.Invoke(this, new ClipFinalizedArgs() { ClipProcessor = this });
        }

        public event ClipFinalizedEvent ClipFinalized;
    }
}
