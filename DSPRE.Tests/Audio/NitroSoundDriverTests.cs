using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The sound driver on hand-built sequences, one mechanic at a time.
    /// </summary>
    public class NitroSoundDriverTests
    {
        private readonly ITestOutputHelper _out;
        public NitroSoundDriverTests(ITestOutputHelper o) => _out = o;

        private const int Rate = 32000;

        /// <summary>An SSEQ holding one track of the given bytes.</summary>
        private static byte[] Sseq(params int[] track)
        {
            var b = new byte[0x1C + track.Length];
            b[0] = (byte)'S'; b[1] = (byte)'S'; b[2] = (byte)'E'; b[3] = (byte)'Q';
            b[0x18] = 0x1C;
            for (int i = 0; i < track.Length; i++) b[0x1C + i] = (byte)track[i];
            return b;
        }

        /// <summary>A looping sine of 32 points a cycle, about 1 kHz on its own key.</summary>
        private static readonly SwavSample Sine = new SwavSample
        {
            SampleRate = Rate,
            Loop = true,
            LoopStartSample = 0,
            Pcm = Enumerable.Range(0, 32).Select(i => (short)(16000 * Math.Sin(2 * Math.PI * i / 32))).ToArray(),
        };

        private static List<SbnkInstrument> Instrument(int attack = 127, int decay = 127, int sustain = 127, int release = 127)
        {
            var inst = new SbnkInstrument();
            inst.Regions.Add(new SbnkRegion { BaseNote = 60, Attack = attack, Decay = decay, Sustain = sustain, Release = release });
            return new List<SbnkInstrument> { inst };
        }

        private static NitroSoundDriver Driver(byte[] sseq, List<SbnkInstrument> bank, NitroSoundDriver.Settings s)
            => new NitroSoundDriver(s, sseq, bank, slot => slot == 0 ? new List<SwavSample> { Sine } : null, 127, 64, 0);

        private static short[] Render(byte[] sseq, List<SbnkInstrument> bank, double seconds = 8.0)
            => Driver(sseq, bank, new NitroSoundDriver.Settings { SampleRate = Rate, MaxSeconds = seconds }).Run();

        private static double Rms(short[] pcm, double from, double to)
        {
            int a = (int)(from * Rate), b = Math.Min(pcm.Length / 2, (int)(to * Rate));
            double sum = 0;
            for (int i = a; i < b; i++) sum += (double)pcm[i * 2] * pcm[i * 2];
            return b > a ? Math.Sqrt(sum / (b - a)) : 0;
        }

        /// <summary>Frequency of the left channel between two times, from interpolated upward zero crossings.</summary>
        private static double Frequency(short[] pcm, double from, double to)
        {
            int a = (int)(from * Rate), b = Math.Min(pcm.Length / 2 - 1, (int)(to * Rate));
            double first = -1, last = -1; int crossings = 0;
            for (int i = a; i < b; i++)
            {
                double y0 = pcm[i * 2], y1 = pcm[(i + 1) * 2];
                if (y0 < 0 && y1 >= 0)
                {
                    double t = i + (-y0 / (y1 - y0));
                    if (first < 0) first = t;
                    last = t;
                    crossings++;
                }
            }
            return crossings > 1 ? (crossings - 1) * Rate / (last - first) : 0;
        }

        private static double Db(double ratio) => 20 * Math.Log10(ratio);

        private static double UpdateSeconds => NitroEnvelope.TickSeconds;

        [Fact]
        public void TempoIsAddedEveryUpdateAndATickRunsEachTimeItPasses240()
        {
            // A note on every tick: each note is followed by a one-tick rest, and notes themselves do not wait.
            var track = new List<int> { 0xC7, 0x00, 0xE1, 0, 0 };
            for (int i = 0; i < 400; i++) track.AddRange(new[] { 0x3C, 0x7F, 0x01, 0x80, 0x01 });
            track.Add(0xFF);

            int[] UpdatesOfNotes(int tempo)
            {
                track[3] = tempo & 0xFF; track[4] = tempo >> 8;
                var d = Driver(Sseq(track.ToArray()), Instrument(), new NitroSoundDriver.Settings { Mix = false, MaxSeconds = 60 });
                d.Run();
                return d.Notes.Select(n => n.Update).ToArray();
            }

            // At 240 a tick runs on every update, starting with the first.
            var fast = UpdatesOfNotes(240);
            Assert.Equal(400, fast.Length);
            Assert.Equal(Enumerable.Range(0, 400), fast);

            // At 120 it takes two updates to gather a tick.
            var half = UpdatesOfNotes(120);
            Assert.Equal(Enumerable.Range(0, 400).Select(i => i * 2), half);

            // At 89 the ticks come unevenly, two or three updates apart, and 399 ticks take 399 * 240 / 89 updates.
            var odd = UpdatesOfNotes(89);
            Assert.InRange(odd[399], 399 * 240.0 / 89 - 1, 399 * 240.0 / 89 + 1);
            var gaps = odd.Zip(odd.Skip(1), (x, y) => y - x).Distinct().OrderBy(x => x).ToArray();
            _out.WriteLine("gaps between notes at tempo 89: " + string.Join(", ", gaps));
            Assert.Equal(new[] { 2, 3 }, gaps);
        }

        [Fact]
        public void ATrackVolumeChangeReachesTheNoteThatIsAlreadySounding()
        {
            // Tempo 240, a 200-tick note, then after 60 ticks the track volume drops to 64 while the note plays on.
            var sseq = Sseq(0xC7, 0x00, 0xE1, 0xF0, 0x00, 0x3C, 0x7F, 0x81, 0x48, 0x80, 0x3C, 0xC1, 0x40, 0x80, 0x81, 0x00, 0xFF);
            var pcm = Render(sseq, Instrument());

            double before = Rms(pcm, 30 * UpdateSeconds, 55 * UpdateSeconds);
            double after = Rms(pcm, 70 * UpdateSeconds, 180 * UpdateSeconds);
            double drop = Db(after / before);
            _out.WriteLine($"before {before:F0}, after {after:F0}, {drop:F2} dB");

            // Volume 64 sits 11.9 dB down the squared table; the channel register rounds it a little.
            Assert.True(before > 1000, "the note never sounded");
            Assert.InRange(drop, -12.9, -10.9);
        }

        [Fact]
        public void TheSweepRunsOverTheNotesWrittenLengthInTicks()
        {
            // Tempo 120 (a tick every two updates), sweep -768 (an octave down), a 40-tick note on the
            // instrument's own key, and a slow release so the pitch can still be heard once the note is keyed off.
            var sseq = Sseq(0xC7, 0x00, 0xE1, 0x78, 0x00, 0xE3, 0x00, 0xFD, 0x3C, 0x7F, 0x28, 0x80, 0x81, 0x00, 0xFF);
            var pcm = Render(sseq, Instrument(release: 0));

            double baseHz = NitroSoundTables.TimerClock / (double)(NitroSoundTables.TimerClock / Rate) / 32;
            double Expected(double ticks) => baseHz * Math.Pow(2, -(1 - Math.Min(1, ticks / 40.0)));

            // Tick k runs on update 2k, and the hardware takes what that update works out one update later.
            double TickTime(double tick) => (tick * 2 + 1) * UpdateSeconds;
            foreach (double tick in new[] { 4.0, 20.0, 30.0 })
            {
                double hz = Frequency(pcm, TickTime(tick), TickTime(tick + 2));
                _out.WriteLine($"tick {tick}: {hz:F1} Hz, expected about {Expected(tick + 0.5):F1}");
                Assert.InRange(hz, Expected(tick + 0.5) * 0.98, Expected(tick + 0.5) * 1.02);
            }
            double settled = Frequency(pcm, TickTime(45), TickTime(70));
            _out.WriteLine($"after the length: {settled:F1} Hz, base {baseHz:F1}");
            Assert.InRange(settled, baseHz * 0.995, baseHz * 1.005);
        }

        [Fact]
        public void ReleaseTakesTheSameStepOffEveryUpdate()
        {
            // Tempo 240, a 10-tick note on an instrument whose release is 100: 7680 / 26 = 295 steps of 1/128 of
            // a tenth of a decibel per update, so 100 updates take 23.0 dB off.
            var sseq = Sseq(0xC7, 0x00, 0xE1, 0xF0, 0x00, 0x3C, 0x7F, 0x0A, 0x80, 0x0A, 0xFF);
            var pcm = Render(sseq, Instrument(release: 100));

            double early = Rms(pcm, 20 * UpdateSeconds, 24 * UpdateSeconds);
            double late = Rms(pcm, 120 * UpdateSeconds, 124 * UpdateSeconds);
            double fall = Db(late / early);
            _out.WriteLine($"{fall:F2} dB over 100 updates");
            Assert.InRange(fall, -24.5, -21.5);
        }

        [Fact]
        public void AttackMultipliesTheRemainingAttenuationEachUpdate()
        {
            // Attack 0: 72.3 dB down times (255/256)^n, so 22.3 dB after 300 updates and 6.9 dB after 600.
            // A note with no length, held by a long rest so the track does not end and release it.
            var sseq = Sseq(0xC7, 0x00, 0xE1, 0xF0, 0x00, 0x3C, 0x7F, 0x00, 0x80, 0xFF, 0x7F, 0xFF);
            var pcm = Render(sseq, Instrument(attack: 0), 5.0);

            double at300 = Rms(pcm, 301 * UpdateSeconds, 303 * UpdateSeconds);
            double at600 = Rms(pcm, 601 * UpdateSeconds, 603 * UpdateSeconds);
            double full = 16000 / Math.Sqrt(2) / 2;     // a centred channel puts half of it on each side
            _out.WriteLine($"300 updates: {Db(at300 / full):F2} dB, 600 updates: {Db(at600 / full):F2} dB");
            Assert.InRange(Db(at300 / full), -23.5, -21.0);
            Assert.InRange(Db(at600 / full), -8.0, -5.8);
        }

        [Fact]
        public void AnEndlessLoopKeepsPlayingToTheLimitButIsReadOnce()
        {
            // loop_start(0) note rest loop_end: forever.
            var sseq = Sseq(0xC7, 0x00, 0xD4, 0x00, 0x3C, 0x7F, 0x05, 0x80, 0x0A, 0xFC, 0xFF);
            var pcm = Render(sseq, Instrument(), 3.0);
            Assert.Equal(3.0, pcm.Length / 2 / (double)Rate, 2);
            Assert.True(Rms(pcm, 2.5, 3.0) > 1000, "the loop stopped before the limit");

            var d = Driver(sseq, Instrument(), new NitroSoundDriver.Settings { Mix = false, FollowLoops = false, MaxSeconds = 60 });
            d.Run();
            Assert.Single(d.Notes);
        }

        [Fact]
        public void ABackwardJumpKeepsPlayingToTheLimitButIsReadOnce()
        {
            // note rest jump-to-start.
            var sseq = Sseq(0xC7, 0x00, 0x3C, 0x7F, 0x05, 0x80, 0x0A, 0x94, 0x00, 0x00, 0x00);
            var pcm = Render(sseq, Instrument(), 3.0);
            Assert.True(Rms(pcm, 2.5, 3.0) > 1000, "the jump was not followed");

            var d = Driver(sseq, Instrument(), new NitroSoundDriver.Settings { Mix = false, FollowLoops = false, MaxSeconds = 60 });
            d.Run();
            Assert.Single(d.Notes);
        }

        [Fact]
        public void ASoundEffectEndsWhenItsLastNoteHasFaded()
        {
            var sseq = Sseq(0xC7, 0x00, 0xE1, 0xF0, 0x00, 0x3C, 0x7F, 0x0A, 0x80, 0x0A, 0xFF);
            var pcm = Render(sseq, Instrument(release: 120));
            double seconds = pcm.Length / 2 / (double)Rate;
            _out.WriteLine($"{seconds:F3} s");
            // Ten updates of note, then release 120 takes 10 tenths of a decibel per update off a 72.3 dB range.
            Assert.InRange(seconds, 80 * UpdateSeconds, 90 * UpdateSeconds);
        }

        private static readonly string HeartGoldSdat = Path.Combine(TestRoms.HeartGold ?? "", "files", "data", "sound", "gs_sound_data.sdat");

        /// <summary>
        /// The ball opening, SEQ_SE_DP_BOWA2, as recorded: loud from the first note, no gaps, faded by about 0.55 s.
        /// </summary>
        [SkippableFact]
        public void TheBallOpeningIsContinuousWithATail()
        {
            Skip.If(!File.Exists(HeartGoldSdat), "the extracted HeartGold project is not on this machine");
            var sdat = SdatArchive.Parse(File.ReadAllBytes(HeartGoldSdat));
            int seq = sdat.SeqNames.First(k => k.Value == "SEQ_SE_DP_BOWA2").Key;
            var pcm = SseqPlayer.Render(sdat, seq);
            Assert.NotNull(pcm);

            var windows = Enumerable.Range(0, (int)(pcm.Length / 2 / (0.02 * Rate)))
                .Select(i => Rms(pcm, i * 0.02, (i + 1) * 0.02)).ToArray();
            double loudest = windows.Max();
            _out.WriteLine(string.Join(" ", windows.Select(w => (w / loudest).ToString("F2"))));

            Assert.True(windows[0] > loudest * 0.5, "the first note is quiet");
            for (int i = 1; i < 21; i++)
                Assert.True(windows[i] > loudest * 0.1, $"gap at {i * 0.02:F2} s");
            int lastAudible = Array.FindLastIndex(windows, w => w > loudest * 0.01);
            Assert.InRange((lastAudible + 1) * 0.02, 0.45, 0.75);
        }
    }
}
