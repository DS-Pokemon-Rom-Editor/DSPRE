using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Walking an event's script and saying what would happen. Built from hand-made command lists so it
    /// runs without a ROM: the walker reads the command's own name and parameters, nothing else.
    /// </summary>
    public class ScriptWalkerTests
    {
        /// <summary>A command the way the script reader hands one over: name plus raw parameter bytes.</summary>
        private static ScriptCommand Cmd(string display, params long[] values)
            => new ScriptCommand(display, values.Select(v => BitConverter.GetBytes((ushort)v)).ToList());

        private static (List<ScriptCommandContainer> scripts, List<ScriptCommandContainer> functions) File(
            IEnumerable<ScriptCommand> script,
            params (int id, ScriptCommand[] commands)[] functions)
            => (new List<ScriptCommandContainer>
                {
                    new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script, -1, script.ToList()),
                },
                functions.Select(fn => new ScriptCommandContainer(
                    (uint)fn.id, ScriptFile.ContainerTypes.Function, -1, fn.commands.ToList())).ToList());

        private static ScriptWalker Walker(
            (List<ScriptCommandContainer> scripts, List<ScriptCommandContainer> functions) file,
            Func<int, string> messages = null)
            => new ScriptWalker(file.scripts, file.functions, messages);

        // ── what a movement actually does ───────────────────────────────
        // The reader names an action from the game's own table; these tests supply the name directly.
        private static ScriptAction Act(string name, ushort? repeats = null)
            => new ScriptAction { name = name, repetitionCount = repeats };

        [Fact]
        public void AMovementSaysWhatItActuallyDoesRatherThanJustItsNumber()
        {
            var file = File(new[] { Cmd("Movement 3 7", 3, 7), Cmd("End") });

            var actions = new List<ScriptAction> { Act("Walk_Up", 2), Act("Face_Down"), Act("End_Movement") };
            var w = new ScriptWalker(file.scripts, file.functions, null,
                                     n => n == 7 ? actions : null);
            w.Start(1);

            var step = w.Steps.First(x => x.Kind == ScriptStepKind.Movement);
            Assert.Contains("Walk_Up", step.Text);
            Assert.Contains("2", step.Text);            // it repeats twice
            Assert.Contains("Face_Down", step.Text);
            Assert.DoesNotContain("End_Movement", step.Text);   // the end marker is not an instruction
        }

        [Fact]
        public void MovementZeroIsTheFirstOneBecauseMovementsCountFromZero()
        {
            // Scripts count from one but movements count from zero: every exported script file that has
            // movements at all starts at action_0, and none of the 965 has a script_0. Taking one off a
            // movement number, the way a script number needs, reads the one before it.
            var file = File(new[] { Cmd("Movement 3 0", 3, 0), Cmd("End") });

            var movements = new List<List<ScriptAction>>
            {
                new List<ScriptAction> { Act("WalkNorth8"), Act("End_Movement") },   // this is movement 0
                new List<ScriptAction> { Act("WalkSouth8"), Act("End_Movement") },   // and this is 1
            };
            var w = new ScriptWalker(file.scripts, file.functions, null,
                                     n => n >= 0 && n < movements.Count ? movements[n] : null);
            w.Start(1);

            var step = w.Steps.First(x => x.Kind == ScriptStepKind.Movement);
            Assert.Contains("WalkNorth8", step.Text);
            Assert.DoesNotContain("WalkSouth8", step.Text);
        }

        [Fact]
        public void AMovementWithNothingToLookUpStillNamesItsNumber()
        {
            var file = File(new[] { Cmd("Movement 3 7", 3, 7), Cmd("End") });
            var w = Walker(file);
            w.Start(1);

            var step = w.Steps.First(x => x.Kind == ScriptStepKind.Movement);
            Assert.Contains("7", step.Text);
        }

        [Fact]
        public void AMovementThatIsNotThereDoesNotThrow()
        {
            var file = File(new[] { Cmd("Movement 3 99", 3, 99), Cmd("End") });
            var w = new ScriptWalker(file.scripts, file.functions, null, n => null);
            w.Start(1);
            Assert.Contains(w.Steps, x => x.Kind == ScriptStepKind.Movement);
        }

        [Fact]
        public void AVeryLongMovementIsCutShortRatherThanFillingTheScreen()
        {
            var file = File(new[] { Cmd("Movement 3 7", 3, 7), Cmd("End") });
            var many = new List<ScriptAction>();
            for (int i = 0; i < 40; i++) many.Add(Act("Walk_Up"));

            var w = new ScriptWalker(file.scripts, file.functions, null, n => many);
            w.Start(1);

            var step = w.Steps.First(x => x.Kind == ScriptStepKind.Movement);
            Assert.True(step.Text.Length < 400, "a long movement should be trimmed, not printed in full");
        }

        [Fact]
        public void AMessageIsQuotedWithItsRealText()
        {
            var file = File(new[] { Cmd("Message 5", 5), Cmd("End") });
            var w = Walker(file, id => id == 5 ? "Hello there!" : null);
            w.Start(1);

            Assert.True(w.Finished);
            Assert.Contains(w.Steps, s => s.Kind == ScriptStepKind.Message && s.Text.Contains("Hello there!"));
        }

        [Fact]
        public void WithoutTheTextItStillNamesTheMessage()
        {
            var file = File(new[] { Cmd("Message 9", 9), Cmd("End") });
            var w = Walker(file);
            w.Start(1);
            Assert.Contains(w.Steps, s => s.Kind == ScriptStepKind.Message && s.Text.Contains("9"));
        }

        [Fact]
        public void ACheckOnAVariableStopsAndAsks()
        {
            var file = File(new[]
            {
                Cmd("CompareVarValue VAR_0x4001 3", 0x4001, 3),
                Cmd("JumpIf EQUAL Function_2", 1, 2),
                Cmd("Message 1", 1),
                Cmd("End"),
            }, (2, new[] { Cmd("Message 2", 2), Cmd("End") }));

            var w = Walker(file, id => "text " + id);
            w.Start(1);

            Assert.False(w.Finished);
            Assert.NotNull(w.Pending);
            Assert.Equal(ScriptQuestion.QuestionKind.Variable, w.Pending.Kind);
            Assert.Contains("VAR_0x4001", w.Pending.Prompt);
            Assert.True(w.Pending.AcceptsAnyNumber);
        }

        [Fact]
        public void TheAnswerDecidesWhichWayTheScriptGoes()
        {
            (List<ScriptCommandContainer>, List<ScriptCommandContainer>) Build() => File(new[]
            {
                Cmd("CompareVarValue VAR_0x4001 3", 0x4001, 3),
                Cmd("JumpIf EQUAL Function_2", 1, 2),
                Cmd("Message 1", 1),
                Cmd("End"),
            }, (2, new[] { Cmd("Message 2", 2), Cmd("End") }));

            var taken = Walker(Build(), id => "text " + id);
            taken.Start(1);
            taken.Answer(3);                       // equal, so the jump is taken
            Assert.True(taken.Finished);
            Assert.Contains(taken.Steps, s => s.Text.Contains("text 2"));
            Assert.DoesNotContain(taken.Steps, s => s.Text.Contains("text 1"));

            var skipped = Walker(Build(), id => "text " + id);
            skipped.Start(1);
            skipped.Answer(0);                     // not equal, so it carries on
            Assert.True(skipped.Finished);
            Assert.Contains(skipped.Steps, s => s.Text.Contains("text 1"));
            Assert.DoesNotContain(skipped.Steps, s => s.Text.Contains("text 2"));
        }

        [Fact]
        public void EveryComparisonOperatorIsHonoured()
        {
            // The script stores how the two values ordered; the jump then tests that ordering.
            (int op, long answer, bool expectJump)[] cases =
            {
                (0, 1, true),    // LESS: 1 < 5
                (0, 9, false),
                (1, 5, true),    // EQUAL
                (2, 9, true),    // GREATER
                (3, 5, true),    // LESS/EQUAL
                (4, 1, false),   // GREATER/EQUAL with a smaller value
                (5, 1, true),    // DIFFERENT
                (5, 5, false),
            };

            foreach (var (op, answer, expectJump) in cases)
            {
                var file = File(new[]
                {
                    Cmd("CompareVarValue VAR_0x4001 5", 0x4001, 5),
                    Cmd("JumpIf X Function_2", op, 2),
                    Cmd("Message 1", 1),
                    Cmd("End"),
                }, (2, new[] { Cmd("Message 2", 2), Cmd("End") }));

                var w = Walker(file, id => "text " + id);
                w.Start(1);
                w.Answer(answer);
                bool jumped = w.Steps.Any(s => s.Text.Contains("text 2"));
                Assert.Equal(expectJump, jumped);
            }
        }

        [Fact]
        public void AFlagCheckOffersSetAndNotSet()
        {
            var file = File(new[] { Cmd("CheckFlag 33", 33), Cmd("End") });
            var w = Walker(file);
            w.Start(1);

            Assert.Equal(ScriptQuestion.QuestionKind.Flag, w.Pending.Kind);
            Assert.Equal(2, w.Pending.Options.Count);
            Assert.False(w.Pending.AcceptsAnyNumber);
            w.Answer(1);
            Assert.True(w.Finished);
        }

        [Fact]
        public void AYesNoBoxAsksTheWatcher()
        {
            var file = File(new[] { Cmd("YesNoBox VAR_0x8000", 0x8000), Cmd("End") });
            var w = Walker(file);
            w.Start(1);
            Assert.Equal(ScriptQuestion.QuestionKind.YesNo, w.Pending.Kind);
            w.Answer(1);
            Assert.True(w.Finished);
        }

        [Fact]
        public void ACallComesBackToWhereItLeftOff()
        {
            var file = File(new[]
            {
                Cmd("Call Function_2", 2),
                Cmd("Message 1", 1),
                Cmd("End"),
            }, (2, new[] { Cmd("Message 2", 2), Cmd("Return") }));

            var w = Walker(file, id => "text " + id);
            w.Start(1);

            Assert.True(w.Finished);
            var said = w.Steps.Where(s => s.Kind == ScriptStepKind.Message).Select(s => s.Text).ToArray();
            Assert.Equal(2, said.Length);
            Assert.Contains("text 2", said[0]);      // the function runs first
            Assert.Contains("text 1", said[1]);      // then it comes back
        }

        [Fact]
        public void MovementSaysWhoMovesAndHow()
        {
            var file = File(new[] { Cmd("Movement Player WalkNorth8", 255, 12), Cmd("End") });
            var w = Walker(file);
            w.Start(1);
            var step = w.Steps.First(s => s.Kind == ScriptStepKind.Movement);
            Assert.Contains("Player", step.Text);
            Assert.Contains("WalkNorth8", step.Text);
        }

        [Fact]
        public void AnythingElseJustReportsItself()
        {
            var file = File(new[] { Cmd("SetPGearMapOpenLevel 2", 2), Cmd("End") });
            var w = Walker(file);
            w.Start(1);
            var step = w.Steps.First(s => s.Kind == ScriptStepKind.Command);
            Assert.Contains("SetPGearMapOpenLevel", step.Text);
            Assert.Null(step.Effect);          // nothing the preview can act on
        }

        [Fact]
        public void TheSoundCommandsCarryWhatToPlay()
        {
            // The leak's table has 0x49 as SePlay and 0x4e as MePlay, so DSPRE's "PlayFanfare" is really
            // the sound effect and its "PlaySound" is really the fanfare. They are mapped across.
            var file = File(new[]
            {
                Cmd("PlayFanfare 1500", 1500),
                Cmd("PlaySound 1200", 1200),
                Cmd("PlayMusic 1010", 1010),
                Cmd("PlayCry 25 0", 25, 0),
                Cmd("End"),
            });
            var w = Walker(file);
            w.Start(1);

            var effects = w.Steps.Where(s => s.Effect != null).Select(s => (s.Effect.Kind, s.Effect.A)).ToArray();
            Assert.Equal(new[]
            {
                (ScriptEffectKind.SoundEffect, 1500),
                (ScriptEffectKind.Fanfare, 1200),
                (ScriptEffectKind.Music, 1010),
                (ScriptEffectKind.Cry, 25),
            }, effects);
        }

        [Fact]
        public void ACrySaysWhoseItIsSoItReadsWithoutTheSound()
        {
            // Cries are not played, so the line has to carry the meaning on its own.
            var file = File(new[] { Cmd("PlayCry 25 0", 25, 0), Cmd("End") });
            var w = Walker(file);
            w.Start(1);

            var step = w.Steps.First(s => s.Effect?.Kind == ScriptEffectKind.Cry);
            Assert.Contains("cry", step.Text, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(25, step.Effect.A);
        }

        [Fact]
        public void TheSeamlessCameraIsRecognisedByItsUnnamedNumberToo()
        {
            // DSPRE has no name for this command, so the raw CMD_610 is matched as well.
            foreach (string name in new[] { "MoveSeamlessCamera 1", "CMD_610 1" })
            {
                var file = File(new[] { Cmd(name, 1), Cmd("End") });
                var w = Walker(file);
                w.Start(1);
                var e = w.Steps.First(s => s.Effect != null).Effect;
                Assert.Equal(ScriptEffectKind.CameraChange, e.Kind);
                Assert.Equal(1, e.A);
            }
        }

        [Fact]
        public void ShakingCarriesAllFourOfItsNumbers()
        {
            var file = File(new[] { Cmd("ShakeCamera 4 2 3 8", 4, 2, 3, 8), Cmd("End") });
            var w = Walker(file);
            w.Start(1);

            var e = w.Steps.First(s => s.Effect != null).Effect;
            Assert.Equal(ScriptEffectKind.CameraShake, e.Kind);
            Assert.Equal((4, 2, 3, 8), (e.A, e.B, e.C, e.D));
        }

        [Fact]
        public void AMovementCarriesWhoMovesAndWhichMovement()
        {
            var file = File(new[] { Cmd("Movement 3 7", 3, 7), Cmd("End") });
            var w = Walker(file);
            w.Start(1);

            var e = w.Steps.First(s => s.Kind == ScriptStepKind.Movement).Effect;
            Assert.Equal(ScriptEffectKind.Movement, e.Kind);
            Assert.Equal(3, e.A);
            Assert.Equal(7, e.B);
        }

        [Fact]
        public void AJumpToNowhereDoesNotStopTheWalk()
        {
            var file = File(new[] { Cmd("Jump Function_99", 99), Cmd("Message 1", 1), Cmd("End") });
            var w = Walker(file, id => "text " + id);
            w.Start(1);
            Assert.True(w.Finished);
            Assert.Contains(w.Steps, s => s.Text.Contains("isn't in this file"));
        }

        [Fact]
        public void AScriptThatLoopsForeverIsCutOff()
        {
            var file = File(new[] { Cmd("Jump Function_2", 2) },
                            (2, new[] { Cmd("Jump Function_2", 2) }));
            var w = Walker(file);
            w.Start(1);
            Assert.True(w.Finished);
            Assert.Contains(w.Steps, s => s.Text.Contains("keeps going round"));
            Assert.True(w.Steps.Count <= ScriptWalker.MaxSteps + 2);
        }

        [Fact]
        public void AMissingScriptSaysSoRatherThanThrowing()
        {
            var file = File(new[] { Cmd("End") });
            var w = Walker(file);
            w.Start(42);
            Assert.True(w.Finished);
            Assert.Contains(w.Steps, s => s.Text.Contains("no script 42"));
        }
    }
}
