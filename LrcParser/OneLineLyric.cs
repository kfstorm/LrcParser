using System;

namespace Kfstorm.LrcParser
{
    internal class OneLineLyric : IOneLineLyric
    {
        public OneLineLyric(TimeSpan timestamp, string content)
        {
            Timestamp = timestamp;
            Content = content;
        }

        public TimeSpan Timestamp { get; internal set; }
        public string Content { get; internal set; }
    }
}