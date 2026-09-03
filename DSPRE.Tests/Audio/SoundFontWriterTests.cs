using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Writing a game's instrument bank out as a SoundFont: that the file holds what the format asks
    /// for, that its tables agree with each other, and that the numbers the DS keeps come across.
    /// </summary>
    [Collection("rom")]
    public class SoundFontWriterTests
    {
        private readonly ITestOutputHelper _out;
        public SoundFontWriterTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Project = TestRoms.HeartGold;

        private static SdatArchive Sound()
        {
            if (!Directory.Exists(Project)) return null;
            try { new RomInfo("IPKE", Project); } catch { return null; }
            SoundArchive.Reset();
            return SoundArchive.Load();
        }

        // ── turning the DS's numbers into a SoundFont's ───────────────────────────────────────────

        [Theory]
        [InlineData(1.0, 0)]            // a second is the format's zero
        [InlineData(2.0, 1200)]         // twice as long is twelve hundred more
        [InlineData(0.5, -1200)]
        [InlineData(0.25, -2400)]
        public void ALengthComesOutAsTwelveHundredPerDoubling(double seconds, int want)
            => Assert.Equal(want, SoundFontWriter.Timecents(seconds));

        [Fact]
        public void SomethingInstantIsAsShortAsTheFormatGoesRatherThanNegativeInfinity()
        {
            Assert.Equal(-12000, SoundFontWriter.Timecents(0));
            Assert.Equal(-12000, SoundFontWriter.Timecents(-1));
            Assert.True(SoundFontWriter.Timecents(1e9) <= 8000);
        }

        [Theory]
        [InlineData(1.0, 0)]            // full volume is no quietening
        [InlineData(0.5, 60)]           // half the amplitude is six decibels down
        [InlineData(0.1, 200)]
        [InlineData(0.0, 1440)]
        public void AGainComesOutAsTenthsOfADecibelOfQuietening(double gain, int want)
            => Assert.Equal(want, SoundFontWriter.Attenuation(gain));

        [Fact]
        public void AFastAttackIsShorterThanASlowOne()
        {
            // The DS keeps attack as a rate, and a bigger rate means the note arrives sooner.
            double quick = SoundFontWriter.AttackSeconds(NitroEnvelope.Compute(127, 0, 127, 0).AttackRate);
            double slow = SoundFontWriter.AttackSeconds(NitroEnvelope.Compute(0, 0, 127, 0).AttackRate);
            _out.WriteLine($"fastest attack {quick:0.0000}s, slowest {slow:0.0000}s");
            Assert.True(quick < slow, $"the fastest attack came out at {quick}s and the slowest at {slow}s");
            Assert.True(quick < 0.05, $"the fastest attack should be near instant but came out at {quick}s");
        }

        // ── the file ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void EveryBankInTheRomWritesAFileWhoseTablesAgreeWithEachOther()
        {
            var sdat = Sound();
            Assert.True(sdat != null, "the HeartGold project this reads is not there, so it proved nothing");

            int wrote = 0, refused = 0;
            var reasons = new Dictionary<string, int>();
            long biggest = 0;
            string biggestName = null;

            for (int b = 0; b < sdat.Banks.Count; b++)
            {
                if (sdat.Banks[b] == null) continue;
                string name = sdat.BankNames.TryGetValue(b, out var n) && !string.IsNullOrWhiteSpace(n)
                    ? n : "Bank " + b;
                var made = SoundFontWriter.Build(sdat, b, name);
                if (made.Whynot != null)
                {
                    refused++;
                    reasons[made.Whynot] = reasons.TryGetValue(made.Whynot, out int c) ? c + 1 : 1;
                    Assert.Null(made.Bytes);
                    continue;
                }
                wrote++;
                if (made.Bytes.Length > biggest) { biggest = made.Bytes.Length; biggestName = name; }
                CheckItHangsTogether(made, name);
            }

            _out.WriteLine($"{sdat.Banks.Count(x => x != null)} banks: {wrote} written, {refused} refused.");
            foreach (var kv in reasons.OrderByDescending(k => k.Value))
                _out.WriteLine($"  {kv.Value} x {kv.Key}");
            _out.WriteLine($"largest was {biggestName} at {biggest / 1024}kb.");

            Assert.True(wrote > 20, $"only {wrote} banks produced a file, which is too few to prove anything");
        }

        /// <summary>
        /// Reads the file back and checks the tables point where they say. This is written from the
        /// format's own rules rather than from the writer, so it catches the writer being wrong.
        /// </summary>
        private void CheckItHangsTogether(SoundFontWriter.Result made, string name)
        {
            byte[] f = made.Bytes;
            Assert.True(f.Length > 100, $"{name} came out at {f.Length} bytes");
            Assert.Equal("RIFF", Tag(f, 0));
            Assert.Equal("sfbk", Tag(f, 8));
            Assert.Equal((uint)(f.Length - 8), U32(f, 4));

            var chunks = WalkChunks(f);
            foreach (string need in new[] { "ifil", "isng", "INAM", "smpl",
                                            "phdr", "pbag", "pmod", "pgen",
                                            "inst", "ibag", "imod", "igen", "shdr" })
                Assert.True(chunks.ContainsKey(need), $"{name} has no {need} table");

            var (phdrAt, phdrLen) = chunks["phdr"];
            var (pbagAt, pbagLen) = chunks["pbag"];
            var (pgenAt, pgenLen) = chunks["pgen"];
            var (instAt, instLen) = chunks["inst"];
            var (ibagAt, ibagLen) = chunks["ibag"];
            var (igenAt, igenLen) = chunks["igen"];
            var (shdrAt, shdrLen) = chunks["shdr"];
            var (smplAt, smplLen) = chunks["smpl"];

            Assert.True(phdrLen % 38 == 0, $"{name}'s preset table is {phdrLen} bytes, not a whole number of records");
            Assert.True(instLen % 22 == 0, $"{name}'s instrument table is {instLen} bytes, not a whole number");
            Assert.True(shdrLen % 46 == 0, $"{name}'s recording table is {shdrLen} bytes, not a whole number");
            Assert.True(pbagLen % 4 == 0 && ibagLen % 4 == 0);
            Assert.True(pgenLen % 4 == 0 && igenLen % 4 == 0);

            int presets = phdrLen / 38 - 1, insts = instLen / 22 - 1, recs = shdrLen / 46 - 1;
            int pbags = pbagLen / 4, ibags = ibagLen / 4, pgens = pgenLen / 4, igens = igenLen / 4;
            Assert.True(presets >= 1, $"{name} has no presets");
            Assert.Equal(made.Instruments, insts);
            Assert.Equal(made.Recordings, recs);

            // Every table's last record is the marker saying where the one before it ends, so the
            // indices have to climb and the last must land exactly on the end.
            int previous = -1;
            for (int i = 0; i <= presets; i++)
            {
                int bag = U16(f, phdrAt + i * 38 + 24);
                Assert.True(bag >= previous, $"{name}'s preset {i} points back before preset {i - 1}");
                Assert.True(bag < pbags, $"{name}'s preset {i} points past the end of the zone table");
                previous = bag;
            }
            Assert.Equal(pbags - 1, U16(f, phdrAt + presets * 38 + 24));

            previous = -1;
            for (int i = 0; i <= insts; i++)
            {
                int bag = U16(f, instAt + i * 22 + 20);
                Assert.True(bag >= previous, $"{name}'s instrument {i} points back before instrument {i - 1}");
                Assert.True(bag < ibags, $"{name}'s instrument {i} points past the end of the zone table");
                previous = bag;
            }
            Assert.Equal(ibags - 1, U16(f, instAt + insts * 22 + 20));

            for (int i = 0; i < pbags; i++)
                Assert.True(U16(f, pbagAt + i * 4) < pgens || i == pbags - 1,
                            $"{name}'s preset zone {i} points past the end of its settings");
            for (int i = 0; i < ibags; i++)
                Assert.True(U16(f, ibagAt + i * 4) < igens || i == ibags - 1,
                            $"{name}'s zone {i} points past the end of its settings");

            // Every zone has to end by naming a recording, and that recording has to exist.
            int zonesEndingInASample = 0;
            for (int i = 0; i < ibags - 1; i++)
            {
                int from = U16(f, ibagAt + i * 4), to = U16(f, ibagAt + (i + 1) * 4);
                Assert.True(to > from, $"{name}'s zone {i} holds no settings at all");
                int lastOp = U16(f, igenAt + (to - 1) * 4);
                int lastVal = U16(f, igenAt + (to - 1) * 4 + 2);
                Assert.True(lastOp == 53, $"{name}'s zone {i} ends on setting {lastOp}, not the recording");
                Assert.True(lastVal < recs, $"{name}'s zone {i} names recording {lastVal} of {recs}");
                Assert.Equal(43, U16(f, igenAt + from * 4));   // the note range comes first
                zonesEndingInASample++;
            }
            Assert.Equal(made.Regions, zonesEndingInASample);

            // Every recording has to sit inside the run of samples, with room after it.
            int totalSamples = smplLen / 2;
            for (int i = 0; i < recs; i++)
            {
                int at = shdrAt + i * 46;
                int start = (int)U32(f, at + 20), end = (int)U32(f, at + 24);
                int loopStart = (int)U32(f, at + 28), loopEnd = (int)U32(f, at + 32);
                int rate = (int)U32(f, at + 36);
                Assert.True(start < end, $"{name}'s recording {i} starts at {start} and ends at {end}");
                Assert.True(end + 46 <= totalSamples,
                            $"{name}'s recording {i} ends at {end} with only {totalSamples} samples written");
                // The format asks for eight samples of room either side, for a player working out what
                // sits between two samples.
                Assert.True(start >= 8, $"{name}'s recording {i} starts at {start}, with no room before it");
                Assert.True(end + 8 <= totalSamples,
                            $"{name}'s recording {i} has no room after it");
                Assert.True(loopStart >= start && loopEnd <= end,
                            $"{name}'s recording {i} loops outside itself");
                Assert.True(rate > 0 && rate < 200000, $"{name}'s recording {i} claims {rate} samples a second");
                Assert.Equal(1, U16(f, at + 44));   // one channel
            }
        }

        // ── reading a RIFF file back ──────────────────────────────────────────────────────────────

        private static Dictionary<string, (int At, int Length)> WalkChunks(byte[] f)
        {
            var found = new Dictionary<string, (int, int)>();
            void Walk(int at, int end)
            {
                while (at + 8 <= end)
                {
                    string tag = Tag(f, at);
                    int len = (int)U32(f, at + 4);
                    if (len < 0 || at + 8 + len > end) return;
                    if (tag == "LIST") Walk(at + 12, at + 8 + len);
                    else found[tag] = (at + 8, len);
                    at += 8 + len + (len & 1);
                }
            }
            Walk(12, f.Length);
            return found;
        }

        private static string Tag(byte[] f, int at) => Encoding.ASCII.GetString(f, at, 4);
        private static int U16(byte[] f, int at) => f[at] | (f[at + 1] << 8);
        private static uint U32(byte[] f, int at) =>
            (uint)(f[at] | (f[at + 1] << 8) | (f[at + 2] << 16) | (f[at + 3] << 24));

        // ── one written out for a reader that is not this one ─────────────────────────────────────

        [Fact]
        public void OneIsWrittenToDiskSoAnotherProgramCanBeAskedWhetherItIsRight()
        {
            var sdat = Sound();
            Assert.True(sdat != null, "no project, so this proved nothing");

            string dir = Environment.GetEnvironmentVariable("DSPRE_SF2_OUT");
            if (string.IsNullOrEmpty(dir)) return;      // only writes when a place to put them is given
            Directory.CreateDirectory(dir);

            // The banks with the most instruments, which are the music ones, plus a few cry banks. Most
            // of this archive is one cry per bank, so taking a plain spread would only ever check those.
            var usable = Enumerable.Range(0, sdat.Banks.Count).Where(b => sdat.Banks[b] != null).ToList();
            var biggest = usable.OrderByDescending(b =>
            {
                try { return sdat.GetBankInstruments(b)?.Count(x => x?.Regions?.Count > 0) ?? 0; }
                catch { return 0; }
            }).Take(6);
            foreach (int bank in biggest.Concat(usable.Take(2)).Distinct())
            {
                string name = sdat.BankNames.TryGetValue(bank, out var n) ? n : "Bank " + bank;
                var made = SoundFontWriter.Build(sdat, bank, name);
                if (made.Whynot != null) { _out.WriteLine($"bank {bank} refused: {made.Whynot}"); continue; }
                string path = Path.Combine(dir, $"bank{bank}.sf2");
                File.WriteAllBytes(path, made.Bytes);
                _out.WriteLine($"wrote {path}, {made.Bytes.Length} bytes: {made.Summary}");
            }
        }
    }
}
