using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Playing a script out on the field's clock: a movement holds things up while it runs, a shake holds
    /// for as long as it shakes, and a message holds until the reader presses on.
    /// </summary>
    public class FieldScriptRunnerTests
    {
        private static ScriptStep Step(ScriptStepKind kind, string text, ScriptEffect effect = null)
            => new ScriptStep { Kind = kind, Text = text, Effect = effect };

        [Fact]
        public void AMovementHoldsTheScriptUpForAsLongAsItTakes()
        {
            var started = new List<(int who, int which)>();
            var after = new List<string>();

            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                StartMovement = (who, which) => { started.Add((who, which)); return 24; },
                Report = s => { if (s.Effect == null) after.Add(s.Text); },
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Movement, "move", new ScriptEffect(ScriptEffectKind.Movement, 3, 7)),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(1);
            Assert.Equal(new[] { (3, 7) }, started);
            Assert.Empty(after);

            // Still holding while the movement plays.
            runner.Advance(23);
            Assert.Empty(after);

            runner.Advance(1);
            Assert.Equal(new[] { "afterwards" }, after);
            Assert.False(runner.Running);
        }

        [Fact]
        public void SoundsArePlayedWithTheirOwnKind()
        {
            var played = new List<(ScriptEffectKind kind, int id)>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                PlaySound = (k, id) => played.Add((k, id)),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Command, "se", new ScriptEffect(ScriptEffectKind.SoundEffect, 1500)),
                Step(ScriptStepKind.Command, "me", new ScriptEffect(ScriptEffectKind.Fanfare, 1200)),
                Step(ScriptStepKind.Command, "bgm", new ScriptEffect(ScriptEffectKind.Music, 1010)),
                Step(ScriptStepKind.Command, "cry", new ScriptEffect(ScriptEffectKind.Cry, 25)),
            });
            runner.Advance(10);

            Assert.Equal(new[]
            {
                (ScriptEffectKind.SoundEffect, 1500),
                (ScriptEffectKind.Fanfare, 1200),
                (ScriptEffectKind.Music, 1010),
                (ScriptEffectKind.Cry, 25),
            }, played);
        }

        [Fact]
        public void SoundsDoNotHoldTheScriptUpOnTheirOwn()
        {
            // Only a wait command makes a script pause for a sound; playing one carries straight on.
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks { Report = s => seen.Add(s.Text) });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Command, "se", new ScriptEffect(ScriptEffectKind.SoundEffect, 1)),
                Step(ScriptStepKind.Command, "next"),
            });
            runner.Advance(2);
            Assert.Equal(new[] { "se", "next" }, seen);
        }

        [Fact]
        public void AShakeHoldsForItsWholeLength()
        {
            var shakes = new List<(int, int, int, int)>();
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                ShakeCamera = (a, b, c, d) => shakes.Add((a, b, c, d)),
                Report = s => { if (s.Effect == null) seen.Add(s.Text); },
            });

            // Three passes of eight frames each is twenty four frames.
            runner.Play(new[]
            {
                Step(ScriptStepKind.Command, "shake", new ScriptEffect(ScriptEffectKind.CameraShake, 4, 2, 3, 8)),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(24);
            Assert.Equal(new[] { (4, 2, 3, 8) }, shakes);
            Assert.Empty(seen);

            runner.Advance(1);
            Assert.Equal(new[] { "afterwards" }, seen);
        }

        [Fact]
        public void AMessageWaitsForTheReaderRatherThanAClock()
        {
            var shown = new List<string>();
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                ShowMessage = t => { shown.Add(t); return true; },
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Message, "hello"),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(1);
            Assert.Equal(new[] { "hello" }, shown);
            Assert.True(runner.WaitingOnReader);

            // However long the clock runs, it stays put until the reader moves on.
            runner.Advance(600);
            Assert.DoesNotContain("afterwards", seen);

            runner.ReaderMovedOn();
            runner.Advance(1);
            Assert.Contains("afterwards", seen);
        }

        [Fact]
        public void AMessageThatOpensNoBoxDoesNotStallTheScriptForever()
        {
            // Nothing on screen means nothing for the reader to press on, so holding there would wedge
            // the script. It carries straight on instead.
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                ShowMessage = _ => false,          // the box refused to open
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Message, "nothing to show"),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(2);
            Assert.False(runner.WaitingOnReader);
            Assert.Contains("afterwards", seen);
        }

        [Fact]
        public void StoppingClearsEverything()
        {
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks());
            runner.Play(new[] { Step(ScriptStepKind.Command, "one"), Step(ScriptStepKind.Command, "two") });
            Assert.True(runner.Running);
            runner.Stop();
            Assert.False(runner.Running);
            Assert.Equal(0, runner.StepCount);
        }

        // ── the shake itself ────────────────────────────────────────────────────────────
        [Fact]
        public void TheShakeTurnsAFullCircleOfSineOverEachPassAndComesBackToNothing()
        {
            var shake = new FieldCameraShake(width: 8, height: 4, count: 2, framesPerPass: 8);
            Assert.True(shake.Running);

            var xs = new List<float>();
            for (int i = 0; i < 8; i++) { shake.Advance(1); xs.Add(shake.OffsetX); }

            // Sine over a whole turn: up, back through zero, down, and home again at the end of the pass.
            Assert.True(xs.Take(3).Max() > 0f, "the first half of the pass should push one way");
            Assert.True(xs.Skip(4).Take(3).Min() < 0f, "the second half should push the other way");
            Assert.Equal(0f, xs[7], 3);

            // The across and down amounts keep the ratio they were given.
            var s2 = new FieldCameraShake(8, 4, 1, 8);
            s2.Advance(2);
            Assert.Equal(s2.OffsetX / 2f, s2.OffsetY, 3);
        }

        [Fact]
        public void TheShakeStopsAfterTheNumberOfPassesItWasGiven()
        {
            var shake = new FieldCameraShake(8, 8, count: 3, framesPerPass: 4);
            shake.Advance(3 * 4);
            Assert.False(shake.Running);
            Assert.Equal(0f, shake.OffsetX, 3);
            Assert.Equal(0f, shake.OffsetY, 3);

            // And keeps still afterwards rather than drifting.
            shake.Advance(100);
            Assert.Equal(0f, shake.OffsetX, 3);
        }

        [Fact]
        public void AShakeOfNoPassesDoesNothingAtAll()
        {
            var shake = new FieldCameraShake(8, 8, 0, 8);
            Assert.False(shake.Running);
            shake.Advance(20);
            Assert.Equal(0f, shake.OffsetX, 3);
        }
    }
}
