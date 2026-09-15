using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Walking an event's script and saying what would happen. </summary>
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

        [Theory]
        [InlineData(1u, "WalkNorth8", "WalkSouth8")]    // read from a ROM, movements are numbered from one
        [InlineData(2u, "WalkSouth8", "WalkNorth8")]
        public void AMovementIsFoundByItsOwnNumberNotItsPlaceInTheList(uint number, string wanted, string other)
        {
            var file = File(new[] { Cmd($"Movement 3 {number}", 3, number), Cmd("End") });
            var actions = new List<ScriptActionContainer>
            {
                new ScriptActionContainer(1, new List<ScriptAction> { Act("WalkNorth8"), Act("End_Movement") }),
                new ScriptActionContainer(2, new List<ScriptAction> { Act("WalkSouth8"), Act("End_Movement") }),
            };
            var w = new ScriptWalker(file.scripts, file.functions, null, ScriptWalker.ActionsById(actions));
            w.Start(1);

            var step = w.Steps.First(x => x.Kind == ScriptStepKind.Movement);
            Assert.Contains(wanted, step.Text);
            Assert.DoesNotContain(other, step.Text);
        }

        [Fact]
        public void AFileWhoseMovementsStartAtZeroStillFindsThem()
        {
            var actions = new List<ScriptActionContainer>
            {
                new ScriptActionContainer(0, new List<ScriptAction> { Act("WalkNorth8") }),
                new ScriptActionContainer(1, new List<ScriptAction> { Act("WalkSouth8") }),
            };
            var lookup = ScriptWalker.ActionsById(actions);
            Assert.Equal("WalkNorth8", lookup(0)[0].name);
            Assert.Equal("WalkSouth8", lookup(1)[0].name);
            Assert.Null(lookup(2));
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
        public void AYesNoBoxWritesYesAsZeroForTheCompareThatFollows()
        {
            // ShowYesNoMenu writes MENU_YES 0 or MENU_NO 1 into its variable; the script then compares it.
            (List<ScriptCommandContainer>, List<ScriptCommandContainer>) Build() => File(new[]
            {
                Cmd("YesNoBox 32780", 0x800C),
                Cmd("CompareVarValue 32780 0", 0x800C, 0),
                Cmd("JumpIf EQUAL Function_2", 1, 2),
                Cmd("Message 1", 1),
                Cmd("End"),
            }, (2, new[] { Cmd("Message 2", 2), Cmd("End") }));

            var yes = Walker(Build(), id => "text " + id);
            yes.Start(1);
            Assert.Equal(ScriptQuestion.QuestionKind.YesNo, yes.Pending.Kind);
            Assert.True(yes.Pending.IsInGame);
            Assert.Equal(("YES", 0L), yes.Pending.Options[0]);
            yes.Answer(ScriptWalker.YesValue);
            while (yes.Next()) { }

            // Nothing is asked about the variable: the box already said what it holds.
            Assert.True(yes.Finished);
            Assert.Contains(yes.Steps, s => s.Text.Contains("text 2"));

            var no = Walker(Build(), id => "text " + id);
            no.Start(1);
            no.Answer(1);
            while (no.Next()) { }
            Assert.True(no.Finished);
            Assert.Contains(no.Steps, s => s.Text.Contains("text 1"));
        }

        [Fact]
        public void AnAnsweredVariableIsNotAskedAboutAgain()
        {
            var file = File(new[]
            {
                Cmd("CompareVarValue 16385 3", 0x4001, 3),
                Cmd("CompareVarValue 16385 4", 0x4001, 4),
                Cmd("End"),
            });
            var w = Walker(file);
            w.Start(1);
            Assert.NotNull(w.Pending);
            w.Answer(3);
            while (w.Next()) { }
            Assert.True(w.Finished);
            Assert.Single(w.Steps, s => s.Kind == ScriptStepKind.Question);
            Assert.True(w.State.TryGetVar(0x4001, out long held) && held == 3);
        }

        [Fact]
        public void SettingAFlagAnswersTheCheckThatFollows()
        {
            var file = File(new[] { Cmd("SetFlag 40", 40), Cmd("CheckFlag 40", 40), Cmd("End") });
            var w = Walker(file);
            w.Start(1);
            Assert.True(w.Finished);
            Assert.Null(w.Pending);
        }

        [Fact]
        public void TheOldAndNewNamesOfACommandWalkTheSame()
        {
            // The newer database names many commands only by number, so the number picks the old name.
            string Legacy(ushort id) => id switch { 0x5E => "Movement", 0x02 => "End", 0x2C => "Message", _ => null };

            foreach (var (move, message, end) in new[] { ("ApplyMovement 3 7", "NPCMsg 5", "End"), ("ScrCmd_094 3 7", "ScrCmd_045 5", "ScrCmd_002") })
            {
                var file = File(new[]
                {
                    new ScriptCommand(move, new List<byte[]> { BitConverter.GetBytes((ushort)3), BitConverter.GetBytes((ushort)7) }, 0x5E),
                    new ScriptCommand(message, new List<byte[]> { BitConverter.GetBytes((ushort)5) }, 0x2C),
                    new ScriptCommand(end, new List<byte[]>(), 0x02),
                });
                var w = new ScriptWalker(file.scripts, file.functions, id => "words " + id) { LegacyNameOf = Legacy };
                w.Start(1);

                Assert.True(w.Finished);
                var effects = w.Steps.Where(s => s.Effect != null).Select(s => s.Effect.Kind).ToArray();
                Assert.Equal(new[] { ScriptEffectKind.Movement, ScriptEffectKind.Message }, effects);
                Assert.Equal("words 5", w.Steps.First(s => s.Effect?.Kind == ScriptEffectKind.Message).Effect.Text);
            }
        }

        [Fact]
        public void AMenuIsBuiltFromItsEntriesAndItsAnswerLandsInTheVariable()
        {
            var file = File(new[]
            {
                Cmd("MultiLocalText 20 3 0 1 32780", 20, 3, 0, 1, 0x800C),
                Cmd("AddMultiOption 10 0", 10, 0),
                Cmd("AddMultiOption 11 1", 11, 1),
                Cmd("ShowMulti"),
                Cmd("End"),
            });
            var w = Walker(file, id => id == 10 ? "BUY" : id == 11 ? "SELL" : null);
            w.Start(1);

            Assert.Equal(ScriptQuestion.QuestionKind.Menu, w.Pending.Kind);
            Assert.Equal(new[] { "BUY", "SELL" }, w.Pending.Options.Select(o => o.Label));
            Assert.Equal((20, 3), (w.Pending.X, w.Pending.Y));
            Assert.True(w.Pending.Cancellable);

            w.Answer(1);
            while (w.Next()) { }
            Assert.True(w.State.TryGetVar(0x800C, out long picked) && picked == 1);
        }

        [Fact]
        public void ThePlayersPositionIsReadRatherThanAsked()
        {
            var file = File(new[]
            {
                Cmd("GetPlayerPosition 16384 16385", 0x4000, 0x4001),
                Cmd("CompareVarValue 16385 397", 0x4001, 397),
                Cmd("End"),
            });
            var w = new ScriptWalker(file.scripts, file.functions) { PlayerPosition = () => (549, 397) };
            w.Start(1);
            Assert.True(w.Finished);
            Assert.DoesNotContain(w.Steps, s => s.Kind == ScriptStepKind.Question);
        }

        [Fact]
        public void WhileAMapLoadsNothingIsAsked()
        {
            var file = File(new[] { Cmd("CheckFlag 9", 9), Cmd("CompareVarValue 16385 3", 0x4001, 3), Cmd("End") });
            var w = new ScriptWalker(file.scripts, file.functions) { GuessUnknowns = true };
            w.Start(1);
            Assert.True(w.Finished);
            Assert.False(w.State.TryGetFlag(9, out _));
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
            // The games' own table has 0x49 as SePlay and 0x4e as MePlay, so DSPRE's "PlayFanfare" is really
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

        [Fact]
        public void ScriptZeroRunsNothingRatherThanTheFirstScript()
        {
            var file = File(new[] { Cmd("SetFlag 5", 5), Cmd("End") });
            var w = Walker(file);
            w.Start(0);
            Assert.True(w.Finished);
            var step = Assert.Single(w.Steps);
            Assert.Equal("Script 0 runs nothing.", step.Text);
        }
    }
}
