using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using DSPRE.Tracking.Kinds;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>
    /// A table inside a code file whose rows are records of their own, like the map headers in arm9. Tables with
    /// <see cref="Fields"/> are one record each, their rows named by <see cref="RowKey"/>.
    /// </summary>
    public sealed record CodeTable(string Name, string TreePath, long Offset, int RowSize, int Count,
        string Title = null, IReadOnlyList<ArrayField> Fields = null, Func<int, string> RowKey = null, bool Insertable = false, long[] Sites = null)
    {
        /// <summary>Where row <paramref name="row"/> starts: one after another, or at its own site for scattered rows.</summary>
        public long RowOffset(int row) => Sites != null ? Sites[row] : Offset + (long)RowSize * row;

        /// <summary>Every byte the rows cover.</summary>
        public IEnumerable<long> Bytes() => Enumerable.Range(0, Count).SelectMany(r => Enumerable.Range(0, RowSize).Select(b => RowOffset(r) + b));

        /// <summary>The tables of the open game, with their place in the project's files.</summary>
        public static IReadOnlyList<CodeTable> ForOpenGame(string projectRoot)
        {
            List<CodeTable> tables = new List<CodeTable>();
            if (!MapHeader.UsesDynamicHeaders && HeaderTableOffset > 0 && File.Exists(InternalNamesPath))
                tables.Add(new CodeTable("MapHeaders", Path.GetRelativePath(projectRoot, Arm9Path).Replace(Path.DirectorySeparatorChar, '/'),
                    HeaderTableOffset, MapHeader.Length, GetHeaderCount()));
            if (File.Exists(InternalNamesPath) && new FileInfo(InternalNamesPath).Length >= (long)InternalNameLength * GetHeaderCount())
                tables.Add(new CodeTable("MapNames", Path.GetRelativePath(projectRoot, InternalNamesPath).Replace(Path.DirectorySeparatorChar, '/'),
                    0, InternalNameLength, GetHeaderCount()));
            foreach ((RomLocator.Thing thing, string name, string title, int rowSize, int count, ArrayField[] fields, Func<int, string> key) in Specs())
            {
                if (count <= 0 || RomLocator.Where(thing) is not RomLocator.Spot spot) continue;
                // A compressed overlay's bytes on disk aren't the table's.
                if (spot.Overlay >= 0 && OverlayUtils.IsStillCompressed(spot.Overlay)) continue;
                string path = spot.Overlay < 0 ? Arm9Path : OverlayUtils.GetPath(spot.Overlay);
                if (!File.Exists(path) || new FileInfo(path).Length < spot.Offset + (long)rowSize * count) continue;
                tables.Add(new CodeTable(name, Path.GetRelativePath(projectRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                    spot.Offset, rowSize, count, title, fields, key));
            }
            foreach ((int overlay, long offset, string name, string title, int rowSize, int count, ArrayField[] fields, Func<int, string> key) in Located())
            {
                if (count <= 0 || offset < 0 || overlay >= 0 && OverlayUtils.IsStillCompressed(overlay)) continue;
                string path = overlay < 0 ? Arm9Path : OverlayUtils.GetPath(overlay);
                if (!File.Exists(path) || new FileInfo(path).Length < offset + (long)rowSize * count) continue;
                tables.Add(new CodeTable(name, Path.GetRelativePath(projectRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                    offset, rowSize, count, title, fields, key, Insertable: name == "egg-moves"));
            }
            // HGSS keeps the tutor learner masks in a plain file, one 8-byte mask per personal file.
            string masks = Path.Combine(DataPath, "fielddata", "wazaoshie", "waza_oshie.bin");
            if (GameFamily == GameFamilies.HGSS && !HgEngine.HgEngineProject.IsActive && File.Exists(masks) && new FileInfo(masks).Length == MoveTutorData.Rows * 8)
                tables.Add(new CodeTable("tutor-learners", Path.GetRelativePath(projectRoot, masks).Replace(Path.DirectorySeparatorChar, '/'), 0, 8, MoveTutorData.Rows,
                    "Move tutor learners", new[] { new ArrayField("tutors", 0, 8, Flags: TutorNames(52, 4)) },
                    i => (i < 493 ? i + 1 : i + 3).ToString(CultureInfo.InvariantCulture)));
            // The Pokégear phone book: a count, then one 20-byte contact each.
            string phone = Path.Combine(DataPath, "tel", "pmtel_book.dat");
            if (GameFamily == GameFamilies.HGSS && File.Exists(phone))
            {
                byte[] book = File.ReadAllBytes(phone);
                int contacts = book.Length >= PokegearPhoneBook.HeaderSize ? BitConverter.ToInt32(book, 0) : 0;
                if (contacts > 0 && PokegearPhoneBook.HeaderSize + (long)contacts * PokegearPhoneBook.EntrySize <= book.Length)
                    tables.Add(new CodeTable("phone-book", Path.GetRelativePath(projectRoot, phone).Replace(Path.DirectorySeparatorChar, '/'),
                        PokegearPhoneBook.HeaderSize, PokegearPhoneBook.EntrySize, contacts, "Pokégear phone book", new[]
                        {
                            new ArrayField("id", 0, 1), new ArrayField("type", 1, 1), new ArrayField("unused_2", 2, 1), new ArrayField("title", 3, 1),
                            new ArrayField("trainer", 4, 2, Values: n => n.Trainer), new ArrayField("map", 6, 2), new ArrayField("gift", 8, 2, Values: n => n.Item),
                            new ArrayField("local_script", 10, 2), new ArrayField("greeting", 12, 1), new ArrayField("weekday", 13, 1), new ArrayField("time_of_day", 14, 1),
                            new ArrayField("random_group", 15, 1), new ArrayField("title_rank", 16, 1), new ArrayField("name_rank", 17, 1),
                            new ArrayField("location_rank", 18, 1), new ArrayField("padding", 19, 1),
                        }));
            }
            // Each slot boundary is the immediate of a compare; Diamond and Pearl compare some twice, so each site is a row.
            foreach (SlotOddsMethod method in RomLocator.SlotOddsMethods ?? Array.Empty<SlotOddsMethod>())
            {
                if (OverlayUtils.IsStillCompressed(method.Overlay)) continue;
                string path = OverlayUtils.GetPath(method.Overlay);
                List<(long At, string Key)> sites = new List<(long, string)>();
                for (int b = 0; b < method.Boundaries.Length; b++)
                    for (int s = 0; s < method.Boundaries[b].Length; s++)
                        sites.Add((method.Boundaries[b][s], "slot_" + (b + 1).ToString(CultureInfo.InvariantCulture) + (s == 0 ? "" : "_copy_" + s.ToString(CultureInfo.InvariantCulture))));
                if (!File.Exists(path) || sites.Count == 0 || new FileInfo(path).Length <= sites.Max(x => x.At)) continue;
                string name = "slot-odds-" + new string(method.Name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                tables.Add(new CodeTable(name, Path.GetRelativePath(projectRoot, path).Replace(Path.DirectorySeparatorChar, '/'), sites[0].At, 1, sites.Count,
                    "Encounter slot odds, " + method.Name, new[] { new ArrayField("boundary", 0, 1) }, i => sites[i].Key, Sites: sites.Select(x => x.At).ToArray()));
            }
            return tables;
        }

        /// <summary>A map header's place, without the number the header list puts first.</summary>
        private static readonly Func<TrackingNames, Func<int, string>> HeaderNames =
            _ => i => HeaderLabels.Friendly() is IReadOnlyList<string> labels && i >= 0 && i < labels.Count ? labels[i].Substring(labels[i].IndexOf(' ') + 1) : null;

        /// <summary>Each tutor slot as its number and the move it teaches now, for learner masks.</summary>
        private static Func<TrackingNames, IReadOnlyList<string>> TutorNames(int slots, int rowSize)
        {
            byte[] pool = null;
            try { pool = RomLocator.Read(RomLocator.Thing.TutorPool, slots * rowSize); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidOperationException) { }
            return n => Enumerable.Range(0, slots).Select(i => (i + 1).ToString(CultureInfo.InvariantCulture)
                + (pool != null && pool.Length >= (i + 1) * rowSize ? " " + n.Move(BitConverter.ToUInt16(pool, i * rowSize)) : "")).ToList();
        }

        // Angle units: a full turn is 65536.
        private const double Degrees = 65536 / 360.0;

        /// <summary>Tables found through a pointer or a site record, where they sit in the original layout.</summary>
        private static IEnumerable<(int, long, string, string, int, int, ArrayField[], Func<int, string>)> Located()
        {
            Func<TrackingNames, Func<int, string>> species = n => n.Species, items = n => n.Item, classes = n => n.TrainerClass;
            byte[] arm9 = File.Exists(Arm9Path) ? File.ReadAllBytes(Arm9Path) : null;
            long InArm9(long ram) => ram >= ARM9.Address && arm9 != null && ram - ARM9.Address < arm9.Length ? ram - ARM9.Address : -1;

            if (arm9 != null && MartCommonCountOffset > 0 && MartCommonPointerOffset > 0 && MartCommonPointerOffset + 4 <= arm9.Length)
                yield return (-1, InArm9(BitConverter.ToUInt32(arm9, (int)MartCommonPointerOffset)), "mart-common", "Common mart stock", 4, arm9[MartCommonCountOffset],
                    new[] { new ArrayField("item", 0, 2, Values: items), new ArrayField("tier", 2, 2) }, null);

            if (arm9 != null && MartSpecialtyPointerOffset > 0 && !HgEngine.HgEngineMarts.Enabled)
            {
                List<(int Offset, string Name, int Count)> shops = new List<(int, string, int)>();
                try
                {
                    // Read from the bytes in hand: loading through the editor's path may decompress or unpack.
                    MartData marts = new MartData(arm9, MartCommonCountOffset, MartCommonPointerOffset, MartSpecialtyPointerOffset,
                        MartSpecialtyShopCount, MartShopNames.ForFamily(GameFamily));
                    for (int i = 0; i < marts.SpecialtyShops.Count; i++)
                        if (marts.SpecialtyOffsetInArm9(i) is int at && at >= 0 && marts.SpecialtyShops[i].Items.Count > 0)
                            shops.Add((at, marts.SpecialtyShops[i].Name, marts.SpecialtyShops[i].Items.Count));
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is OverflowException) { }
                foreach ((int at, string shop, int count) in shops)
                    yield return (-1, at, "mart-" + ChangesJson.Slug(shop), "Mart, " + shop, 2, count, new[] { new ArrayField("item", 0, 2, Values: items) }, null);
            }

            if (MonIconPalTableAddress != 0)
                yield return (-1, InArm9(MonIconPalTableAddress), "icon-palettes", "Party icon palettes", 1, GetPersonalFilesCount(),
                    new[] { new ArrayField("palette", 0, 1) }, null);

            GameCameraTable.Location cameras = null;
            try
            {
                if (RomLocator.Where(RomLocator.Thing.CameraTablePointers) != null) cameras = GameCameraTable.Locate();
            }
            catch (IOException) { }
            if (cameras is { PointersAgree: true })
            {
                List<ArrayField> camera = new List<ArrayField>
                {
                    new ArrayField("distance_tiles", 0, 4, Scale: 65536, Decimals: -1), new ArrayField("tilt_degrees", 4, 2, Signed: true, Scale: -Degrees, Decimals: 3),
                    new ArrayField("turn_degrees", 6, 2, Signed: true, Scale: Degrees, Decimals: 3), new ArrayField("roll_degrees", 8, 2, Signed: true, Scale: 2 * Degrees, Decimals: 3),
                    new ArrayField("unknown_10", 10, 2, Signed: true), new ArrayField("perspective", 12, 1), new ArrayField("unknown_13", 13, 1),
                    new ArrayField("field_of_view_degrees", 14, 2, Scale: Degrees / 2, Decimals: 3), new ArrayField("near_clip_tiles", 16, 4, Scale: 65536, Decimals: -1),
                    new ArrayField("far_clip_tiles", 20, 4, Scale: 65536, Decimals: -1),
                };
                if (GameFamily == GameFamilies.HGSS)
                    camera.AddRange(new[]
                    {
                        new ArrayField("x_offset_tiles", 24, 4, Signed: true, Scale: 65536, Decimals: -1), new ArrayField("y_offset_tiles", 28, 4, Signed: true, Scale: 65536, Decimals: -1),
                        new ArrayField("z_offset_tiles", 32, 4, Signed: true, Scale: 65536, Decimals: -1),
                    });
                yield return (CameraTblOverlayNumber, cameras.Offset, "cameras", "Cameras", CameraSize, CameraCount, camera.ToArray(), null);
            }

            if (RomLocator.VsIntroSites is VsIntroSites vs && vs.RecordOverlay >= 0)
            {
                bool dp = GameFamily == GameFamilies.DP;
                ArrayField[] gym = dp
                    ? new[] { new ArrayField("end_x", 0, 4, Signed: true, Scale: 4096, Decimals: -1), new ArrayField("class", 4, 2, Values: classes), new ArrayField("face_column", 6, 2) }
                    : new[]
                    {
                        new ArrayField("end_x", 0, 4, Signed: true, Scale: 4096, Decimals: -1), new ArrayField("trainer", 4, 4, Values: n => n.Trainer), new ArrayField("class", 8, 2, Values: classes),
                        new ArrayField("face_palette", 12, 1), new ArrayField("face_tiles", 13, 1), new ArrayField("face_cells", 14, 1), new ArrayField("face_animation", 15, 1),
                        new ArrayField("banner_palette", 16, 1), new ArrayField("banner_tiles", 17, 1), new ArrayField("banner_screen", 18, 1),
                    };
                if (vs.GymTable >= 0)
                    yield return (vs.RecordOverlay, vs.GymTable, "vs-intro-gyms", "VS intros, gym leaders", vs.GymSize, vs.GymCount, gym, null);
                if (vs.RivalRecord >= 0)
                    yield return (vs.RecordOverlay, vs.RivalRecord, "vs-intro-rival", "VS intro, rival", vs.GymSize, 1, gym, _ => "rival");
                if (vs.LeagueTable >= 0)
                    yield return (vs.RecordOverlay, vs.LeagueTable, "vs-intro-league", "VS intros, Pokémon League", 8, 5, dp
                        ? new[] { new ArrayField("camera_turn", 0, 4, Signed: true, Scale: Degrees, Decimals: 3), new ArrayField("pan_frames", 4, 1), new ArrayField("class", 5, 1, Values: classes), new ArrayField("frame_palette", 6, 1) }
                        : new[] { new ArrayField("face_base", 0, 2), new ArrayField("frame_palette", 2, 1), new ArrayField("clash_frames", 3, 1), new ArrayField("class", 4, 2, Values: classes), new ArrayField("trainer", 6, 2, Values: n => n.Trainer) },
                        null);
            }

            BattleMusicTables music = null;
            try { music = BattleMusicTables.IsSupported ? BattleMusicTables.LoadRom() : null; }
            catch (IOException) { }
            if (music != null && string.Equals(Path.GetFullPath(music.Combos.Path), Path.GetFullPath(Arm9Path), StringComparison.OrdinalIgnoreCase))
                yield return (-1, music.Combos.Start, "battle-intros", "Battle intros and music", 4, music.Combos.Rows.Count,
                    new[] { new ArrayField("transition", 0, 2), new ArrayField("sequence", 2, 2) }, null);
            // HGSS picks an intro by class and by species from packed rows: the id in the low 10 bits, the intro above.
            foreach ((BattleMusicTables.Table<(int, int)> packed, string name, string title, Func<TrackingNames, Func<int, string>> ids) in new[]
                     {
                         (music?.Classes, "battle-intro-classes", "Battle intros by trainer class", classes),
                         (music?.Species, "battle-intro-species", "Battle intros by Pokémon", species),
                     })
                if (packed != null && packed.Rows.Count > 0 && !packed.Repointed && packed.Path != null
                    && string.Equals(Path.GetFullPath(packed.Path), Path.GetFullPath(Arm9Path), StringComparison.OrdinalIgnoreCase))
                    yield return (-1, packed.Start, name, title, 2, packed.Rows.Count,
                        new[] { new ArrayField("id", 0, 2, Values: ids, Bits: 10), new ArrayField("intro", 0, 2, Shift: 10, Bits: 6) }, null);

            if (RomLocator.BpShopSites is BpShopSites bp && bp.ExchangeTable > 0)
                yield return (-1, bp.ExchangeTable, "bp-exchange", "Battle Point exchange counter", 4, BpShopData.ExchangeRows,
                    new[] { new ArrayField("item", 0, 2, Values: items), new ArrayField("price", 2, 2) }, null);

            if (RomLocator.TrainerPrizeTable is TrainerPrizeTable prize)
                yield return prize.Paired
                    ? (prize.Overlay, prize.Table, "trainer-prizes", "Trainer class prize money", 4, prize.Count,
                        new[] { new ArrayField("class", 0, 2, Values: classes), new ArrayField("multiplier", 2, 2) }, null)
                    : (prize.Overlay, prize.Table, "trainer-prizes", "Trainer class prize money", 1, prize.Count,
                        new[] { new ArrayField("multiplier", 0, 1) }, null);

            if (RomLocator.SwarmSites is SwarmSites swarms)
                yield return (swarms.Overlay, swarms.Table, "swarm-places", "Swarm places", swarms.RowSize, swarms.Rows,
                    swarms.RowSize == 4 && GameFamily == GameFamilies.HGSS
                        ? new[] { new ArrayField("header", 0, 2), new ArrayField("method", 2, 2) }
                        : new[] { new ArrayField("header", 0, swarms.RowSize) }, null);

            if (GameFamily != GameFamilies.HGSS && RomLocator.Where(RomLocator.Thing.EggMoves) is RomLocator.Spot eggs)
                yield return (eggs.Overlay, eggs.Offset, "egg-moves", "Egg moves", 2, GetEggMoveTableMaxBytes() / 2,
                    new[] { new ArrayField("entry", 0, 2, Values: n => v => v == 0xFFFF ? "end" : v >= EggMoveData.SpeciesConstant ? "Pokémon " + n.Species(v - EggMoveData.SpeciesConstant) : n.Move(v)) },
                    null);

            if (GameFamily != GameFamilies.HGSS && RomLocator.Where(RomLocator.Thing.StarterSpecies) is RomLocator.Spot starters)
                yield return (starters.Overlay, starters.Offset, "starters", "Starter Pokémon", 4, 3, new[] { new ArrayField("species", 0, 4, Values: species) }, null);
            else if (GameFamily == GameFamilies.HGSS && !HgEngine.HgEngineProject.IsActive)
            {
                // HGSS builds each starter from a halfword in arm9 code; the halfword after it is an instruction.
                if (RomLocator.Where(RomLocator.Thing.StarterSpecies) is RomLocator.Spot hgStarters)
                    yield return (hgStarters.Overlay, hgStarters.Offset, "starters", "Starter Pokémon", 4, 3,
                        new[] { new ArrayField("species", 0, 2, Values: species), new ArrayField("code", 2, -2) }, null);
                if (RomLocator.Where(RomLocator.Thing.StarterCries) is RomLocator.Spot cries)
                    yield return (cries.Overlay, cries.Offset, "starter-cries", "Starter cries", 4, 3, new[] { new ArrayField("species", 0, 4, Values: species) }, null);
            }

            if (!HgEngine.HgEngineProject.IsActive && MoveBackgroundTableSite.Overlay >= 0 && !OverlayUtils.IsStillCompressed(MoveBackgroundTableSite.Overlay)
                && MoveBackgroundTable.TryLoad(out MoveBackgroundTable backgrounds, out _) && backgrounds.Overlay >= 0)
                yield return (backgrounds.Overlay, backgrounds.Offset, "move-backgrounds", "Move backgrounds", MoveBackgroundTable.Columns * 4, backgrounds.Rows.Count,
                    MoveBackgroundTable.ColumnNames.Select((c, i) => new ArrayField(c.ToLowerInvariant().Replace(' ', '_'), i * 4, 4)).ToArray(), null);

            if (PokegearRematchTable.IsSupported && !HgEngine.HgEngineProject.IsActive && !OverlayUtils.IsStillCompressed(PokegearRematchOverlayNumber)
                && PokegearRematchTable.Resolve(out _) is RematchTable.Location gear && gear.RowCount > 0)
                yield return (gear.OverlayNumber, gear.Offset, "pokegear-rematches", "Pokégear rematches", RematchTable.RowSize, gear.RowCount,
                    Enumerable.Range(0, RematchTable.SlotCount).Select(slot => new ArrayField(slot == 0 ? "trainer" : "rematch_" + slot.ToString(CultureInfo.InvariantCulture), slot * 2, 2,
                        Values: n => n.Trainer)).ToArray(), null);

            if (GameFamily == GameFamilies.HGSS && DungeonCutinTableOffsetToRAMAddress != 0 && arm9 != null
                && InArm9(BitConverter.ToUInt32(arm9, (int)DungeonCutinTableOffsetToRAMAddress)) is long cutins and >= 0)
            {
                List<ArrayField> cutin = new List<ArrayField> { new ArrayField("header", 0, 4, Values: HeaderNames), new ArrayField("wipe_type", 4, 4) };
                foreach ((string time, int t) in new[] { ("morning", 0), ("noon", 1), ("evening", 2), ("night", 3) })
                    cutin.AddRange(new[]
                    {
                        new ArrayField(time + "_palette", 8 + t * 12, 4), new ArrayField(time + "_tiles", 12 + t * 12, 4), new ArrayField(time + "_screen", 16 + t * 12, 4),
                    });
                cutin.Add(new ArrayField("name_line", 56, 4));
                yield return (-1, cutins, "dungeon-cutins", "Dungeon cut-ins", 60, DungeonCutinTable.RowCount, cutin.ToArray(), null);
            }

            // Header music that plays while a flag is set: the count sits eight bytes before the table's pointer.
            if (arm9 != null && ConditionalMusicTableOffsetToRAMAddress >= 8 && ConditionalMusicTableOffsetToRAMAddress + 4 <= arm9.Length
                && InArm9(BitConverter.ToUInt32(arm9, (int)ConditionalMusicTableOffsetToRAMAddress)) is long flagMusic and >= 0)
                yield return (-1, flagMusic, "conditional-music", "Conditional music", 6, arm9[ConditionalMusicTableOffsetToRAMAddress - 8], new[]
                {
                    new ArrayField("header", 0, 2, Values: HeaderNames),
                    new ArrayField("flag", 2, 2), new ArrayField("music", 4, 2),
                }, null);

            // The overworld tables where the game keeps them; the expansion patch and hg-engine move them elsewhere.
            int owOverlay = new[] { 5, 1, 131 }.FirstOrDefault(n => OWtablePath != null && OverlayUtils.GetPath(n) == OWtablePath, -1);
            if (owOverlay >= 0 && OWTableOffset > 0 && !OverworldSpriteTableExpansion.IsApplied && !HgEngine.HgEngineProject.IsActive
                && !OverlayUtils.IsStillCompressed(owOverlay) && File.Exists(OWtablePath))
            {
                byte[] ow = File.ReadAllBytes(OWtablePath);
                bool hgss = GameFamily == GameFamilies.HGSS;
                int size = hgss ? 6 : 8;
                List<int> ids = new List<int>();
                for (long at = OWTableOffset; at + size <= ow.Length; at += size)
                {
                    int id = hgss ? BitConverter.ToUInt16(ow, (int)at) : BitConverter.ToInt32(ow, (int)at);
                    if (id == 0xFFFF) break;
                    ids.Add(id);
                }
                ArrayField[] sprites = hgss
                    ? new[] { new ArrayField("id", 0, 2), new ArrayField("sprite", 2, 2), new ArrayField("properties", 4, 2) }
                    : new[] { new ArrayField("id", 0, 4), new ArrayField("sprite", 4, 4) };
                yield return (owOverlay, OWTableOffset, "overworld-sprites", "Overworld sprites", size, ids.Count, sprites, i => "id " + ids[i].ToString(CultureInfo.InvariantCulture));

                if (OverworldSpriteTableExpansion.VanillaRenderTable() is (long start, int rows))
                {
                    List<int> renderIds = Enumerable.Range(0, rows).Select(r => BitConverter.ToInt32(ow, (int)(start + r * 8L))).ToList();
                    Func<TrackingNames, Func<int, string>> Named(params string[] names) => _ => v => v < names.Length ? names[v] : null;
                    yield return (owOverlay, start, "overworld-drawing", "Overworld drawing", 8, rows, new[]
                    {
                        new ArrayField("id", 0, 4), new ArrayField("draw", 4, 4, Values: Named("none", "billboard", "3D model"), Bits: 4),
                        new ArrayField("shadow", 4, 4, Values: Named("none", "on"), Shift: 4, Bits: 2),
                        new ArrayField("footprints", 4, 4, Values: Named("none", "walking", "cycling"), Shift: 6, Bits: 4),
                        new ArrayField("reflection", 4, 4, Values: Named("none", "on"), Shift: 10, Bits: 2),
                        new ArrayField("other_bits", 4, 4, Shift: 12, Bits: 20),
                    }, i => "id " + renderIds[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            if (RomLocator.MiningCodeSites is MiningCodeSites mining && RomLocator.Where(RomLocator.Thing.MiningTreasures) is RomLocator.Spot wall)
                foreach ((uint at, string name, string title) in new[]
                         {
                             (mining.Range, "range", "treasure count range"), (mining.Minimum, "minimum", "treasure minimum"),
                             (mining.FirstDig, "first-dig", "first dig treasures"), (mining.Tries, "rock-tries", "rock placement tries"),
                         })
                    if (at != 0)
                        yield return (wall.Overlay, at, "mining-" + name, "Underground " + title, 1, 1, new[] { new ArrayField("value", 0, 1) }, _ => "value");
        }

        private static IEnumerable<(RomLocator.Thing, string, string, int, int, ArrayField[], Func<int, string>)> Specs()
        {
            Func<TrackingNames, Func<int, string>> species = n => n.Species, items = n => n.Item, moves = n => n.Move, types = n => n.Type;
            string Snake(string s) => new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');

            yield return (RomLocator.Thing.FriendshipChanges, "friendship-changes", "Friendship changes", FriendshipTable.Bands, FriendshipTable.Events,
                new[]
                {
                    new ArrayField("below_100", 0, 1, Signed: true), new ArrayField("100_to_199", 1, 1, Signed: true), new ArrayField("200_and_up", 2, 1, Signed: true),
                },
                i => i < FriendshipTable.EventNames.Length ? Snake(FriendshipTable.EventNames[i]) : i.ToString(CultureInfo.InvariantCulture));
            yield return (RomLocator.Thing.WildHeldItemOdds, "wild-held-item-odds", "Wild held item odds", WildHeldItemOdds.Size, 1,
                new[]
                {
                    new ArrayField("none_below", 0, 2), new ArrayField("rare_from", 2, 2),
                    new ArrayField("compound_eyes_none_below", 4, 2), new ArrayField("compound_eyes_rare_from", 6, 2),
                }, _ => "odds");
            yield return (RomLocator.Thing.IncenseBabies, "incense-babies", "Breeding items", IncenseBreedingTable.RowSize, IncenseBreedingTable.RowCount,
                new[] { new ArrayField("baby", 0, 2, Values: species), new ArrayField("item", 2, 2, Values: items), new ArrayField("otherwise", 4, 2, Values: species) }, null);
            yield return (RomLocator.Thing.TypeChart, "type-chart", "Type chart", TypeChart.RecordSize, 112,
                new[] { new ArrayField("attacker", 0, 1, Values: types), new ArrayField("defender", 1, 1, Values: types), new ArrayField("effect", 2, 1) }, null);
            yield return (RomLocator.Thing.PoketchTypeChart, "poketch-type-chart", "Pokétch move tester type chart", TypeChart.VanillaTypes, TypeChart.VanillaTypes,
                Enumerable.Range(0, TypeChart.VanillaTypes).Select(d => new ArrayField("vs_" + d.ToString(CultureInfo.InvariantCulture), d, 1, Signed: true)).ToArray(),
                i => "type_" + i.ToString(CultureInfo.InvariantCulture));
            yield return (RomLocator.Thing.MiningTreasures, "mining-treasures", "Underground treasures", MiningTable.RowSize, MiningTable.RowCount,
                new[]
                {
                    new ArrayField("shape_address", 0, 4), new ArrayField("weight_1", 4, 2), new ArrayField("weight_2", 6, 2), new ArrayField("weight_3", 8, 2),
                    new ArrayField("weight_4", 10, 2), new ArrayField("bytes_12", 12, -4), new ArrayField("sprite", 16, 2), new ArrayField("palette", 18, 2),
                }, null);
            yield return (RomLocator.Thing.MiningItems, "mining-items", "Underground bag items", 2, MiningTable.ItemListCount,
                new[] { new ArrayField("item", 0, 2, Values: items) }, null);
            yield return (RomLocator.Thing.MachineMoves, "machine-moves", "TM and HM moves", 2, TMEditor.VanillaMachineCount,
                new[] { new ArrayField("move", 0, 2, Values: moves) },
                i => i < PokemonPersonalData.TmsCount ? "TM" + (i + 1).ToString("D2", CultureInfo.InvariantCulture) : "HM" + (i - PokemonPersonalData.TmsCount + 1).ToString("D2", CultureInfo.InvariantCulture));
            yield return (RomLocator.Thing.TrainerClassGenders, "trainer-class-genders", "Trainer class genders", 1, VanillaTrainerClassCount,
                new[] { new ArrayField("gender", 0, 1) }, null);

            bool hgss = GameFamily == GameFamilies.HGSS, pt = GameFamily == GameFamilies.Plat;
            if (pt)
            {
                yield return (RomLocator.Thing.BpShopItems, "bp-shop-items", "Battle Point shop, item counter", 2, BpShopData.VanillaLeft + 1,
                    new[] { new ArrayField("item", 0, 2, Values: items) }, null);
                yield return (RomLocator.Thing.BpShopTms, "bp-shop-tms", "Battle Point shop, TM counter", 2, BpShopData.VanillaRight + 1,
                    new[] { new ArrayField("item", 0, 2, Values: items) }, null);
                yield return (RomLocator.Thing.BpShopPrices, "bp-shop-prices", "Battle Point shop prices", 4, BpShopData.ExchangeRows,
                    new[] { new ArrayField("item", 0, 2, Values: items), new ArrayField("price", 2, 2) }, null);
                yield return (RomLocator.Thing.TutorPool, "tutor-moves", "Move tutor moves", 12, 38,
                    new[]
                    {
                        new ArrayField("move", 0, 2, Values: moves), new ArrayField("red_shards", 2, 1), new ArrayField("blue_shards", 3, 1),
                        new ArrayField("yellow_shards", 4, 1), new ArrayField("green_shards", 5, 1), new ArrayField("bytes_6", 6, -2), new ArrayField("place", 8, 4),
                    }, null);
                yield return (RomLocator.Thing.TutorCompatibility, "tutor-learners", "Move tutor learners", 5, MoveTutorData.Rows,
                    new[] { new ArrayField("tutors", 0, 5, Flags: TutorNames(38, 12)) },
                    i => (i < 493 ? i + 1 : i + 3).ToString(CultureInfo.InvariantCulture));
            }
            else if (hgss)
                yield return (RomLocator.Thing.TutorPool, "tutor-moves", "Move tutor moves", 4, 52,
                    new[] { new ArrayField("move", 0, 2, Values: moves), new ArrayField("cost", 2, 1), new ArrayField("place", 3, 1) }, null);

            if (GameFamily != GameFamilies.HGSS)
                yield return (RomLocator.Thing.VsSeekerRematches, "vs-seeker-rematches", "Vs. Seeker rematches", RematchTable.RowSize, VsSeekerRematchTable.RowCount,
                    Enumerable.Range(0, RematchTable.SlotCount).Select(s => new ArrayField(s == 0 ? "trainer" : "rematch_" + s.ToString(CultureInfo.InvariantCulture), s * 2, 2,
                        Values: n => n.Trainer)).ToArray(), null);
            yield return (RomLocator.Thing.PickupCommonItems, "pickup-common", "Pickup common items", 2, PickupTable.CommonCount,
                new[] { new ArrayField("item", 0, 2, Values: items) }, null);
            yield return (RomLocator.Thing.PickupRareItems, "pickup-rare", "Pickup rare items", 2, PickupTable.RareCount,
                new[] { new ArrayField("item", 0, 2, Values: items) }, null);
            yield return (RomLocator.Thing.PickupWeights, "pickup-odds", "Pickup odds", 1, PickupTable.WeightSize,
                new[] { new ArrayField("percent", 0, 1) }, null);
            yield return (RomLocator.Thing.PickupDivisor, "pickup-chance", "Pickup chance", 1, 1,
                new[] { new ArrayField("one_in", 0, 1) }, _ => "pickup");
            yield return (RomLocator.Thing.HiddenItems, "hidden-items", "Hidden items", HiddenItemTable.EntrySize, HiddenItemTable.Count(),
                new[]
                {
                    new ArrayField("item", 0, 2, Values: items), new ArrayField("quantity", 2, 1), new ArrayField("range", 3, 1),
                    new ArrayField("padding", 4, 2), new ArrayField("script", 6, 2),
                }, null);
            yield return (RomLocator.Thing.FlyTable, "fly-points", "Fly and warp points", hgss ? 18 : 16, FlyTableRows,
                hgss
                    ? new[]
                    {
                        new ArrayField("flag", 0, 1), new ArrayField("flags", 1, 1), new ArrayField("game_over_header", 2, 2), new ArrayField("local_x", 4, 1),
                        new ArrayField("local_y", 5, 1), new ArrayField("fly_header", 6, 2), new ArrayField("global_x", 8, 2), new ArrayField("global_y", 10, 2),
                        new ArrayField("unlock_header", 12, 2), new ArrayField("unlock_x", 14, 2), new ArrayField("unlock_y", 16, 2),
                    }
                    : new[]
                    {
                        new ArrayField("game_over_header", 0, 2), new ArrayField("local_x", 2, 2), new ArrayField("local_y", 4, 2), new ArrayField("fly_header", 6, 2),
                        new ArrayField("global_x", 8, 2), new ArrayField("global_y", 10, 2), new ArrayField("teleport", 12, 1), new ArrayField("unlock_on_entry", 13, 1),
                        new ArrayField("unlock_id", 14, 2),
                    }, null);
            yield return (RomLocator.Thing.SpawnPoint, "spawn-point", "New game spawn point", 18, 1,
                new[] { new ArrayField("header", 0, 2), new ArrayField("global_x", 8, 2), new ArrayField("global_y", 12, 2), new ArrayField("direction", 16, 2) },
                _ => "spawn");
            yield return (RomLocator.Thing.InitialMoney, "starting-money", "Starting money", 4, 1, new[] { new ArrayField("money", 0, 4) }, _ => "money");
            if (hgss)
                yield return (RomLocator.Thing.RockSmashItemTables, "rock-smash-items", "Rock Smash items", RockSmashItemSlots.Slots * 2, 3,
                    Enumerable.Range(0, RockSmashItemSlots.Slots).Select(s => new ArrayField("slot_" + (s + 1).ToString(CultureInfo.InvariantCulture), s * 2, 2, Values: items)).ToArray(),
                    i => i == 0 ? "ruins_of_alph" : i == 1 ? "default" : "cliff_cave");
        }
    }
}
