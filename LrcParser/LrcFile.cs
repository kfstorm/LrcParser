using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kfstorm.LrcParser
{
    /// <summary>
    /// Represents an LRC format lyrics file.
    /// </summary>
    /// <remarks>
    /// Duplicate timestamps is not supported due to Before, BeforeOrAt, After, AfterOrAt method.
    /// </remarks>
    public class LrcFile : ILrcFile
    {
        private readonly IOneLineLyric[] _lyrics;
        /// <summary>
        /// Gets the metadata.
        /// </summary>
        /// <value>
        /// The metadata.
        /// </value>
        public ILrcMetadata Metadata { get; private set; }
        /// <summary>
        /// Gets the lyrics.
        /// </summary>
        /// <value>
        /// The lyrics.
        /// </value>
        public IList<IOneLineLyric> Lyrics { get { return _lyrics; } }

        private readonly OneLineLyricComparer _comparer = new OneLineLyricComparer();

        /// <summary>
        /// Gets the one line lyric content with the specified timestamp.
        /// </summary>
        /// <value>
        /// The one line lyric content.
        /// </value>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        string ILrcFile.this[TimeSpan timestamp]
        {
            get
            {
                var index = Array.BinarySearch(_lyrics, new OneLineLyric(timestamp, null), _comparer);
                return index >= 0 ? _lyrics[index].Content : null;
            }
        }

        /// <summary>
        /// Gets the <see cref="IOneLineLyric"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="IOneLineLyric"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        IOneLineLyric ILrcFile.this[int index]
        {
            get { return _lyrics[index]; }
        }

        /// <summary>
        /// Gets the <see cref="IOneLineLyric" /> before the specified timestamp.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        public IOneLineLyric Before(TimeSpan timestamp)
        {
            var index = Array.BinarySearch(_lyrics, new OneLineLyric(timestamp, null), _comparer);
            if (index >= 0)
            {
                return index > 0 ? _lyrics[index - 1] : null;
            }
            index = -index;
            return index > 0 ? _lyrics[index - 1] : null;
        }

        /// <summary>
        /// Gets the <see cref="IOneLineLyric" /> before or at the specified timestamp.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        public IOneLineLyric BeforeOrAt(TimeSpan timestamp)
        {
            var index = Array.BinarySearch(_lyrics, new OneLineLyric(timestamp, null), _comparer);
            if (index >= 0) return _lyrics[index];
            index = -index;
            return index > 0 ? _lyrics[index - 1] : null;
        }

        /// <summary>
        /// Gets the <see cref="IOneLineLyric" /> after the specified timestamp.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        public IOneLineLyric After(TimeSpan timestamp)
        {
            var index = Array.BinarySearch(_lyrics, new OneLineLyric(timestamp, null), _comparer);
            if (index >= 0)
            {
                return index + 1 < _lyrics.Length ? _lyrics[index + 1] : null;
            }
            index = -index;
            return index < _lyrics.Length ? _lyrics[index] : null;
        }

        /// <summary>
        /// Gets the <see cref="IOneLineLyric" /> after or at the specified timestamp.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns></returns>
        public IOneLineLyric AfterOrAt(TimeSpan timestamp)
        {
            var index = Array.BinarySearch(_lyrics, new OneLineLyric(timestamp, null), _comparer);
            if (index >= 0) return _lyrics[index];
            index = -index;
            return index < _lyrics.Length ? _lyrics[index] : null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LrcFile"/> class.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <param name="lyrics">The lyrics.</param>
        /// <param name="applyOffset">if set to <c>true</c> apply the offset in metadata, otherwise ignore the offset.</param>
        public LrcFile(ILrcMetadata metadata, IEnumerable<IOneLineLyric> lyrics, bool applyOffset)
        {
            if (metadata == null) throw new ArgumentNullException("metadata");
            if (lyrics == null) throw new ArgumentNullException("lyrics");
            Metadata = metadata;
            var array = lyrics.OrderBy(l => l.Timestamp).ToArray();
            for (var i = 0; i < array.Length; ++i)
            {
                if (applyOffset && metadata.Offset.HasValue)
                {
                    var oneLineLyric = array[i] as OneLineLyric;
                    if (oneLineLyric != null)
                    {
                        oneLineLyric.Timestamp -= metadata.Offset.Value;
                    }
                    else
                    {
                        array[i] = new OneLineLyric(array[i].Timestamp - metadata.Offset.Value, array[i].Content);
                    }
                }
                if (i > 0 && array[i].Timestamp == array[i - 1].Timestamp)
                {
                    throw new ArgumentException(string.Format("Found duplicate timestamp '{0}' with lyric '{1}' and '{2}'.", array[i].Timestamp, array[i - 1].Content, array[i].Content), "lyrics");
                }
            }
            _lyrics = array;
        }

        /// <summary>
        /// Create a new new instance of the <see cref="ILrcFile"/> interface with the specified LRC text.
        /// </summary>
        /// <param name="lrcText">The LRC text.</param>
        /// <returns></returns>
        public static ILrcFile FromText(string lrcText)
        {
            var lines = lrcText.Replace(@"\'", "'").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var pairs = from line in lines
                let matches = Regex.Matches(line, @"(?'titles'\[(\d+:\d+(\.\d+)?|(ti|ar|al|by|offset):.*?)\])+(?'content'.+?(?=\[(\d+:\d+(\.\d+)?|(ti|ar|al|by|offset):.*?)\])|.*$)", RegexOptions.None)
                from Match match in matches
                let content = match.Groups["content"].Value
                from Capture title in match.Groups["titles"].Captures
                select new KeyValuePair<string, string>(title.Value, content);
            var lyrics = new List<IOneLineLyric>();
            var metadata = new LrcMetadata();
            string offsetString = null;
            foreach (var pair in pairs)
            {
                // Parse timestamp
                var match = Regex.Match(pair.Key, @"\[(?'minutes'\d+):(?'seconds'\d+(\.\d+)?)\]", RegexOptions.None);
                if (match.Success)
                {
                    int minutes = int.Parse(match.Groups["minutes"].Value);
                    double seconds = double.Parse(match.Groups["seconds"].Value);
                    var timestamp = TimeSpan.FromSeconds(minutes * 60 + seconds);
                    lyrics.Add(new OneLineLyric(timestamp, pair.Value));
                }

                // Parse metadata
                match = Regex.Match(pair.Key, @"\[(?'title'.+?):(?'content'.*)\]", RegexOptions.None);
                if (match.Success)
                {
                    var title = match.Groups["title"].Value.ToLower();
                    var content = match.Groups["content"].Value;
                    if (title == "ti")
                    {
                        if (metadata.Title != null && content != metadata.Title)
                        {
                            throw new ArgumentException(string.Format("Duplicate LRC metadata found. Metadata name: '{0}', Values: '{1}', '{2}'", "ti", metadata.Title, content), "lrcText");
                        }
                        metadata.Title = content;
                    }
                    if (title == "ar")
                    {
                        if (metadata.Artist != null && content != metadata.Artist)
                        {
                            throw new ArgumentException(string.Format("Duplicate LRC metadata found. Metadata name: '{0}', Values: '{1}', '{2}'", "ar", metadata.Artist, content), "lrcText");
                        }
                        metadata.Artist = content;
                    }
                    if (title == "al")
                    {
                        if (metadata.Album != null && content != metadata.Album)
                        {
                            throw new ArgumentException(string.Format("Duplicate LRC metadata found. Metadata name: '{0}', Values: '{1}', '{2}'", "al", metadata.Album, content), "lrcText");
                        }
                        metadata.Album = content;
                    }
                    if (title == "by")
                    {
                        if (metadata.Maker != null && content != metadata.Maker)
                        {
                            throw new ArgumentException(string.Format("Duplicate LRC metadata found. Metadata name: '{0}', Values: '{1}', '{2}'", "by", metadata.Maker, content), "lrcText");
                        }
                        metadata.Maker = content;
                    }
                    if (title == "offset")
                    {
                        if (offsetString != null && content != offsetString)
                        {
                            throw new ArgumentException(string.Format("Duplicate LRC metadata found. Metadata name: '{0}', Values: '{1}', '{2}'", "offset", offsetString, content), "lrcText");
                        }
                        offsetString = content;
                        metadata.Offset = TimeSpan.FromMilliseconds(double.Parse(content));
                    }
                }
            }

            return new LrcFile(metadata, lyrics, true);
        }
    }
}
