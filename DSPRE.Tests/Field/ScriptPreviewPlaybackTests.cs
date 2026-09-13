using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels.Battle;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Scripts played through the preview the way stepping in plays them, without a window: the game's own
    /// windows turn up when the script reaches them, answers land where scripts read them, people move side
    /// by side, and the map's own scripts and triggers go off when they should.
    /// </summary>
    [Collection("rom")]
    public class ScriptPreviewPlaybackTests
    {
        // ── a tiny stage ─────────────────────────────────────────────────────────────

        private static ScriptCommand Cmd(string name, params long[] values) =>
            new ScriptCommand(name, values.Select(v => BitConverter.GetBytes((ushort)v)).ToList());

        private static ScriptCommandContainer Script(uint number, params ScriptCommand[] commands) =>
            new ScriptCommandContainer(number, ScriptFile.ContainerTypes.Script, -1, commands.ToList());

        private static ScriptCommandContainer Function(uint number, params ScriptCommand[] commands) =>
            new ScriptCommandContainer(number, ScriptFile.ContainerTypes.Function, -1, commands.ToList());

        private static List<ScriptAction> Moves(params string[] names) =>
            names.Select(n => new ScriptAction { name = n, repetitionCount = 1 }).ToList();

        private static Overworld Person(int id, int x, int z, int script, ushort flag = 0, ushort movement = 0, short range = 0) =>
            new Overworld(id, 0, 0)
            {
                xMapPosition = (short)x, yMapPosition = (short)z,
                scriptNumber = (ushort)script,
                orientation = (short)MoveFacing.Down,
                flag = flag, movement = movement, xRange = range, yRange = range,
            };

        private sealed class Stage
        {
            public readonly List<ScriptCommandContainer> Scripts = new List<ScriptCommandContainer>();
            public readonly List<ScriptCommandContainer> Functions = new List<ScriptCommandContainer>();
            public readonly Dictionary<int, string> Text = new Dictionary<int, string>();
            public readonly List<List<ScriptAction>> Movements = new List<List<ScriptAction>>();
            public readonly EventFile Events = new EventFile();
            public LevelScriptFile LevelScripts;

            public AnimatedPreviewViewModel Open(int standBesideX, int standBesideZ)
            {
                var open = new MapCollisionGrid();
                open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);
                var vm = new AnimatedPreviewViewModel
                {
                    MeasureText = t => (t ?? "").Length * 6,
                    Family = RomInfo.GameFamilies.HGSS,
                    PlaySounds = false,
                };
                vm.LevelScripts = LevelScripts;
                vm.Load(new NsbmdRenderModel { CellStrideX = 1f, CellStrideZ = 1f, Scale = 1f }, null, Events, false, open, 0,
                        (x, z) => (x, 0f, z),
                        _ => new ScriptWalker(Scripts, Functions,
                                              id => Text.TryGetValue(id, out string s) ? s : null,
                                              n => n >= 0 && n < Movements.Count ? Movements[n] : null));
                vm.StandBeside(standBesideX, standBesideZ);
                vm.StepInto = true;
                return vm;
            }
        }

        private static void RunUntil(AnimatedPreviewViewModel vm, Func<bool> done, int frames = 3000, Action eachFrame = null)
        {
            for (int i = 0; i < frames && !done(); i++)
            {
                eachFrame?.Invoke();
                vm.Advance(1);
            }
            Assert.True(done(), "the preview never got there: " + string.Join(" | ", vm.ScriptLines)
                                + $" || box={vm.MessageText} prompt={vm.HasStatePrompt} running={vm.ScriptRunning}"
                                + $" camera={vm.CameraTarget()} player={vm.PlayerWorldPosition()}");
        }

        private static void Step(AnimatedPreviewViewModel vm, MoveFacing dir)
        {
            vm.Move(dir);
            vm.Advance(FieldPlayer.WalkFrames + 1);
        }

        /// <summary>Taps A the way a player reads through, until the script lets go.</summary>
        private static void ReadToTheEnd(AnimatedPreviewViewModel vm)
        {
            for (int i = 0; i < 200 && vm.ScriptRunning; i++)
            {
                vm.PressA();
                vm.Advance(1);
                vm.ReleaseA();
                vm.Advance(4);
            }
            Assert.False(vm.ScriptRunning, "the script never let go: " + string.Join(" | ", vm.ScriptLines));
        }

        // ── yes or no ────────────────────────────────────────────────────────────────

        private static Stage YesNoStage()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Do you like tests?";
            s.Text[2] = "No?";
            s.Text[3] = "Yes!";
            s.Scripts.Add(Script(1,
                Cmd("Message", 0), Cmd("YesNoBox", 0x800C),
                Cmd("CompareVarValue", 0x800C, 0), Cmd("JumpIf", 1, 1),
                Cmd("Message", 2), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            s.Functions.Add(Function(1, Cmd("Message", 3), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            return s;
        }

        [Fact]
        public void TheYesNoBoxOnlyComesUpOnceTheQuestionHasPrinted()
        {
            var vm = YesNoStage().Open(5, 5);
            vm.Interact();

            RunUntil(vm, () => vm.HasChoiceWindow, eachFrame: () =>
            {
                // Never early: while the words are still going in there is no box to answer.
                if (vm.HasChoiceWindow) Assert.Equal("Do you like tests?", vm.MessageText);
            });
            Assert.Equal("Do you like tests?", vm.MessageText);
            Assert.Equal(new[] { "YES", "NO" }, vm.ChoiceItems);
            Assert.Equal(0, vm.ChoiceCursor);
            Assert.False(vm.HasStatePrompt);
        }

        [Theory]
        [InlineData(0, "Yes!")]
        [InlineData(1, "No?")]
        public void TheAnswerTakesTheBranchTheScriptWroteForIt(int cursor, string reply)
        {
            var vm = YesNoStage().Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);

            vm.MoveChoiceCursor(cursor);
            vm.ConfirmChoice();
            RunUntil(vm, () => vm.MessageText == reply);
            // The answer went into the variable, and nothing was asked about it.
            Assert.True(vm.GameState.TryGetVar(0x800C, out long held));
            Assert.Equal(cursor, held);
            Assert.DoesNotContain(vm.ScriptLines, l => l.Contains("What is it?"));
        }

        [Fact]
        public void BOnTheYesNoBoxAnswersNo()
        {
            var vm = YesNoStage().Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);
            vm.CancelChoice();
            RunUntil(vm, () => vm.MessageText == "No?");
        }

        // ── menus ────────────────────────────────────────────────────────────────────

        private static Stage MenuStage()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Pick a snack.";
            s.Text[5] = "BERRY"; s.Text[6] = "COOKIE"; s.Text[7] = "STAY"; s.Text[8] = "CAKE";
            s.Text[9] = "Good choice.";
            s.Text[10] = "You backed out.";
            s.Scripts.Add(Script(1,
                Cmd("Message", 0),
                Cmd("MultiLocalText", 20, 3, 0, 1, 0x800C),
                Cmd("AddMultiOption", 5, 0), Cmd("AddMultiOption", 6, 1), Cmd("AddMultiOption", 7, 2), Cmd("AddMultiOption", 8, 3),
                Cmd("ShowMulti"),
                Cmd("CompareVarValue", 0x800C, ScriptWalker.MenuCancelled), Cmd("JumpIf", 1, 1),
                Cmd("Message", 9), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            s.Functions.Add(Function(1, Cmd("Message", 10), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            return s;
        }

        [Fact]
        public void AMenuShowsItsEntriesWhereTheScriptPutItAndWrapsRound()
        {
            var vm = MenuStage().Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);

            Assert.Equal(new[] { "BERRY", "COOKIE", "STAY", "CAKE" }, vm.ChoiceItems);
            Assert.Equal((20, 3), (vm.ChoiceLeft, vm.ChoiceTop));
            vm.MoveChoiceCursor(-1);
            Assert.Equal(3, vm.ChoiceCursor);           // four or more entries wrap
            vm.MoveChoiceCursor(1);
            Assert.Equal(0, vm.ChoiceCursor);

            vm.MoveChoiceCursor(1);
            vm.ConfirmChoice();
            RunUntil(vm, () => vm.MessageText == "Good choice.");
            Assert.True(vm.GameState.TryGetVar(0x800C, out long picked) && picked == 1);
        }

        [Fact]
        public void BackingOutOfAMenuWritesTheCancelValue()
        {
            var vm = MenuStage().Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);
            vm.CancelChoice();
            RunUntil(vm, () => vm.MessageText == "You backed out.");
        }

        [Fact]
        public void TheTouchScreenYesNoIsAskedOnTheBottomScreen()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Tap one.";
            s.Text[1] = "You tapped NO.";
            s.Scripts.Add(Script(1,
                Cmd("Message", 0), Cmd("YesNoTouchScreen", 0x800C),
                Cmd("CompareVarValue", 0x800C, 1), Cmd("JumpIf", 1, 1), Cmd("End")));
            s.Functions.Add(Function(1, Cmd("Message", 1), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));

            var vm = s.Open(5, 5);
            Assert.True(vm.ShowTouchScreen);
            vm.Interact();
            RunUntil(vm, () => vm.HasTouchChoice);
            Assert.False(vm.HasChoiceWindow);
            Assert.Equal(2, vm.ChoiceEntries.Count);

            vm.ChoiceCursor = 1;
            Assert.True(vm.ChoiceEntries[1].IsSelected);
            vm.ConfirmChoice();
            RunUntil(vm, () => vm.MessageText == "You tapped NO.");
        }

        [Fact]
        public void AMenuWritesItsEntriesElevenPixelsInWithTheCursorAtTheEdge()
        {
            var vm = MenuStage().Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);
            Assert.Equal((11, 0, 0), (vm.ChoiceIndent, vm.ChoiceCursorX, vm.ChoiceRowOffset));
            Assert.Equal(0, vm.ChoiceHeightTiles);        // two tiles an entry, worked out by the window
        }

        [Fact]
        public void BOnAMenuThatCannotBeBackedOutOfBeepsAndLeavesItUp()
        {
            var s = MenuStage();
            s.Scripts[0].commands[1] = Cmd("MultiLocalText", 20, 3, 0, 0, 0x800C);
            var vm = s.Open(5, 5);
            var beeps = new List<int>();
            vm.PlaySound = (kind, id) => { if (kind == ScriptEffectKind.SoundEffect) beeps.Add(id); };
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);
            beeps.Clear();

            vm.CancelChoice();
            vm.Advance(10);
            Assert.True(vm.HasChoiceWindow);
            Assert.Equal(new[] { AnimatedPreviewViewModel.MenuSound }, beeps);
        }

        private static Stage ListStage(int entries)
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Pick one of many.";
            var commands = new List<ScriptCommand> { Cmd("Message", 0), Cmd("ListLocalText", 1, 1, 0, 1, 0x800C) };
            for (int i = 0; i < entries; i++)
            {
                s.Text[10 + i] = $"ENTRY {i}";
                commands.Add(Cmd("AddListOption", 10 + i, 0xFFFF, i));
            }
            commands.Add(Cmd("ShowList"));
            commands.Add(Cmd("End"));
            s.Scripts.Add(Script(1, commands.ToArray()));
            return s;
        }

        [Fact]
        public void AListShowsEightRowsAndScrollsToKeepTheCursorOnShow()
        {
            var vm = ListStage(10).Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);

            Assert.Equal((12, 2, 1), (vm.ChoiceIndent, vm.ChoiceCursorX, vm.ChoiceRowOffset));
            Assert.Equal(AnimatedPreviewViewModel.ListRows, vm.ChoiceItems.Count);
            Assert.Equal(AnimatedPreviewViewModel.ListRows * 2, vm.ChoiceHeightTiles);
            Assert.Equal(10, vm.ChoiceAllItems.Count);

            for (int i = 0; i < 9; i++) vm.MoveChoiceCursor(1);
            Assert.Equal(9, vm.ChoiceCursor);
            Assert.Equal(AnimatedPreviewViewModel.ListRows - 1, vm.ChoiceCursorRow);
            Assert.Equal("ENTRY 2", vm.ChoiceItems[0]);

            // A list does not wrap round the way a menu does.
            vm.MoveChoiceCursor(1);
            Assert.Equal(9, vm.ChoiceCursor);
        }

        [Fact]
        public void LeftAndRightPageThroughAList()
        {
            var vm = ListStage(20).Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);

            vm.PageChoice(1);
            Assert.Equal(AnimatedPreviewViewModel.ListRows, vm.ChoiceCursor);
            vm.PageChoice(1);
            vm.PageChoice(1);
            Assert.Equal(19, vm.ChoiceCursor);
            vm.PageChoice(-1);
            Assert.Equal(11, vm.ChoiceCursor);
            Assert.Contains("ENTRY 11", vm.ChoiceItems);
        }

        [Fact]
        public void PagingDoesNothingOnAnOrdinaryMenu()
        {
            var vm = MenuStage().Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasChoiceWindow);
            vm.PageChoice(1);
            Assert.Equal(0, vm.ChoiceCursor);
        }

        [Fact]
        public void GivingUpOnAScriptTakesTheBoxDownAndLetsEverybodyMoveAgain()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Events.overworlds.Add(Person(2, 12, 12, 0, movement: 0x03, range: 3));
            s.Text[0] = "This one runs for ages.";
            s.Scripts.Add(Script(1,
                Cmd("LockAll"), Cmd("Message", 0), Cmd("WaitButton"), Cmd("CloseMessage"),
                Cmd("WaitTime", 600, 0x800C), Cmd("ReleaseAll"), Cmd("End")));

            var vm = s.Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.MessageVisible && vm.ScriptRunning);
            Assert.True(vm.Npcs[1].Motion.Paused);

            vm.StopScript();
            vm.Advance(2);
            Assert.False(vm.ScriptRunning);
            Assert.False(vm.MessageVisible);
            Assert.Null(vm.Question);
            Assert.False(vm.Npcs[1].Motion.Paused, "the wanderer was left frozen");
            Assert.Contains(vm.ScriptLines, l => l.Contains("Stopped"));
        }

        // ── HeartGold and SoulSilver's touch screen ──────────────────────────────────

        [Fact]
        public void TheTouchYesNoBlinksItsFrameBeforeTheAnswerCounts()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Tap one.";
            s.Text[1] = "You tapped NO.";
            s.Scripts.Add(Script(1,
                Cmd("Message", 0), Cmd("OpenTouchScreen"), Cmd("YesNoTouchScreen", 0x800C), Cmd("CloseTouchScreen"),
                Cmd("CompareVarValue", 0x800C, 1), Cmd("JumpIf", 1, 1), Cmd("End")));
            s.Functions.Add(Function(1, Cmd("Message", 1), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));

            var vm = s.Open(5, 5);
            Assert.False(vm.TouchScreenShowsChoices);
            vm.Interact();
            RunUntil(vm, () => vm.HasTouchChoice);
            // The Poké Ball screen came up, faded all the way back in, before the question.
            Assert.True(vm.TouchScreenShowsChoices);
            Assert.Equal(1.0, vm.TouchScreenBrightness);
            Assert.True(vm.TouchChoiceIsYesNo);
            Assert.Equal(2, vm.TouchChoiceItems.Count);

            vm.TouchChoice(1);
            var shown = new List<bool>();
            for (int i = 0; i < 13; i++)
            {
                Assert.True(vm.HasTouchChoice, $"the answer counted {i} frames in, before the blink was over");
                shown.Add(vm.TouchCursorShown);
                vm.Advance(1);
            }
            Assert.Contains(false, shown);
            Assert.Contains(true, shown);

            RunUntil(vm, () => vm.MessageText == "You tapped NO.");
            Assert.False(vm.TouchScreenShowsChoices);     // put back before the reply
        }

        private static Stage TouchListStage(bool cancellable)
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Choose on the touch screen.";
            string[] names = { "ONE", "TWO", "THREE", "FOUR", "FIVE" };
            var commands = new List<ScriptCommand>
            {
                Cmd("Message", 0), Cmd("OpenTouchScreen"),
                Cmd("MultiTouchLocalText", 1, 1, 2, cancellable ? 1 : 0, 0x800C),
            };
            for (int i = 0; i < names.Length; i++)
            {
                s.Text[5 + i] = names[i];
                s.Text[20 + i] = "About " + names[i].ToLowerInvariant();
                commands.Add(Cmd("CreateMultiTouchBox", 5 + i, 20 + i, 10 + i));
            }
            commands.Add(Cmd("CloseMultiTouch"));
            commands.Add(Cmd("CloseTouchScreen"));
            commands.Add(Cmd("End"));
            s.Scripts.Add(Script(1, commands.ToArray()));
            return s;
        }

        [Fact]
        public void ATouchListStartsAtTheTopAndDescribesTheEntryUnderTheCursor()
        {
            var vm = TouchListStage(cancellable: false).Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasTouchChoice);

            Assert.False(vm.TouchChoiceIsYesNo);
            Assert.Equal(new[] { "ONE", "TWO", "THREE", "FOUR", "FIVE" }, vm.TouchChoiceItems);
            Assert.Equal(0, vm.ChoiceCursor);             // whatever the script asked for

            // Two columns: down stays in the left one, right crosses over.
            vm.MoveChoiceCursor(1);
            Assert.Equal(2, vm.ChoiceCursor);
            Assert.Equal("About three", vm.MessageText);
            vm.PageChoice(1);
            Assert.Equal(3, vm.ChoiceCursor);
            Assert.Equal("About four", vm.MessageText);

            // This one cannot be backed out of.
            vm.CancelChoice();
            vm.Advance(20);
            Assert.True(vm.HasTouchChoice);

            vm.TouchChoice(4);
            RunUntil(vm, () => !vm.HasTouchChoice);
            Assert.True(vm.GameState.TryGetVar(0x800C, out long picked));
            Assert.Equal(14, picked);                     // the entry's value, not its place
        }

        [Fact]
        public void BOnATouchListThatAllowsItPicksTheLastEntry()
        {
            var vm = TouchListStage(cancellable: true).Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasTouchChoice);
            vm.CancelChoice();
            RunUntil(vm, () => !vm.HasTouchChoice);
            Assert.True(vm.GameState.TryGetVar(0x800C, out long picked));
            Assert.Equal(14, picked);
        }

        [Fact]
        public void TheTouchMenusAButtonSaysWhatAWouldDo()
        {
            var vm = YesNoStage().Open(5, 5);
            Assert.Equal(DSPRE.Avalonia.Data.HgssTouchScreen.TalkMessage, vm.TouchALabel);

            vm.Move(MoveFacing.Down);                     // turn away from them
            Assert.Equal(DSPRE.Avalonia.Data.HgssTouchScreen.CheckMessage, vm.TouchALabel);

            vm.Move(MoveFacing.Up);
            vm.Interact();
            RunUntil(vm, () => vm.MessageVisible);
            Assert.Equal(DSPRE.Avalonia.Data.HgssTouchScreen.NextMessage, vm.TouchALabel);
        }

        // ── what only the game would know ────────────────────────────────────────────

        [Fact]
        public void BagSpaceIsAskedOnceInPlainWordsAndRemembered()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Your bag is full.";
            s.Text[1] = "Your bag has room.";
            s.Scripts.Add(Script(1,
                Cmd("CheckItemSpace", 1, 1, 0x800C), Cmd("CompareVarValue", 0x800C, 1), Cmd("JumpIf", 1, 1),
                Cmd("Message", 0), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            s.Functions.Add(Function(1, Cmd("Message", 1), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));

            var vm = s.Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.HasStatePrompt);
            Assert.Equal("Something the game knows", vm.QuestionTitle);
            Assert.Contains("room in the bag", vm.QuestionPrompt);
            Assert.Equal(new[] { "Room", "No room" }, vm.AnswerOptions);

            vm.AnswerOption(0);
            RunUntil(vm, () => vm.MessageText == "Your bag has room.");
            ReadToTheEnd(vm);
            Assert.Contains(vm.GameStateEntries, e => e.Fact != null && e.Value == "1");

            // Talking again uses what was said instead of asking.
            vm.Interact();
            RunUntil(vm, () => vm.MessageText == "Your bag has room.", eachFrame: () => Assert.False(vm.HasStatePrompt));
        }

        // ── people moving ────────────────────────────────────────────────────────────

        [Fact]
        public void APersonAndThePlayerMoveTogetherAndWaitMovementWaitsForBoth()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(6, 5, 5, 1));
            s.Movements.Add(Moves("WalkEast8", "WalkEast8"));
            s.Movements.Add(Moves("WalkSouth8"));
            s.Text[0] = "We both moved.";
            s.Scripts.Add(Script(1,
                Cmd("Movement", 6, 0), Cmd("Movement", ScriptWalker.PlayerObject, 1), Cmd("WaitMovement"),
                Cmd("Message", 0), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));

            var vm = s.Open(5, 5);
            int startZ = vm.Player.TileZ;
            vm.Interact();
            vm.Advance(3);
            Assert.True(vm.Npcs[0].Motion.IsWalking);
            Assert.True(vm.Player.IsWalking);
            Assert.Null(vm.MessageText);

            RunUntil(vm, () => vm.MessageText != null);
            Assert.Equal(2, vm.Npcs[0].Motion.OffsetX);
            Assert.Equal(startZ + 1, vm.Player.TileZ);
            Assert.False(vm.Npcs[0].Motion.IsScripted);
            Assert.False(vm.Player.IsScripted);
        }

        [Fact]
        public void LockAllStopsSomebodyWanderingAndReleaseAllLetsThemGo()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Events.overworlds.Add(Person(2, 12, 12, 0, movement: 0x03, range: 3));
            s.Scripts.Add(Script(1, Cmd("LockAll"), Cmd("WaitTime", 600, 0x800C), Cmd("ReleaseAll"), Cmd("End")));

            var vm = s.Open(5, 5);
            vm.Interact();
            vm.Advance(OverworldAnimator.WalkFrames + 2);    // a step already under way finishes
            var wanderer = vm.Npcs[1].Motion;
            var held = (wanderer.OffsetX, wanderer.OffsetZ);
            vm.Advance(500);
            Assert.Equal(held, (wanderer.OffsetX, wanderer.OffsetZ));

            RunUntil(vm, () => !vm.ScriptRunning);
            RunUntil(vm, () => (wanderer.OffsetX, wanderer.OffsetZ) != held, frames: 2000);
        }

        [Fact]
        public void SomebodyTakenOffTheMapComesBackOnceTheirFlagIsCleared()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(9, 5, 5, 1));
            s.Events.overworlds.Add(Person(10, 9, 5, 0, flag: 0x07F0));
            s.Scripts.Add(Script(1,
                Cmd("RemoveOW", 10), Cmd("WaitTime", 30, 0x800C),
                Cmd("ClearFlag", 0x07F0), Cmd("AddOW", 10), Cmd("End")));

            var vm = s.Open(5, 5);
            Assert.Equal(0, vm.HiddenCount);
            vm.Interact();
            RunUntil(vm, () => vm.HiddenCount == 1);
            Assert.True(vm.GameState.TryGetFlag(0x07F0, out bool set) && set);

            RunUntil(vm, () => !vm.ScriptRunning);
            Assert.Equal(0, vm.HiddenCount);
        }

        [Fact]
        public void TheCameraFollowsTheObjectAScriptHandsItToAndComesBack()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Movements.Add(Moves("WalkNorth8", "WalkNorth8", "WalkNorth8", "WalkNorth8"));
            s.Scripts.Add(Script(1,
                Cmd("GetPlayerPosition", 0x8000, 0x8001), Cmd("LockCamera", 0x8000, 0x8001),
                Cmd("Movement", ScriptWalker.CameraObject, 0), Cmd("WaitMovement"),
                Cmd("WaitTime", 60, 0x800C), Cmd("ReleaseCamera"), Cmd("End")));

            var vm = s.Open(5, 5);
            var (px, _, pz) = vm.PlayerWorldPosition();
            vm.Interact();
            RunUntil(vm, () => Math.Abs(vm.CameraTarget().z - (pz - 4)) < 0.01f);
            Assert.Equal(px, vm.CameraTarget().x, 3);

            RunUntil(vm, () => !vm.ScriptRunning);
            Assert.Equal(pz, vm.CameraTarget().z, 3);
        }

        // ── the map's own scripts ────────────────────────────────────────────────────

        [Fact]
        public void AFlagSetAsTheMapLoadsKeepsThatPersonOffIt()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Events.overworlds.Add(Person(11, 9, 5, 1, flag: 0x07F1));
            s.Scripts.Add(Script(1, Cmd("End")));
            s.Scripts.Add(Script(2, Cmd("SetFlag", 0x07F1), Cmd("End")));
            s.LevelScripts = new LevelScriptFile();
            s.LevelScripts.bufferSet.Add(new MapScreenLoadTrigger(LevelScriptTrigger.MAPCHANGE, 2));

            var vm = s.Open(5, 5);
            Assert.Equal(1, vm.HiddenCount);
            Assert.False(vm.HasStatePrompt);
        }

        [Fact]
        public void AVariableAScriptSetsSetsOffTheLevelScriptWatchingIt()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "The watched variable changed!";
            s.Scripts.Add(Script(1, Cmd("SetVar", 0x4052, 1), Cmd("End")));
            s.Scripts.Add(Script(2, Cmd("Message", 0), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            s.LevelScripts = new LevelScriptFile();
            s.LevelScripts.bufferSet.Add(new VariableValueTrigger(2, 0x4052, 1));

            var vm = s.Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => !vm.ScriptRunning || vm.MessageText != null);
            if (vm.MessageText == null)
            {
                // The engine looks at these on the next step taken.
                Step(vm, MoveFacing.Left);
                Step(vm, MoveFacing.Left);
            }
            RunUntil(vm, () => vm.MessageText == "The watched variable changed!");
        }

        [Fact]
        public void ThePressThatEndsOneScriptDoesNotReadThroughTheNextOne()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 5, 1));
            s.Text[0] = "Take a step after this.";
            s.Text[1] = "The watched variable changed!";
            s.Scripts.Add(Script(1, Cmd("Message", 0), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("SetVar", 0x4052, 1), Cmd("End")));
            s.Scripts.Add(Script(2, Cmd("LockAll"), Cmd("Message", 1), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("End")));
            s.LevelScripts = new LevelScriptFile();
            s.LevelScripts.bufferSet.Add(new VariableValueTrigger(2, 0x4052, 1));

            var vm = s.Open(5, 5);
            vm.Interact();
            RunUntil(vm, () => vm.MessageText == "Take a step after this.");
            vm.Advance(30);

            // One tap, held the way a finger holds it for a few frames.
            vm.PressA();
            vm.Advance(3);
            vm.ReleaseA();
            RunUntil(vm, () => vm.MessageText != null && vm.MessageText.StartsWith("The watched"), frames: 120);

            vm.Advance(300);
            Assert.True(vm.ScriptRunning, "the watcher's box went away without a press: " + string.Join(" | ", vm.ScriptLines));
            Assert.Equal("The watched variable changed!", vm.MessageText);
        }

        [Fact]
        public void ATriggerWhoseVariableIsKnownGoesOffWithoutAsking()
        {
            var s = new Stage();
            s.Events.overworlds.Add(Person(1, 5, 2, 1));
            s.Events.triggers.Add(new Trigger(0, 0) { xMapPosition = 5, yMapPosition = 8, scriptNumber = 2, variableWatched = 0x4051, expectedVarValue = 0 });
            s.Text[0] = "You found the trigger.";
            s.Scripts.Add(Script(1, Cmd("End")));
            s.Scripts.Add(Script(2, Cmd("Message", 0), Cmd("WaitButton"), Cmd("CloseMessage"), Cmd("SetVar", 0x4051, 1), Cmd("End")));

            var vm = s.Open(5, 5);
            vm.GameState.SetVar(0x4051, 0);
            vm.StandOn(5, 7, MoveFacing.Down);
            vm.Move(MoveFacing.Down);
            RunUntil(vm, () => vm.MessageText == "You found the trigger.", eachFrame: () => Assert.False(vm.HasStatePrompt));
            ReadToTheEnd(vm);

            // Its variable now holds 1, so stepping off and back on again does nothing.
            Step(vm, MoveFacing.Up);
            Step(vm, MoveFacing.Down);
            Assert.False(vm.ScriptRunning);
            Assert.False(vm.HasStatePrompt);
        }
    }
}
