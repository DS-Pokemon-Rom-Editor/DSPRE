using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.ROMFiles;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    public partial class UnchangedSaveTests
    {
        private static string ExpandedText => TextConverter.GetExpandedFolderPath();
        private static string ExpandedScripts => Path.Combine(workDir, "expanded", "scripts");
        private static string TextBins => Dir(DirNames.textArchives);

        private static int SaveEach(string dir, Action<int> save)
        {
            int n = 0;
            foreach (var (id, _) in Members(dir).ToList()) { save(id); n++; }
            return n;
        }

        private static void BuildText()
        {
            Assert.True(TextArchive.BuildRequiredBins(out string error), "the text archives did not build: " + error);
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Headers(string game)
        {
            Open(game);
            var guarded = new List<string>();
            if (MapHeader.UsesDynamicHeaders) guarded.Add(Filesystem.dynamicHeaders);
            SavesUnchanged($"{game} headers", guarded, () =>
            {
                int count = MapHeader.GetHeaderCount();
                for (int id = 0; id < count; id++) MapHeader.Save(MapHeader.GetMapHeader((ushort)id));
                return count;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Matrices(string game)
        {
            Open(game);
            Unpack(DirNames.matrices);
            SavesUnchanged($"{game} matrices", new[] { Dir(DirNames.matrices) }, () =>
                SaveEach(Dir(DirNames.matrices), id => new GameMatrix(id).SaveToFileDefaultDir(id, showSuccessMessage: false)));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Maps(string game)
        {
            Open(game);
            Unpack(DirNames.maps);
            SavesUnchanged($"{game} maps", new[] { Dir(DirNames.maps) }, () =>
                SaveEach(Dir(DirNames.maps), id => new MapFile(id, gameFamily).SaveToFileDefaultDir(id, showSuccessMessage: false)));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Events(string game)
        {
            Open(game);
            Unpack(DirNames.eventFiles);
            SavesUnchanged($"{game} events", new[] { Dir(DirNames.eventFiles) }, () =>
                SaveEach(Dir(DirNames.eventFiles), id => new EventFile(id).SaveToFileDefaultDir(id, showSuccessMessage: false)));
        }

        /// <summary>The level script editor saves with word-alignment padding on unless the user turns it off.</summary>
        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void LevelScripts(string game)
        {
            Open(game);
            Unpack(DirNames.scripts);
            SavesUnchanged($"{game} level scripts", new[] { Filesystem.scripts }, () =>
            {
                int n = 0;
                foreach (var (id, _) in Members(Filesystem.scripts).ToList())
                {
                    LevelScriptFile file;
                    try { file = new LevelScriptFile(id); }
                    catch (InvalidDataException) { continue; }
                    file.SaveToFileDefaultDir(id, word_alignment_padding: true);
                    n++;
                }
                return n;
            }, restored: new[] { ExpandedScripts });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Text(string game)
        {
            Open(game);
            Unpack(DirNames.textArchives);
            SavesUnchanged($"{game} text", new[] { TextBins }, () =>
            {
                int n = SaveEach(TextBins, id => new TextArchive(id).SaveToExpandedDir(id, showSuccessMessage: false));
                BuildText();
                return n;
            }, restored: new[] { ExpandedText });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void PokemonData(string game)
        {
            Open(game);
            Unpack(DirNames.personalPokeData, DirNames.learnsets, DirNames.moveData, DirNames.itemData);
            SavesUnchanged($"{game} personal data", new[] { Dir(DirNames.personalPokeData) }, () =>
                SaveEach(Dir(DirNames.personalPokeData), id => new PokemonPersonalData(id).SaveToFileDefaultDir(id, showSuccessMessage: false)));
            SavesUnchanged($"{game} learnsets", new[] { Dir(DirNames.learnsets) }, () =>
                SaveEach(Dir(DirNames.learnsets), id => new LearnsetData(id).SaveToFileDefaultDir(id, showSuccessMessage: false)));
            SavesUnchanged($"{game} moves", new[] { Dir(DirNames.moveData) }, () =>
                SaveEach(Dir(DirNames.moveData), id => new MoveData(id).SaveToFileDefaultDir(id, showSuccessMessage: false)));
            SavesUnchanged($"{game} items", new[] { Dir(DirNames.itemData) }, () =>
                SaveEach(Dir(DirNames.itemData), id => new ItemData(id).SaveToFileDefaultDir(id, showSuccessMessage: false)));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void ItemTables(string game)
        {
            Open(game);
            int n = 0;
            if (pickupTableOverlayNumber != -1)
            {
                if (OverlayUtils.IsCompressed(pickupTableOverlayNumber)) OverlayUtils.Decompress(pickupTableOverlayNumber);
                SavesUnchanged($"{game} Pickup", Array.Empty<string>(), () => { PickupTable.Read().Write(); return 1; });
                n++;
            }
            if (IsHiddenItemsEditorAvailable() && HiddenItemTable.Count() is int count && count >= 0)
            {
                SavesUnchanged($"{game} hidden items", Array.Empty<string>(), () =>
                {
                    HiddenItemTable.Write(HiddenItemTable.Read(count), hiddenItemTableCapacity);
                    return count;
                });
                n++;
            }
            if (IsRockSmashEditorAvailable())
            {
                // The editor unpacks the archive before reading it; without this the folder exists only if an earlier test made it.
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.rockSmash });
                string dir = Path.GetDirectoryName(Filesystem.GetRockSmashPath(0));
                SavesUnchanged($"{game} Rock Smash", new[] { dir }, () =>
                {
                    int headers = GetHeaderCount();
                    for (int i = 0; i < headers; i++) new RockSmashData((ushort)i).SaveToFile();
                    if (IsRockSmashItemTableAvailable())
                        foreach (uint at in new[] { RockSmashItemSlots.DefaultOffset, RockSmashItemSlots.RuinsOfAlphOffset, RockSmashItemSlots.CliffCaveOffset })
                            RockSmashItemSlots.Write(at, RockSmashItemSlots.Read(at));
                    return headers;
                });
                n++;
            }
            Skip.If(n == 0, $"{game} has none of the item tables");
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void SpawnCameraAndFly(string game)
        {
            Open(game);
            SavesUnchanged($"{game} spawn point", Array.Empty<string>(), () => { SpawnPoint.Read().Write(); return 1; });

            SavesUnchanged($"{game} cameras", Array.Empty<string>(), () =>
            {
                var at = GameCameraTable.Locate();
                bool hgss = gameFamily == GameFamilies.HGSS;
                var rows = GameCameraTable.Read(at).Select((camera, i) =>
                {
                    var row = new CameraRowVM(i);
                    row.LoadFrom(camera);
                    return row.ToGameCamera(hgss);
                }).ToList();
                GameCameraTable.Write(rows, at.OverlayPath, at.Offset);
                return rows.Count;
            });

            var fly = FlyTable.TryRead();
            if (fly != null)
                SavesUnchanged($"{game} fly", Array.Empty<string>(), () => { FlyTable.Write(fly); return fly.Count; });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Starters(string game)
        {
            Open(game);
            Unpack(DirNames.scripts);
            SavesUnchanged($"{game} starters", new[] { Filesystem.scripts }, () =>
            {
                int[] starters = StarterPokemonData.GetStarters();
                Assert.True(StarterPokemonData.ApplyStarters(starters, out _), "the starter save failed");
                return starters.Length;
            }, restored: new[] { ExpandedScripts });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void MusicTables(string game)
        {
            Open(game);
            int n = 0;
            if (gameFamily == GameFamilies.HGSS)
            {
                SavesUnchanged($"{game} conditional music", Array.Empty<string>(), () =>
                {
                    var (start, rows) = ConditionalMusicTable.Read();
                    ConditionalMusicTable.Write(start, rows);
                    return rows.Count;
                });
                n++;
            }
            if (BattleMusicTables.IsSupported && BattleMusicTables.LoadRom() is BattleMusicTables tables)
            {
                SavesUnchanged($"{game} battle music", new[] { Filesystem.expArmPath }, () =>
                {
                    for (int i = 0; i < tables.Combos.Rows.Count; i++) tables.WriteCombo(i);
                    for (int i = 0; i < tables.Classes.Rows.Count; i++) tables.WriteClass(i);
                    return tables.Combos.Rows.Count + tables.Classes.Rows.Count;
                });
                n++;
            }
            SavesUnchanged($"{game} encounter music", new[] { Filesystem.expArmPath }, () =>
            {
                var (where, rows) = EncounterMusicTable.Read();
                foreach (var row in rows) EncounterMusicTable.WriteMusic(where, row);
                return rows.Count;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Rematches(string game)
        {
            Open(game);
            int n = 0;
            if (VsSeekerRematchTable.IsSupported)
            {
                SavesUnchanged($"{game} VS Seeker", Array.Empty<string>(), () =>
                {
                    var rows = VsSeekerRematchTable.ReadAll(out _, out string error);
                    Assert.True(rows != null, error);
                    for (int i = 0; i < rows.Count; i++) Assert.True(VsSeekerRematchTable.WriteRow(i, rows[i], out error), error);
                    return rows.Count;
                });
                n++;
            }
            if (PokegearRematchTable.IsSupported)
            {
                SavesUnchanged($"{game} Pokégear rematches", Array.Empty<string>(), () =>
                {
                    var rows = PokegearRematchTable.ReadAll(out var location, out string error);
                    Assert.True(rows != null, error);
                    for (int i = 0; i < rows.Count; i++) Assert.True(PokegearRematchTable.WriteRow(location, i, rows[i], out error), error);
                    return rows.Count;
                });
                n++;
            }
            if (PokegearPhoneBook.IsSupported)
            {
                Unpack(DirNames.textArchives);
                SavesUnchanged($"{game} phone book", Array.Empty<string>(), () =>
                {
                    var book = PokegearPhoneBook.Load(out string error);
                    Assert.True(book != null, error);
                    Assert.True(PokegearPhoneBook.Save(book, out error), error);
                    return 1;
                });
                n++;
            }
            Skip.If(n == 0, $"{game} has no rematch tables");
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void TrainerClassesAndMessages(string game)
        {
            Open(game);
            Unpack(DirNames.trainerTextTable, DirNames.trainerTextOffset, DirNames.textArchives);
            SavesUnchanged($"{game} battle messages", new[] { Dir(DirNames.trainerTextTable), Dir(DirNames.trainerTextOffset), TextBins }, () =>
            {
                var entries = TrainerMessageTable.Read();
                TrainerMessageTable.Write(entries, new TextArchive(trainerMessageTextNumber).messages);
                BuildText();
                return entries.Count;
            }, restored: new[] { ExpandedText });

            if (TrainerClassTableExpansion.IsSupportedForCurrentRom)
                SavesUnchanged($"{game} trainer class gender and prize", Array.Empty<string>(), () =>
                {
                    int classes = GetTrainerClassNames().Length, n = 0;
                    for (int c = 0; c < classes; c++)
                    {
                        if (TrainerClassTableExpansion.TryReadGender(c, out byte gender, out _)) { TrainerClassTableExpansion.TryWriteGender(c, gender, out _); n++; }
                        if (TrainerClassTableExpansion.TryReadPrizeMul(c, out byte prize, out _)) { TrainerClassTableExpansion.TryWritePrizeMul(c, prize, out _); n++; }
                    }
                    return n;
                });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void GameTables(string game)
        {
            Open(game);
            Unpack(DirNames.textArchives);
            int items = GetItemNames().Length, species = GetPokemonNames().Length, moves = GetAttackNames().Length;
            var tables = new (string Name, Func<string> WhyNot, Func<int> Save)[]
            {
                ("berries", BerryData.WhyNot, () => { var all = BerryData.LoadAll(); for (int i = 0; i < all.Count; i++) all[i].Save(i); return all.Count; }),
                ("BP shop", BpShopData.WhyNot, () => { BpShopData.Load().Save(items); return 1; }),
                ("Underground mining", MiningTable.WhyNot, () => { MiningTable.Load().Save(); return 1; }),
                ("incense breeding", IncenseBreedingTable.WhyNot, () => { IncenseBreedingTable.Load().Save(species, items); return 1; }),
                ("encounter slot odds", EncounterSlotOdds.WhyNot, () => { EncounterSlotOdds.Load().Save(); return 1; }),
                ("friendship changes", FriendshipTable.WhyNot, () => { FriendshipTable.Load().Save(); return 1; }),
                ("growth curves", GrowthTable.WhyNot, () => { var t = GrowthTable.Load(); for (int c = 0; c < GrowthTable.Curves; c++) t.Save(c); return GrowthTable.Curves; }),
                ("move tutors", MoveTutorData.WhyNot, () => { MoveTutorData.Load().Save(moves); return 1; }),
                ("type chart", TypeChart.WhyNot, () => { TypeChart.Load().Save(); return 1; }),
                ("wild held item odds", WildHeldItemOdds.WhyNot, () => { WildHeldItemOdds.Load().Save(); return 1; }),
                ("trainer intros", VsIntroTables.WhyNot, () => { VsIntroTables.Load(VsIntroTables.Part.Trainers).Save(); return 1; }),
                ("wild intros", VsIntroTables.WhyNot, () => { VsIntroTables.Load(VsIntroTables.Part.Wild).Save(); return 1; }),
            };
            int checkedTables = 0;
            foreach (var (name, whyNot, save) in tables)
            {
                if (whyNot() != null) continue;
                SavesUnchanged($"{game} {name}", Array.Empty<string>(), save);
                checkedTables++;
            }
            Assert.True(checkedTables > 0, $"{game}: no game table could be opened");
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void BattleDisplay(string game)
        {
            Open(game);
            bool records = gameFamily != GameFamilies.DP;
            var dirs = records
                ? new[] { DirNames.pokemonSpriteOffsets, DirNames.pokeHeight }
                : new[] { DirNames.pokeYofs, DirNames.pokeShadowOfx, DirNames.pokeShadow, DirNames.pokeHeight };
            dirs = dirs.Where(d => gameDirs.ContainsKey(d)).ToArray();
            Unpack(dirs);
            SavesUnchanged($"{game} battle display", dirs.Select(Dir), () =>
            {
                var recordNarc = records ? new OffsetNarc(DirNames.pokemonSpriteOffsets, SpeciesSpriteData.Size) : null;
                IBattleOffsetSource source = records
                    ? new CombinedTailSource(recordNarc, withHeights: gameDirs.ContainsKey(DirNames.pokeHeight))
                    : new SeparateByteSource(DirNames.pokeYofs, DirNames.pokeShadowOfx, DirNames.pokeShadow);
                int n = 0, species = GetPokemonNames().Length;
                for (int id = 0; id < species; id++)
                {
                    if (records && SpeciesSpriteData.Parse(recordNarc.GetRecord(id)) is SpeciesSpriteData data)
                        recordNarc.PutRecord(id, data.ToBytes());
                    if (!source.TryLoad(id, out BattleOffsetRecord rec)) continue;
                    source.Save(id, in rec);
                    n++;
                }
                return n;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void PokemonSprites(string game)
        {
            Open(game);
            var dirs = new[] { DirNames.pokemonBattleSprites, DirNames.otherPokemonBattleSprites }.Where(d => gameDirs.ContainsKey(d)).ToArray();
            Unpack(dirs);
            SavesUnchanged($"{game} Pokémon sprites", dirs.SelectMany(d => new[] { gameDirs[d].unpackedDir, gameDirs[d].packedDir }), () =>
            {
                int n = 0;
                foreach (var dir in dirs)
                {
                    var archive = PokemonBattleSpriteArchive.Open(dir);
                    if (archive == null) continue;
                    foreach (var (id, _) in Members(Dir(dir)).ToList())
                    {
                        if (archive.ReadSprite(id) is byte[] sprite) { Assert.True(archive.WriteSprite(id, sprite)); n++; }
                        else if (archive.ReadPalette(id) is uint[] palette) { Assert.True(archive.WritePalette(id, palette)); n++; }
                    }
                }
                return n;
            });
        }

        public static TheoryData<string> PtHg => new() { "Platinum", "HeartGold" };

        /// <summary>The binary script writer the starter, ground item and trainer roster edits go through.</summary>
        [SkippableTheory]
        [MemberData(nameof(PtHg))]
        public void Scripts(string game)
        {
            Open(game);
            Unpack(DirNames.scripts);
            SavesUnchanged($"{game} scripts", new[] { Filesystem.scripts }, () =>
            {
                int n = 0;
                foreach (var (id, _) in Members(Filesystem.scripts).ToList())
                {
                    var file = new ScriptFile(id);
                    if (file.isLevelScript || file.hasNoScripts) continue;
                    Assert.False(file.parseFailedDueToInvalidCommand, $"{game} script file {id} did not parse");
                    Assert.True(file.SaveToFileDefaultDir(id, showSuccessMessage: false), $"{game} script file {id} did not save");
                    n++;
                }
                return n;
            }, restored: new[] { ExpandedScripts });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Marts(string game)
        {
            Open(game);
            Skip.If(!IsMartEditorAvailable(), $"the Mart editor does not open {game}");
            SavesUnchanged($"{game} marts", Array.Empty<string>(), () =>
            {
                var marts = MartData.LoadCurrent();
                Assert.True(marts.SaveCurrent(), "the mart save failed");
                return 1;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Fonts(string game)
        {
            Open(game);
            Unpack(DirNames.fonts);
            SavesUnchanged($"{game} fonts", new[] { Dir(DirNames.fonts) }, () =>
            {
                var files = FieldFont.UnpackedEntries();
                int n = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    FieldFont font;
                    try { font = FieldFont.Read(File.ReadAllBytes(files[i])); } catch { continue; }
                    if (font == null) continue;
                    font.Save(i);
                    n++;
                }
                return n;
            });
        }
    }
}
