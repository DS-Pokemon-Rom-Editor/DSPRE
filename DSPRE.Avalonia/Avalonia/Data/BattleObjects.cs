using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// What is in the battle furniture archive, and what each piece is for.
    ///
    /// This is the archive the whole battle screen is drawn from: the HP gauges, the box with the
    /// Pokemon's name in it, the six balls showing who is still standing, the type badges, the ground
    /// the Pokemon stand on, the message frame, the cursors. Every one of them was a numbered file with
    /// no name until now, which made the one thing people most want to change, the HP bar, impossible to
    /// find.
    ///
    /// The games name every file themselves. Diamond and Pearl in batt_obj_def.h, Platinum in
    /// pl_batt_obj_def.h and HeartGold and SoulSilver in batt_obj_gs_def.h, all under
    /// src/battle/graphic in the leaked source, each listing every entry in order with no gaps.
    /// Those lists are in BattleObjectNames.
    ///
    /// A thing is usually four files: its drawing, the layout saying how the pieces sit together, the
    /// timing for its animation and its colours. The game gives them one name each with the kind on the
    /// end, so GAUGE_AA_NCGR_BIN, GAUGE_AA_NCER_BIN and GAUGE_AA_NANR_BIN are one gauge, and they are
    /// gathered back into one row here.
    /// </summary>
    public static class BattleObjects
    {
        /// <summary>Which part of the battle screen a thing belongs to.</summary>
        public enum Section
        {
            Gauges,      // the HP bars, the name boxes, the balls beside them
            Icons,       // type badges, move category badges, the balls that get thrown
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
                packed = gameFamily switch
                {
                    GameFamilies.HGSS => BattleObjectNames.HeartGold,
                    GameFamilies.Plat => BattleObjectNames.Platinum,
                    GameFamilies.DP => BattleObjectNames.Diamond,
                    _ => null,
                };
            }
            catch { packed = null; }

            if (packed == null) return Array.Empty<string>();
            return packed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(n => n == "-" ? "" : n)
                         .ToList();
        }

        // The kind of file, taken off the end of the name. The games write it either bare or with _BIN
        // after it, depending on which list the file came out of.
        private static readonly (string Suffix, string Part)[] Kinds =
        {
            ("_NCGR_BIN", "Drawing"), ("_NCER_BIN", "As it appears"), ("_NANR_BIN", "Animation"),
            ("_NCLR_BIN", "Colours"), ("_NSCR_BIN", "Arrangement"),
            ("_NCGR", "Drawing"), ("_NCER", "As it appears"), ("_NANR", "Animation"),
            ("_NCLR", "Colours"), ("_NSCR", "Arrangement"),
        };

        /// <summary>Splits a name into the thing it belongs to and which piece of it this is.</summary>
        public static (string Thing, string Part) Split(string name)
        {
            if (string.IsNullOrEmpty(name)) return (null, null);
            foreach (var (suffix, part) in Kinds)
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                    return (name.Substring(0, name.Length - suffix.Length), part);
            return (name, "File");
        }

        /// <summary>
        /// What to call a thing, in the words somebody looking for it would use.
        ///
        /// The games' own names are shouted abbreviations, several of them Japanese: WAKU is a frame,
        /// GAGE is a gauge, BUNRUI is what kind of move it is, SHINKA is evolving. Left as they are,
        /// somebody looking for the HP bar would have to know to look for SINGLE_GAGE1.
        /// </summary>
        public static string Friendly(string thing)
        {
            if (string.IsNullOrEmpty(thing)) return null;

            // The ones worth saying properly, longest first so GAUGE_NAME_AA beats GAUGE.
            foreach (var (starts, says) in Spoken)
                if (thing.Equals(starts, StringComparison.Ordinal)) return says;

            if (thing.StartsWith("BATT_GROUND", StringComparison.Ordinal))
            {
                string rest = thing.Substring("BATT_GROUND".Length);
                string when = rest.EndsWith("_D") ? "day" : rest.EndsWith("_E") ? "evening"
                            : rest.EndsWith("_N") ? "night" : null;
                string number = new string(rest.TakeWhile(char.IsDigit).ToArray());
                return when == null ? $"Platform {number} colours" : $"Platform {number} colours, {when}";
            }
            if (thing.StartsWith("GROUND", StringComparison.Ordinal))
            {
                string rest = thing.Substring("GROUND".Length);
                string number = new string(rest.TakeWhile(char.IsDigit).ToArray());
                string side = rest.EndsWith("_M") ? "your side" : rest.EndsWith("_E") ? "their side" : null;
                return side == null ? $"Platform {number}" : $"Platform {number}, {side}";
            }
            if (thing.StartsWith("BATT_BALL_", StringComparison.Ordinal))
            {
                string number = thing.Substring("BATT_BALL_".Length);
                string ball = BallNamed(number, out _);
                return ball ?? "Thrown ball " + number;
            }
            if (thing.StartsWith("P_ST_TYPE_", StringComparison.Ordinal))
                return Pretty(thing.Substring("P_ST_TYPE_".Length)) + " type badge";
            if (thing.StartsWith("P_ST_BUNRUI_", StringComparison.Ordinal))
                return thing.EndsWith("BUTURI") ? "Physical move badge"
                     : thing.EndsWith("HENKA") ? "Status move badge"
                     : thing.EndsWith("TOKUSYU") ? "Special move badge"
                     : Pretty(thing.Substring("P_ST_BUNRUI_".Length)) + " move badge";
            if (thing.StartsWith("BATTLE_W_WAKU", StringComparison.Ordinal))
                return "Message frame " + thing.Substring("BATTLE_W_WAKU".Length);
            if (thing.StartsWith("SINGLE_ARROW_ANIMATION", StringComparison.Ordinal))
                return "Pointing arrow " + thing.Substring("SINGLE_ARROW_ANIMATION".Length);

            return Pretty(thing);
        }

        // Which drawing each row of MonsterBall_GRA_Table uses, in Diamond, Pearl and Platinum. The rows
        // are not in drawing order: the plain ball is the fourth row and uses drawing 00, and Quick and
        // Dusk are the other way round. HeartGold and SoulSilver gave every ball its own drawing, so
        // there row R uses drawing R + 1 and no table is needed.
        private static readonly int[] SinnohDrawingForRow =
        {
            1, 2, 3, 0, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 12, 15,   // the sixteen that are items
            16, 18, 17, 17,                                          // Park, mud, bait, and putting one back
        };

        // The four at the end of the table are not items, so they have no name in the ROM to read.
        // ball_effect.h calls them BALL_EFF_PARK_BALL, BALL_EFF_STONE, BALL_EFF_FOOD and BALL_EFF_BACK.
        private static readonly string[] NotItems = { "Park Ball", "Mud", "Bait", "Putting one back" };

        /// <summary>
        /// Which item a row of the ball table belongs to, or 0 when it is not an item.
        ///
        /// ball_effect.c's DP_BallEffectID_Get takes an item number and returns the row, so the first
        /// sixteen rows are simply items 1 to 16. HeartGold and SoulSilver added the Apricorn balls as
        /// items 492 to 499 and gave them the next eight rows. Everything after that is an effect with no
        /// item behind it.
        /// </summary>
        private static int ItemForBallRow(int row, bool johto)
        {
            if (row < 0) return 0;
            if (row < 16) return row + 1;
            if (johto && row < 24) return 492 + (row - 16);
            return 0;
        }

        private static int FirstRowWithoutAnItem(bool johto) => johto ? 24 : 16;

        /// <summary>
        /// What a thrown-ball drawing is called, taken from the ROM's own item names.
        ///
        /// Reading the names rather than keeping a list means renaming an item in the Item Editor renames
        /// it here too, and a ROM whose balls have been changed says what it actually holds. Where more
        /// than one ball shares a drawing, which happens for the Safari ones, both are named.
        /// </summary>
        private static string BallNamed(string number, out bool everyOneIsABall)
        {
            everyOneIsABall = true;
            if (!int.TryParse(number, out int drawing)) return null;

            bool johto;
            try { johto = gameFamily == GameFamilies.HGSS; } catch { return null; }

            string[] items = null;
            try { items = RomInfo.GetItemNames(); } catch { }

            int rows = johto ? 28 : SinnohDrawingForRow.Length;
            var said = new List<string>();
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
            ("GAUGE", "HP bar colours"),
            ("GAGE_PALETTE", "HP bar colours, shared"),

            // Which file is whose comes from gauge.c's own tables, not from the names. The header called
            // GaugeObjParam_aa draws at 192,116, the bottom right of the screen, and it loads
            // SINGLE_GAGE2; GaugeObjParam_bb draws at 58,36, the top left, and loads SINGLE_GAGE1. The
            // four double-battle headers load DOUBLE_GAGE3 and 4 low on the screen and 1 and 2 high.
            ("SINGLE_GAGE2", "HP bar, your side"),
            ("SINGLE_GAGE1", "HP bar, their side"),
            ("DOUBLE_GAGE3", "HP bar, your side, two on two"),
            ("DOUBLE_GAGE4", "HP bar, your partner, two on two"),
            ("DOUBLE_GAGE1", "HP bar, their side, two on two"),
            ("DOUBLE_GAGE2", "HP bar, their partner, two on two"),

            // These four sit in the archive and no code in the leaked source refers to them, in either
            // the plain or the enum form, so the game never draws them. Named for what they are rather
            // than left looking like the gauges the game actually uses.
            ("GAUGE_AA", "Spare HP bar, unused"),
            ("GAUGE_BB", "Spare HP bar, unused"),
            ("GAUGE_NAME_AA", "Spare name box, unused"),
            ("GAUGE_NAME_BB", "Spare name box, unused"),
            ("GAUGE_M_BALL", "Caught ball on the bar"),
            ("BATT_M_BALL", "Caught ball"),
            ("BATTLE_STOCK_M", "Your six balls"),
            ("BATTLE_STOCK_E", "Their six balls"),
            ("BATT_WAKU", "Message frame colours"),
            ("BATTLE_WOBJ", "Message frame pieces"),
            ("BATTLE_CURSOR_OAM_SUB", "Choice cursor"),
            ("LV_UP_PLATE", "Level up panel"),
            ("SAFARI_GAUGE", "Safari counter"),
            ("SAFARI_W", "Safari counter colours"),
            ("POKE_OAM", "Pokemon slot"),
            ("POKE_OAM128K", "Pokemon slot, large"),
            ("ST_TYPE", "Type badge colours"),
            ("SPACE_COLOR", "Blank colours"),
            ("SPACE_32K_32X16", "Blank piece"),
        };

        /// <summary>Turns a SHOUTED_NAME into something readable when there is nothing better to say.</summary>
        private static string Pretty(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var words = name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                            .Select(w => w.Length <= 1 ? w
                                        : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
            return string.Join(" ", words);
        }

        /// <summary>Which part of the battle screen a thing belongs to.</summary>
        public static Section SectionOf(string thing)
        {
            if (string.IsNullOrEmpty(thing)) return Section.Screen;

            if (thing.StartsWith("GROUND", StringComparison.Ordinal)
             || thing.StartsWith("BATT_GROUND", StringComparison.Ordinal)) return Section.Platforms;

            if (thing.StartsWith("GAUGE", StringComparison.Ordinal)
             || thing.StartsWith("GAGE", StringComparison.Ordinal)
             || thing.StartsWith("SINGLE_GAGE", StringComparison.Ordinal)
             || thing.StartsWith("DOUBLE_GAGE", StringComparison.Ordinal)
             || thing.StartsWith("BATTLE_STOCK", StringComparison.Ordinal)
             || thing.StartsWith("SAFARI", StringComparison.Ordinal)
             || thing == "LV_UP_PLATE") return Section.Gauges;

            if (thing.StartsWith("P_ST_", StringComparison.Ordinal)
             || thing == "ST_TYPE"
             || thing.StartsWith("BATT_BALL_", StringComparison.Ordinal)
             || thing.EndsWith("M_BALL", StringComparison.Ordinal)) return Section.Icons;

            return Section.Screen;
        }

        /// <summary>
        /// One row per thing, with its drawing, layout, animation and colours together, in the order the
        /// game lists them. A file the list does not name still gets a row of its own.
        /// </summary>
        public static List<GraphicAssets.Unit> Units(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var names = Names();
            var spokenFor = new HashSet<int>();

            // Keep the order the game lists them in, so the rows read the way the archive is built.
            var order = new List<string>();
            var pieces = new Dictionary<string, List<(int Index, string Part)>>();

            for (int i = 0; i < fileCount && i < names.Count; i++)
            {
                var (thing, part) = Split(names[i]);
                if (thing == null) continue;
                if (!pieces.TryGetValue(thing, out var list))
                {
                    pieces[thing] = list = new List<(int, string)>();
                    order.Add(thing);
                }
                list.Add((i, part));
                spokenFor.Add(i);
            }

            foreach (string thing in order)
            {
                var u = new GraphicAssets.Unit
                {
                    Archive = a,
                    Name = Friendly(thing),
                    In = GroupFor(SectionOf(thing)),
                };
                // Drawing first, then how it is put together, then its colours: the order somebody works
                // in rather than the order the archive happens to store them.
                foreach (var (index, part) in pieces[thing].OrderBy(p => Rank(p.Part)).ThenBy(p => p.Index))
                    u.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = index, Name = part });
                units.Add(u);
            }

            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                var lone = new GraphicAssets.Unit { Archive = a, Name = a.Title, In = GroupFor(Section.Screen) };
                lone.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = i, Name = "File " + i });
                units.Add(lone);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }

        // The assembled picture first, because that is the thing itself: a gauge is a box with a bar and
        // a name in it, and its drawing on its own is a heap of pieces that looks like nothing.
        private static int Rank(string part) => part switch
        {
            "As it appears" => 0, "Drawing" => 1, "Animation" => 2, "Arrangement" => 3, "Colours" => 4, _ => 5,
        };

        private static GraphicAssets.Group GroupFor(Section s) => s switch
        {
            Section.Gauges => GraphicAssets.Group.BattleGauges,
            Section.Icons => GraphicAssets.Group.BattleIcons,
            Section.Platforms => GraphicAssets.Group.BattleScenery,
            _ => GraphicAssets.Group.BattleChrome,
        };

        /// <summary>
        /// The drawing a layout puts together, which is the one belonging to the same thing.
        ///
        /// The game's list settles this: GAUGE_AA_NCER_BIN and GAUGE_AA_NCGR_BIN are one gauge. Looking
        /// for the nearest drawing instead finds whatever happens to sit before the layout, which for the
        /// gauges is the previous thing entirely.
        /// </summary>
        public static int DrawingFor(int fileIndex)
        {
            var names = Names();
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            var (thing, part) = Split(names[fileIndex]);
            if (thing == null) return -1;
            return IndexOf(names, thing, "Drawing");
        }

        /// <summary>The colours a battle drawing is meant to use, where the game says so plainly.</summary>
        public static int ColoursFor(int fileIndex)
        {
            var names = Names();
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            var (thing, part) = Split(names[fileIndex]);
            if (thing == null || part == "Colours") return -1;

            // A thing's own colours, when it has some.
            int own = IndexOf(names, thing, "Colours");
            if (own >= 0) return own;

            // Everything on the gauge shares one set, which is what gauge.c loads for all of them.
            var section = SectionOf(thing);
            if (section == Section.Gauges)
            {
                int shared = IndexOf(names, "GAGE_PALETTE", "Colours");
                if (shared >= 0) return shared;
                shared = IndexOf(names, "GAUGE", "Colours");
                if (shared >= 0) return shared;
            }
            return -1;
        }

        private static int IndexOf(IReadOnlyList<string> names, string thing, string part)
        {
            for (int i = 0; i < names.Count; i++)
            {
                var (t, p) = Split(names[i]);
                if (t == thing && p == part) return i;
            }
            return -1;
        }

        /// <summary>What one file is, for the line above the picture.</summary>
        public static string NameOf(int fileIndex)
        {
            var names = Names();
            if (fileIndex < 0 || fileIndex >= names.Count) return null;
            var (thing, part) = Split(names[fileIndex]);
            if (thing == null) return null;
            string friendly = Friendly(thing);
            return part == "File" ? friendly : $"{friendly}, {part.ToLowerInvariant()}";
        }
    }
}
