using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using Images;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Every 2D graphic in the game, in one list, with a picture of each where one can be made.
    /// </summary>
    public static partial class GraphicAssets
    {
        /// <summary>Where a person would look for something, rather than where the ROM keeps it. </summary>
        public enum Group
        {
            PokemonSprites, PokemonIcons,
            Trainers,
            BattleScenery, BattleGauges, BattleIcons, BattleChrome,
            MoveEffects,
            Items,
            TextAndFonts, Windows,
            Places,
        }

        /// <summary>How the colours for a drawing are found.</summary>
        public enum Pairing
        {
            /// <summary>The colours sit in this same archive; find the nearest palette before the drawing.</summary>
            NearestInSameArchive,
            /// <summary>The colours are in another archive, one for one by position.</summary>
            SameIndexInOtherArchive,
            /// <summary>One palette in this archive serves everything in it.</summary>
            OnePaletteForAll,
            /// <summary>Not worked out yet. Say so rather than show wrong colours.</summary>
            NotKnown,
        }

        public sealed class Archive
        {
            public DirNames Dir;
            public string Title;
            public Group In;
            public string What;                 // one line, what these are for
            public Pairing Colours = Pairing.NearestInSameArchive;
            public DirNames? ColourArchive;     // when the colours live somewhere else
            public DirNames? DrawingArchive;    // when the drawing a layout arranges lives somewhere else
            public string DeepEditor;           // the editor that already knows this, if any
            public string CannotImportBecause;  // null when a PNG can go back in

            // What the games actually do with these files, where it is known.
            public int PixelWidth;              // 0 when the drawing has to be measured instead
            public Func<int, int> PixelWidthOf; // when the width differs entry by entry; 0 to fall back
            public bool ScrambledPixels;        // the pixels are run through a rolling key and must be undone

            // Several of these files hold more than one picture: a party icon is two frames of an animation
            // stacked up, a battle sprite is two side by side.
            public int FrameWidth, FrameHeight; // 0 for both when the file holds a single picture
            public Func<int, int> ColourBank;   // which bank of the palette an entry uses
            public Func<int, int> ColourEntry;  // which entry holds the colours, when the game says so
            public Func<int, string> NameOf;    // what this entry is, in the player's words

            // Most of these archives do not hold one picture per file.
            public int LeadIn;                  // files before the run of things starts
            public int Stride;                  // how many files make one thing; 0 means one each
            public string[] PartNames;          // what each of those files is, in order
            public string LeadInName;           // what the files before the run are, together

            /// <summary>For archives whose things cannot be described by a plain run: builds them itself.</summary>
            public Func<int, List<Unit>> BuildUnits;

            /// <summary>The shiny colours for a drawing, when the archive keeps a second set. Only used
            /// when the shiny view is asked for.</summary>
            public Func<int, int> ShinyColourEntry;

            /// <summary>The file saying how a drawing's tiles are arranged, when it needs one. </summary>
            public Func<int, int> ArrangementEntry;

        /// <summary>Which entry holds the drawing a layout arranges, when the game says so.</summary>
        public Func<int, int> DrawingEntry;
        }

        /// <summary>One file of a thing, and what that file is.</summary>
        public sealed class UnitPart
        {
            public Archive Archive;             // usually the unit's own, but not always
            public int Index;
            public string Name;
        }

        /// <summary>One thing in an archive, which is usually several files. </summary>
        public sealed class Unit
        {
            public Archive Archive;
            public string Name;
            public List<UnitPart> Parts = new();
            /// <summary>
            /// The lowest file this thing occupies, which is where it starts in the archive.
            /// </summary>
            public int First
            {
                get
                {
                    if (Parts.Count == 0) return 0;
                    int lowest = Parts[0].Index;
                    foreach (var p in Parts) if (p.Index < lowest) lowest = p.Index;
                    return lowest;
                }
            }

            /// <summary>Which tab this row belongs on, when that is not the whole archive's tab.</summary>
            public Group? In;
        }

        /// <summary>Breaks an archive into the things it holds rather than the files it holds.</summary>
        /// <summary>
        /// The order a thing's pieces are shown in, which is the order somebody works in rather than the
        /// order the archive stores them.
        /// The assembled picture comes first where there is one, because that is the thing itself: a
        /// trainer or an HP bar looks like nothing as a sheet of loose tiles. Then the drawing, then how
        /// it moves, then its colours.
        /// </summary>
        public static int PartRank(string part) => part switch
        {
            "As it appears" => 0, "Drawing" => 1, "Animation" => 2, "Arrangement" => 3, "Colours" => 4,
            _ => 5,
        };

        public static List<Unit> Units(Archive a, int fileCount)
        {
            if (a.BuildUnits != null)
            {
                try { return a.BuildUnits(fileCount) ?? new List<Unit>(); }
                catch (Exception ex) { AppLogger.Error("GraphicAssets.Units failed: " + ex.Message); }
            }

            var units = new List<Unit>();
            if (fileCount <= 0) return units;

            int stride = a.Stride > 0 ? a.Stride : 1;
            int at = 0;

            if (a.LeadIn > 0)
            {
                int n = Math.Min(a.LeadIn, fileCount);
                var lead = new Unit { Archive = a, Name = a.LeadInName ?? "Shared pieces" };
                for (int k = 0; k < n; k++)
                    lead.Parts.Add(new UnitPart { Archive = a, Index = k, Name = "File " + k });
                units.Add(lead);
                at = n;
            }

            for (; at < fileCount; at += stride)
            {
                int n = Math.Min(stride, fileCount - at);
                string name = null;
                try { name = a.NameOf?.Invoke(at); } catch { }
                var u = new Unit { Archive = a, Name = name ?? a.Title };
                for (int k = 0; k < n; k++)
                    u.Parts.Add(new UnitPart
                    {
                        Archive = a, Index = at + k,
                        Name = a.PartNames != null && k < a.PartNames.Length
                            ? a.PartNames[k] : "File " + (at + k),
                    });
                u.Parts.Sort((x, y) => PartRank(x.Name).CompareTo(PartRank(y.Name)));
                units.Add(u);
            }
            return units;
        }

        /// <summary>Which files in the item icon archive belong to which item.</summary>
        internal static class ItemIcons
        {
            /// <summary>One icon: the drawing, the colours, and every item that uses that pair.</summary>
            internal sealed class Icon
            {
                public int Drawing, Colours;
                public List<string> Items = new();
                public string Name => Items.Count <= 1 ? Items.FirstOrDefault()
                                    : $"{Items[0]} and {Items.Count - 1} more";
            }

            private static List<Icon> _icons;
            private static Dictionary<int, int> _sharing;   // drawing -> how many icons use it
            private static Dictionary<int, int> _colours;   // drawing -> a colours entry that suits it
            private static string _builtFor;

            // Every game's item icon archive starts with these two, and ends with a spare drawing and an
            // arrow. The archive's own index names them, and they are in the same place in every game.
            public const int AnimationFile = 0;
            public const int LayoutFile = 1;

            private static void Build()
            {
                string now = RomInfo.gameDirs != null && RomInfo.gameDirs.ContainsKey(DirNames.itemIcons)
                    ? RomInfo.gameDirs[DirNames.itemIcons].unpackedDir : null;
                if (_builtFor == now && _icons != null) return;

                _icons = new List<Icon>();
                _sharing = new Dictionary<int, int>();
                _colours = new Dictionary<int, int>();
                _builtFor = now;
                if (now == null) return;

                try
                {
                    var itemNames = RomInfo.GetItemNames();
                    var byPair = new Dictionary<(int, int), Icon>();
                    for (int item = 0; item < itemNames.Length; item++)
                    {
                        uint at = (uint)(RomInfo.itemTableOffset + item * 8);
                        int drawing = ARM9.ReadWordLE(at + 2);
                        int colours = ARM9.ReadWordLE(at + 4);
                        if (drawing < 0 || colours < 0) continue;

                        // Items with the same drawing and the same colours look identical, so they share a
                        // row. Items sharing only the drawing get a row each: four of the status healers
                        // are one bottle in four colours, and folding those together lost three of them.
                        if (!byPair.TryGetValue((drawing, colours), out var icon))
                        {
                            icon = new Icon { Drawing = drawing, Colours = colours };
                            byPair[(drawing, colours)] = icon;
                            _icons.Add(icon);
                            _sharing[drawing] = _sharing.TryGetValue(drawing, out int n) ? n + 1 : 1;
                        }
                        string name = itemNames[item]?.Trim();
                        icon.Items.Add(string.IsNullOrEmpty(name) || name.Trim('-').Length == 0
                                       ? "Item " + item : name);
                        if (!_colours.ContainsKey(drawing)) _colours[drawing] = colours;
                    }
                }
                catch (Exception ex) { AppLogger.Error("ItemIcons.Build failed: " + ex.Message); }
            }

            /// <summary>Every icon in the archive, in item order.</summary>
            public static IReadOnlyList<Icon> Icons() { Build(); return _icons ?? new List<Icon>(); }

            /// <summary>How many icons use this drawing, so a row can say when one is shared.</summary>
            public static int Sharing(int drawing)
            {
                Build();
                return _sharing != null && _sharing.TryGetValue(drawing, out int n) ? n : 0;
            }

            /// <summary>The drawing an item uses, from the game's own table, or -1.</summary>
            public static int DrawingForItem(int itemId)
            {
                try
                {
                    uint at = (uint)(RomInfo.itemTableOffset + itemId * 8);
                    int drawing = ARM9.ReadWordLE(at + 2);
                    return drawing >= 0 ? drawing : -1;
                }
                catch { return -1; }
            }

            public static string NameOfEntry(int entry)
            {
                Build();
                if (entry == AnimationFile) return "Item icon animation";
                if (entry == LayoutFile) return "Item icon layout";
                var icon = _icons?.FirstOrDefault(i => i.Drawing == entry);
                return icon?.Name;
            }

            public static int ColoursFor(int entry)
            {
                Build();
                return _colours != null && _colours.TryGetValue(entry, out int c) ? c : -1;
            }

            public static void Forget() { _icons = null; _sharing = null; _colours = null; _builtFor = null; }
        }

        /// <summary>The drawing an item uses, for an editor handing one over.</summary>
        public static int DrawingForItem(int itemId) => ItemIcons.DrawingForItem(itemId);

        /// <summary>Looks a name up in a list, or gives nothing when the number is off the end of it.</summary>
        private static string FromList(Func<string[]> list, int at)
        {
            try
            {
                var names = list();
                if (names == null || at < 0 || at >= names.Length) return null;
                string name = names[at]?.Trim();
                // The games fill their unused slots with dashes. That is not a name, so fall back to
                // saying which archive the entry is in.
                if (string.IsNullOrEmpty(name) || name.Trim('-').Length == 0) return null;
                return name;
            }
            catch { return null; }
        }

        /// <summary>
        /// The eighteen archives that hold 2D graphics, from the census in
        /// Research/Graphics/GraphicsCensus.md.
        /// </summary>
        public static readonly Archive[] All =
        {
            // Six files per Pokemon, in this order, per PokemonSpriteEditorViewModel.cs:24 and :855.
            new Archive { Dir = DirNames.pokemonBattleSprites, Title = "Pokemon battle sprites", In = Group.PokemonSprites,
                What = "Front and back of every Pokemon as it appears in a battle, with its normal and shiny colours.",
                Colours = Pairing.NearestInSameArchive, DeepEditor = "Pokemon Sprite Editor",
                ScrambledPixels = true, FrameWidth = 80, FrameHeight = 80,
                Stride = 6,
                PartNames = new[] { "Back, female", "Back, male", "Front, female", "Front, male",
                                    "Colours", "Shiny colours" },
                // All four of a Pokemon's sprites share one set of colours; the only other set is the shiny
                // one.
                ColourEntry = i => (i / 6) * 6 + 4,
                ShinyColourEntry = i => (i / 6) * 6 + 5,
                NameOf = i => FromList(RomInfo.GetPokemonNames, i / 6) },
            // This archive is not laid out in runs: a form's two drawings sit near the front and its two
            // sets of colours a hundred files further on.
            new Archive { Dir = DirNames.otherPokemonBattleSprites, Title = "Alternate form sprites", In = Group.PokemonSprites,
                What = "Battle sprites for the forms that are not a Pokemon's default one.",
                Colours = Pairing.NearestInSameArchive, DeepEditor = "Pokemon Sprite Editor",
                ScrambledPixels = true, FrameWidth = 80, FrameHeight = 80,
                ColourEntry = i => AlternateFormSprites.ColoursFor(i, shiny: false),
                ShinyColourEntry = i => AlternateFormSprites.ColoursFor(i, shiny: true),
                NameOf = i => AlternateFormSprites.WhoOwns(i)?.Form.Name,
                BuildUnits = count => AlternateFormSprites.UnitsFor(
                    All.First(x => x.Dir == DirNames.otherPokemonBattleSprites), count) },
            // Seven files come first, then one icon per Pokemon, per DSUtils.cs:1338 (species + 7).
            new Archive { Dir = DirNames.monIcons, Title = "Pokemon party icons", In = Group.PokemonIcons,
                What = "The small pictures used in the party, the box and the menus.",
                Colours = Pairing.OnePaletteForAll, DeepEditor = "Pokemon Editor",
                PixelWidth = 32, FrameWidth = 32, FrameHeight = 32,
                ColourBank = i =>
                {
                    if (i < 7) return 0;
                    try { return DSUtils.GetMonIconPaletteId(i - 7); } catch { return 0; }
                },
                NameOf = i => i < 7 ? null : FromList(RomInfo.GetPokemonNames, i - 7),
                BuildUnits = n => GraphicUnits.PartyIcons(All.First(x => x.Dir == DirNames.monIcons), n) },

            // Five files per trainer class, per TrainerSpriteEditorViewModel.cs:646-678.
            new Archive { Dir = DirNames.trainerGraphics, Title = "Trainer sprites", In = Group.Trainers,
                What = "Every trainer class as it appears when a battle starts.",
                DeepEditor = "Trainer Sprite Editor",
                Stride = 5,
                PartNames = new[] { "Drawing", "Colours", "As it appears", "Animation", "Extra" },
                // Five files a class, and which is which is not a guess: TrainerClassSpriteRenderer reads
                // the drawing at 5n, the colours at 5n+1, the layout at 5n+2 and the animation at 5n+3.
                ColourEntry = i => (i / 5) * 5 + 1,
                DrawingEntry = i => (i / 5) * 5,
                NameOf = i => FromList(RomInfo.GetTrainerClassNames, i / 5) },
            new Archive { Dir = DirNames.trainerCardGraphics, Title = "Trainer card", In = Group.Trainers,
                What = "The trainer card's face and back, and the poses drawn on it.",
                BuildUnits = n => GraphicUnits.TrainerCard(
                    All.First(x => x.Dir == DirNames.trainerCardGraphics), n) },

            new Archive { Dir = DirNames.battleBg, Title = "Battle backgrounds", In = Group.BattleScenery,
                What = "The scenery behind a battle, and the sweeping backgrounds some moves put up.",
                ColourEntry = GraphicUnits.BackdropColours,
                DrawingEntry = GraphicUnits.PanelDrawing,
                ArrangementEntry = GraphicUnits.BackdropArrangement,
                NameOf = GraphicUnits.BackdropName,
                BuildUnits = n => GraphicUnits.BattleBackdrops(All.First(x => x.Dir == DirNames.battleBg), n) },
            new Archive { Dir = DirNames.battleObj, Title = "Battle furniture", In = Group.BattleScenery,
                What = "The ground the Pokemon stand on, the HP bars, the name boxes and the rest of the "
                     + "battle screen.",
                ColourEntry = BattleObjects.ColoursFor,
                DrawingEntry = BattleObjects.DrawingFor,
                ColourBank = BattleObjects.ColourBankFor,
                PixelWidthOf = BattleObjects.WidthFor,
                NameOf = BattleObjects.NameOf,
                BuildUnits = n => BattleObjects.Units(All.First(x => x.Dir == DirNames.battleObj), n) },
            new Archive { Dir = DirNames.battleBgPlanm, Title = "Battle background colour cycles", In = Group.BattleChrome,
                What = "Colour changes played over a battle background. HeartGold and SoulSilver only.",
                Colours = Pairing.NotKnown,
                // One per entry rather than the archive's title on all of them, which listed thirty one
                // rows reading the same thing and made the tab unusable.
                NameOf = i => "Colour cycle " + i,
                CannotImportBecause = "These are lists of colour changes played over a background, not pictures. "
                                    + "There is nothing here to put a PNG in place of." },

            new Archive { Dir = DirNames.wazaEffectChar, Title = "Move effect drawings", In = Group.MoveEffects,
                What = "The flat drawings a move animation puts on the screen.",
                Colours = Pairing.SameIndexInOtherArchive, ColourArchive = DirNames.wazaEffectPltt,
                BuildUnits = n => GraphicUnits.MoveEffects(All.First(x => x.Dir == DirNames.wazaEffectChar), n) },
            new Archive { Dir = DirNames.wazaEffectPltt, Title = "Move effect colours", In = Group.MoveEffects,
                What = "The colours the move effect drawings are painted with." },
            new Archive { Dir = DirNames.wazaEffectCell, Title = "Move effect layouts", In = Group.MoveEffects,
                What = "How the move effect drawings are cut up and placed on screen.",
                Colours = Pairing.SameIndexInOtherArchive, ColourArchive = DirNames.wazaEffectPltt,
                DrawingArchive = DirNames.wazaEffectChar,
                CannotImportBecause = "This is the arrangement of the pieces, not a picture of them. Edit the "
                                    + "drawing it arranges instead." },
            new Archive { Dir = DirNames.wazaEffectCellAnm, Title = "Move effect animations", In = Group.MoveEffects,
                What = "The order and timing the move effect layouts are shown in.",
                Colours = Pairing.NotKnown,
                CannotImportBecause = "This is timing, not a picture. There is nothing here a PNG could replace." },

            // The game holds a table saying which drawing and which colours each item uses, so neither has
            // to be guessed at. See DSUtils.GetItemPicRaw for the same table read the same way.
            new Archive { Dir = DirNames.itemIcons, Title = "Item icons", In = Group.Items,
                What = "The picture of every item as it appears in the bag.",
                Colours = Pairing.NearestInSameArchive, DeepEditor = "Item Editor",
                NameOf = i => ItemIcons.NameOfEntry(i),
                ColourEntry = i => ItemIcons.ColoursFor(i),
                BuildUnits = n => GraphicUnits.ItemIcons(All.First(x => x.Dir == DirNames.itemIcons), n) },
            new Archive { Dir = DirNames.fonts, Title = "Fonts", In = Group.TextAndFonts,
                What = "The letters the game writes text with.",
                NameOf = i => NamedArchives.NameOf(DirNames.fonts, i),
                ColourEntry = i => NamedArchives.ColoursFor(DirNames.fonts, i),
                BuildUnits = n => NamedArchives.Units(All.First(x => x.Dir == DirNames.fonts), n) },
            new Archive { Dir = DirNames.windowFrames, Title = "Text box frames", In = Group.Windows,
                What = "The borders drawn around text boxes and menus. The cursors have no colours of "
                     + "their own: the game draws each one in the colours of the box it appears in, so "
                     + "what is shown here is a stand-in.",
                ColourEntry = i => GraphicUnits.WindowFrameColours(i),
                BuildUnits = n => GraphicUnits.WindowFrames(All.First(x => x.Dir == DirNames.windowFrames), n) },
            // Left ungrouped on purpose.
            new Archive { Dir = DirNames.synthOverlay, Title = "Map screen overlay", In = Group.Windows,
                What = "Pieces drawn over the map screen. In Diamond, Pearl and Platinum this is the "
                     + "weather; in HeartGold and SoulSilver it is something else.",
                DeepEditor = "Header Editor" },
            // Grouped by what the files are rather than by anything the games say about them.
            new Archive { Dir = DirNames.dynamicHeaders, Title = "Location banner", In = Group.Places,
                What = "The banner shown when you walk into a new place.", DeepEditor = "Header Editor",
                BuildUnits = n => GraphicUnits.ByDrawing(All.First(x => x.Dir == DirNames.dynamicHeaders), n) },

            new Archive { Dir = DirNames.dungeonCutinGraphics, Title = "Place splash screens", In = Group.Places,
                BuildUnits = n => DungeonCutinTable.UnitsFor(
                    All.First(x => x.Dir == DirNames.dungeonCutinGraphics), n),
                What = "The picture shown when you enter a dungeon. HeartGold and SoulSilver only." },
        };

        // ── reading ────────────────────────────────────────────────────────────────────────────────

        /// <summary>What one file in an archive is, from its own first four bytes.</summary>
        public enum Kind { Unknown, Palette, TileGraphic, TileMap, CellLayout, CellAnimation, Empty, NotAGraphic }

        public static Kind Identify(byte[] b)
        {
            if (b == null || b.Length == 0) return Kind.Empty;
            byte[] d = b;
            if (b.Length > 4 && (b[0] == 0x10 || b[0] == 0x11))
            {
                try { var u = NitroBgCodec.Inflate(b); if (u != null && u.Length >= 4) d = u; } catch { }
            }
            if (d.Length < 4) return Kind.Empty;
            switch (System.Text.Encoding.ASCII.GetString(d, 0, 4))
            {
                case "RLCN": return Kind.Palette;
                case "RGCN": return Kind.TileGraphic;
                case "RCSN": return Kind.TileMap;
                case "RECN": return Kind.CellLayout;
                case "RNAN": return Kind.CellAnimation;
                default: return Kind.NotAGraphic;
            }
        }

        /// <summary>A picture of one entry, or the reason there is not one.</summary>
        public sealed class Preview
        {
            public byte[] Rgba;      // width*height*4, null when there is no picture
            public int Width, Height;
            public string Whynot;    // set when Rgba is null
            public Kind Kind;
        }

        /// <summary>Unsqueezes a file if it is stored squeezed down. The move effect archives all are, and
        /// handing a squeezed file to the reader gets nothing back.</summary>
        public static byte[] Unsqueeze(byte[] b)
        {
            if (b == null || b.Length < 5) return b;
            if (b[0] != 0x10 && b[0] != 0x11) return b;
            try { var u = NitroBgCodec.Inflate(b); return u != null && u.Length >= 4 ? u : b; }
            catch { return b; }
        }

        /// <summary>Squeezes a file back down the way the game stores it. </summary>
        public static byte[] Squeeze(byte[] plain, byte marker)
        {
            if (plain == null || plain.Length == 0 || marker != 0x10) return null;
            try
            {
                var squeezed = NSMBe4.ROM.LZ77_Compress(plain);
                if (squeezed == null || squeezed.Length < 5 || squeezed[0] != 0x10) return null;
                // Never hand back something that will not come out again as what went in.
                var check = NitroBgCodec.Inflate(squeezed);
                return check != null && check.Length == plain.Length && check.SequenceEqual(plain)
                    ? squeezed : null;
            }
            catch (Exception ex) { AppLogger.Error("GraphicAssets.Squeeze failed: " + ex.Message); return null; }
        }

        /// <summary>How a file is squeezed, or zero when it is not.</summary>
        public static byte SqueezeMarker(byte[] b)
            => b != null && b.Length > 4 && (b[0] == 0x10 || b[0] == 0x11) ? b[0] : (byte)0;

        private static string WriteTemp(byte[] data, List<string> temps)
        {
            if (data == null || data.Length == 0) return null;
            string p = Path.GetTempFileName();
            File.WriteAllBytes(p, Unsqueeze(data));
            temps.Add(p);
            return p;
        }

        /// <summary>Draws a palette as a grid of its colours, which is the only sensible picture of one.</summary>
        private static Preview PaletteSwatch(byte[] pal)
        {
            var colours = NitroBgCodec.ReadPalette(Unsqueeze(pal), out int count);
            if (colours == null || count == 0)
                return new Preview { Whynot = "The colours in this file could not be read.", Kind = Kind.Palette };
            const int cell = 12, across = 16;
            int rows = (count + across - 1) / across;
            int w = across * cell, h = Math.Max(1, rows) * cell;
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < count; i++)
            {
                var c = colours[i];
                int cx = (i % across) * cell, cy = (i / across) * cell;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        int o = ((cy + y) * w + cx + x) * 4;
                        rgba[o] = c.r; rgba[o + 1] = c.g; rgba[o + 2] = c.b; rgba[o + 3] = 255;
                    }
            }
            return new Preview { Rgba = rgba, Width = w, Height = h, Kind = Kind.Palette };
        }

        // Where the colours sit in each archive.
        private static readonly Dictionary<(string Rom, DirNames Dir), List<int>> _paletteIndexes = new();

        private static string OpenGame => (RomInfo.romID ?? "") + "|" + (RomInfo.workDir ?? "");

        private static List<int> PaletteIndexes(DirNames dir, ScriptNarc narc)
        {
            var key = (OpenGame, dir);
            lock (_paletteIndexes)
            {
                if (_paletteIndexes.TryGetValue(key, out var cached)) return cached;
                var found = new List<int>();
                int n = narc.Count;
                for (int i = 0; i < n; i++)
                {
                    var b = narc.Get(i);
                    if (b != null && Identify(b) == Kind.Palette) found.Add(i);
                }
                _paletteIndexes[key] = found;
                return found;
            }
        }

        /// <summary>Forgets where the colours were, for when a different game is opened.</summary>
        public static void Forget()
        {
            lock (_paletteIndexes) _paletteIndexes.Clear();
            ItemIcons.Forget();
        }

        /// <summary>The colours to paint entry <paramref name="index"/> with, following the archive's rule.</summary>
        private static byte[] FindColours(Archive a, ScriptNarc narc, int index, bool shiny = false)
        {
            // A file the game itself names beats any rule about where colours usually sit.
            int told = shiny && a.ShinyColourEntry != null
                ? a.ShinyColourEntry(index)
                : a.ColourEntry?.Invoke(index) ?? -1;
            if (told >= 0)
            {
                var b = narc.Get(told);
                if (b != null && Identify(b) == Kind.Palette) return b;
            }

            switch (a.Colours)
            {
                case Pairing.SameIndexInOtherArchive:
                    if (a.ColourArchive == null) return null;
                    var other = new ScriptNarc(a.ColourArchive.Value);
                    return other.Available ? other.Get(index) : null;

                case Pairing.OnePaletteForAll:
                {
                    // One set of colours at the very start serves the whole archive.
                    var first = narc.Get(0);
                    if (first != null && Identify(first) == Kind.Palette) return first;
                    return null;
                }

                case Pairing.NearestInSameArchive:
                {
                    // The closest set of colours to the drawing, looking through the whole archive rather
                    // than a window around it.
                    var palettes = PaletteIndexes(a.Dir, narc);
                    if (palettes.Count == 0) return null;
                    int best = -1, bestGap = int.MaxValue;
                    foreach (int i in palettes)
                    {
                        int gap = Math.Abs(i - index);
                        // prefer the one before, which is how these archives are usually laid out
                        if (i < index) gap = gap * 2 - 1;
                        if (gap < bestGap) { bestGap = gap; best = i; }
                    }
                    return best >= 0 ? narc.Get(best) : null;
                }
                default: return null;
            }
        }

        /// <summary>The drawing that a layout or a tile map arranges: the nearest one before it, else after.</summary>
        private static byte[] FindDrawing(ScriptNarc narc, int index)
        {
            for (int i = index - 1; i >= 0 && i > index - 64; i--)
            {
                var b = narc.Get(i);
                if (b != null && Identify(b) == Kind.TileGraphic) return b;
            }
            for (int i = index + 1; i < index + 64; i++)
            {
                var b = narc.Get(i);
                if (b == null) break;
                if (Identify(b) == Kind.TileGraphic) return b;
            }
            return null;
        }


        /// <summary>
        /// Puts a painted picture of an assembled sprite back into the tiles it is drawn from.
        /// </summary>
        public static string PutAssembledBack(Archive a, int layoutIndex, byte[] painted, int width, int height)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";

            byte[] raw = narc.Get(layoutIndex);
            if (raw == null || Identify(raw) != Kind.CellLayout)
                return "This entry is not a layout, so there is no assembled picture to put back.";

            int drawingAt = -1;
            try { drawingAt = a.DrawingEntry?.Invoke(layoutIndex) ?? -1; } catch { }
            byte[] drawing = drawingAt >= 0 ? narc.Get(drawingAt) : null;
            if (drawing == null)
                return "The drawing this layout arranges is not known, so it cannot be put back.";
            if (drawingAt < 0) return "The drawing this layout arranges is not known, so it cannot be put back.";

            byte[] pal = FindColours(a, narc, layoutIndex, false);
            if (pal == null) return "No colours could be found for this sprite.";

            // The drawing may be kept squeezed down. Work on the opened-out file and squeeze it again, the
            // same way painting a flat drawing does.
            byte drawMarker = SqueezeMarker(drawing);
            if (drawMarker == 0x11)
                return "This drawing is squeezed down in a way DSPRE cannot put back yet, so nothing was "
                     + "changed.";
            byte[] openDrawing = drawMarker != 0 ? Unsqueeze(drawing) : drawing;

            var temps = new List<string>();
            try
            {
                string chrPath = WriteTemp(raw, temps);
                string palPath = WriteTemp(Unsqueeze(pal), temps);
                string drawPath = WriteTemp(openDrawing, temps);

                var nclr = new NCLR(palPath, 0, Path.GetFileName(palPath));
                var ncgr = new NCGR(drawPath, 0, Path.GetFileName(drawPath));
                var ncer = new NCER(chrPath, 0, Path.GetFileName(chrPath));
                if (ncer.Banks == null || ncer.Banks.Length == 0) return "This layout has no pieces in it.";

                // Draw it once to find where the trim cut, so the pieces can be put back against the
                // canvas they were laid out on rather than against the trimmed picture.
                var whole = ncer.Get_RawImage(ncgr, nclr, 0, CellCanvas, CellCanvas, true, -1, null);
                if (whole == null || whole.IsEmpty) return "This sprite could not be put together.";
                var (_, shownW, shownH) = TrimBlank(ToRgba(whole), whole.Width, whole.Height,
                                                    out int cutLeft, out int cutTop);

                // The picture has to be the one this sprite is drawn at, or every piece would be read
                // from the wrong place and the edit would land somewhere else entirely.
                if (width != shownW || height != shownH)
                    return $"This sprite is {shownW} by {shownH} and that picture is {width} by {height}. "
                         + "Save this one first and paint over what comes out.";

                string why = CellDecompose.PutBack(ncer.Banks[0], ncer.BlockSize, ncgr, nclr,
                                                  painted, width, height, ncgr.Tiles, out byte[] tiles,
                                                  CellCanvas, cutLeft, cutTop);
                if (why != null) return why;

                // The tile bytes sit inside the drawing file, so put them back where they came from.
                var outp = (byte[])openDrawing.Clone();
                int at = TilesStartInNcgr(openDrawing);
                if (at < 0 || at + tiles.Length > outp.Length)
                    return "This drawing could not be taken apart, so nothing was changed.";
                Buffer.BlockCopy(tiles, 0, outp, at, tiles.Length);

                if (drawMarker != 0)
                {
                    var packed = Squeeze(outp, drawMarker);
                    if (packed == null)
                        return "This drawing could not be squeezed back down, so nothing was changed.";
                    outp = packed;
                }

                narc.Put(drawingAt, outp);
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("GraphicAssets.PutAssembledBack: " + ex.Message);
                return "This sprite could not be put back.";
            }
            finally { foreach (var t in temps) { try { File.Delete(t); } catch { } } }
        }

        /// <summary>Where the pixels start inside a drawing, from the one place that reads its header.</summary>
        private static int TilesStartInNcgr(byte[] ncgr)
        {
            if (ncgr == null || ncgr.Length < 0x30) return -1;
            int at = NitroBgCodec.ReadTileHeader(ncgr).TilesAt;
            return at > 0 && at < ncgr.Length ? at : -1;
        }


        /// <summary>
        /// Puts a painted picture of a whole background back into the tiles it is drawn from.
        /// </summary>
        /// <param name="drawingIndex">The background's own drawing, which is what the picture was drawn
        /// from. Several backgrounds share one arrangement, so the drawing is what names which one this
        /// is, not the other way round.</param>
        public static string PutBackgroundBack(Archive a, int drawingIndex, byte[] painted,
                                               int width, int height, out int changed, out int shared,
                                               out int fought)
        {
            changed = 0; shared = 0; fought = 0;

            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";

            int drawingAt = drawingIndex;
            byte[] drawing = narc.Get(drawingAt);
            if (drawing == null || Identify(drawing) != Kind.TileGraphic)
                return "This entry is not a drawing, so there is no background to put back.";

            int arrangementAt = -1;
            try { arrangementAt = a.ArrangementEntry?.Invoke(drawingIndex) ?? -1; } catch { }
            byte[] scr = arrangementAt >= 0 ? narc.Get(arrangementAt) : null;
            if (scr == null)
                return "The arrangement this drawing is laid out by is not known, so it cannot be put back.";

            byte[] pal = FindColours(a, narc, drawingIndex, false);
            if (pal == null) return "No colours could be found for this background.";

            byte marker = SqueezeMarker(drawing);
            if (marker == 0x11)
                return "This drawing is squeezed down in a way DSPRE cannot put back yet, so nothing was "
                     + "changed.";
            byte[] open = marker != 0 ? Unsqueeze(drawing) : drawing;

            var it = BackgroundDecompose.PutBack(open, Unsqueeze(pal), Unsqueeze(scr),
                                                 painted, width, height);
            if (it.Whynot != null) return it.Whynot;
            if (it.Tiles == null) return "This background could not be taken apart.";

            changed = it.SquaresChanged;
            shared = it.SquaresSharingATile;
            fought = it.PixelsPaintedTwoWays;

            byte[] outp = it.Tiles;
            if (marker != 0)
            {
                var packed = Squeeze(outp, marker);
                if (packed == null)
                    return "This drawing could not be squeezed back down, so nothing was changed.";
                outp = packed;
            }

            narc.Put(drawingAt, outp);
            return null;
        }

        /// <summary>A picture of one entry of one archive, or why there is not one.</summary>
        public static Preview Render(Archive a, int index, bool shiny = false)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available)
                return new Preview { Whynot = "This game does not have this archive." };

            byte[] raw = narc.Get(index);
            var kind = Identify(raw);

            if (kind == Kind.Empty) return new Preview { Kind = kind, Whynot = "This entry is empty." };
            if (kind == Kind.Palette) { var p = PaletteSwatch(raw); p.Kind = kind; return p; }

            if (kind == Kind.CellAnimation)
                return new Preview { Kind = kind, Whynot = "This is timing for an animation, not a picture." };
            if (kind == Kind.NotAGraphic)
                return new Preview { Kind = kind, Whynot = "This is not a picture. It is data of some other kind." };

            if (a.Colours == Pairing.NotKnown)
                return new Preview { Kind = kind, Whynot = "Which colours go with this drawing is not worked out "
                                                        + "yet, so showing it would show the wrong colours." };

            byte[] pal = FindColours(a, narc, index, shiny);
            if (pal == null)
                return new Preview { Kind = kind, Whynot = "No colours could be found for this drawing, so it "
                                                        + "cannot be shown in the right ones." };

            var temps = new List<string>();
            try
            {
                string palPath = WriteTemp(pal, temps);
                string chrPath = WriteTemp(raw, temps);
                if (palPath == null || chrPath == null)
                    return new Preview { Kind = kind, Whynot = "This entry could not be read." };

                var nclr = new NCLR(palPath, 0, Path.GetFileName(palPath));
                var ncgr = new NCGR(chrPath, 0, Path.GetFileName(chrPath));

                if (kind == Kind.TileMap || kind == Kind.CellLayout)
                {
                    // A layout and a tile map are both instructions for arranging a drawing that lives in a
                    // different entry. Find that drawing and put the two together.
                    byte[] drawing = null;
                    if (a.DrawingArchive != null)
                    {
                        var dn = new ScriptNarc(a.DrawingArchive.Value);
                        drawing = dn.Available ? dn.Get(index) : null;
                    }
                    else
                    {
                        // What the game itself pairs this layout with, where that is known.
                        int said = -1;
                        try { said = a.DrawingEntry?.Invoke(index) ?? -1; } catch { }
                        if (said >= 0) drawing = narc.Get(said);
                        drawing ??= FindDrawing(narc, index);
                    }
                    if (drawing == null)
                        return new Preview { Kind = kind, Whynot = kind == Kind.TileMap
                            ? "This arranges a background's tiles, and the drawing it arranges could not be found."
                            : "This arranges the pieces of a sprite, and the drawing it arranges could not be found." };

                    string drawPath = WriteTemp(drawing, temps);
                    var drawNcgr = new NCGR(drawPath, 0, Path.GetFileName(drawPath));

                    if (kind == Kind.TileMap)
                    {
                        var bg = NitroBgCodec.Composite(Unsqueeze(drawing), Unsqueeze(pal), Unsqueeze(raw));
                        if (bg?.Rgba == null)
                            return new Preview { Kind = kind, Whynot = "This background could not be put together." };
                        return new Preview { Rgba = bg.Rgba, Width = bg.Width, Height = bg.Height, Kind = kind };
                    }

                    var ncer = new NCER(chrPath, 0, Path.GetFileName(chrPath));
                    var cell = ncer.Get_RawImage(drawNcgr, nclr, 0, CellCanvas, CellCanvas, trans: true, currOAM: -1, draw_index: null);
                    if (cell == null || cell.IsEmpty)
                        return new Preview { Kind = kind, Whynot = "This sprite could not be put together." };
                    // These are laid out on a whole screen's worth of room and most of them use a corner of
                    // it, so show what was actually drawn rather than a mostly empty screen.
                    var (crop, cw, ch) = TrimBlank(ToRgba(cell), cell.Width, cell.Height);
                    return new Preview { Rgba = crop, Width = cw, Height = ch, Kind = kind };
                }

                // A drawing that needs an arrangement is only a heap of tiles without it, so put the two
                // together the way the game does rather than showing the heap.
                int arrangedBy = a.ArrangementEntry?.Invoke(index) ?? -1;
                if (arrangedBy >= 0 && kind == Kind.TileGraphic)
                {
                    var map = narc.Get(arrangedBy);
                    if (map != null)
                    {
                        var put = NitroBgCodec.Composite(Unsqueeze(raw), Unsqueeze(pal), Unsqueeze(map));
                        if (put?.Rgba != null)
                            return new Preview { Rgba = put.Rgba, Width = put.Width, Height = put.Height, Kind = kind };
                    }
                }

                // Read it the same way the painter does, so the size shown here and the size you paint on
                // are never different numbers.
                var art = ReadIndexed(a, index, out string cannot, shiny);
                if (art != null)
                    return new Preview { Rgba = Flatten(art), Width = art.Width, Height = art.Height, Kind = kind };

                var img = ncgr.Get_RawImage(nclr);
                if (img == null || img.IsEmpty)
                    return new Preview { Kind = kind, Whynot = cannot ?? "This drawing could not be turned into a picture." };
                return new Preview { Rgba = ToRgba(img), Width = img.Width, Height = img.Height, Kind = kind };
            }
            catch (Exception ex)
            {
                AppLogger.Error("GraphicAssets.Render failed: " + ex.Message);
                return new Preview { Kind = kind, Whynot = "This entry says it is a drawing but does not read "
                                                        + "like one, so there is nothing to show." };
            }
            finally { foreach (var t in temps) { try { File.Delete(t); } catch { } } }
        }

        /// <summary>Cuts the see-through border off a picture, leaving what was drawn.</summary>
        /// <summary>How much room the pieces of a sprite are laid out on before the blank border is
        /// trimmed off. The game draws them against the middle of this.</summary>
        private const int CellCanvas = 256;

        private static (byte[] rgba, int w, int h) TrimBlank(byte[] rgba, int w, int h)
            => TrimBlank(rgba, w, h, out _, out _);

        /// <summary>Trims the blank border off a picture, saying where the picture it kept began.</summary>
        private static (byte[] rgba, int w, int h) TrimBlank(byte[] rgba, int w, int h,
                                                             out int cutLeft, out int cutTop)
        {
            cutLeft = 0; cutTop = 0;
            int left = w, right = -1, top = h, bottom = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rgba[(y * w + x) * 4 + 3] != 0)
                    {
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
            if (right < left || bottom < top) return (rgba, w, h);   // nothing drawn, leave it alone

            int nw = right - left + 1, nh = bottom - top + 1;
            if (nw == w && nh == h) return (rgba, w, h);
            cutLeft = left; cutTop = top;
            var outp = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++)
                Array.Copy(rgba, ((top + y) * w + left) * 4, outp, y * nw * 4, nw * 4);
            return (outp, nw, nh);
        }

        private static byte[] ToRgba(RawImage img)
        {
            int w = img.Width, h = img.Height;
            var outp = new byte[w * h * 4];
            var src = img.Bgra;
            for (int i = 0; i < w * h && i * 4 + 3 < src.Length; i++)
            {
                outp[i * 4] = src[i * 4 + 2];
                outp[i * 4 + 1] = src[i * 4 + 1];
                outp[i * 4 + 2] = src[i * 4];
                outp[i * 4 + 3] = src[i * 4 + 3];
            }
            return outp;
        }

        /// <summary>The first entry in an archive that holds colours, or -1 if none does. Used where the
        /// layout of an archive shifts between games but the order of what is in it does not.</summary>
        public static int FirstPaletteIndex(Archive a)
        {
            try
            {
                var narc = new ScriptNarc(a.Dir);
                if (!narc.Available) return -1;
                var found = PaletteIndexes(a.Dir, narc);
                return found.Count > 0 ? found[0] : -1;
            }
            catch { return -1; }
        }

        /// <summary>How many entries an archive has in the game that is open, or 0 if it has none.</summary>
        public static int Count(Archive a)
        {
            var narc = new ScriptNarc(a.Dir);
            return narc.Available ? narc.Count : 0;
        }
    }
}
