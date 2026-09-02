using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.ViewModels.Battle;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The animated preview, driven the way the window drives it but without a window. </summary>
    [Collection("rom")]
    public class AnimatedPreviewTests
    {
        private const string Project = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Archive = Project + @"\files\a\1\4\0";

        /// <summary>The real HeartGold water animation. </summary>
        private static TextureSrtAnimation RequireWater()
        {
            Assert.True(File.Exists(Archive),
                $"these tests read the real animation out of {Archive}, and it is not there");
            byte[] b = File.ReadAllBytes(Archive);
            int fat = 16, img = 16;
            int count = BitConverter.ToInt32(b, fat + 8);
            img += BitConverter.ToInt32(b, img + 4);
            img += BitConverter.ToInt32(b, img + 4);
            int start = BitConverter.ToInt32(b, fat + 12);
            int end = BitConverter.ToInt32(b, fat + 16);
            Assert.True(count > 0);
            var water = TextureSrtAnimation.Load(b.Skip(img + 8 + start).Take(end - start).ToArray());
            Assert.NotNull(water);
            return water;
        }

        /// <summary>A stand-in scene with one water part and one part the animation never touches.</summary>
        private static NsbmdRenderModel SceneWith(params string[] materialNames)
        {
            var m = new NsbmdRenderModel { CellStrideX = 1f, CellStrideZ = 1f, Scale = 1f };
            for (int i = 0; i < materialNames.Length; i++) m.MaterialNameByKey[i] = materialNames[i];
            return m;
        }

        [Fact]
        public void OnlyTheMaterialsTheAnimationNamesGetAMatrix()
        {
            var water = RequireWater();

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("river", "roof", "sea_on"), water, null);

            var mats = vm.TextureMatrices;
            Assert.NotNull(mats);
            Assert.True(mats.ContainsKey(0));      // river
            Assert.False(mats.ContainsKey(1));     // roof: the animation says nothing about it
            Assert.True(mats.ContainsKey(2));      // sea_on
        }

        [Fact]
        public void WaterMovesAsTheClockRuns()
        {
            var water = RequireWater();

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("river"), water, null);
            float[] start = (float[])vm.TextureMatrices[0].Clone();

            vm.Advance(90);
            float[] later = vm.TextureMatrices[0];

            // Translation is elements 6 and 7 of the column-major 3x3.
            Assert.NotEqual(start[6], later[6]);
            Assert.Equal(start[0], later[0]);       // and nothing but the translation changed
            Assert.Equal(start[4], later[4]);
        }

        [Fact]
        public void PausingStopsTheClock()
        {
            var water = RequireWater();

            var vm = new AnimatedPreviewViewModel { Playing = false };
            vm.Load(SceneWith("river"), water, null);
            float[] start = (float[])vm.TextureMatrices[0].Clone();

            vm.Advance(90);
            Assert.Equal(start[6], vm.TextureMatrices[0][6]);
        }

        [Fact]
        public void TurningWaterOffHandsTheRendererNothing()
        {
            var water = RequireWater();

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("river"), water, null);
            Assert.NotNull(vm.TextureMatrices);

            vm.AnimateTerrain = false;
            Assert.Null(vm.TextureMatrices);
        }

        [Fact]
        public void AMapWithNoAnimationAnimatesNothing()
        {
            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("river"), null, null);
            Assert.Null(vm.TextureMatrices);
        }

        [Fact]
        public void PeopleTurnWhereTheirMovementCodeSaysTheyShould()
        {
            // A spinning overworld: it should be facing somewhere else after one spin interval.
            var ow = new Overworld(owID: 0, xMatrixPosition: 0, yMatrixPosition: 0)
            {
                movement = 0x13,                 // spin, clockwise
                orientation = (short)MoveFacing.Up,
            };
            var events = new EventFile();
            events.overworlds.Add(ow);

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("grass"), null, events);
            Assert.Equal(MoveFacing.Up, vm.Facings[0]);

            vm.Advance(OverworldAnimator.SpinIntervalFrames);
            Assert.Equal(MoveFacing.Right, vm.Facings[0]);
        }

        [Fact]
        public void RestartPutsEveryoneBackWhereTheyStarted()
        {
            var ow = new Overworld(0, 0, 0) { movement = 0x13, orientation = (short)MoveFacing.Up };
            var events = new EventFile();
            events.overworlds.Add(ow);

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("grass"), null, events);
            vm.Advance(OverworldAnimator.SpinIntervalFrames * 3);
            Assert.NotEqual(MoveFacing.Up, vm.Facings[0]);

            vm.Restart();
            Assert.Equal(MoveFacing.Up, vm.Facings[0]);
            Assert.Equal(0, vm.Frame);
        }

        // ── what actually reaches the screen ──────────────────────────────────────────── The unit tests
        // on OverworldAnimator prove it walks.

        private static Overworld Walker(byte moveCode, int tileX = 5, int tileZ = 5, ushort range = 4)
            => new Overworld(1, 0, 0)
            {
                movement = moveCode,
                orientation = (short)MoveFacing.Down,
                xRange = (short)range,
                yRange = (short)range,
                xMapPosition = (short)tileX,
                yMapPosition = (short)tileZ,
            };

        private static AnimatedPreviewViewModel WithWalker(byte moveCode = 0x03)
        {
            // Load the project so the overworld actually gets a sprite. Without this the sprite list is
            // empty and every check below would pass while proving nothing.
            try { new RomInfo("IPKE", Project); } catch { }

            var events = new EventFile();
            events.overworlds.Add(Walker(moveCode));

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("grass"), null, events);
            vm.PlaceNpcs(_ => (0f, 0f, 0f));
            return vm;
        }

        /// <summary>
        /// There has to be a sprite before any of this is worth measuring, so no sprite is a failure rather
        /// than something to shrug off and carry on past.
        /// </summary>
        private static void RequireSprite(AnimatedPreviewViewModel vm)
        {
            Assert.True(Directory.Exists(Project), $"these tests need the project at {Project}");
            Assert.True(vm.Sprites.Count > 0,
                "the project is present but the overworld got no sprite, so this test would prove nothing");
        }

        [Fact]
        public void TheDrawnSpriteSlidesRatherThanJumping()
        {
            var vm = WithWalker();
            RequireSprite(vm);

            float lastX = vm.Sprites[0].Cx, lastZ = vm.Sprites[0].Cz;
            float biggest = 0f;
            int vanished = 0;

            for (int i = 0; i < 2000; i++)
            {
                vm.Advance(1);

                // Somebody who is on the map has to be drawn on every frame. Letting a gap slide here
                // would also hide real jumps, because the next frame's move would look like one step.
                if (vm.Sprites.Count == 0) { vanished++; continue; }

                float moved = Math.Abs(vm.Sprites[0].Cx - lastX) + Math.Abs(vm.Sprites[0].Cz - lastZ);
                biggest = Math.Max(biggest, moved);
                lastX = vm.Sprites[0].Cx; lastZ = vm.Sprites[0].Cz;
            }

            Assert.Equal(0, vanished);

            // One tile is 1/32 of a cell here, covered over eight frames, so a frame moves about 1/256.
            float oneTile = 1f / MapFile.mapSize;
            Assert.True(biggest <= oneTile / OverworldAnimator.WalkFrames + 0.0005f,
                $"the drawn sprite jumped {biggest}, which is more than one frame of walking");
            Assert.True(biggest > 0f, "the sprite never moved at all");
        }

        [Fact]
        public void SomebodyWhoOnlyTurnsIsDrawnStockStill()
        {
            var vm = WithWalker(0x02);              // look around: turns, never walks
            RequireSprite(vm);

            float x = vm.Sprites[0].Cx, z = vm.Sprites[0].Cz;
            for (int i = 0; i < 2000; i++)
            {
                vm.Advance(1);
                Assert.NotEmpty(vm.Sprites);
                Assert.Equal(x, vm.Sprites[0].Cx, 5);
                Assert.Equal(z, vm.Sprites[0].Cz, 5);
            }
        }

        [Fact]
        public void PausingFreezesTheWalkToo()
        {
            var vm = WithWalker();
            RequireSprite(vm);

            vm.Advance(200);                        // get it moving
            vm.Playing = false;
            float x = vm.Sprites[0].Cx, z = vm.Sprites[0].Cz;
            vm.Advance(500);
            Assert.Equal(x, vm.Sprites[0].Cx, 5);
            Assert.Equal(z, vm.Sprites[0].Cz, 5);
        }

        [Fact]
        public void TheCameraKeepsUpAcrossTheGroundAndOnlyEasesItsHeight()
        {
            var ow = new Overworld(0, 0, 0) { movement = 0x02, orientation = (short)MoveFacing.Down };
            var events = new EventFile();
            events.overworlds.Add(ow);

            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);

            // Height rises with x, so a step sideways changes the height as well as the position and the
            // two can be told apart.
            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("grass"), null, events, false, open, 0,
                    (x, z) => (x, x * 10f, z));
            Assert.NotNull(vm.Player);
            vm.StepInto = true;

            var history = new List<(float x, float y, float z)> { vm.PlayerWorldPosition() };
            var seen = new List<((float x, float y, float z) cam, int frame)>();

            for (int i = 0; i < 40; i++)
            {
                vm.Move(MoveFacing.Right);
                for (int f = 0; f < FieldPlayer.WalkFrames; f++)
                {
                    vm.Advance(1);
                    history.Add(vm.PlayerWorldPosition());
                    seen.Add((vm.CameraTarget(), history.Count - 1));
                }
            }

            foreach (var (cam, frame) in seen)
            {
                // Across the ground it sits on the player, with no delay at all.
                Assert.Equal(history[frame].x, cam.x, 4);
                Assert.Equal(history[frame].z, cam.z, 4);

                // The height is the one thing that lags, by six frames.
                Assert.Equal(history[Math.Max(0, frame - FieldCamera.TrailFrames)].y, cam.y, 4);
            }

            // The whole thing is worthless if the player never actually went anywhere.
            Assert.True(history[history.Count - 1].x - history[0].x > 1f, "the player never moved");
            Assert.NotEqual(history[history.Count - 1].y, history[0].y);
        }

        [Fact]
        public void SomebodyWalkingOneTileMovesExactlyOneTilesWorthOfScene()
        {
            // People are drawn with their walked offset added to where their feet were put, and their feet
            // come from the same place the player's do.
            const float stride = 16f;      // one cell across, in raw scene units
            const float scale = 0.05f;     // and the scene is shrunk by this much to fit the view

            var scene = new NsbmdRenderModel { CellStrideX = stride, CellStrideZ = stride, Scale = scale };
            scene.MaterialNameByKey[0] = "grass";

            var ow = new Overworld(1, 0, 0)
            {
                movement = 0x05,                       // walks left and right, so it only moves on x
                orientation = (short)MoveFacing.Right,
                xRange = 8, yRange = 8,
                xMapPosition = 5, yMapPosition = 5,
            };
            var events = new EventFile();
            events.overworlds.Add(ow);

            // The same placement the editors use: whole tiles, in the scene's own normalised units.
            (float x, float y, float z) TileToWorld(float tx, float tz)
                => (tx * stride / MapFile.mapSize * scale, 0f, tz * stride / MapFile.mapSize * scale);

            float oneTile = TileToWorld(1, 0).x - TileToWorld(0, 0).x;
            Assert.True(oneTile > 0);

            var vm = new AnimatedPreviewViewModel();
            vm.Load(scene, null, events, false, null, 3, TileToWorld);
            vm.PlaceNpcs(o => TileToWorld(o.xMapPosition, o.yMapPosition));

            var npc = vm.Npcs[0];
            float startX = npc.FootX + npc.Motion.DrawOffsetX * vm.TileWidth;

            // Wind on until it has finished a whole step.
            int guard = 0;
            while (npc.Motion.OffsetX == 0 && guard++ < 500) vm.Advance(1);
            Assert.True(npc.Motion.OffsetX != 0, "it never took a step");
            while (npc.Motion.IsWalking && guard++ < 500) vm.Advance(1);

            float movedX = npc.FootX + npc.Motion.DrawOffsetX * vm.TileWidth - startX;
            int tiles = Math.Abs(npc.Motion.OffsetX);

            // However many tiles it got through, it has to have moved exactly that many tiles of scene.
            Assert.Equal(tiles * oneTile, Math.Abs(movedX), 5);
        }

        // ── flags that keep people off the map ──────────────────────────────────────────
        private static AnimatedPreviewViewModel WithFlaggedPeople()
        {
            var events = new EventFile();
            // Two people share a flag, one has its own, one is never gated.
            events.overworlds.Add(new Overworld(0, 0, 0) { movement = 0x02, xMapPosition = 4, yMapPosition = 4, flag = 300 });
            events.overworlds.Add(new Overworld(1, 0, 0) { movement = 0x02, xMapPosition = 6, yMapPosition = 4, flag = 300 });
            events.overworlds.Add(new Overworld(2, 0, 0) { movement = 0x02, xMapPosition = 8, yMapPosition = 4, flag = 512 });
            events.overworlds.Add(new Overworld(3, 0, 0) { movement = 0x02, xMapPosition = 10, yMapPosition = 4, flag = 0 });

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("grass"), null, events);
            vm.PlaceNpcs(_ => (0f, 0f, 0f));
            return vm;
        }

        [Fact]
        public void TheFlagListNamesEveryFlagThePeopleHereAreGatedOn()
        {
            var vm = WithFlaggedPeople();

            // Flag 0 is the one nothing sets, so it is not something to offer switching.
            Assert.Equal(new ushort[] { 300, 512 }, vm.EventFlags.Select(f => f.Number).ToArray());
            Assert.Equal(2, vm.EventFlags[0].Users);
            Assert.Equal(1, vm.EventFlags[1].Users);
            Assert.True(vm.HasEventFlags);
            Assert.Equal(0, vm.HiddenCount);
        }

        [Fact]
        public void TurningAFlagOnTakesEveryoneWhoCarriesItAway()
        {
            var vm = WithFlaggedPeople();

            vm.EventFlags.First(f => f.Number == 300).IsSet = true;
            Assert.Equal(2, vm.HiddenCount);

            // The ungated one and the one on the other flag are still here.
            Assert.True(vm.IsPresent(vm.Events.overworlds[3]));
            Assert.True(vm.IsPresent(vm.Events.overworlds[2]));
            Assert.False(vm.IsPresent(vm.Events.overworlds[0]));
            Assert.False(vm.IsPresent(vm.Events.overworlds[1]));

            vm.EventFlags.First(f => f.Number == 300).IsSet = false;
            Assert.Equal(0, vm.HiddenCount);
        }

        [Fact]
        public void SomebodyAFlagTookAwayCannotBeBumpedInto()
        {
            var vm = WithFlaggedPeople();
            var gone = vm.Events.overworlds[0];
            int x = gone.xMapPosition, z = gone.yMapPosition;

            // While they are here the tile is theirs, and once the flag is set it is free.
            Assert.NotNull(FieldInteraction.OverworldAt(vm.Events, x, z, vm.FlagIsSet));
            vm.EventFlags.First(f => f.Number == 300).IsSet = true;
            Assert.Null(FieldInteraction.OverworldAt(vm.Events, x, z, vm.FlagIsSet));
            Assert.DoesNotContain((x, z), FieldInteraction.OccupiedTiles(vm.Events, vm.FlagIsSet));
        }

        [Fact]
        public void AMapWhereNobodyIsGatedOffersNoFlags()
        {
            var events = new EventFile();
            events.overworlds.Add(new Overworld(0, 0, 0) { movement = 0x02, flag = 0 });

            var vm = new AnimatedPreviewViewModel();
            vm.Load(SceneWith("grass"), null, events);
            Assert.False(vm.HasEventFlags);
            Assert.Empty(vm.EventFlags);
        }

        // ── the box an NPC talks from ───────────────────────────────────────────────────
        [Fact]
        public void TheBoxTakesTheWordsOutOfWhatTheScriptViewerSays()
        {
            // The viewer writes a message as a sentence with the words quoted inside it.
            Assert.Equal("I like SHORTS!",
                AnimatedPreviewViewModel.Spoken("The script shows \"I like SHORTS!\""));

            // Nothing quoted means there is nothing to strip.
            Assert.Equal("plain words", AnimatedPreviewViewModel.Spoken("plain words"));
            Assert.Equal("", AnimatedPreviewViewModel.Spoken(null));
        }

        [Fact]
        public void ReadingOnePageAtATimeGetsThroughTheWholeThingAndThenCloses()
        {
            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            Assert.False(vm.MessageVisible);

            vm.ShowMessage(string.Join(" ", Enumerable.Repeat("WORD", 60)));
            Assert.True(vm.MessageVisible);

            int pages = 0;
            while (vm.MessageVisible)
            {
                Assert.True(pages++ < 100, "the box never closed");
                // Never more lines than the box shows, and never wider than it is.
                var lines = vm.MessageText.Split('\n');
                Assert.True(lines.Length <= FieldMessageWindow.LinesPerPage);
                foreach (var l in lines)
                    Assert.True(l.Length * 6 <= FieldMessageWindow.TextWidth);

                // The arrow is shown on every page but the last.
                Assert.Equal(pages < 100 && vm.MessageHasMore, vm.MessageHasMore);
                vm.AdvanceMessage();
            }

            Assert.True(pages > 1, "this should have taken more than one page");
            Assert.Null(vm.MessageText);
        }

        [Fact]
        public void TheArrowOnlyShowsWhileThereIsMoreToRead()
        {
            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            vm.ShowMessage("short");

            Assert.True(vm.MessageVisible);
            Assert.False(vm.MessageHasMore);      // it all fits, so nothing follows
            vm.AdvanceMessage();
            Assert.False(vm.MessageVisible);
        }

        [Fact]
        public void SteppingBackOutClosesTheBox()
        {
            var ow = new Overworld(0, 0, 0) { movement = 0x02 };
            var events = new EventFile();
            events.overworlds.Add(ow);

            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            vm.Load(SceneWith("grass"), null, events);
            vm.StepInto = true;
            vm.ShowMessage("hello there");
            Assert.True(vm.MessageVisible);

            vm.StepInto = false;
            Assert.False(vm.MessageVisible);
        }

        // ── talking to somebody ─────────────────────────────────────────────────────────
        [Fact]
        public void FacingSomebodyAndPressingTalkFindsThem()
        {
            // The player and the overworlds have to agree about which tile is which. If one counted in
            // whole-matrix tiles and the other in map-relative ones, talking would never find anybody.
            var ow = new Overworld(7, 0, 0)
            {
                movement = 0x00,                    // stands still
                orientation = (short)MoveFacing.Down,
                xMapPosition = 6, yMapPosition = 5,
                scriptNumber = 1,
            };
            var events = new EventFile();
            events.overworlds.Add(ow);

            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);

            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            vm.Load(SceneWith("grass"), null, events, false, open, 0, (x, z) => (x, 0f, z));
            Assert.NotNull(vm.Player);
            vm.StepInto = true;

            // Stand next to them and look their way.
            while (vm.Player.TileX > 5 && vm.Move(MoveFacing.Left) != null) vm.Advance(FieldPlayer.WalkFrames);
            while (vm.Player.TileX < 5) { vm.Move(MoveFacing.Right); vm.Advance(FieldPlayer.WalkFrames); }
            while (vm.Player.TileZ > 5) { vm.Move(MoveFacing.Up); vm.Advance(FieldPlayer.WalkFrames); }
            while (vm.Player.TileZ < 5) { vm.Move(MoveFacing.Down); vm.Advance(FieldPlayer.WalkFrames); }
            Assert.Equal(5, vm.Player.TileX);
            Assert.Equal(5, vm.Player.TileZ);

            // The overworld is one tile to the right, so face that way and talk.
            vm.Move(MoveFacing.Right);
            vm.ScriptLines.Clear();
            vm.Interact();

            Assert.NotEmpty(vm.ScriptLines);
            Assert.DoesNotContain(vm.ScriptLines, l => l.Contains("nothing there"));
            Assert.Contains(vm.ScriptLines, l => l.Contains("overworld 7"));
        }

        [Fact]
        public void FacingEmptyGroundSaysThereIsNobodyThere()
        {
            var events = new EventFile();
            events.overworlds.Add(new Overworld(7, 0, 0)
            {
                movement = 0x00, xMapPosition = 20, yMapPosition = 20,
            });

            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);

            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            vm.Load(SceneWith("grass"), null, events, false, open, 0, (x, z) => (x, 0f, z));
            vm.StepInto = true;
            vm.ScriptLines.Clear();
            vm.Interact();

            Assert.Contains(vm.ScriptLines, l => l.Contains("nothing there"));
        }

        [Fact]
        public void StartingBesideSomebodyStandsNextToThemFacingThem()
        {
            var ow = new Overworld(9, 0, 0)
            {
                movement = 0x00, orientation = (short)MoveFacing.Down,
                xMapPosition = 10, yMapPosition = 10, scriptNumber = 1,
            };
            var events = new EventFile();
            events.overworlds.Add(ow);

            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);

            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            vm.Load(SceneWith("grass"), null, events, false, open, 0, (x, z) => (x, 0f, z));
            vm.StepInto = true;

            // The first entry leaves it to the marker; the next is the first person.
            Assert.Equal("Wherever the marker is", vm.StartBesideNames[0]);
            Assert.Contains("9", vm.StartBesideNames[1]);

            vm.StartBesideIndex = 1;

            // One tile away, and looking their way.
            int dx = Math.Abs(vm.Player.TileX - 10), dz = Math.Abs(vm.Player.TileZ - 10);
            Assert.Equal(1, dx + dz);

            // So talking works without walking a step.
            vm.ScriptLines.Clear();
            vm.Interact();
            Assert.DoesNotContain(vm.ScriptLines, l => l.Contains("nothing there"));
            Assert.Contains(vm.ScriptLines, l => l.Contains("overworld 9"));
        }

        [Fact]
        public void GoingBackToTheMiddleForgetsTheChosenSpot()
        {
            var events = new EventFile();
            events.overworlds.Add(new Overworld(1, 0, 0) { movement = 0x00, xMapPosition = 10, yMapPosition = 10 });

            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);

            var vm = new AnimatedPreviewViewModel { MeasureText = t => (t ?? "").Length * 6 };
            vm.Load(SceneWith("grass"), null, events, false, open, 0, (x, z) => (x, 0f, z));

            vm.StartBesideIndex = 1;
            Assert.NotNull(vm.StartTile);
            Assert.Contains("tile", vm.StartTileText);

            vm.StartBesideIndex = 0;
            Assert.Null(vm.StartTile);
            Assert.Contains("middle", vm.StartTileText);
        }

        // ── the header's camera ─────────────────────────────────────────────────────────
        [Fact]
        public void ThePreviewTakesTheCameraItsHeaderAsksFor()
        {
            var vm = new AnimatedPreviewViewModel();
            Assert.Equal(0, vm.CameraId);
            Assert.Equal(FieldCamera.Normal.Id, vm.CameraEntry.Id);

            vm.CameraId = 9;                                   // Vermilion Gym looks down much harder
            Assert.Equal(9, vm.CameraEntry.Id);
            Assert.True(vm.CameraEntry.PitchDegrees > FieldCamera.Normal.PitchDegrees + 5f);
            Assert.Contains("Vermilion", vm.CameraDescription);

            vm.CameraId = 4;                                   // the indoor one is a flat view
            Assert.True(vm.CameraEntry.Orthographic);
            Assert.Contains("flat", vm.CameraDescription);
        }

        [Fact]
        public void TheClockTheViewerRunsAtIsTheOneTheGamesUse()
        {
            Assert.Equal(30, AnimatedPreviewViewModel.FramesPerSecond);
        }

        [Fact]
        public void TheTimeOfDayStartsAtTheComputersClock()
        {
            var vm = new AnimatedPreviewViewModel();
            Assert.Equal(FieldTimeOfDay.Now, vm.TimeOfDay);
            Assert.Equal((int)FieldTimeOfDay.Now, vm.TimeOfDayIndex);
            Assert.Equal(5, vm.TimesOfDay.Count);
        }

        [Fact]
        public void EveryTimeOfDayCanBeChosen()
        {
            var vm = new AnimatedPreviewViewModel();
            foreach (FieldTimeZone zone in Enum.GetValues(typeof(FieldTimeZone)))
            {
                vm.TimeOfDayIndex = (int)zone;
                Assert.Equal(zone, vm.TimeOfDay);
                Assert.Contains(FieldTimeOfDay.Name(zone), vm.TimeOfDayName);
            }
        }

        [Fact]
        public void TheEngineDirectionNumbersAreTheOnesWeUse()
        {
            // fieldobj_code.h: DIR_UP 0, DIR_DOWN 1, DIR_LEFT 2, DIR_RIGHT 3. The overworld's own
            // orientation field is written in those numbers, so the preview can use it directly.
            Assert.Equal(0, (int)MoveFacing.Up);
            Assert.Equal(1, (int)MoveFacing.Down);
            Assert.Equal(2, (int)MoveFacing.Left);
            Assert.Equal(3, (int)MoveFacing.Right);
        }
    }
}
