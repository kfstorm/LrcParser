using System;
using System.Linq;
using Kfstorm.LrcParser;
using NUnit.Framework;
// ReSharper disable StringLiteralTypo

namespace LrcParser.UnitTest
{
    [TestFixture]
    public class LrcFileTests
    {
        [TestCase("[00:01]First lyrics\r\n[00:02]Second lyrics")]
        public void TestFromText(string lrcText)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile);
            Assert.IsNotNull(lrcFile.Metadata);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.IsNotEmpty(lrcFile.Lyrics);
            foreach (var oneLineLyric in lrcFile.Lyrics)
            {
                Assert.AreNotEqual(TimeSpan.Zero, oneLineLyric.Timestamp);
                Assert.IsNotNullOrEmpty(oneLineLyric.Content);
            }
            for (var i = 0; i < lrcFile.Lyrics.Count; i++)
            {
                if (i > 0)
                {
                    Assert.Less(lrcFile.Lyrics[i - 1].Timestamp, lrcFile.Lyrics[i].Timestamp);
                }
                Assert.AreNotEqual(TimeSpan.Zero, lrcFile.Lyrics[i].Timestamp);
                Assert.IsNotNullOrEmpty(lrcFile.Lyrics[i].Content);
            }
        }
        [TestCase("[00:01][00:02]lyrics text")]
        [TestCase("[00:01][00:50][00:10]lyrics text")]
        [TestCase("[00:01][00:50][00:10]lyrics text[00:02][00:51][00:12]lyrics text")]
        public void TestMultipleTimestamps(string lrcText)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.Greater(lrcFile.Lyrics.Count, 1);
            Assert.AreEqual(lrcFile.Lyrics.Count, lrcFile.Lyrics.Select(l => l.Timestamp).Distinct().Count());
            var contents = lrcFile.Lyrics.Select(l => l.Content).Distinct().ToList();
            Assert.AreEqual(1, contents.Count);
            Assert.AreEqual("lyrics text", contents[0]);
        }

        [TestCase("[99:01][43:00.1][123:43:59.99999999]lyrics text")]
        public void TestTimestampFormats(string lrcText)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.Greater(lrcFile.Lyrics.Count, 1);
            var timestamps = lrcFile.Lyrics.Select(l => l.Timestamp);
            foreach (var timestamp in timestamps)
            {
                Assert.AreNotEqual(TimeSpan.Zero, timestamp);
            }
        }

        [TestCase("")]
        [TestCase("123")]
        [TestCase("abc")]
        [TestCase("jhIUG*^T*&Hfhdhf98Y*&YD*SJSFOI")]
        [TestCase("[")]
        [TestCase("[[[[[")]
        [TestCase("]")]
        [TestCase("]]]]]")]
        [TestCase("][")]
        [TestCase("]]]]][[[[[[")]
        [TestCase("[][][]")]
        [TestCase("[sfdsfs][sdfdsf][sdfsfsdf]")]
        [TestCase("[ti][ar][al]")]
        [TestCase("[0000]")]
        [TestCase("[00;00]")]
        [TestCase("[00+00]")]
        public void TestInvalidLyrics(string lrcText)
        {
            Assert.Catch<ArgumentException>(() => LrcFile.FromText(lrcText));
        }
    }
}
