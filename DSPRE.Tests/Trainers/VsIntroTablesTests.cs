using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;
using static DSPRE.ROMFiles.VsIntroTables;

namespace DSPRE.Tests
{
    /// <summary>
    /// Special trainer battle intros on US HeartGold, Platinum Rev 1 and Diamond. Expected values are the retail
    /// rows in the pokeheartgold, pokeplatinum and pokediamond decomps.
    /// </summary>
    [Collection("rom")]
    public class VsIntroTablesTests
    {
        public static readonly TheoryData<string> Games = new() { "HeartGold", "Platinum", "Diamond" };

        private static void Open(string game) => DSPRE.Tests.Pokemon.GameTablesTests.Open(game);

        /// <summary>Every file the intro tables live in, with its bytes now.</summary>
        private static Dictionary<string, byte[]> Files()
        {
            var sites = RomInfo.VsIntroCodeSites;
            var paths = new List<string> { RomInfo.arm9Path, OverlayUtils.GetPath(sites.TaskOverlay), OverlayUtils.GetPath(sites.RecordOverlay) };
            if (sites.ExecutiveOverlay >= 0) paths.Add(OverlayUtils.GetPath(sites.ExecutiveOverlay));
            return paths.Distinct().ToDictionary(p => p, File.ReadAllBytes);
        }

        private static void Restoring(Action<Dictionary<string, byte[]>> body)
        {
            var original = Files();
            try { body(original); }
            finally { foreach (var kv in original) File.WriteAllBytes(kv.Key, kv.Value); }
        }

        private static List<(string File, int Offset)> Changes(Dictionary<string, byte[]> before)
        {
            var list = new List<(string, int)>();
            foreach (var kv in before)
            {
                byte[] now = File.ReadAllBytes(kv.Key);
                Assert.Equal(kv.Value.Length, now.Length);
                for (int i = 0; i < now.Length; i++) if (now[i] != kv.Value[i]) list.Add((kv.Key, i));
            }
            return list;
        }

        [SkippableFact]
        public void MakingClassRoomKeepsEveryRowAndLetsANewClassIn()
        {
            Open("HeartGold");
            var t = VsIntroTables.Load();
            string why = t.WhyNoClassRoom();
            Skip.If(why != null, "Class table can't be moved in this project: " + why);

            string synth = Filesystem.expArmPath;
            byte[] synthBefore = File.ReadAllBytes(synth);
            Restoring(_ =>
            {
                try
                {
                    var before = Enumerable.Range(0, t.ClassRowCount).Select(t.ClassRow).ToList();
                    Assert.Equal(0, t.FreeClassRows);
                    t.MakeClassRoom();

                    var moved = VsIntroTables.Load();
                    Assert.True(moved.ClassTableMoved);
                    Assert.Equal(MostClassRows, moved.ClassRowCount);
                    Assert.Equal(before, Enumerable.Range(0, before.Count).Select(moved.ClassRow).ToList());
                    Assert.Equal(MostClassRows - before.Count, moved.FreeClassRows);

                    // Class 2 has no intro in retail; it can now be given one without freeing another row.
                    int gym = moved.ComboForClass(66);
                    Assert.True(moved.AssignClass(2, gym));
                    moved.Save();
                    var reread = VsIntroTables.Load();
                    Assert.Equal(gym, reread.ComboForClass(2));
                    Assert.Equal(gym, reread.ComboForClass(66));
                }
                finally { File.WriteAllBytes(synth, synthBefore); }
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void SavingUnchangedTablesWritesNothing(string game)
        {
            Open(game);
            Assert.Null(VsIntroTables.WhyNot());
            Restoring(before =>
            {
                var t = VsIntroTables.Load();
                Assert.False(t.HasChanges);
                t.Restore(t.Snapshot());
                t.Save();
                Assert.Empty(Changes(before));
            });
        }

        [SkippableFact]
        public void HeartGoldRoutinesLeadToTheirRecords()
        {
            Open("HeartGold");
            var t = VsIntroTables.Load();
            Assert.Equal(45, t.ComboCount);
            Assert.Equal(32, t.ClassRowCount);

            var falkner = t.RecordFor(12);
            Assert.Equal((RecordKind.Gym, 0), (falkner.Kind, falkner.Index));
            Assert.Equal(214, falkner.Get(RecordField.EndX));
            Assert.Equal(20, falkner.Get(RecordField.TrainerId));
            Assert.Equal(new[] { 63, 64, 65, 66 }, falkner.FaceMembers());
            Assert.Equal(new[] { 21, 20, 22 }, falkner.BannerMembers());
            Assert.Equal(new[] { 42, 41, 43 }, t.RecordFor(27).BannerMembers());   // Blue reuses Clair's banner

            var rival = t.RecordFor(28);
            Assert.Equal(RecordKind.Rival, rival.Kind);
            Assert.Equal(HgssRivalClass, rival.Get(RecordField.Class));

            var will = t.RecordFor(29);
            Assert.Equal((RecordKind.League, 0), (will.Kind, will.Index));
            Assert.Equal((131, 47, 32, 245), (will.Get(RecordField.FaceBase), will.Get(RecordField.FramePalette), will.Get(RecordField.ClashFrames), will.Get(RecordField.TrainerId)));
            Assert.Equal(9, t.RecordFor(33).Get(RecordField.ClashFrames));         // Lance

            var petrel = t.RecordFor(40);
            Assert.Equal((RecordKind.Executive, 0), (petrel.Kind, petrel.Index));
            Assert.Equal(new[] { 222, 223, 224, 225 }, petrel.FaceMembers());
            Assert.Equal(700, t.RecordFor(44).Get(RecordField.TrainerId));        // Giovanni
            Assert.Null(t.RecordFor(39));                                    // grunts have no record

            Assert.Equal(new[] { 151, 152 }, t.ParticleFiles);
            Assert.Equal(0, t.ComboForClass(66));
            Assert.Equal(21, t.ComboForClass(119));                          // the first rival battle
            Assert.Equal(41, t.ComboForClass(2));
            Assert.Equal(new[] { 23, 119 }, t.ClassesUsing(21));
            Assert.Equal(12, t.EffectOf(0));
            Assert.Equal(1118, t.SequenceOf(0));
        }

        [SkippableFact]
        public void PlatinumRoutinesLeadToTheirRecords()
        {
            Open("Platinum");
            var t = VsIntroTables.Load();
            Assert.Equal(35, t.ComboCount);

            var roark = t.RecordFor(12);
            Assert.Equal((RecordKind.Gym, 0), (roark.Kind, roark.Index));
            Assert.Equal((214, 246, 62), (roark.Get(RecordField.EndX), roark.Get(RecordField.TrainerId), roark.Get(RecordField.Class)));
            Assert.Equal(new[] { 55, 56, 57, 58 }, roark.FaceMembers());
            Assert.Equal(new[] { 15, 16, 17 }, roark.BannerMembers());
            Assert.Equal(83, t.RecordFor(19).FaceMembers()[0]);              // Volkner

            var aaron = t.RecordFor(20);
            Assert.Equal((RecordKind.League, 0), (aaron.Kind, aaron.Index));
            Assert.Equal((87, 39, 32, 65, 261), (aaron.Get(RecordField.FaceBase), aaron.Get(RecordField.FramePalette), aaron.Get(RecordField.ClashFrames),
                                                 aaron.Get(RecordField.Class), aaron.Get(RecordField.TrainerId)));
            Assert.Equal(267, t.RecordFor(24).Get(RecordField.TrainerId));        // Cynthia

            Assert.Equal(new[] { 107, 108 }, t.ParticleFiles);
            Assert.Equal(new[] { 3, 4 }, t.ParticleEmitters);
        }

        [SkippableFact]
        public void DiamondRoutinesLeadToTheirRecords()
        {
            Open("Diamond");
            var t = VsIntroTables.Load();
            Assert.Equal(31, t.ComboCount);

            var roark = t.RecordFor(12);
            Assert.Equal((176, 62, 1), (roark.Get(RecordField.EndX), roark.Get(RecordField.Class), roark.Get(RecordField.FaceColumn)));
            Assert.Null(roark.FaceMembers());
            Assert.Equal(192, t.RecordFor(14).Get(RecordField.EndX));              // Wake
            Assert.Equal(0, t.RecordFor(14).Get(RecordField.FaceColumn));

            var aaron = t.RecordFor(20);
            Assert.Equal((1500, 15, 65, 14), (aaron.Get(RecordField.CameraTurn), aaron.Get(RecordField.PanFrames), aaron.Get(RecordField.Class), aaron.Get(RecordField.FramePalette)));
            Assert.Equal(3000, t.RecordFor(24).Get(RecordField.CameraTurn));
            Assert.Empty(t.ParticleFiles);
        }

        private static readonly Dictionary<int, int> PlatinumSwitch = new()
        {
            [62] = 0, [74] = 1, [75] = 2, [76] = 3, [77] = 4, [78] = 5, [64] = 6, [79] = 7,
            [65] = 8, [66] = 9, [67] = 10, [68] = 11, [69] = 12, [63] = 13,
            [73] = 24, [89] = 24, [72] = 25, [87] = 25, [88] = 25, [86] = 26,
            [97] = 31, [99] = 31, [100] = 31, [101] = 31, [102] = 31,
        };

        private static readonly Dictionary<int, int> DiamondSwitch = new()
        {
            [62] = 0, [74] = 1, [75] = 2, [76] = 3, [77] = 4, [78] = 5, [64] = 6, [79] = 7,
            [65] = 8, [66] = 9, [67] = 10, [68] = 11, [69] = 12, [63] = 13,
            [73] = 21, [89] = 21, [72] = 22, [87] = 22, [88] = 22, [86] = 23, [97] = 28,
        };

        [SkippableTheory]
        [InlineData("Platinum", 62, 41, 33)]
        [InlineData("Diamond", 62, 36, 29)]
        public void ClassSwitchDecodesToTheRetailPairs(string game, int first, int count, int ordinary)
        {
            Open(game);
            var jumps = BattleMusicTables.LoadRom().ClassJumps;
            Assert.NotNull(jumps);
            Assert.Equal((first, count, ordinary), (jumps.FirstClass, jumps.Count, jumps.DefaultCombo));
            var expected = game == "Platinum" ? PlatinumSwitch : DiamondSwitch;
            for (int c = 0; c < 128; c++)
                Assert.True((expected.TryGetValue(c, out int p) ? p : ordinary) == jumps.ComboOf(c), $"class {c}");
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void OneEditWritesOnlyItsOwnBytes(string game)
        {
            Open(game);
            Restoring(before =>
            {
                var t = VsIntroTables.Load();
                var sites = RomInfo.VsIntroCodeSites;
                var gym = t.RecordFor(13);
                int start = sites.GymTable + sites.GymSize * gym.Index;
                if (game == "Diamond")
                {
                    gym.Set(RecordField.EndX, 160);
                    t.Save();
                    Assert.Equal(new[] { start + 2 }, Changes(before).Select(c => c.Offset));   // 160.0 and 176.0 differ in one byte
                    Assert.Equal(160, VsIntroTables.Load().RecordFor(13).Get(RecordField.EndX));
                    return;
                }
                gym.Set(RecordField.TrainerId, 300);
                t.Save();
                var changed = Changes(before);
                Assert.NotEmpty(changed);
                Assert.All(changed, c => Assert.Equal(OverlayUtils.GetPath(sites.RecordOverlay), c.File));
                Assert.All(changed, c => Assert.InRange(c.Offset, start + 4, start + 7));
                var again = VsIntroTables.Load().RecordFor(13);
                Assert.Equal(300, again.Get(RecordField.TrainerId));
                Assert.Equal(gym.FaceMembers(), again.FaceMembers());
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void MusicEditWritesOnlyItsSequence(string game)
        {
            Open(game);
            Restoring(before =>
            {
                var t = VsIntroTables.Load();
                int old = t.SequenceOf(0);
                t.SetSequence(0, old + 1);
                t.Save();
                var music = BattleMusicTables.LoadRom();
                Assert.All(Changes(before), c => Assert.InRange(c.Offset, (int)music.Combos.Start + 2, (int)music.Combos.Start + 3));
                Assert.Equal(old + 1, music.Combos.Rows[0].Sequence);
                Assert.Equal(old + 1, music.TrainerSequence(game == "HeartGold" ? 66 : 62));
            });
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("Diamond")]
        public void AClassInTheSwitchMovesToAnotherIntro(string game)
        {
            Open(game);
            Restoring(before =>
            {
                var t = VsIntroTables.Load();
                Assert.True(t.AssignClass(70, 0));        // an ordinary class gets Roark's intro
                Assert.False(t.AssignClass(20, 0));       // outside the switch
                t.Save();
                var changes = Changes(before);
                Assert.NotEmpty(changes);
                Assert.All(changes, c => Assert.Equal(RomInfo.arm9Path, c.File));
                Assert.All(changes, c => Assert.InRange(c.Offset, RomInfo.VsIntroCodeSites.ClassJumpTable + 16, RomInfo.VsIntroCodeSites.ClassJumpTable + 17));

                var again = VsIntroTables.Load();
                Assert.Equal(0, again.ComboForClass(70));
                Assert.Equal(new[] { 62, 70 }, again.ClassesUsing(0));
                Assert.True(again.UnassignClass(70));
                Assert.Equal(again.Music.ClassJumps.DefaultCombo, again.ComboForClass(70));
            });
        }

        [SkippableFact]
        public void HeartGoldClassRowsAreFreedAndReused()
        {
            Open("HeartGold");
            Restoring(before =>
            {
                var t = VsIntroTables.Load();
                Assert.Equal(0, t.FreeClassRows);
                Assert.False(t.AssignClass(2, 0));        // no free row in retail
                Assert.True(t.UnassignClass(119));        // row 22
                Assert.Equal(1, t.FreeClassRows);
                Assert.True(t.AssignClass(2, 16));        // a Youngster with Will's intro, in the freed row
                Assert.Equal((2, 16), t.ClassRow(22));
                Assert.True(t.AssignClass(66, 1));        // Falkner's own row moves to Bugsy's intro
                Assert.Equal((66, 1), t.ClassRow(0));
                t.Save();

                var changes = Changes(before);
                var music = BattleMusicTables.LoadRom();
                Assert.All(changes, c => Assert.True(c.Offset == music.Classes.Start + 44 || c.Offset == music.Classes.Start + 45
                                                  || c.Offset == music.Classes.Start || c.Offset == music.Classes.Start + 1, $"0x{c.Offset:X}"));
                Assert.Equal(16, music.Classes.Rows.First(r => r.Class == 2).Combo);
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void EachComboHasMusicInExactlyOneEditor(string game)
        {
            Open(game);
            var trainers = VsIntroTables.Load(Part.Trainers);
            var wild = VsIntroTables.Load(Part.Wild);
            int trainerCombos = 0;
            for (int c = 0; c < trainers.ComboCount; c++)
            {
                Assert.True(trainers.OwnsMusic(c) != wild.OwnsMusic(c), $"combo {c}");
                if (trainers.OwnsMusic(c)) trainerCombos++;
            }
            Assert.InRange(trainerCombos, 1, trainers.ComboCount - 1);
            Assert.True(trainers.OwnsMusic(0));                                     // the first gym
            int ordinaryWild = Enumerable.Range(0, trainers.ComboCount).Single(c => trainers.RoleOf(c) == ComboRole.OrdinaryWild);
            Assert.True(wild.OwnsMusic(ordinaryWild));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void AnEditorNeverWritesTheOtherEditorsBytes(string game)
        {
            Open(game);
            Restoring(before =>
            {
                var probe = VsIntroTables.Load();
                int wildCombo = Enumerable.Range(0, probe.ComboCount).Single(c => probe.RoleOf(c) == ComboRole.OrdinaryWild);
                var trainers = VsIntroTables.Load(Part.Trainers);
                var wild = VsIntroTables.Load(Part.Wild);

                trainers.SetSequence(wildCombo, 1000);                   // not the trainer editor's to write
                wild.RecordFor(12).Set(RecordField.EndX, 100);           // nor the wild editor's
                Assert.False(trainers.HasChanges);
                Assert.False(wild.HasChanges);

                wild.SetSequence(wildCombo, 1000);
                trainers.SetSequence(0, 1001);
                trainers.Save();
                wild.Save();                                             // after the other save, from its older copy
                var music = BattleMusicTables.LoadRom();
                Assert.Equal(1000, music.Combos.Rows[wildCombo].Sequence);
                Assert.Equal(1001, music.Combos.Rows[0].Sequence);
                Assert.Equal(4, Changes(before).Count(c => c.File == RomInfo.arm9Path || c.File == music.Combos.Path));
                Assert.Equal(1001, wild.SequenceOf(0));                  // the saved copy picked up the other editor's write
            });
        }

        [SkippableFact]
        public void ASaveStopsWhenItsOwnBytesChangedOnDisk()
        {
            Open("Platinum");
            Restoring(before =>
            {
                var t = VsIntroTables.Load();
                var gym = t.RecordFor(12);
                gym.Set(RecordField.TrainerId, 300);

                string path = OverlayUtils.GetPath(RomInfo.VsIntroCodeSites.RecordOverlay);
                int at = RomInfo.VsIntroCodeSites.GymTable + 4;
                byte[] file = File.ReadAllBytes(path);
                file[at] ^= 0xFF;
                File.WriteAllBytes(path, file);

                Assert.Throws<IOException>(() => t.Save());
                Assert.Equal(file[at], File.ReadAllBytes(path)[at]);    // the other change survives
            });
        }

        [SkippableFact]
        public void HeartGoldSpeciesRowsBelongToTheWildEditor()
        {
            Open("HeartGold");
            Restoring(before =>
            {
                var wild = VsIntroTables.Load(Part.Wild);
                Assert.Equal(11, wild.SpeciesRowCount);
                Assert.Equal((243, 22), wild.SpeciesRow(0));                // Raikou
                Assert.Equal(new[] { 243 }, wild.SpeciesUsing(22));
                wild.SetSpeciesRow(0, 150, 22);                             // Mewtwo takes Raikou's row
                wild.Save();

                var music = BattleMusicTables.LoadRom();
                Assert.Equal((150, 22), music.Species.Rows[0]);
                Assert.All(Changes(before), c => Assert.InRange(c.Offset, (int)music.Species.Start, (int)music.Species.Start + 1));
                Assert.Equal(1123, music.WildSequence(150));                // Raikou's theme now

                var trainers = VsIntroTables.Load(Part.Trainers);
                trainers.SetSpeciesRow(1, 150, 23);
                Assert.False(trainers.HasChanges);
            });
        }

        [SkippableFact]
        public void PlatinumWildPairsListTheirSpecies()
        {
            Open("Platinum");
            var wild = VsIntroTables.Load(Part.Wild);
            Assert.Equal(0, wild.SpeciesRowCount);
            Assert.Equal(new[] { 483, 484 }, wild.SpeciesUsing(15));      // Dialga, Palkia
            Assert.True(wild.OwnsMusic(15));
            Assert.False(wild.OwnsMusic(13));                             // the rival's pair is a trainer one
        }
    }
}
