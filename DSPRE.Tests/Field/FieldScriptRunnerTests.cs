using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Playing a script out on the field's clock the way the script VM does: movements run side by side
    /// until WaitMovement, a shake holds for as long as it shakes, a message holds until it is printed, and
    /// every satisfied wait hands the next command the following frame.
    /// </summary>
    public class FieldScriptRunnerTests
    {
        private static ScriptStep Step(ScriptStepKind kind, string text, ScriptEffect effect = null)
            => new ScriptStep { Kind = kind, Text = text, Effect = effect };

        [Fact]
        public void AMovementDoesNotHoldTheScriptUpOnItsOwn()
        {
            // ApplyMovement returns straight away, so two in a row start together on the same frame.
            var started = new List<(int who, int which)>();
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                StartMovement = (who, which) => { started.Add((who, which)); return 24; },
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Movement, "npc", new ScriptEffect(ScriptEffectKind.Movement, 3, 7)),
                Step(ScriptStepKind.Movement, "player", new ScriptEffect(ScriptEffectKind.Movement, 255, 8)),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(1);
            Assert.Equal(new[] { (3, 7), (255, 8) }, started);
            Assert.Equal(new[] { "npc", "player", "afterwards" }, seen);
        }

        [Fact]
        public void WaitMovementHoldsUntilEveryMovementHasLandedAndThreeFramesMore()
        {
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                StartMovement = (who, which) => who == 1 ? 8 : 16,
                Report = s => { if (s.Effect == null) seen.Add(s.Text); },
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Movement, "short", new ScriptEffect(ScriptEffectKind.Movement, 1, 0)),
                Step(ScriptStepKind.Movement, "long", new ScriptEffect(ScriptEffectKind.Movement, 2, 0)),
                Step(ScriptStepKind.Command, "wait", new ScriptEffect(ScriptEffectKind.WaitMovement)),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            // The longer movement lands on frame 16, the script sees it on 19 and carries on at 20.
            runner.Advance(18);
            Assert.Equal(FieldScriptRunner.WaitKind.Movement, runner.Waiting);
            runner.Advance(1);
            Assert.Empty(seen);
            runner.Advance(1);
            Assert.Equal(new[] { "afterwards" }, seen);
        }

        [Fact]
        public void WaitTimeRunsTheNextCommandFramesPlusOneLater()
        {
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks { Report = s => { if (s.Effect == null) seen.Add(s.Text); } });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Command, "wait", new ScriptEffect(ScriptEffectKind.WaitFrames, 30)),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(31);
            Assert.Empty(seen);
            runner.Advance(1);
            Assert.Equal(new[] { "afterwards" }, seen);
        }

        [Fact]
        public void LockAllGivesUpTheRestOfItsFrame()
        {
            var seen = new List<string>();
            var locks = new List<int>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                Apply = e => locks.Add(e.A),
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Command, "lock", new ScriptEffect(ScriptEffectKind.Lock, -1)),
                Step(ScriptStepKind.Command, "next"),
            });

            runner.Advance(1);
            Assert.Equal(new[] { "lock" }, seen);
            Assert.Equal(new[] { -1 }, locks);
            runner.Advance(1);
            Assert.Equal(new[] { "lock", "next" }, seen);
        }

        [Fact]
        public void AButtonWaitEndsOnAPressAndTheDPadOnlyWhenItSaysSo()
        {
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks { Report = s => { if (s.Effect == null) seen.Add(s.Text); } });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Command, "a only", new ScriptEffect(ScriptEffectKind.WaitButton, 0)),
                Step(ScriptStepKind.Command, "pad too", new ScriptEffect(ScriptEffectKind.WaitButton, 2)),
                Step(ScriptStepKind.Command, "done"),
            });

            runner.Advance(1);
            Assert.False(runner.ButtonTakesPad);
            runner.Pressed(pad: true);
            runner.Advance(5);
            Assert.Equal(FieldScriptRunner.WaitKind.Button, runner.Waiting);

            runner.Pressed();
            runner.Advance(2);
            Assert.True(runner.ButtonTakesPad);
            Assert.True(runner.ButtonTurnsPlayer);
            runner.Pressed(pad: true);
            runner.Advance(2);
            Assert.Equal(new[] { "done" }, seen);
        }

        [Fact]
        public void AQuestionHoldsUntilTheWalkerIsAnswered()
        {
            ScriptCommand Cmd(string name, params long[] values) =>
                new ScriptCommand(name, values.Select(v => System.BitConverter.GetBytes((ushort)v)).ToList());

            var walker = new ScriptWalker(
                new List<ScriptCommandContainer>
                {
                    new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script, -1, new List<ScriptCommand>
                    {
                        Cmd("CheckFlag 5", 5),
                        Cmd("PlayFanfare 1500", 1500),
                        Cmd("End"),
                    }),
                },
                new List<ScriptCommandContainer>()) { LegacyNameOf = _ => null };

            ScriptQuestion asked = null;
            var played = new List<int>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                Ask = q => asked = q,
                PlaySound = (k, id) => played.Add(id),
            });

            walker.Begin(1);
            runner.Play(walker);
            runner.Advance(10);
            Assert.NotNull(asked);
            Assert.Empty(played);

            walker.Answer(1);
            runner.Advance(1);          // the check finds it answered
            Assert.Empty(played);
            runner.Advance(1);
            Assert.Equal(new[] { 1500 }, played);
            runner.Advance(1);
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

            // The shake runs 24 frames; the script finds it over on the last and carries on the next.
            runner.Advance(25);
            Assert.Equal(new[] { (4, 2, 3, 8) }, shakes);
            Assert.Empty(seen);

            runner.Advance(1);
            Assert.Equal(new[] { "afterwards" }, seen);
        }

        [Fact]
        public void AMessageWaitsForThePrinterRatherThanAClock()
        {
            var shown = new List<string>();
            var seen = new List<string>();
            bool printing = false;
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                ShowMessage = e => { shown.Add(e.Text); printing = true; return true; },
                MessagePrinting = () => printing,
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Message, "hello", new ScriptEffect(ScriptEffectKind.Message) { Text = "hello" }),
                Step(ScriptStepKind.Command, "afterwards"),
            });

            runner.Advance(1);
            Assert.Equal(new[] { "hello" }, shown);
            Assert.True(runner.WaitingOnReader);

            // However long the clock runs, it stays put until the printer is done.
            runner.Advance(600);
            Assert.DoesNotContain("afterwards", seen);

            printing = false;
            runner.Advance(1);
            Assert.DoesNotContain("afterwards", seen);
            runner.Advance(1);
            Assert.Contains("afterwards", seen);
        }

        [Fact]
        public void AnInstantMessageDoesNotWait()
        {
            var seen = new List<string>();
            var runner = new FieldScriptRunner(new FieldScriptRunner.Hooks
            {
                ShowMessage = _ => true,
                MessagePrinting = () => true,
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Message, "all at once", new ScriptEffect(ScriptEffectKind.Message, 1) { Text = "x" }),
                Step(ScriptStepKind.Command, "afterwards"),
            });

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
                MessagePrinting = () => true,
                Report = s => seen.Add(s.Text),
            });

            runner.Play(new[]
            {
                Step(ScriptStepKind.Message, "nothing to show", new ScriptEffect(ScriptEffectKind.Message) { Text = "x" }),
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
