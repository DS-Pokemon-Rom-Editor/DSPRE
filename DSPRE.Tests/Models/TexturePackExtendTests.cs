using DSPRE.Models;
using LibNDSFormats.NSBTX;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class TexturePackExtendTests
    {
        private readonly ITestOutputHelper _out;
        public TexturePackExtendTests(ITestOutputHelper o) { _out = o; }

        private static DsTexture Checker(string name)
        {
            var rgba = new byte[16 * 16 * 4];
            for (int i = 0; i < 256; i++)
            {
                bool on = ((i % 16) / 4 + (i / 64)) % 2 == 0;
                rgba[i * 4] = (byte)(on ? 255 : 0); rgba[i * 4 + 1] = 40; rgba[i * 4 + 2] = (byte)(on ? 0 : 255); rgba[i * 4 + 3] = 255;
            }
            var t = DsTexture.From(rgba, 16, 16, name);
            Assert.Null(t.Whynot);
            return t;
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AddingPicturesToARealPackKeepsEveryTextureItHad(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");
            string folder = Path.Combine(project, "unpacked", "mapTextures");
            Skip.If(!Directory.Exists(folder), "No unpacked map textures");

            int packs = 0, kept = 0;
            foreach (string file in RomFiles.Settled(folder).Take(25))
            {
                byte[] was = File.ReadAllBytes(file);
                NSBTXLoader.LoadNsbtx(new MemoryStream(was), out var texBefore, out var palBefore);
                if (texBefore == null || texBefore.Count == 0) continue;

                var made = NsbtxWriter.Extend(was, new[] { Checker("dspre_added") });
                Assert.True(made.Whynot == null, $"{Path.GetFileName(file)}: {made.Whynot}");

                NSBTXLoader.LoadNsbtx(new MemoryStream(made.Bytes), out var texAfter, out var palAfter);
                Assert.Equal(texBefore.Count + 1, texAfter.Count);
                Assert.Equal(palBefore.Count + 1, palAfter.Count);
                foreach (var t in texBefore)
                {
                    var same = texAfter.Single(a => a.texname == t.texname);
                    Assert.Equal((t.format, t.width, t.height, t.color0), (same.format, same.width, same.height, same.color0));
                    Assert.Equal(t.texdata, same.texdata);
                    Assert.Equal(t.spdata ?? Array.Empty<byte>(), same.spdata ?? Array.Empty<byte>());
                    kept++;
                }
                foreach (var p in palBefore)
                {
                    var same = palAfter.Single(a => a.palname == p.palname);
                    Assert.Equal(p.paldata.Select(c => c.ToString()), same.paldata.Take(p.paldata.Length).Select(c => c.ToString()));
                }
                var added = texAfter.Single(a => a.texname == "dspre_added");
                Assert.Equal((16, 16), (added.width, added.height));
                packs++;
            }

            Assert.True(packs > 0, "No pack was extended, so this proved nothing.");
            _out.WriteLine($"{game}: {packs} packs extended, {kept} textures kept byte for byte.");
        }
    }
}
