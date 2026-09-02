using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
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

        /// <summary>What to call a thing, in the words somebody looking for it would use.</summary>
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
            {
                string tag = thing.Substring("P_ST_TYPE_".Length);
                if (ContestIcon.TryGetValue(tag, out string condition)) return condition + " contest icon";
                return (TypeIcon.TryGetValue(tag, out string type) ? type : Pretty(tag)) + " type icon";
            }
            if (thing.StartsWith("P_ST_BUNRUI_", StringComparison.Ordinal))
                return thing.EndsWith("BUTURI") ? "Physical move icon"
                     : thing.EndsWith("HENKA") ? "Status move icon"
                     : thing.EndsWith("TOKUSYU") ? "Special move icon"
                     : Pretty(thing.Substring("P_ST_BUNRUI_".Length)) + " move icon";
            if (thing.StartsWith("BATTLE_W_WAKU", StringComparison.Ordinal))
                return "Message frame " + thing.Substring("BATTLE_W_WAKU".Length);
            if (thing.StartsWith("SINGLE_ARROW_ANIMATION", StringComparison.Ordinal))
                return "Pointing arrow " + thing.Substring("SINGLE_ARROW_ANIMATION".Length);

            return Pretty(thing);
        }

        // Which drawing each row of MonsterBall_GRA_Table uses, in Diamond, Pearl and Platinum.
        private static readonly int[] SinnohDrawingForRow =
        {
            1, 2, 3, 0, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 12, 15,   // the sixteen that are items
            16, 18, 17, 17,                                          // Park, mud, bait, and putting one back
        };

        // The four at the end of the table are not items, so they have no name in the ROM to read.
        // ball_effect.h calls them BALL_EFF_PARK_BALL, BALL_EFF_STONE, BALL_EFF_FOOD and BALL_EFF_BACK.
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

            // Which file is whose comes from the games' own gauge tables, not from the names.
            ("SINGLE_GAGE2", "HP bar, your side"),
            ("SINGLE_GAGE1", "HP bar, their side"),
            ("DOUBLE_GAGE3", "HP bar, your side, two on two"),
            ("DOUBLE_GAGE4", "HP bar, your partner, two on two"),
            ("DOUBLE_GAGE1", "HP bar, their side, two on two"),
            ("DOUBLE_GAGE2", "HP bar, their partner, two on two"),

            // These four sit in the archive and nothing in the games ever asks for them, in either the
            // plain or the enum form, so the game never draws them.
            ("GAUGE_AA", "Spare HP bar, unused"),
            ("GAUGE_BB", "Spare HP bar, unused"),
            ("GAUGE_NAME_AA", "Spare name box, unused"),
            ("GAUGE_NAME_BB", "Spare name box, unused"),
            ("GAUGE_M_BALL", "Caught ball on the bar"),
            ("BATT_M_BALL", "Caught ball"),
            ("BATTLE_STOCK_M", "Your six balls"),
            ("BATTLE_STOCK_E", "Their six balls"),
            ("BATT_WAKU", "Spare message frame colours, unused"),
            ("BATTLE_WOBJ", "Message frame colours"),
            ("BATTLE_CURSOR_OAM_SUB", "Choice cursor"),
            ("LV_UP_PLATE", "Level up panel"),
            ("SAFARI_GAUGE", "Safari counter"),
            ("SAFARI_W", "Safari counter colours"),
            ("POKE_OAM", "Pokemon slot"),
            ("POKE_OAM128K", "Pokemon slot, large"),
            ("ST_TYPE", "Type and contest icon colours"),
            ("SPACE_COLOR", "Blank colours"),
            ("SPACE_32K_32X16", "Blank piece"),
        };

        // The icons are all one shape, and the game keeps one cell layout for the lot of them, so the
        // drawings themselves record no size. The games' own icon table says which of the
        // three banks of ST_TYPE_NCLR each one is painted with; without it they all came out in the
        // first bank's colours.
        private static readonly Dictionary<string, int> IconBank = new(StringComparer.Ordinal)
        {
            ["NORMAL"] = 0, ["FIGHT"] = 0, ["FLIGHT"] = 1, ["POISON"] = 1, ["GROUND"] = 0,
            ["ROCK"] = 0, ["INSECT"] = 2, ["GHOST"] = 1, ["STEEL"] = 0, ["QUES"] = 2,
            ["FIRE"] = 0, ["WATER"] = 1, ["GRASS"] = 2, ["ELE"] = 0, ["ESP"] = 1,
            ["ICE"] = 1, ["DRAGON"] = 2, ["EVIL"] = 0,
            ["STYLE"] = 0, ["BEAUTIFUL"] = 1, ["CUTE"] = 1, ["INTELLI"] = 2, ["STRONG"] = 0,
        };

        // WazaKindPlttOffset, from the same file.
        private static readonly Dictionary<string, int> KindBank = new(StringComparer.Ordinal)
        {
            ["BUTURI"] = 0, ["TOKUSYU"] = 1, ["HENKA"] = 0,
        };

        /// <summary>Whether this entry is one of the type, contest or move-category icons.</summary>
        private static bool IsIcon(string thing) =>
            thing != null && thing.StartsWith("P_ST_", StringComparison.Ordinal)
                          && thing.EndsWith("_NCGR_BIN", StringComparison.Ordinal);

        /// <summary>The icons are thirty two by sixteen. Nothing in the file says so, so it is said here.</summary>
        public static int WidthFor(int index)
        {
            var names = Names();
            if (index < 0 || index >= names.Count) return 0;
            return IsIcon(names[index]) ? 32 : 0;
        }

        /// <summary>Which bank of ST_TYPE_NCLR an icon is painted with.</summary>
        public static int ColourBankFor(int index)
        {
            var names = Names();
            if (index < 0 || index >= names.Count) return 0;
            string thing = names[index];
            if (!IsIcon(thing)) return 0;
            string tag = thing.Substring(0, thing.Length - "_NCGR_BIN".Length);
            if (tag.StartsWith("P_ST_TYPE_", StringComparison.Ordinal)
                && IconBank.TryGetValue(tag.Substring("P_ST_TYPE_".Length), out int bank)) return bank;
            if (tag.StartsWith("P_ST_BUNRUI_", StringComparison.Ordinal)
                && KindBank.TryGetValue(tag.Substring("P_ST_BUNRUI_".Length), out int kind)) return kind;
            return 0;
        }

        // The names the game writes on these icons, read off the drawings themselves. Several of the
        // file names are abbreviations that do not match what the icon says.
        private static readonly Dictionary<string, string> TypeIcon = new(StringComparer.Ordinal)
        {
            ["ELE"] = "Electric", ["ESP"] = "Psychic", ["EVIL"] = "Dark", ["FIGHT"] = "Fighting",
            ["FLIGHT"] = "Flying", ["INSECT"] = "Bug", ["QUES"] = "???",
        };

        // Five of the files in the same group are contest conditions rather than types.
        private static readonly Dictionary<string, string> ContestIcon = new(StringComparer.Ordinal)
        {
            ["STYLE"] = "Cool", ["BEAUTIFUL"] = "Beauty", ["CUTE"] = "Cute",
            ["INTELLI"] = "Smart", ["STRONG"] = "Tough",
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
        /// game lists them.
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

        private static int Rank(string part) => GraphicAssets.PartRank(part);

        private static GraphicAssets.Group GroupFor(Section s) => s switch
        {
            Section.Gauges => GraphicAssets.Group.BattleGauges,
            Section.Icons => GraphicAssets.Group.BattleIcons,
            Section.Platforms => GraphicAssets.Group.BattleScenery,
            _ => GraphicAssets.Group.BattleChrome,
        };

        /// <summary>
        /// The drawing a layout puts together, which is the one belonging to the same thing.
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

            // Every type, contest and move-category icon is painted from the one set, which is what
            // the games load for all of them.
            if (IsIcon(names[fileIndex]))
            {
                int icons = IndexOf(names, "ST_TYPE", "Colours");
                if (icons >= 0) return icons;
            }

            // The message frames carry no colours of their own. battle_input.c:2760 in HeartGold and
            // :2623 in Platinum load BATTLE_WOBJ_NCLR for the screen they are drawn on.
            if (thing.StartsWith("BATTLE_W_WAKU", StringComparison.Ordinal))
            {
                int frame = IndexOf(names, "BATTLE_WOBJ", "Colours");
                if (frame >= 0) return frame;
            }

            // Everything on the gauge shares one set, which is what the games load for all of them.
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

        /// <summary>Which file holds one part of one thing, by the name the game gives it. The number
        /// differs per game, so nothing should hold one of these as a constant.</summary>
        public static int Find(string thing, string part) => IndexOf(Names(), thing, part);

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
            if (part == "File") return friendly;
            // "Message frame colours, colours" reads worse than the name on its own.
            if (friendly.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return friendly;
            return $"{friendly}, {part.ToLowerInvariant()}";
        }
    }
}
