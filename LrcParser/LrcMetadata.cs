using System;

namespace Kfstorm.LrcParser
{
    internal class LrcMetadata : ILrcMetadata
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Maker { get; set; }
        public TimeSpan? Offset { get; set; }
    }
}