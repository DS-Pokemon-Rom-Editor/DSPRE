using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Turns one header of a disposable project copy into a test ground for the script preview: its people,
    /// triggers and signs are cleared, and a strip of open ground gets a line of people whose scripts each
    /// exercise one part of running a script, with a trigger and level scripts alongside.
    ///
    /// Opt in with DSPRE_SCRIPT_STAGE=heartgold or platinum, and point DSPRE_SCRIPT_STAGE_PROJECT at a copy of
    /// that game's project. The configured test projects are refused, so a real project is never rewritten.
    /// </summary>
    [Collection("rom")]
    public sealed class ScriptPreviewStager
    {
        private readonly ITestOutputHelper _out;
        public ScriptPreviewStager(ITestOutputHelper output) => _out = output;

        // Save variables and flags the stage scripts own. Nothing else on these maps reads them.
        private const int TriggerVar = 0x4051, WatcherVar = 0x4052;
        private const int VanishFlag = 0x07F0, HiddenOnLoadFlag = 0x07F1;

        // The strip everything stands on: its length, and its depth across. Towns have little open ground,
        // so the stage is a line of people along a path rather than a square.
        private const int StripLong = 21, StripDeep = 4;

        [SkippableFact]
        public void StageHeartGold()
        {
            Skip.If(!ModeIs("heartgold"), "DSPRE_SCRIPT_STAGE is not heartgold");
            Stage("IPKE", GameFamilies.HGSS, headerId: 60, TestRoms.HeartGold);
        }

        [SkippableFact]
        public void StagePlatinum()
        {
            Skip.If(!ModeIs("platinum"), "DSPRE_SCRIPT_STAGE is not platinum");
            Stage("CPUE", GameFamilies.Plat, headerId: 411, TestRoms.Platinum);
        }

        /// <summary>Where the strip lies: its first tile, and whether it runs north to south.</summary>
        private readonly struct Strip
        {
            public readonly int X, Z;
            public readonly bool Upright;
            public Strip(int x, int z, bool upright) { X = x; Z = z; Upright = upright; }

            /// <summary>A place along the strip (u) and across it (v), as a whole-matrix tile.</summary>
            public (int x, int z) At(int u, int v) => Upright ? (X + v, Z + u) : (X + u, Z + v);

            /// <summary>The compass name for a step along (+u), back (-u), across (+v) and toward (-v).</summary>
            public string Along => Upright ? "South" : "East";
            public string Back => Upright ? "North" : "West";
            public string Across => Upright ? "East" : "South";
            public string Toward => Upright ? "West" : "North";

            /// <summary>Facing across the strip, and back towards its front line.</summary>
            public MoveFacing FacingAcross => Upright ? MoveFacing.Right : MoveFacing.Down;
            public MoveFacing FacingToward => Upright ? MoveFacing.Left : MoveFacing.Up;
        }

        private void Stage(string gameCode, GameFamilies family, int headerId, string configuredProject)
        {
            string project = Environment.GetEnvironmentVariable("DSPRE_SCRIPT_STAGE_PROJECT");
            Assert.False(string.IsNullOrWhiteSpace(project), "DSPRE_SCRIPT_STAGE_PROJECT is required");
            project = Path.GetFullPath(project).TrimEnd('\\', '/');
            Assert.True(Directory.Exists(project), $"No project at {project}");
            Assert.NotEqual(Path.GetFullPath(configuredProject).TrimEnd('\\', '/'), project, StringComparer.OrdinalIgnoreCase);

            SettingsManager.Load();
            new RomInfo(gameCode, project);
            Assert.Equal(family, gameFamily);
            DSUtils.TryUnpackNarcs(new List<DirNames>
            {
                DirNames.scripts, DirNames.eventFiles, DirNames.matrices, DirNames.maps,
            });

            MapHeader header = MapHeader.GetMapHeader(checked((ushort)headerId));
            Strip strip = FindStrip(header, family);
            _out.WriteLine($"Header {headerId}: events {header.eventFileID}, scripts {header.scriptFileID}, " +
                           $"level scripts {header.levelScriptID}, text {header.textArchiveID}; " +
                           $"strip at tile {strip.X}, {strip.Z}, {(strip.Upright ? "north to south" : "west to east")}");

            WriteEvents(header, strip);
            WriteScripts(header, family, strip);
            WriteLevelScripts(header);
            WriteText(header);

            File.WriteAllLines(Path.Combine(project, "script-stage.txt"), new[]
            {
                $"Script preview stage, {family}",
                $"Header {headerId}, strip starting at tile {strip.X}, {strip.Z}, {(strip.Upright ? "running north to south" : "running west to east")}",
                $"Event file {header.eventFileID}, script file {header.scriptFileID}, level scripts {header.levelScriptID}, text {header.textArchiveID}",
            });
        }

        // ── where the stage goes ─────────────────────────────────────────────────────

        /// <summary>The open, dry strip nearest the middle of one of the header's maps, either way round.</summary>
        private static Strip FindStrip(MapHeader header, GameFamilies family)
        {
            var matrix = new GameMatrix(header.matrixID);
            var events = new EventFile(header.eventFileID);
            var warps = new HashSet<(int, int)>(events.warps.Select(w => (FieldInteraction.TileX(w), FieldInteraction.TileZ(w))));

            var cells = new List<(int x, int y)>();
            for (int y = 0; y < matrix.height; y++)
                for (int x = 0; x < matrix.width; x++)
                {
                    if (matrix.maps[y, x] == GameMatrix.EMPTY) continue;
                    if (matrix.hasHeadersSection && matrix.headers[y, x] != header.ID) continue;
                    cells.Add((x, y));
                }
            Assert.NotEmpty(cells);

            Strip? best = null;
            double bestDistance = double.MaxValue;
            foreach (var (cx, cy) in cells)
            {
                var map = new MapFile(matrix.maps[cy, cx], family, false, false);
                foreach (bool upright in new[] { false, true })
                {
                    int wide = upright ? StripDeep : StripLong, high = upright ? StripLong : StripDeep;
                    for (int lz = 0; lz + high <= MapFile.mapSize; lz++)
                        for (int lx = 0; lx + wide <= MapFile.mapSize; lx++)
                        {
                            if (!AllOpen(map, lx, lz, wide, high, family, cx, cy, warps)) continue;
                            double d = Math.Abs(lx + wide / 2.0 - 16) + Math.Abs(lz + high / 2.0 - 16);
                            if (d >= bestDistance) continue;
                            bestDistance = d;
                            best = new Strip(cx * MapFile.mapSize + lx, cy * MapFile.mapSize + lz, upright);
                        }
                }
            }
            Assert.True(best.HasValue, $"No open {StripLong} by {StripDeep} strip on header {header.ID}");
            return best.Value;
        }

        private static bool AllOpen(MapFile map, int lx, int lz, int wide, int high, GameFamilies family,
                                    int cx, int cy, HashSet<(int, int)> warps)
        {
            for (int z = lz; z < lz + high; z++)
                for (int x = lx; x < lx + wide; x++)
                {
                    if ((map.collisions[z, x] & MapCollisionGrid.BlockedBit) != 0) return false;
                    if (FieldTileBehaviors.IsWater(map.types[z, x], family)) return false;
                    if (warps.Contains((cx * MapFile.mapSize + x, cy * MapFile.mapSize + z))) return false;
                }
            return true;
        }

        // ── the people, the trigger ──────────────────────────────────────────────────

        // Local ids, which are also how the scripts address them.
        private const int TalkId = 1, YesNoId = 2, MenuId = 3, FourthId = 4, StateId = 5,
                          MoverId = 6, SoundId = 7, CameraId = 8, VanisherId = 9, VanishTargetId = 10,
                          HiddenOnLoadId = 11, WatcherSetterId = 12, WandererId = 13, ConductorId = 14;

        // The front line stands at v 0 and is spoken to from v 1; v 2 is a walkway; the back line stands at v 3.
        private static void WriteEvents(MapHeader header, Strip strip)
        {
            var events = new EventFile(header.eventFileID);
            var looks = events.overworlds.Select(o => o.overlayTableEntry)
                                         .Where(e => e != 0 && e < 0x100 && (e < 101 || e > 116))
                                         .Distinct().ToList();
            if (looks.Count == 0) looks = new List<ushort> { 1, 3, 5, 7 };

            events.overworlds.Clear();
            events.triggers.Clear();
            events.spawnables.Clear();

            int look = 0;
            void Person(int id, int u, int v, int script, MoveFacing facing, ushort flag = 0, ushort movement = 0, short range = 0)
            {
                var (x, z) = strip.At(u, v);
                events.overworlds.Add(new Overworld(id, x / MapFile.mapSize, z / MapFile.mapSize)
                {
                    overlayTableEntry = looks[look++ % looks.Count],
                    scriptNumber = (ushort)script,
                    orientation = (short)facing,
                    flag = flag,
                    movement = movement,
                    xRange = range,
                    yRange = range,
                    xMapPosition = (short)(x % MapFile.mapSize),
                    yMapPosition = (short)(z % MapFile.mapSize),
                });
            }

            Person(ConductorId, 0, 0, 15, strip.FacingAcross);
            Person(TalkId, 1, 0, 1, strip.FacingAcross);
            Person(YesNoId, 3, 0, 2, strip.FacingAcross);
            Person(MenuId, 5, 0, 3, strip.FacingAcross);
            Person(FourthId, 7, 0, 4, strip.FacingAcross);
            Person(StateId, 9, 0, 10, strip.FacingAcross);
            Person(WatcherSetterId, 11, 0, 14, strip.FacingAcross);
            Person(HiddenOnLoadId, 13, 0, 9, strip.FacingAcross, flag: HiddenOnLoadFlag);
            Person(SoundId, 15, 0, 6, strip.FacingAcross);
            Person(CameraId, 17, 0, 7, strip.FacingAcross);
            Person(VanisherId, 19, 0, 8, strip.FacingAcross);

            Person(MoverId, 1, 3, 5, strip.FacingToward);
            Person(WandererId, 11, 3, 9, strip.FacingToward, movement: 3, range: 1);
            Person(VanishTargetId, 19, 3, 9, strip.FacingToward, flag: VanishFlag);

            var (tx, tz) = strip.At(20, 2);
            events.triggers.Add(new Trigger(tx / MapFile.mapSize, tz / MapFile.mapSize)
            {
                scriptNumber = 11,
                xMapPosition = (short)(tx % MapFile.mapSize),
                yMapPosition = (short)(tz % MapFile.mapSize),
                variableWatched = TriggerVar,
                expectedVarValue = 0,
            });

            File.WriteAllBytes(Filesystem.GetEventPath(header.eventFileID), events.ToByteArray());
            var reopened = new EventFile(new MemoryStream(File.ReadAllBytes(Filesystem.GetEventPath(header.eventFileID))));
            Assert.Equal(14, reopened.overworlds.Count);
            Assert.Single(reopened.triggers);
        }

        // ── the scripts ──────────────────────────────────────────────────────────────

        private void WriteScripts(MapHeader header, GameFamilies family, Strip strip)
        {
            bool hg = family == GameFamilies.HGSS;
            int itemFanfare = ScriptDatabase.soundNames.FirstOrDefault(s => s.Value == "SEQ_ME_ITEM").Key;
            if (itemFanfare == 0) itemFanfare = 1185;
            _out.WriteLine($"Item fanfare sequence {itemFanfare}");

            const string Open = "\tPlayFanfare 1500\n\tLockAll\n\tFacePlayer";
            const string Close = "\tWaitButton\n\tCloseMessage\n\tReleaseAll\n\tEnd";

            var scripts = new List<string>
            {
                // 1: a message that types out, clears, scrolls and ends on a wait.
                $"Script 1:\n{Open}\n\tMessage 0\n{Close}",
                // 2: yes or no, then the compare that reads the answer.
                $"Script 2:\n{Open}\n\tMessage 1\n\tYesNoBox 0x800C\n\tCompareVarValue 0x800C 0\n\tJumpIf EQUAL Function#1\n\tMessage 2\n{Close}",
                // 3: a four-entry menu, which wraps, and backing out of it.
                $"Script 3:\n{Open}\n\tMessage 4\n\tMultiLocalText 20 3 0 1 0x800C\n\tAddMultiOption 5 0\n\tAddMultiOption 6 1\n\tAddMultiOption 7 2\n\tAddMultiOption 8 3\n\tShowMulti\n\tCompareVarValue 0x800C 65534\n\tJumpIf EQUAL Function#2\n\tCompareVarValue 0x800C 2\n\tJumpIf EQUAL Function#3\n\tMessage 9\n{Close}",
                // 4: HGSS asks on the touch screen; Platinum shows an instant message and one that cannot be hurried.
                hg
                    ? $"Script 4:\n{Open}\n\tMessage 12\n\tOpenTouchScreen\n\tYesNoTouchScreen 0x800C\n\tCompareVarValue 0x800C 1\n\tJumpIf EQUAL Function#4\n\tMessage 35\n\tMultiTouchLocalText 1 1 0 1 0x800C\n\tCreateMultiTouchBox 5 36 0\n\tCreateMultiTouchBox 6 37 1\n\tCreateMultiTouchBox 7 38 2\n\tCreateMultiTouchBox 8 39 3\n\tCreateMultiTouchBox 40 41 4\n\tCloseMultiTouch\n\tCloseTouchScreen\n\tMessage 13\n{Close}"
                    : $"Script 4:\n{Open}\n\tMessageAll 15\n\tWaitButton\n\tMessageNoSkip 16\n{Close}",
                // 5: two movements at once, the player's included, with an emote, then WaitMovement.
                $"Script 5:\n{Open}\n\tMessage 17\n\tWaitButton\n\tCloseMessage\n\tMovement {MoverId} Action#1\n\tMovement 255 Action#2\n\tWaitMovement\n\tMessage 18\n\tWaitButton\n\tCloseMessage\n\tMovement {MoverId} Action#3\n\tMovement 255 Action#4\n\tWaitMovement\n\tReleaseAll\n\tEnd",
                // 6: a fanfare and a cry, each waited for.
                $"Script 6:\n{Open}\n\tMessage 19\n\tWaitButton\n\tPlaySound {itemFanfare}\n\tWaitSound\n\tPlayCry 25 0\n\tWaitCry\n\tMessage 20\n{Close}",
                // 7: the camera handed to an object, panned away and back, then a shake on HGSS.
                $"Script 7:\n{Open}\n\tMessage 21\n\tWaitButton\n\tCloseMessage\n\tGetPlayerPosition 0x8000 0x8001\n\tLockCamera 0x8000 0x8001\n\tMovement 241 Action#5\n\tWaitMovement\n\tWaitTime 30 0x800C\n\tMovement 241 Action#6\n\tWaitMovement\n\tReleaseCamera\n" +
                    (hg ? "\tShakeCamera 4 2 3 8\n" : "") + $"\tMessage 22\n{Close}",
                // 8: somebody taken off the map and put back.
                $"Script 8:\n{Open}\n\tMessage 23\n\tWaitButton\n\tCloseMessage\n\tRemoveOW {VanishTargetId}\n\tWaitTime 30 0x800C\n\tMessage 24\n\tWaitButton\n\tCloseMessage\n\tClearFlag {VanishFlag}\n\tAddOW {VanishTargetId}\n\tReleaseAll\n\tEnd",
                // 9: an ordinary person.
                $"Script 9:\n{Open}\n\tMessage 25\n{Close}",
                // 10: things only the game would know: bag space and the player's gender.
                $"Script 10:\n{Open}\n\tCheckItemSpace 1 1 0x800C\n\tCompareVarValue 0x800C 1\n\tJumpIf EQUAL Function#5\n\tMessage 26\n\tWaitButton\n\tJump Function#6",
                // 11: the trigger.
                $"Script 11:\n\tLockAll\n\tPlayFanfare 1501\n\tMovement 255 Action#7\n\tWaitMovement\n\tMessage 29\n\tWaitButton\n\tCloseMessage\n\tSetVar {TriggerVar} 1\n\tReleaseAll\n\tEnd",
                // 12: on arrival, hides somebody by their flag.
                $"Script 12:\n\tSetFlag {HiddenOnLoadFlag}\n\tEnd",
                // 13: the level script that watches a variable.
                $"Script 13:\n\tLockAll\n\tMessage 30\n\tWaitButton\n\tCloseMessage\n\tSetVar {WatcherVar} 2\n\tReleaseAll\n\tEnd",
                // 14: sets the watched variable.
                $"Script 14:\n{Open}\n\tMessage 31\n\tWaitButton\n\tCloseMessage\n\tSetVar {WatcherVar} 1\n\tReleaseAll\n\tEnd",
                // 15: the parade: four people and the player moving at once, every kind of step between them.
                $"Script 15:\n{Open}\n\tMessage 33\n\tWaitButton\n\tCloseMessage\n\tMovement {TalkId} Action#8\n\tMovement {YesNoId} Action#9\n\tMovement {MenuId} Action#10\n\tMovement {FourthId} Action#11\n\tMovement 255 Action#12\n\tWaitMovement\n\tMessage 34\n{Close}",
            };

            var functions = new List<string>
            {
                $"Function 1:\n\tMessage 3\n{Close}",
                $"Function 2:\n\tMessage 10\n{Close}",
                $"Function 3:\n\tMessage 11\n{Close}",
                hg ? $"Function 4:\n\tCloseTouchScreen\n\tMessage 14\n{Close}" : $"Function 4:\n\tMessage 14\n{Close}",
                "Function 5:\n\tMessage 27\n\tWaitButton\n\tJump Function#6",
                $"Function 6:\n\tCheckPlayerGender 0x800C\n\tCompareVarValue 0x800C 0\n\tJumpIf EQUAL Function#7\n\tMessage 28\n{Close}",
                $"Function 7:\n\tMessage 32\n{Close}",
            };

            // The mover walks along the strip and back; the player, talking to it from the walkway, steps
            // away from it and back at the same time. The camera object pans across the strip and back.
            var actions = new List<string>
            {
                // Every action carries how many times it repeats.
                $"Action 1:\n\tEmoteExclamation 0x1\n\tWalk{strip.Along}8 0x2\n\tFace{strip.Back} 0x1\nEnd",
                $"Action 2:\n\tDelay8 0x1\n\tWalk{strip.Toward}8 0x1\n\tFace{strip.Across} 0x1\nEnd",
                $"Action 3:\n\tWalk{strip.Back}8 0x2\n\tFace{strip.Toward} 0x1\nEnd",
                $"Action 4:\n\tWalk{strip.Across}8 0x1\nEnd",
                $"Action 5:\n\tWalk{strip.Across}8 0x4\nEnd",
                $"Action 6:\n\tWalk{strip.Toward}8 0x4\nEnd",
                "Action 7:\n\tEmoteExclamation 0x1\nEnd",
                // The parade. Slow, normal and fast walks, a hop on the spot.
                $"Action 8:\n\tWalk{strip.Across}16 0x1\n\tWalk{strip.Along}8 0x2\n\tJumpOnSpot{strip.Across}8 0x2\n\tWalk{strip.Back}4 0x2\n\tWalk{strip.Toward}16 0x1\n\tFace{strip.Across} 0x1\nEnd",
                // Walking on the spot, then running there and back.
                $"Action 9:\n\tDelay8 0x1\n\tWalk{strip.Across}8 0x1\n\tWalkOnSpot{strip.Along}8 0x3\n\tRun{strip.Along} 0x2\n\tRun{strip.Back} 0x2\n\tWalk{strip.Toward}8 0x1\n\tFace{strip.Across} 0x1\nEnd",
                // Two jumps along, a turn, and back.
                $"Action 10:\n\tDelay16 0x1\n\tWalk{strip.Across}8 0x2\n\tJump{strip.Along}8 0x2\n\tFace{strip.Back} 0x1\n\tWalk{strip.Back}4 0x2\n\tWalk{strip.Toward}8 0x2\n\tFace{strip.Across} 0x1\nEnd",
                // A surprise, a disappearing act, and a quick shuffle on the spot.
                $"Action 11:\n\tEmoteDoubleExclamation 0x1\n\tWalk{strip.Across}4 0x1\n\tSetInvisible 0x1\n\tDelay16 0x1\n\tSetVisible 0x1\n\tWalkOnSpot{strip.Back}4 0x4\n\tWalk{strip.Toward}4 0x1\n\tFace{strip.Across} 0x1\nEnd",
                // The player cheers on the spot and hops.
                $"Action 12:\n\tFace{strip.Along} 0x1\n\tWalkOnSpot{strip.Along}8 0x4\n\tDelay32 0x1\n\tJumpOnSpot{strip.Toward}16 0x1\n\tFace{strip.Toward} 0x1\nEnd",
            };

            IEnumerable<string> Lines(IEnumerable<string> blocks) => blocks.SelectMany(b => (b + "\n").Split('\n'));
            var file = new ScriptFile(Lines(scripts), Lines(functions), Lines(actions), header.scriptFileID);
            // The parser says what it could not read through the log rather than an exception.
            if (file.allScripts == null || file.allFunctions == null || file.allActions == null)
                foreach (string line in AppLogger.GetRecentLogs().Split('\n').Where(l => l.Contains("[ERROR]")).TakeLast(5))
                    _out.WriteLine(line.Trim());
            Assert.NotNull(file.allScripts);
            Assert.NotNull(file.allFunctions);
            Assert.NotNull(file.allActions);
            Assert.Equal(scripts.Count, file.allScripts.Count);

            File.WriteAllBytes(Filesystem.GetScriptPath(header.scriptFileID), file.ToByteArray());
            // A plaintext copy newer than the binary would be read instead of it.
            string plain = ScriptFile.GetFilePaths(header.scriptFileID).txtPath;
            if (File.Exists(plain)) File.Delete(plain);
            ScriptFile.ClearPlaintextCache();

            var reopened = new ScriptFile(header.scriptFileID);
            Assert.Equal(scripts.Count, reopened.allScripts.Count);
            _out.WriteLine($"Wrote {reopened.allScripts.Count} scripts, {reopened.allFunctions.Count} functions, {reopened.allActions.Count} movements");
        }

        private static void WriteLevelScripts(MapHeader header)
        {
            var file = new LevelScriptFile();
            file.bufferSet.Add(new MapScreenLoadTrigger(LevelScriptTrigger.MAPCHANGE, 12));
            file.bufferSet.Add(new VariableValueTrigger(13, WatcherVar, 1));
            string path = Filesystem.GetScriptPath(header.levelScriptID);
            if (File.Exists(path)) File.Delete(path);
            file.write_file(path);

            var reopened = new LevelScriptFile(header.levelScriptID);
            Assert.Equal(2, reopened.bufferSet.Count);
        }

        // ── the words ────────────────────────────────────────────────────────────────

        private static void WriteText(MapHeader header)
        {
            var messages = new List<string>
            {
                /* 0 */ "Welcome to the script stage!\\nThis box types one letter\\rat a time. A clear wait\\nwiped the box just now.\\fThat was a scroll wait.\\nThe last line ends on a wait.\\r",
                /* 1 */ "Do you like watching\\nscripts run?",
                /* 2 */ "No? That is a shame.",
                /* 3 */ "Yes! Me too.",
                /* 4 */ "Pick a snack.",
                /* 5 */ "BERRY",
                /* 6 */ "COOKIE",
                /* 7 */ "STAY",
                /* 8 */ "CAKE",
                /* 9 */ "Good choice.",
                /* 10 */ "You backed out of the menu.",
                /* 11 */ "Then stay a while!",
                /* 12 */ "Answer on the touch screen.",
                /* 13 */ "Enjoy your snack!",
                /* 14 */ "You tapped NO.",
                /* 15 */ "This line appears all at once.",
                /* 16 */ "This line cannot be hurried.",
                /* 17 */ "Watch me walk!",
                /* 18 */ "We both moved at once.",
                /* 19 */ "Listen to this!",
                /* 20 */ "A fanfare, then a cry.",
                /* 21 */ "Let me show you the camera.",
                /* 22 */ "Back to you again.",
                /* 23 */ "Watch my friend vanish.",
                /* 24 */ "And come back!",
                /* 25 */ "Just an ordinary person here.",
                /* 26 */ "Your bag is full.",
                /* 27 */ "Your bag has room.",
                /* 28 */ "You are a girl.",
                /* 29 */ "You stepped on the trigger.",
                /* 30 */ "The watched variable changed!",
                /* 31 */ "Take a step after this.",
                /* 32 */ "You are a boy.",
                /* 33 */ "Parade time! Watch everyone go.",
                /* 34 */ "Bravo, everybody!",
                /* 35 */ "Then pick a snack by touch.",
                /* 36 */ "Sweet and juicy.",
                /* 37 */ "Crunchy and sweet.",
                /* 38 */ "Stay a little longer.",
                /* 39 */ "A whole CAKE, just for you.",
                /* 40 */ "TEA",
                /* 41 */ "A warm cup of TEA.",
            };

            string json = TextArchive.GetFilePaths(header.textArchiveID).jsonPath;
            if (File.Exists(json)) File.Delete(json);
            new TextArchive(header.textArchiveID, messages).SaveToExpandedDir(header.textArchiveID, false);

            var reopened = new TextArchive(header.textArchiveID);
            Assert.Equal(messages.Count, reopened.messages.Count);
            Assert.Equal(messages[1], reopened.messages[1]);
        }

        private static bool ModeIs(string mode) =>
            string.Equals(Environment.GetEnvironmentVariable("DSPRE_SCRIPT_STAGE"), mode, StringComparison.OrdinalIgnoreCase);
    }
}
