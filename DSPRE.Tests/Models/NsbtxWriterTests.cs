using DSPRE.Models;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class NsbtxWriterTests
    {
        private readonly ITestOutputHelper _out;
        public NsbtxWriterTests(ITestOutputHelper o) { _out = o; }

        private static DsTexture Picture(string name, int wide, int tall, int colours)
        {
            var rgba = new byte[wide * tall * 4];
            for (int i = 0; i < wide * tall; i++)
            {
                int shade = (i % Math.Max(1, colours)) * (255 / Math.Max(1, colours));
                rgba[i * 4] = (byte)shade;
                rgba[i * 4 + 1] = (byte)(255 - shade);
                rgba[i * 4 + 2] = (byte)((shade * 3) & 0xFF);
                rgba[i * 4 + 3] = 255;
            }
            return DsTexture.From(rgba, wide, tall, name);
        }

        [Fact]
        public void ABundleIsReadBackByTheReaderTheGameFollows()
        {
            var pictures = new List<DsTexture>
            {
                Picture("grass", 32, 32, 12),
                Picture("path", 64, 32, 40),
                Picture("water", 16, 16, 4),
            };

            var made = NsbtxWriter.Build(pictures);
            Assert.True(made.Whynot == null, made.Whynot);
            Assert.Equal(3, made.Pictures);

            using var stream = new MemoryStream(made.Bytes);
            var materials = NSBTXLoader.LoadNsbtx(stream, out var readTextures, out var readPalettes);

            Assert.NotNull(materials);
            Assert.NotNull(readTextures);

            var names = readTextures.Select(t => t.texname).ToList();
            foreach (var picture in pictures)
                Assert.Contains(picture.Name, names);

            foreach (var picture in pictures)
            {
                var back = readTextures.First(t => t.texname == picture.Name);
                Assert.Equal(picture.Width, back.width);
                Assert.Equal(picture.Height, back.height);
            }

            _out.WriteLine($"{made.Summary} Read back as "
                         + $"{string.Join(", ", names.Select(n => n ?? "?"))}.");
        }

        [Fact]
        public void EveryColourCountIsCarriedThrough()
        {
            foreach (var (colours, kind) in new[]
            {
                (10, DsTexture.Kind.SixteenColours),
                (100, DsTexture.Kind.TwoHundredFiftySix),
            })
            {
                var picture = Picture("p", 32, 32, colours);
                Assert.Equal(kind, picture.Format);

                var made = NsbtxWriter.Build(new[] { picture });
                Assert.True(made.Whynot == null, made.Whynot);

                using var stream = new MemoryStream(made.Bytes);
                NSBTXLoader.LoadNsbtx(stream, out var back, out _);
                Assert.Single(back);
                Assert.Equal(32, back[0].width);
            }
        }

        [Fact]
        public void TwoPicturesCalledTheSameThingAreRefusedRatherThanWritten()
        {
            var made = NsbtxWriter.Build(new[] { Picture("same", 16, 16, 4), Picture("same", 32, 32, 4) });

            Assert.Null(made.Bytes);
            Assert.Contains("same", made.Whynot);
        }

        [Fact]
        public void APictureWithNoNameIsRefusedBecauseNothingCouldEverFindIt()
        {
            Assert.Equal("texture", Picture("", 16, 16, 4).Name);

            var made = NsbtxWriter.Build(new[] { new DsTexture { Name = "" } });
            Assert.Null(made.Bytes);
            Assert.Contains("name", made.Whynot);
        }

        [Fact]
        public void NothingToPutInABundleIsSaidRatherThanWrittenEmpty()
        {
            Assert.NotNull(NsbtxWriter.Build(null).Whynot);
            Assert.NotNull(NsbtxWriter.Build(Array.Empty<DsTexture>()).Whynot);
        }
    }
}
