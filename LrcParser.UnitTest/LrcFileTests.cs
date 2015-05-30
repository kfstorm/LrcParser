using System;
using System.Linq;
using Kfstorm.LrcParser;
using Moq;
using NUnit.Framework;
// ReSharper disable StringLiteralTypo

namespace LrcParser.UnitTest
{
    [TestFixture]
    public class LrcFileTests
    {
        [TestCase("[00:01]First lyrics", 1)]
        [TestCase("[00:01]First lyrics\r\n[00:02]Second lyrics", 2)]
        [TestCase("[00:01]First lyrics\r\n[00:02]Second lyrics[00:03]Third lyrics\r\n", 3)]
        [TestCase("[00:04]First lyrics\r\n[00:03]Second lyrics[00:02]Third lyrics\r\n[00:01]Fourth lyrics", 4)]
        public void TestFromText(string lrcText, int count)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile);
            Assert.IsNotNull(lrcFile.Metadata);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.AreEqual(count, lrcFile.Lyrics.Count);
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

        [TestCase("[00:01][00:02]lyrics text", 2)]
        [TestCase("[00:01][00:50][00:10]lyrics text", 3)]
        [TestCase("[00:01][00:50][00:10]lyrics text[00:02][00:51][00:12]lyrics text", 6)]
        public void TestMultipleTimestamps(string lrcText, int count)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.AreEqual(count, lrcFile.Lyrics.Count);
            Assert.AreEqual(lrcFile.Lyrics.Count, lrcFile.Lyrics.Select(l => l.Timestamp).Distinct().Count());
            var contents = lrcFile.Lyrics.Select(l => l.Content).Distinct().ToList();
            Assert.AreEqual(1, contents.Count);
            Assert.AreEqual("lyrics text", contents[0]);
        }

        [TestCase("[00:01]", 1)]
        [TestCase("[00:01][00:02]", 2)]
        [TestCase("[00:01][00:50][00:10]", 3)]
        [TestCase("[00:01][00:50][00:10][00:02][00:51][00:12]", 6)]
        public void TestEmptyContent(string lrcText, int count)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.AreEqual(count, lrcFile.Lyrics.Count);
            Assert.AreEqual(lrcFile.Lyrics.Count, lrcFile.Lyrics.Select(l => l.Timestamp).Distinct().Count());
            Assert.IsTrue(lrcFile.Lyrics.All(l => l.Content == string.Empty));
        }

        [TestCase("[99:01]lyrics text", 1)]
        [TestCase("[43:00.1]lyrics text", 1)]
        [TestCase("[43:00.123532343]lyrics text\r\n[43:01.1]lyrics text", 2)]
        public void TestTimestampFormats(string lrcText, int count)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile.Lyrics);
            Assert.AreEqual(count, lrcFile.Lyrics.Count);
            var timestamps = lrcFile.Lyrics.Select(l => l.Timestamp);
            foreach (var timestamp in timestamps)
            {
                Assert.AreNotEqual(TimeSpan.Zero, timestamp);
            }
        }

        [TestCase("")]
        [TestCase("    ")]
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
        [TestCase("[123:43:59.99999999]only mm:ss(.xx) is valid")]
        [TestCase(@"[00:00]lyrics text \")]
        public void TestInvalidLyrics(string lrcText)
        {
            Assert.Catch<FormatException>(() => LrcFile.FromText(lrcText));
        }

        [TestCase("[ti:title][ar:artist][al:album][offset:100][by:kfstorm][00:00]", "title", "artist", "album", 100D, "kfstorm")]
        [TestCase(@"[\t\i\:ti\]tl\[e][ar\:artist][a\l:al\bum][of\fset:10\0][00:00]", "ti]tl[e", "artist", "album", 100D, null)]
        [TestCase("[ti:title][00:00]", "title", null, null, null, null)]
        [TestCase("[ti:title][ti:title][00:00]", "title", null, null, null, null)]
        [TestCase("[ar:artist][00:00]", null, "artist", null, null, null)]
        [TestCase("[ar:artist][ar:artist][00:00]", null, "artist", null, null, null)]
        [TestCase("[al:album][00:00]", null, null, "album", null, null)]
        [TestCase("[al:album][al:album][00:00]", null, null, "album", null, null)]
        [TestCase("[offset:12345][00:00]", null, null, null, 12345D, null)]
        [TestCase("[offset:12345][offset:12345][00:00]", null, null, null, 12345D, null)]
        [TestCase("[by:kfstorm][00:00]", null, null, null, null, "kfstorm")]
        [TestCase("[by:kfstorm][by:kfstorm][00:00]", null, null, null, null, "kfstorm")]
        [TestCase("[al:album][unsupportedtag:value][00:00]", null, null, "album", null, null)]
        public void TestMetadata(string lrcText, string expectedTitle, string expectedArtist, string expectedAlbum, double? expectedMilliseconds, string expectedMaker)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.IsNotNull(lrcFile.Metadata);
            Assert.AreEqual(expectedTitle, lrcFile.Metadata.Title);
            Assert.AreEqual(expectedArtist, lrcFile.Metadata.Artist);
            Assert.AreEqual(expectedAlbum, lrcFile.Metadata.Album);
            Assert.AreEqual(expectedMilliseconds, lrcFile.Metadata.Offset == null ? (double?)null : lrcFile.Metadata.Offset.Value.TotalMilliseconds);
        }

        [TestCase("[0:1]one[0:2]two[0:3]three", 0, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1.5, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2.5, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3.5, "three")]
        [TestCase("[0:1]one", 0, null)]
        [TestCase("[0:1]one", 1, null)]
        [TestCase("[0:1]one", 2, "one")]
        public void TestBefore(string lrcText, double seconds, string expectedLyric)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            var result = lrcFile.Before(TimeSpan.FromSeconds(seconds));
            Assert.AreEqual(expectedLyric, result == null ? null : result.Content);
        }

        [TestCase("[0:1]one[0:2]two[0:3]three", 0, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1.5, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2.5, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3, "three")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3.5, "three")]
        [TestCase("[0:1]one", 0, null)]
        [TestCase("[0:1]one", 1, "one")]
        [TestCase("[0:1]one", 2, "one")]
        public void TestBeforeOrAt(string lrcText, double seconds, string expectedLyric)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            var result = lrcFile.BeforeOrAt(TimeSpan.FromSeconds(seconds));
            Assert.AreEqual(expectedLyric, result == null ? null : result.Content);
        }

        [TestCase("[0:1]one[0:2]two[0:3]three", 0, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1.5, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2, "three")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2.5, "three")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3.5, null)]
        [TestCase("[0:1]one", 0, "one")]
        [TestCase("[0:1]one", 1, null)]
        [TestCase("[0:1]one", 2, null)]
        public void TestAfter(string lrcText, double seconds, string expectedLyric)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            var result = lrcFile.After(TimeSpan.FromSeconds(seconds));
            Assert.AreEqual(expectedLyric, result == null ? null : result.Content);
        }

        [TestCase("[0:1]one[0:2]two[0:3]three", 0, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1.5, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2.5, "three")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3, "three")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3.5, null)]
        [TestCase("[0:1]one", 0, "one")]
        [TestCase("[0:1]one", 1, "one")]
        [TestCase("[0:1]one", 2, null)]
        public void TestAfterOrAt(string lrcText, double seconds, string expectedLyric)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            var result = lrcFile.AfterOrAt(TimeSpan.FromSeconds(seconds));
            Assert.AreEqual(expectedLyric, result == null ? null : result.Content);
        }

        [TestCase("[0:1]one[0:2]two[0:3]three", 0, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1.5, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2.5, null)]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3, "three")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 3.5, null)]
        [TestCase("[0:1]one", 0, null)]
        [TestCase("[0:1]one", 1, "one")]
        [TestCase("[0:1]one", 2, null)]
        public void TestTimestampIndex(string lrcText, double seconds, string expectedLyric)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.AreEqual(expectedLyric, lrcFile[TimeSpan.FromSeconds(seconds)]);
        }

        [TestCase("[0:1]one[0:2]two[0:3]three", 0, "one")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 1, "two")]
        [TestCase("[0:1]one[0:2]two[0:3]three", 2, "three")]
        [TestCase("[0:1]one", 0, "one")]
        public void TestIntegerIndex(string lrcText, int index, string expectedLyric)
        {
            var lrcFile = LrcFile.FromText(lrcText);
            Assert.AreEqual(expectedLyric, lrcFile[index].Content);
        }

        [TestCase("[ti:title][00:00][ti:title2]")]
        [TestCase("[ar:artist][00:00][ar:artist2]")]
        [TestCase("[al:album][00:00][al:album2]")]
        [TestCase("[offset:100][00:00][offset:200]")]
        [TestCase("[by:kfstorm][00:00][by:kfstorm2]")]
        public void TestDuplicateMetadata(string lrcText)
        {
            Assert.Catch<FormatException>(() => LrcFile.FromText(lrcText));
        }
        
        [TestCase(@"[00\.0\0]Hello\, worl\d\!\[\]", 0D, "Hello, world![]")]
        public void TestCharacterEscape(string lrcText, double expectedSeconds, string expectedContent)
        {
            Assert.Catch<FormatException>(() => LrcFile.FromText(lrcText));
        }

        [Test]
        public void TestArgumentNullException()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Catch<ArgumentNullException>(() => new LrcFile(null, Enumerable.Empty<IOneLineLyric>(), true));
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Catch<ArgumentNullException>(() => new LrcFile(new Mock<ILrcMetadata>().Object, null, true));
            Assert.Catch<ArgumentNullException>(() => LrcFile.FromText(null));
        }

        [TestCase("[00:00][00:00]")]
        [TestCase("[12:34]hello[123:456.789]world[56:78]foo[123:456.789]bar")]
        public void TestDuplicateTimestamp(string lrcText)
        {
            Assert.Catch<FormatException>(() => LrcFile.FromText(lrcText));
        }

        [TestCase(123D)]
        [TestCase(-456D)]
        [TestCase(0D)]
        public void TestApplyOffsetToCustomizedOneLineLyric(double milliseconds)
        {
            var metadataMock = new Mock<ILrcMetadata>();
            metadataMock.Setup(m => m.Offset).Returns(TimeSpan.FromMilliseconds(milliseconds));
            var oneLineLyricMock = new Mock<IOneLineLyric>();
            oneLineLyricMock.Setup(l => l.Timestamp).Returns(TimeSpan.FromMilliseconds(12));
            var lrcFile = new LrcFile(metadataMock.Object, new[] { oneLineLyricMock.Object }, true);
            Assert.AreEqual(TimeSpan.FromMilliseconds(12) - TimeSpan.FromMilliseconds(milliseconds), lrcFile[0].Timestamp);
        }
    }
}
