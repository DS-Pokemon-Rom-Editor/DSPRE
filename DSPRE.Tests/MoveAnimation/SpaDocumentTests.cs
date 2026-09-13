using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Editing a particle archive changes what was edited and nothing else.</summary>
    public class SpaDocumentTests
    {
        private readonly ITestOutputHelper _out;
        public SpaDocumentTests(ITestOutputHelper o) { _out = o; }

        private const uint LayoutBits = (1u << 8) | (1u << 9) | (1u << 10) | (1u << 11) | (1u << 16)
                                       | (1u << 24) | (1u << 25) | (1u << 26) | (1u << 27) | (1u << 28) | (1u << 29);
        private const int FullRecord = 88 + 12 + 12 + 8 + 12 + 20 + 8 + 8 + 16 + 4 + 8 + 16;

        internal sealed class Tex
        {
            public int Format, SizeShift, PaletteColors; public bool Color0; public bool Shared;
            public int TexelSize => Format switch { 1 or 4 or 6 => Pixels, 2 => Pixels / 4, 3 => Pixels / 2, 5 => Pixels / 4, _ => Pixels * 2 };
            public int Side => 8 << SizeShift;
            public int Pixels => Side * Side;
        }

        private static void W16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        private static void W32(byte[] b, int o, uint v) { for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (8 * i)); }

        /// <summary>One emitter carrying every optional block, filled with noise, then the given textures.</summary>
        internal static byte[] Build(int seed, params Tex[] textures)
        {
            var rng = new Random(seed);
            int texSection = textures.Sum(t => 32 + t.TexelSize + t.PaletteColors * 2);
            int texOffset = 32 + FullRecord;
            var b = new byte[texOffset + texSection];
            rng.NextBytes(b);

            W32(b, 0, 0x53504120); W32(b, 4, 0x315F3231);
            W16(b, 8, 1); W16(b, 10, textures.Length);
            W32(b, 12, 0); W32(b, 16, (uint)FullRecord); W32(b, 20, (uint)texSection); W32(b, 24, (uint)texOffset); W32(b, 28, 0);
            W32(b, 32, (BitConverter.ToUInt32(b, 32) & ~LayoutBits) | LayoutBits);

            int pos = texOffset;
            foreach (var t in textures)
            {
                uint param = (uint)t.Format | (uint)(t.SizeShift << 4) | (uint)(t.SizeShift << 8) | (t.Color0 ? 1u << 16 : 0)
                             | (t.Shared ? 1u << 17 : 0);
                W32(b, pos, 0x53505420); W32(b, pos + 4, param);
                W32(b, pos + 8, (uint)t.TexelSize); W32(b, pos + 12, (uint)(32 + t.TexelSize));
                W32(b, pos + 16, (uint)(t.PaletteColors * 2));
                W32(b, pos + 28, (uint)(32 + t.TexelSize + t.PaletteColors * 2));
                pos += 32 + t.TexelSize + t.PaletteColors * 2;
            }
            return b;
        }

        private static readonly Tex[] AllFormats =
        {
            new Tex { Format = 1, SizeShift = 1, PaletteColors = 12, Color0 = true },
            new Tex { Format = 6, SizeShift = 1, PaletteColors = 8, Color0 = true },
            new Tex { Format = 2, SizeShift = 0, PaletteColors = 4, Color0 = true },
            new Tex { Format = 3, SizeShift = 1, PaletteColors = 16, Color0 = false },
            new Tex { Format = 4, SizeShift = 1, PaletteColors = 40, Color0 = true },
            new Tex { Format = 7, SizeShift = 0, PaletteColors = 0 },
        };

        private static int FieldAt(SpaDocument doc, SpaField f)
            => doc.Parse().Records[0].BlockOffset(f.Block) + f.Offset;

        [Fact]
        public void AnUneditedArchiveWritesBackIdentical()
        {
            var bytes = Build(1, AllFormats);
            var doc = SpaDocument.Load(bytes);
            Assert.Equal(1, doc.EmitterCount);
            Assert.Equal(AllFormats.Length, doc.TextureCount);
            Assert.False(doc.IsModified);
            Assert.Equal(bytes, doc.ToBytes());
        }

        [Fact]
        public void EveryFieldEditChangesOnlyItsOwnBits()
        {
            var bytes = Build(2, AllFormats);
            int edited = 0, locked = 0;
            foreach (var f in SpaFields.All)
            {
                var doc = SpaDocument.Load(bytes);
                Assert.True(doc.Has(0, f), f + " is missing from a record that carries every block");

                if (f.DecidesLayout)
                {
                    Assert.Throws<InvalidOperationException>(() => doc.SetRaw(0, f, 1 - doc.GetRaw(0, f)));
                    Assert.Equal(bytes, doc.ToBytes());
                    locked++;
                    continue;
                }

                long before = doc.GetRaw(0, f);
                long target = before == f.MaxRaw ? f.MinRaw : f.MaxRaw;
                doc.SetRaw(0, f, target);
                var written = doc.ToBytes();

                var reread = SpaDocument.Load(written);
                Assert.Equal(target, reread.GetRaw(0, f));

                int at = FieldAt(doc, f);
                ulong mask = (((1UL << f.Bits) - 1) << f.Shift);
                for (int i = 0; i < bytes.Length; i++)
                {
                    int diff = bytes[i] ^ written[i];
                    if (diff == 0) continue;
                    int k = i - at;
                    Assert.True(k >= 0 && k < f.Size && (diff & ~(int)((mask >> (8 * k)) & 0xFF)) == 0,
                        $"{f}: byte 0x{i:X} changed outside the field (field word at 0x{at:X})");
                }

                foreach (var other in SpaFields.All)
                    if (other != f)
                        Assert.True(SpaDocument.Load(bytes).GetRaw(0, other) == reread.GetRaw(0, other),
                            $"editing {f} changed {other}");
                edited++;
            }
            _out.WriteLine($"{edited} fields edited, {locked} layout flags refused");
            Assert.True(edited > 120, $"only {edited} fields were edited");
            Assert.Equal(11, locked);
            Assert.Equal(SpaFields.All.Count, edited + locked);
        }

        [Fact]
        public void TheParserReadsBackWhatWasWritten()
        {
            var doc = SpaDocument.Load(Build(3));
            void Set(SpaField f, long raw) => doc.SetRaw(0, f, raw);

            Set(SpaFields.ParticleLifeTime, 45); Set(SpaFields.EmitterLifeTime, 90);
            Set(SpaFields.TextureIndex, 3); Set(SpaFields.EmissionInterval, 5); Set(SpaFields.BaseAlpha, 20);
            Set(SpaFields.AirResistance, 200); doc.SetValue(0, SpaFields.EmissionCount, 2.5);
            doc.SetValue(0, SpaFields.Radius, 10); doc.SetValue(0, SpaFields.InitVelAxis, -3);
            doc.SetValue(0, SpaFields.BaseScale, 1.25); doc.SetValue(0, SpaFields.AxisZ, -0.5);
            Set(SpaFields.StartDelay, 7); doc.SetValue(0, SpaFields.InitAngle, 90);
            Set(SpaFields.Color, SpaFields.FromRgb888(255, 0, 255));
            Set(SpaFields.LoopFrames, 12); doc.SetValue(0, SpaFields.DbbScale, 0.5); Set(SpaFields.ScaleAnimDir, 2);
            Set(SpaFields.FlipTextureT, 1); doc.SetValue(0, SpaFields.PolygonY, -0.5); Set(SpaFields.HideParent, 1);
            Set(SpaFields.RandomInitVel, 9);
            doc.SetValue(0, SpaFields.ScaleAnimEnd, 1.5); Set(SpaFields.ScaleAnimOut, 99);
            Set(SpaFields.ColorAnimPeak, 128); Set(SpaFields.ColorAnimInterpolate, 1);
            Set(SpaFields.AlphaAnimEnd, 7); Set(SpaFields.AlphaAnimRandomRange, 33);
            Set(SpaFields.TexAnimFrame3, 6); Set(SpaFields.TexAnimStep, 4);
            Set(SpaFields.ChildTextureIndex, 2); Set(SpaFields.ChildScaleRatio, 63);
            Set(SpaFields.ChildColor, SpaFields.FromRgb888(0, 255, 0)); Set(SpaFields.ChildRotationType, 2);
            doc.SetValue(0, SpaFields.GravityY, -0.5); Set(SpaFields.RandomInterval, 3);
            doc.SetValue(0, SpaFields.MagnetForce, 0.25); Set(SpaFields.SpinAngle, -1000); Set(SpaFields.SpinAxis, 1);
            doc.SetValue(0, SpaFields.CollisionY, 2); Set(SpaFields.CollisionType, 1);
            doc.SetValue(0, SpaFields.ConvergenceTargetX, -1);

            var e = SpaArchive.Parse(doc.ToBytes()).Emitters[0];
            Assert.Equal(45, e.ParticleLife); Assert.Equal(90, e.EmitterLife);
            Assert.Equal(3, e.TexNo); Assert.Equal(5, e.GenInterval); Assert.Equal(20, e.BaseAlpha);
            Assert.Equal(200, e.AirResist); Assert.Equal(2.5, e.GenNum);
            Assert.Equal(10, e.Radius, 2); Assert.Equal(-3, e.InitVelAxis, 2);
            Assert.Equal(1.25, e.BaseScale); Assert.Equal(-0.5, e.AxisZ);
            Assert.Equal(7, e.StartOffset); Assert.Equal(Math.PI / 2, e.InitRot, 6);
            Assert.Equal((255, 0, 255), ((int)e.ColorR, (int)e.ColorG, (int)e.ColorB));
            Assert.Equal(12, e.LoopFrames); Assert.Equal(0.5, e.DbbScale); Assert.Equal(2, e.ScaleAnimDir);
            Assert.True(e.FlipT); Assert.Equal(-0.5, e.OffsetY); Assert.True(e.HideParent);
            Assert.Equal(9, e.RndVel);
            Assert.Equal(1.5, e.SclE); Assert.Equal(99, e.SclOut);
            Assert.Equal(128, e.ClrPeak); Assert.True(e.ClrInterp);
            Assert.Equal(7, e.AlpE); Assert.Equal(33, e.AlpFlick);
            Assert.Equal(6, e.TexSeq[3]); Assert.Equal(4, e.TexDiff);
            Assert.Equal(2, e.ChildTexNo); Assert.Equal(63, e.ChildSclRatioRaw);
            Assert.Equal((0, 255, 0), ((int)e.ChildR, (int)e.ChildG, (int)e.ChildB)); Assert.Equal(2, e.ChildRotType);
            Assert.Equal(-0.5, e.GravityY, 2); Assert.Equal(3, e.RandIntvl);
            Assert.Equal(0.25, e.MagnetMag); Assert.Equal(-1000, e.SpinRadian); Assert.Equal(1, e.SpinAxis);
            Assert.Equal(2, e.CollY, 2); Assert.Equal(1, e.CollEvent);
            Assert.Equal(-1, e.ConvX, 2);
        }

        [Fact]
        public void OutOfRangeValuesAreRefusedAndChangeNothing()
        {
            var bytes = Build(4);
            var doc = SpaDocument.Load(bytes);
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetRaw(0, SpaFields.BaseAlpha, 32));
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetRaw(0, SpaFields.AxisX, 40000));
            Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetRaw(0, SpaFields.ParticleLifeTime, -1));
            Assert.False(doc.IsModified);
            Assert.Equal(bytes, doc.ToBytes());
        }

        [Fact]
        public void AFileThatDoesNotAccountForItsBytesIsRefused()
        {
            var bytes = Build(5, AllFormats);
            var moved = (byte[])bytes.Clone();
            W32(moved, 24, BitConverter.ToUInt32(bytes, 24) + 4);
            Assert.False(SpaDocument.TryLoad(moved, out _, out string why));
            _out.WriteLine(why);
            Assert.False(SpaDocument.TryLoad(bytes.Take(100).ToArray(), out _, out why));
            _out.WriteLine(why);
            Assert.False(SpaDocument.TryLoad(new byte[40], out _, out why));
        }

        private static byte[] RepresentableImage(Tex t, int seed)
        {
            var rng = new Random(seed);
            bool alphaFormat = t.Format == 1 || t.Format == 6;
            int slots = t.Format == 7 ? 64 : Math.Min(t.PaletteColors, 1 << (t.Format switch { 1 => 5, 2 => 2, 3 => 4, 4 => 8, _ => 3 }))
                                             - (!alphaFormat && t.Color0 ? 1 : 0);
            var colours = Enumerable.Range(0, slots).Select(_ => (r: rng.Next(32) << 3, g: rng.Next(32) << 3, b: rng.Next(32) << 3)).ToArray();
            int[] a3 = { 0, 32, 72, 104, 144, 176, 216, 248 };

            var img = new byte[t.Pixels * 4];
            for (int j = 0; j < t.Pixels; j++)
            {
                var c = colours[j % colours.Length];
                int a = t.Format switch
                {
                    1 => a3[rng.Next(8)],
                    6 => rng.Next(32) * 8,
                    7 => rng.Next(2) * 255,
                    _ => t.Color0 && j % 5 == 0 ? 0 : 255,
                };
                if (a == 0 && !alphaFormat && t.Format != 7) c = (255, 0, 0);   // how the decoder draws a clear index 0
                img[j * 4] = (byte)c.r; img[j * 4 + 1] = (byte)c.g; img[j * 4 + 2] = (byte)c.b; img[j * 4 + 3] = (byte)a;
            }
            return img;
        }

        [Fact]
        public void AnImageTheFormatCanHoldComesBackExactlyInEveryFormat()
        {
            var bytes = Build(6, AllFormats);
            for (int i = 0; i < AllFormats.Length; i++)
            {
                var t = AllFormats[i];
                var doc = SpaDocument.Load(bytes);
                var img = RepresentableImage(t, 100 + i);

                var imp = doc.ReplaceTexture(i, t.Side, t.Side, img);
                Assert.True(imp.Succeeded, $"format {t.Format}: {imp.Error}");
                Assert.False(imp.Quantized);

                var written = doc.ToBytes();
                var decoded = SpaArchive.Parse(written).Textures[i].Rgba;
                Assert.True(img.SequenceEqual(decoded), $"format {t.Format}: the texture does not draw as the image given");
                Assert.True(img.SequenceEqual(imp.Rgba), $"format {t.Format}: the reported result is not what draws");

                int pos = SpaArchive.Parse(bytes).Textures[i].ResourceOffset;
                int palOfs = pos + 32 + t.TexelSize, palEnd = palOfs + t.PaletteColors * 2;
                for (int k = 0; k < bytes.Length; k++)
                    if (bytes[k] != written[k])
                        Assert.True(k >= pos + 32 && k < palEnd, $"format {t.Format}: byte 0x{k:X} outside the texture changed");
                _out.WriteLine($"format {t.Format}: {imp.SourceColors} colours, palette kept {imp.PaletteKept}");
            }
        }

        [Theory]
        [InlineData(1, 8)]
        [InlineData(6, 2)]
        [InlineData(3, 16)]
        public void TooManyColoursAreMergedIntoThePaletteAndDrawAsReported(int format, int paletteColors)
        {
            var t = new Tex { Format = format, SizeShift = 1, PaletteColors = paletteColors, Color0 = false };
            var doc = SpaDocument.Load(Build(7, t));
            var img = new byte[t.Pixels * 4];
            for (int j = 0; j < t.Pixels; j++)
            {
                img[j * 4] = (byte)(j % 16 * 16); img[j * 4 + 1] = (byte)(j / 16 * 16); img[j * 4 + 2] = 128; img[j * 4 + 3] = 255;
            }

            var imp = doc.ReplaceTexture(0, t.Side, t.Side, img);
            Assert.True(imp.Succeeded, imp.Error);
            Assert.True(imp.Quantized);
            Assert.Equal(256, imp.SourceColors);

            var decoded = SpaArchive.Parse(doc.ToBytes()).Textures[0].Rgba;
            Assert.True(decoded.SequenceEqual(imp.Rgba), "the texture does not draw as the import reported");

            var distinct = Enumerable.Range(0, t.Pixels).Select(j => (decoded[j * 4], decoded[j * 4 + 1], decoded[j * 4 + 2])).Distinct().Count();
            Assert.True(distinct <= paletteColors, $"{distinct} colours drawn from a {paletteColors}-colour palette");
            double error = Enumerable.Range(0, t.Pixels * 4).Where(k => k % 4 != 3).Average(k => Math.Abs(img[k] - decoded[k]));
            _out.WriteLine($"format {format}: {distinct} colours, mean channel error {error:F1}");
            Assert.True(error < (paletteColors <= 2 ? 64 : 32), $"mean channel error {error:F1} is too far from the image");
        }

        [Fact]
        public void ImagesThatDoNotFitAreRefusedWithAReasonAndChangeNothing()
        {
            var textures = new[]
            {
                new Tex { Format = 3, SizeShift = 1, PaletteColors = 16, Color0 = false },
                new Tex { Format = 5, SizeShift = 1, PaletteColors = 8 },
                new Tex { Format = 1, SizeShift = 1, PaletteColors = 8, Shared = true },
            };
            var bytes = Build(8, textures);
            var doc = SpaDocument.Load(bytes);

            var opaque = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
            var clear = new byte[16 * 16 * 4];

            var wrongSize = doc.ReplaceTexture(0, 32, 16, new byte[32 * 16 * 4]);
            var noClearColour = doc.ReplaceTexture(0, 16, 16, clear);
            var compressed = doc.ReplaceTexture(1, 16, 16, opaque);
            var shared = doc.ReplaceTexture(2, 16, 16, opaque);

            foreach (var r in new[] { wrongSize, noClearColour, compressed, shared })
            {
                Assert.False(r.Succeeded);
                Assert.False(string.IsNullOrWhiteSpace(r.Error));
                _out.WriteLine(r.Error);
            }
            Assert.Contains("32x16", wrongSize.Error);
            Assert.NotNull(doc.GetTextureInfo(1).CannotReplace);
            Assert.False(doc.IsModified);
            Assert.Equal(bytes, doc.ToBytes());
        }
    }
}
