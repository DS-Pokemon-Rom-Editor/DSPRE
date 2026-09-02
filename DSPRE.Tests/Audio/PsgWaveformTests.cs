using System;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The DS's own tone generators, which make a note without any recording behind them.
    /// </summary>
    public class PsgWaveformTests
    {
        private static SbnkRegion Square(int duty, int baseNote = 60)
            => new SbnkRegion { Psg = PsgKind.Square, PsgDuty = duty, BaseNote = baseNote };

        [Fact]
        public void ARegionWithARecordingBehindItIsLeftAlone()
            => Assert.Null(PsgWaveform.For(new SbnkRegion()));

        [Fact]
        public void ASquareIsHighForAsMuchOfItsCycleAsTheDutyAsksFor()
        {
            // Setting 0 is an eighth of the cycle, and each step up adds another eighth.
            for (int duty = 0; duty <= 6; duty++)
            {
                var w = PsgWaveform.For(Square(duty));
                int high = w.Pcm.Count(s => s > 0);
                Assert.Equal((duty + 1) * w.Pcm.Length / 8, high);
            }
        }

        [Fact]
        public void TheSilentSettingIsSilent()
        {
            var w = PsgWaveform.For(Square(7));
            Assert.DoesNotContain(w.Pcm, s => s > 0);
        }

        [Fact]
        public void ASquarePlayedAtItsOwnNoteComesOutAtThatNotesPitch()
        {
            // Middle C is 261.6 Hz, and the clip is 256 points per cycle, so its rate has to be 256 times
            // that for one pass through the clip to take exactly one cycle.
            var w = PsgWaveform.For(Square(3, baseNote: 60));
            Assert.Equal(256, w.Pcm.Length);
            Assert.True(w.Loop);
            double hz = w.SampleRate / (double)w.Pcm.Length;
            Assert.InRange(hz, 261.0, 262.3);
        }

        [Fact]
        public void EveryNoteIsAnOctaveApartFromTheNoteTwelveAbroveIt()
        {
            var low = PsgWaveform.For(Square(3, baseNote: 48));
            var high = PsgWaveform.For(Square(3, baseNote: 60));
            Assert.Equal(2.0, high.SampleRate / (double)low.SampleRate, 2);
        }

        [Fact]
        public void NoiseRunsTheShiftRegisterRightRoundOnce()
        {
            var w = PsgWaveform.For(new SbnkRegion { Psg = PsgKind.Noise, BaseNote = 60 });
            Assert.Equal(32767, w.Pcm.Length);   // fifteen bits, so 2^15-1 steps before it repeats
            Assert.True(w.Loop);

            // It has to be noise, not a pattern: both values have to turn up, in roughly equal amounts.
            int up = w.Pcm.Count(s => s > 0);
            Assert.InRange(up, w.Pcm.Length / 3, w.Pcm.Length * 2 / 3);
        }

        [Fact]
        public void TheSameSettingsGiveBackTheSameClip()
            => Assert.Same(PsgWaveform.For(Square(2)), PsgWaveform.For(Square(2)));
    }
}
