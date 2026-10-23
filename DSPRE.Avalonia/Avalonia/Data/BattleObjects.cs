using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>What is in the battle furniture archive, and what each piece is for.</summary>
    public static class BattleObjects
    {
        /// <summary>Which part of the battle screen a thing belongs to.</summary>
        public enum Section
        {
            Gauges,      // the HP bars, the name boxes, the balls beside them
            Icons,       // type, contest and move-category icons, and the balls that get thrown
            Platforms,   // the ground each side stands on
            Screen,      // message frames, cursors, arrows, everything else on screen
        }

        public static string Title(Section s) => s switch
        {
            Section.Gauges => "Battle HP bars",
            Section.Icons => "Battle icons",
            Section.Platforms => "Battle platforms",
            _ => "Battle screen",
        };

        /// <summary>The names for the game that is open, or an empty list when it is not one of these.</summary>
        public static IReadOnlyList<string> Names()
        {
            string packed;
            try
            {
                packed = GameFamily switch
                {
                    GameFamilies.HGSS => BattleObjectNames.HeartGold,
                    GameFamilies.Plat => BattleObjectNames.Platinum,
                    GameFamilies.DP => BattleObjectNames.Diamond,
                    _ => null,
                };
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleObjects.Names", ex); packed = null; }

            if (packed == null) return Array.Empty<string>();
            return packed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(n => n == "-" ? "" : n)
                         .ToList();
        }

        /// <summary>Splits an entry into the thing it belongs to and which piece of it this is.</summary>
        public static (string Thing, string Part) Split(string name)
        {
            if (string.IsNullOrEmpty(name)) return (null, null);
            int at = name.LastIndexOf(':');
            if (at < 0) return (name, "File");
            string part = name[(at + 1)..] switch
            {
                "Cells" => "As it appears",
                "Screen" => "Arrangement",
                string p => p,
            };
            return (name[..at], part);
        }

        /// <summary>What to call a thing, in the words somebody looking for it would use.</summary>
        public static string Friendly(string thing)
        {
            if (string.IsNullOrEmpty(thing)) return null;

            foreach ((string name, string says) in Spoken)
                if (thing.Equals(name, StringComparison.Ordinal)) return says;

            string[] w = thing.Split('.');
            switch (w[0])
            {
                case "Platform" when w.Length == 3:
                    return w[2] switch
                    {
                        "Yours" => $"Your platform {w[1]}",
                        "Theirs" => $"Their platform {w[1]}",
                        _ => $"Platform {w[1]} {w[2].ToLowerInvariant()} colours",
                    };
                case "ThrownBall" when w.Length == 2:
                    return BallNamed(w[1], out _) ?? "Thrown ball " + w[1];
                case "TypeIcon" when w.Length == 2:
                    return (w[1] == "Mystery" ? "???" : w[1]) + " type icon";
                case "ContestIcon" when w.Length == 2:
                    return w[1] + " contest icon";
                case "MoveIcon" when w.Length == 2:
                    return w[1] + " move icon";
                case "MessageFrame" when w.Length == 2:
                    return "Message frame " + w[1];
                case "TrainerBack" when w.Length == 2:
                    return w[1] + " back sprite";
            }
            return Pretty(w[^1]);
        }

        // Which drawing each row of the thrown-ball table uses, in Diamond, Pearl and Platinum.
        private static readonly int[] SinnohDrawingForRow =
        {
            1, 2, 3, 0, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 12, 15,   // the sixteen that are items
            16, 18, 17, 17,                                          // Park, mud, bait, and putting one back
        };

        // The four at the end of the table are not items, so they have no name in the ROM to read.
        private static readonly string[] NotItems = { "Park Ball", "Mud", "Bait", "Putting one back" };

        /// <summary>Which item a row of the ball table belongs to, or 0 when it is not an item.</summary>
        private static int ItemForBallRow(int row, bool johto)
        {
            if (row < 0) return 0;
            if (row < 16) return row + 1;
            if (johto && row < 24) return 492 + (row - 16);
            return 0;
        }

        private static int FirstRowWithoutAnItem(bool johto) => johto ? 24 : 16;

        /// <summary>What a thrown-ball drawing is called, taken from the ROM's own item names.</summary>
        private static string BallNamed(string number, out bool everyOneIsABall)
        {
            everyOneIsABall = true;
            if (!int.TryParse(number, out int drawing)) return null;

            bool johto;
            try { johto = GameFamily == GameFamilies.HGSS; } catch (Exception ex) { AppLogger.WarnOnce("BattleObjects.BallNamed", ex); return null; }

            string[] items = null;
            try { items = RomInfo.GetItemNames(); } catch (Exception ex) { AppLogger.Warn("Battle objects: item names: " + ex.Message); }

            int rows = johto ? 28 : SinnohDrawingForRow.Length;
            List<string> said = new List<string>();
            for (int row = 0; row < rows; row++)
            {
                int usesDrawing = johto ? row + 1
                    : row < SinnohDrawingForRow.Length ? SinnohDrawingForRow[row] : -1;
                if (usesDrawing != drawing) continue;

                int item = ItemForBallRow(row, johto);
                string name = null;
                if (item > 0 && items != null && item < items.Length) name = items[item]?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    int spare = row - FirstRowWithoutAnItem(johto);
                    name = spare >= 0 && spare < NotItems.Length ? NotItems[spare] : null;
                    if (name != null) everyOneIsABall = false;
                }
                if (!string.IsNullOrWhiteSpace(name) && !said.Contains(name)) said.Add(name);
            }

            return said.Count == 0 ? null : string.Join(" - ", said);
        }

        private static readonly (string Name, string Says)[] Spoken =
        {
            ("HpBar.Debug", "HP bar colours"),
            ("HpBar.Shared", "Shared HP bar colours"),

            // Which file is whose comes from the games' own gauge tables, not from the names.
            ("HpBar.Yours", "Your HP bar"),
            ("HpBar.Theirs", "Their HP bar"),
            ("HpBar.YoursDouble", "Your HP bar (double battle)"),
            ("HpBar.YourPartner", "Your partner's HP bar (double battle)"),
            ("HpBar.TheirsDouble", "Their HP bar (double battle)"),
            ("HpBar.TheirPartner", "Their partner's HP bar (double battle)"),

            // Nothing in the games ever loads these four.
            ("HpBar.SpareA", "Unused spare HP bar"),
            ("HpBar.SpareB", "Unused spare HP bar"),
            ("NameBox.SpareA", "Unused spare name box"),
            ("NameBox.SpareB", "Unused spare name box"),
            ("HpBar.CaughtBall", "Caught ball on the bar"),
            ("CaughtBall.Unused", "Caught ball"),
            ("PartyBalls.Yours", "Your six balls"),
            ("PartyBalls.Theirs", "Their six balls"),
            ("MessageFrame.Debug", "Unused message frame colours"),
            ("MessageFrame.Shared", "Message frame colours"),
            ("Cursor.Choice", "Choice cursor"),
            ("LevelUp.Panel", "Level up panel"),
            ("Safari.Counter", "Safari counter"),
            ("Safari.Shared", "Safari counter colours"),
            ("PokemonSlot.Normal", "Pokémon slot"),
            ("PokemonSlot.Large", "Large Pokémon slot"),
            ("TypeIcon.Shared", "Type and contest icon colours"),
            ("Blank.Shared", "Blank colours"),
            ("Blank.Piece", "Blank piece"),
            ("Arrows.Thin", "Pointing arrow 1"),
            ("Arrows.Wide", "Pointing arrow 2"),
            ("BugContest.Net", "Bug Contest net"),
        };

        // The game's icon table picks each icon's colour bank. It's read from the ROM; these retail values are the fallback.
        private static readonly Dictionary<string, int> IconBank = new(StringComparer.Ordinal)
        {
            ["TypeIcon.Normal"] = 0, ["TypeIcon.Fighting"] = 0, ["TypeIcon.Flying"] = 1, ["TypeIcon.Poison"] = 1,
            ["TypeIcon.Ground"] = 0, ["TypeIcon.Rock"] = 0, ["TypeIcon.Bug"] = 2, ["TypeIcon.Ghost"] = 1,
            ["TypeIcon.Steel"] = 0, ["TypeIcon.Mystery"] = 2, ["TypeIcon.Fire"] = 0, ["TypeIcon.Water"] = 1,
            ["TypeIcon.Grass"] = 2, ["TypeIcon.Electric"] = 0, ["TypeIcon.Psychic"] = 1, ["TypeIcon.Ice"] = 1,
            ["TypeIcon.Dragon"] = 2, ["TypeIcon.Dark"] = 0,
            ["ContestIcon.Cool"] = 0, ["ContestIcon.Beauty"] = 1, ["ContestIcon.Cute"] = 1,
            ["ContestIcon.Smart"] = 2, ["ContestIcon.Tough"] = 0,
            ["MoveIcon.Physical"] = 0, ["MoveIcon.Special"] = 1, ["MoveIcon.Status"] = 0,
        };

        /// <summary>The icons in the order of the game's icon tables: 18 types, then the five contest conditions.</summary>
        public static readonly string[] IconOrder =
        {
            "TypeIcon.Normal", "TypeIcon.Fighting", "TypeIcon.Flying", "TypeIcon.Poison", "TypeIcon.Ground",
            "TypeIcon.Rock", "TypeIcon.Bug", "TypeIcon.Ghost", "TypeIcon.Steel", "TypeIcon.Mystery",
            "TypeIcon.Fire", "TypeIcon.Water", "TypeIcon.Grass", "TypeIcon.Electric", "TypeIcon.Psychic",
            "TypeIcon.Ice", "TypeIcon.Dragon", "TypeIcon.Dark",
            "ContestIcon.Cool", "ContestIcon.Beauty", "ContestIcon.Cute", "ContestIcon.Smart", "ContestIcon.Tough",
        };

        // Physical, special, status: the order of the game's category icon table.
        public static readonly string[] KindOrder = { "MoveIcon.Physical", "MoveIcon.Special", "MoveIcon.Status" };

        private static readonly object BanksLock = new();
        private static string _banksFor;
        private static BattleUiTables.IconTables _banks;

        /// <summary>
        /// The ROM's own icon bank tables, or null. They are trusted only when the member table beside them
        /// names the same archive files as the icon names do, which rules out a wrong address or a moved table.
        /// </summary>
        private static BattleUiTables.IconTables GameBanks()
        {
            lock (BanksLock)
            {
                string rom = RomInfo.WorkDir;
                if (_banksFor == rom) return _banks;
                _banksFor = rom;
                _banks = null;
                BattleUiTables.IconTables t = BattleUiTables.ReadIconTables();
                if (t == null) return null;
                IReadOnlyList<string> names = Names();
                for (int i = 0; i < IconOrder.Length; i++)
                    if (IndexOf(names, IconOrder[i], "Drawing") != t.TypeMembers[i]) return null;
                return _banks = t;
            }
        }

        /// <summary>Whether this entry is one of the type, contest or move-category icons.</summary>
        private static bool IsIcon(string name)
        {
            (string thing, string part) = Split(name);
            return part == "Drawing" && thing != null
                && (thing.StartsWith("TypeIcon.", StringComparison.Ordinal)
                    || thing.StartsWith("ContestIcon.", StringComparison.Ordinal)
                    || thing.StartsWith("MoveIcon.", StringComparison.Ordinal));
        }

        /// <summary>The icons are 32 by 16; the file doesn't record a size.</summary>
        public static int WidthFor(int index)
        {
            IReadOnlyList<string> names = Names();
            if (index < 0 || index >= names.Count) return 0;
            return IsIcon(names[index]) ? 32 : 0;
        }

        /// <summary>Which bank of the shared icon colours an icon is painted with.</summary>
        public static int ColourBankFor(int index)
        {
            IReadOnlyList<string> names = Names();
            if (index < 0 || index >= names.Count) return 0;
            if (!IsIcon(names[index])) return 0;
            string thing = Split(names[index]).Thing;
            BattleUiTables.IconTables game = GameBanks();
            int at = Array.IndexOf(IconOrder, thing);
            if (game != null && at >= 0) return game.TypeBanks[at];
            at = Array.IndexOf(KindOrder, thing);
            if (game != null && at >= 0) return game.CategoryBanks[at];
            return IconBank.TryGetValue(thing, out int bank) ? bank : 0;
        }

        /// <summary>Splits a PascalCase key segment into words.</summary>
        private static string Pretty(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(i > 0 && sb.Length > 0 && sb[^1] == ' ' ? char.ToLowerInvariant(name[i]) : name[i]);
            }
            return sb.ToString();
        }

        /// <summary>Which part of the battle screen a thing belongs to.</summary>
        public static Section SectionOf(string thing)
        {
            if (string.IsNullOrEmpty(thing)) return Section.Screen;
            return thing.Split('.')[0] switch
            {
                "Platform" => Section.Platforms,
                "HpBar" or "NameBox" or "PartyBalls" or "Safari" or "LevelUp" => Section.Gauges,
                "TypeIcon" or "ContestIcon" or "MoveIcon" or "ThrownBall" or "CaughtBall" => Section.Icons,
                _ => Section.Screen,
            };
        }

        /// <summary>
        /// One row per thing, with its drawing, layout, animation and colours together, in the order the
        /// game lists them.
        /// </summary>
        public static List<GraphicAssets.Unit> Units(GraphicAssets.Archive a, int fileCount)
        {
            List<GraphicAssets.Unit> units = new List<GraphicAssets.Unit>();
            IReadOnlyList<string> names = Names();
            HashSet<int> spokenFor = new HashSet<int>();

            // Keep the order the game lists them in, so the rows read the way the archive is built.
            List<string> order = new List<string>();
            Dictionary<string, List<(int Index, string Part)>> pieces = new Dictionary<string, List<(int Index, string Part)>>();

            for (int i = 0; i < fileCount && i < names.Count; i++)
            {
                (string thing, string part) = Split(names[i]);
                if (thing == null) continue;
                if (!pieces.TryGetValue(thing, out List<(int Index, string Part)> list))
                {
                    pieces[thing] = list = new List<(int, string)>();
                    order.Add(thing);
                }
                list.Add((i, part));
                spokenFor.Add(i);
            }

            foreach (string thing in order)
            {
                GraphicAssets.Unit u = new GraphicAssets.Unit
                {
                    Archive = a,
                    Name = Friendly(thing),
                    In = GroupFor(SectionOf(thing)),
                };
                // Drawing, then layout, then colours: the order people work in.
                foreach ((int index, string part) in pieces[thing].OrderBy(p => Rank(p.Part)).ThenBy(p => p.Index))
                    u.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = index, Name = part });
                units.Add(u);
            }

            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                GraphicAssets.Unit lone = new GraphicAssets.Unit { Archive = a, Name = a.Title, In = GroupFor(Section.Screen) };
                lone.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = i, Name = "File " + i });
                units.Add(lone);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }

        private static int Rank(string part) => GraphicAssets.PartRank(part);

        private static GraphicAssets.Group GroupFor(Section s) => s switch
        {
            Section.Gauges => GraphicAssets.Group.BattleGauges,
            Section.Icons => GraphicAssets.Group.BattleIcons,
            Section.Platforms => GraphicAssets.Group.BattleScenery,
            _ => GraphicAssets.Group.BattleChrome,
        };

        /// <summary>
        /// The drawing that belongs to the same thing as the layout.
        /// </summary>
        public static int DrawingFor(int fileIndex)
        {
            IReadOnlyList<string> names = Names();
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            (string thing, string part) = Split(names[fileIndex]);
            if (thing == null) return -1;
            return IndexOf(names, thing, "Drawing");
        }

        /// <summary>
        /// What draws with the HP bar colours besides the bars: the pieces the battle code copies in, and the
        /// name, level and HP numbers, written in colours 14 (letters), 2 (shadow) and 15 (behind them).
        /// </summary>
        public static IEnumerable<(int Slot, string Who)> AlsoUsesColours(int paletteEntry, int bank)
        {
            if (bank != 0 || paletteEntry < 0 || paletteEntry != Find("HpBar.Shared", "Colours")) yield break;
            foreach (int slot in new[] { BattleGaugeGlyphs.Letter, BattleGaugeGlyphs.Shadow, BattleGaugeGlyphs.Background })
                yield return (slot, "the name, level and HP numbers");
            byte[] sheet = BattleGaugePieces.ReadSheet();
            if (sheet == null) yield break;
            bool[] seen = new bool[16];
            foreach (byte b in sheet) { seen[b & 0xF] = true; seen[b >> 4] = true; }
            for (int s = 1; s < 16; s++) if (seen[s]) yield return (s, "the HP bar pieces");
        }

        /// <summary>The colours a battle drawing is meant to use, where the game says so plainly.</summary>
        public static int ColoursFor(int fileIndex)
        {
            IReadOnlyList<string> names = Names();
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            (string thing, string part) = Split(names[fileIndex]);
            if (thing == null || part == "Colours") return -1;

            // A thing's own colours, when it has some.
            int own = IndexOf(names, thing, "Colours");
            if (own >= 0) return own;

            // Every type, contest and move category icon uses this one set, as the games load it.
            if (IsIcon(names[fileIndex]))
            {
                int icons = IndexOf(names, "TypeIcon.Shared", "Colours");
                if (icons >= 0) return icons;
            }

            // The message frames carry no colours of their own; the battle loads the shared frame
            // colours for the screen they are drawn on.
            if (thing.StartsWith("MessageFrame.", StringComparison.Ordinal) && char.IsDigit(thing[^1]))
            {
                int frame = IndexOf(names, "MessageFrame.Shared", "Colours");
                if (frame >= 0) return frame;
            }

            // Everything on the gauge shares the one set the games load.
            Section section = SectionOf(thing);
            if (section == Section.Gauges)
            {
                int shared = IndexOf(names, "HpBar.Shared", "Colours");
                if (shared >= 0) return shared;
                shared = IndexOf(names, "HpBar.Debug", "Colours");
                if (shared >= 0) return shared;
            }
            return -1;
        }

        /// <summary>Which file holds one part of one thing, by the name the game gives it. The number
        /// differs per game, so nothing should hold one of these as a constant.</summary>
        public static int Find(string thing, string part) => IndexOf(Names(), thing, part);

        private static int IndexOf(IReadOnlyList<string> names, string thing, string part)
        {
            for (int i = 0; i < names.Count; i++)
            {
                (string t, string p) = Split(names[i]);
                if (t == thing && p == part) return i;
            }
            return -1;
        }

        /// <summary>What one file is, for the line above the picture.</summary>
        public static string NameOf(int fileIndex)
        {
            IReadOnlyList<string> names = Names();
            if (fileIndex < 0 || fileIndex >= names.Count) return null;
            (string thing, string part) = Split(names[fileIndex]);
            if (thing == null) return null;
            string friendly = Friendly(thing);
            if (part == "File") return friendly;
            // "Message frame colours (colours)" reads worse than the name on its own.
            if (friendly.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return friendly;
            return $"{friendly} ({part.ToLowerInvariant()})";
        }
    }
}
