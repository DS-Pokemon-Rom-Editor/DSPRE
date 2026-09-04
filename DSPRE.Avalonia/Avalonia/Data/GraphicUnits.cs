using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>How the files in each archive group into things.</summary>
    public static class GraphicUnits
    {
        private static GraphicAssets.UnitPart Part(GraphicAssets.Archive a, int index, string name)
            => new GraphicAssets.UnitPart
            {
                Archive = a, Index = index, Name = name,
                // The colours in an archive often come before the picture they paint, and a browser that
                // opens on the first part then shows a row of swatches instead of the thing itself.
                Kind = name != null && name.StartsWith("Colours", StringComparison.OrdinalIgnoreCase)
                    ? GraphicAssets.Kind.Palette : KindIn(a, index),
            };

        private static GraphicAssets.Archive Find(DirNames dir)
            => GraphicAssets.All.FirstOrDefault(x => x.Dir == dir);

        /// <summary>Rows for whatever the caller did not account for, so nothing goes missing.</summary>
        private static void FillGaps(List<GraphicAssets.Unit> units, GraphicAssets.Archive a,
                                     int fileCount, HashSet<int> spokenFor)
        {
            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                var u = new GraphicAssets.Unit { Archive = a, Name = a.Title };
                u.Parts.Add(Part(a, i, "File " + i));
                units.Add(u);
            }
            units.Sort((x, y) => x.First.CompareTo(y.First));
        }

        // ── the scenery behind a battle ────────────────────────────────────────────────────────────

        /// <summary>One row per backdrop: its drawing, the tilemap every backdrop shares, and its three
        /// sets of colours. From BattleBgRenderer, which has the games' own mapping.</summary>
        public static List<GraphicAssets.Unit> BattleBackdrops(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            for (int bg = 0; bg < BattleBgRenderer.BackdropCount; bg++)
            {
                var (drawing, tilemap, palDay) = BattleBgRenderer.BackdropFiles(bg);
                if (drawing >= fileCount || palDay + 2 >= fileCount) continue;

                var u = new GraphicAssets.Unit { Archive = a, Name = $"Backdrop {bg}" };
                u.Parts.Add(Part(a, drawing, "Drawing"));
                u.Parts.Add(Part(a, palDay, "Colours, day"));
                u.Parts.Add(Part(a, palDay + 1, "Colours, evening"));
                u.Parts.Add(Part(a, palDay + 2, "Colours, night"));
                if (tilemap < fileCount) u.Parts.Add(Part(a, tilemap, "Arrangement, shared"));
                units.Add(u);

                spokenFor.Add(tilemap);          // shared by every backdrop, so not a row of its own
                spokenFor.Add(drawing);
                spokenFor.Add(palDay); spokenFor.Add(palDay + 1); spokenFor.Add(palDay + 2);
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }

        /// <summary>Which backdrop a file is the drawing of, or -1.</summary>
        private static int BackdropOf(int fileIndex)
        {
            for (int bg = 0; bg < BattleBgRenderer.BackdropCount; bg++)
                if (BattleBgRenderer.BackdropFiles(bg).Drawing == fileIndex) return bg;
            return -1;
        }

        /// <summary>A backdrop drawing is painted with its own daytime colours, not the nearest palette.</summary>
        public static int BackdropColours(int fileIndex)
        {
            int bg = BackdropOf(fileIndex);
            if (bg >= 0) return BattleBgRenderer.BackdropFiles(bg).PaletteDay;
            return PanelColours(fileIndex);
        }

        /// <summary>
        /// The touch screen panel is not a backdrop, and its files had no colours of their own, so
        /// they were shown and put back in whatever palette came to hand. battle_input.c:2471 loads
        /// BATTLE_W_NCLR for the panel, and :1290 says every one of its layers is drawn from
        /// BATTLE_W_NCGR.
        /// </summary>
        private static int PanelColours(int fileIndex) =>
            IsPanelFile(fileIndex) ? BattleBgNames.Find("BATTLE_W_NCLR") : -1;

        /// <summary>The tiles a touch screen panel layer is arranged from.</summary>
        public static int PanelDrawing(int fileIndex)
        {
            var names = BattleBgNames.Names();
            if (fileIndex < 0 || fileIndex >= names.Length) return -1;
            string n = names[fileIndex];
            return n != null && n.StartsWith("BATTLE_WBG", StringComparison.Ordinal) && n.Contains("_NSCR")
                ? BattleBgNames.Find("BATTLE_W_NCGR_BIN") : -1;
        }

        private static bool IsPanelFile(int fileIndex)
        {
            var names = BattleBgNames.Names();
            if (fileIndex < 0 || fileIndex >= names.Length) return false;
            string n = names[fileIndex];
            return n != null
                && (n.StartsWith("BATTLE_WBG", StringComparison.Ordinal)
                    || string.Equals(n, "BATTLE_W_NCGR_BIN", StringComparison.Ordinal));
        }

        /// <summary>Every backdrop's tiles are arranged by the one file they all share.</summary>
        public static int BackdropArrangement(int fileIndex)
        {
            int bg = BackdropOf(fileIndex);
            return bg < 0 ? -1 : BattleBgRenderer.BackdropFiles(bg).Tilemap;
        }

        public static string BackdropName(int fileIndex)
        {
            int bg = BackdropOf(fileIndex);
            return bg < 0 ? null : $"Backdrop {bg}";
        }

        // ── the ground the Pokemon stand on ────────────────────────────────────────────────────────

        /// <summary>One row per terrain: the two sides' drawings and its three sets of colours. </summary>
        public static List<GraphicAssets.Unit> BattleGrounds(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            for (int t = 0; t < BattleGroundRenderer.TerrainCount; t++)
            {
                var files = BattleGroundRenderer.TerrainFiles(t);
                if (files == null) continue;
                var (mine, enemy, mineCell, enemyCell, palDay) = files.Value;
                if (mine >= fileCount || enemy >= fileCount || palDay + 2 >= fileCount) continue;

                var u = new GraphicAssets.Unit
                {
                    Archive = a,
                    Name = BattleGroundRenderer.TerrainNames[t] + " ground",
                };
                u.Parts.Add(Part(a, mine, "Your side"));
                u.Parts.Add(Part(a, enemy, "Their side"));
                u.Parts.Add(Part(a, palDay, "Colours, day"));
                u.Parts.Add(Part(a, palDay + 1, "Colours, evening"));
                u.Parts.Add(Part(a, palDay + 2, "Colours, night"));
                if (mineCell < fileCount) u.Parts.Add(Part(a, mineCell, "Layout, shared"));
                if (enemyCell < fileCount) u.Parts.Add(Part(a, enemyCell, "Their layout, shared"));
                units.Add(u);

                // The two cell layouts are shared by every terrain, so they are named on each row and must
                // not also turn up as leftover rows of their own.
                foreach (int k in new[] { mine, enemy, palDay, palDay + 1, palDay + 2, mineCell, enemyCell })
                    spokenFor.Add(k);
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }

        /// <summary>A terrain drawing is painted with that terrain's own daytime colours. The nearest
        /// palette in the archive belongs to some other terrain, which drew the grass in black and white.</summary>
        public static int TerrainColours(int fileIndex)
        {
            for (int t = 0; t < BattleGroundRenderer.TerrainCount; t++)
            {
                var f = BattleGroundRenderer.TerrainFiles(t);
                if (f == null) continue;
                if (fileIndex == f.Value.MineDrawing || fileIndex == f.Value.EnemyDrawing)
                    return f.Value.PaletteDay;
            }
            return -1;
        }

        public static string TerrainName(int fileIndex)
        {
            for (int t = 0; t < BattleGroundRenderer.TerrainCount; t++)
            {
                var f = BattleGroundRenderer.TerrainFiles(t);
                if (f == null) continue;
                if (fileIndex == f.Value.MineDrawing) return BattleGroundRenderer.TerrainNames[t] + " ground";
                if (fileIndex == f.Value.EnemyDrawing) return BattleGroundRenderer.TerrainNames[t] + " ground";
            }
            return null;
        }

        // ── the drawings a move animation puts on screen ───────────────────────────────────────────

        /// <summary>One row per move effect: its drawing, colours, layout and timing, which sit at the
        /// same position in four different archives. See effectclact, where the four NARCs are parallel.</summary>
        public static List<GraphicAssets.Unit> MoveEffects(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var pltt = Find(DirNames.wazaEffectPltt);
            var cell = Find(DirNames.wazaEffectCell);
            var anim = Find(DirNames.wazaEffectCellAnm);

            int Count(GraphicAssets.Archive other)
            {
                if (other == null) return 0;
                try { return GraphicAssets.Count(other); } catch { return 0; }
            }
            int nPltt = Count(pltt), nCell = Count(cell), nAnim = Count(anim);

            for (int i = 0; i < fileCount; i++)
            {
                var u = new GraphicAssets.Unit { Archive = a, Name = $"Move effect {i}" };
                u.Parts.Add(Part(a, i, "Drawing"));
                if (i < nPltt) u.Parts.Add(Part(pltt, i, "Colours"));
                if (i < nCell) u.Parts.Add(Part(cell, i, "Layout"));
                if (i < nAnim) u.Parts.Add(Part(anim, i, "Timing"));
                units.Add(u);
            }
            return units;
        }

        // ── the pictures of the items in the bag ───────────────────────────────────────────────────

        /// <summary>One row per drawing, with the colours the game's own table pairs it with. </summary>
        public static List<GraphicAssets.Unit> ItemIcons(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            if (fileCount > GraphicAssets.ItemIcons.LayoutFile)
            {
                units.Add(Single(a, GraphicAssets.ItemIcons.AnimationFile,
                                 "Item icon animation", "How the pieces move"));
                units.Add(Single(a, GraphicAssets.ItemIcons.LayoutFile,
                                 "Item icon layout", "How the pieces are placed"));
                spokenFor.Add(GraphicAssets.ItemIcons.AnimationFile);
                spokenFor.Add(GraphicAssets.ItemIcons.LayoutFile);
            }

            foreach (var icon in GraphicAssets.ItemIcons.Icons())
            {
                if (icon.Drawing >= fileCount || icon.Colours >= fileCount) continue;

                int shared = GraphicAssets.ItemIcons.Sharing(icon.Drawing);
                var u = new GraphicAssets.Unit { Archive = a, Name = icon.Name ?? a.Title };
                u.Parts.Add(Part(a, icon.Drawing, shared > 1
                    ? $"Drawing, shared with {shared - 1} other" + (shared > 2 ? " icons" : " icon")
                    : "Drawing"));
                u.Parts.Add(Part(a, icon.Colours, "Colours"));
                units.Add(u);
                spokenFor.Add(icon.Drawing);
                spokenFor.Add(icon.Colours);
            }

            // The last two files are the arrow drawn beside Back at the bottom of the bag list, which
            // the games reach for by name rather than through the item table. It is last in every
            // game, so it is the last two files that are named, not a fixed pair of numbers.
            if (fileCount >= 2 && !spokenFor.Contains(fileCount - 1) && !spokenFor.Contains(fileCount - 2))
            {
                var back = new GraphicAssets.Unit { Archive = a, Name = "Back arrow" };
                back.Parts.Add(Part(a, fileCount - 2, "Drawing"));
                back.Parts.Add(Part(a, fileCount - 1, "Colours"));
                units.Add(back);
                spokenFor.Add(fileCount - 2);
                spokenFor.Add(fileCount - 1);
            }

            // The archive holds art for item slots the game never asks for. Saying so beats a row named
            // after the archive with a file number after it.
            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                var u = new GraphicAssets.Unit { Archive = a, Name = "No item uses this" };
                u.Parts.Add(Part(a, i, KindIn(a, i) == GraphicAssets.Kind.Palette ? "Colours" : "Drawing"));
                units.Add(u);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }

        /// <summary>Whether a file in an archive is a drawing or a set of colours.</summary>
        private static GraphicAssets.Kind KindIn(GraphicAssets.Archive a, int index)
        {
            try { return GraphicAssets.Identify(new ScriptNarc(a.Dir).Get(index)); }
            catch { return GraphicAssets.Kind.Unknown; }
        }

        /// <summary>A row that is one file on its own.</summary>
        private static GraphicAssets.Unit Single(GraphicAssets.Archive a, int index, string name, string part)
        {
            var u = new GraphicAssets.Unit { Archive = a, Name = name };
            u.Parts.Add(Part(a, index, part));
            return u;
        }


        // ── the borders drawn around text boxes and menus ──────────────────────────────────────────

        /// <summary>One row per window frame, named the way the games name them.</summary>
        public static List<GraphicAssets.Unit> WindowFrames(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            // Where the drawings stop and the colours start. Everything else follows from it.
            int firstColour = GraphicAssets.FirstPaletteIndex(a);

            // Twenty text box styles is what both games carry. If this archive does not look like the one
            // the games use, leave it flat rather than pairing things up wrongly.
            const int Styles = 20;
            if (firstColour < 2 + Styles || firstColour + Styles >= fileCount)
            {
                FillGaps(units, a, fileCount, spokenFor);
                return units;
            }

            void Row(string name, params (int Index, string Part)[] parts)
            {
                var u = new GraphicAssets.Unit { Archive = a, Name = name };
                foreach (var (index, part) in parts)
                {
                    if (index < 0 || index >= fileCount) continue;
                    u.Parts.Add(Part(a, index, part));
                    spokenFor.Add(index);
                }
                if (u.Parts.Count > 0) units.Add(u);
            }

            Row("System window", (0, "Drawing"), (firstColour, "Colours"));
            // The field menu has its own drawing but borrows the system colours, so those colours belong
            // to two rows and must not also turn up as a row of their own.
            Row("Field menu window", (1, "Drawing"), (firstColour, "Colours"));

            for (int i = 0; i < Styles; i++)
                Row($"Text box style {i:00}", (2 + i, "Drawing"), (firstColour + 1 + i, "Colours"));

            for (int i = 2 + Styles; i < firstColour; i++)
                Row($"Window cursor {i - (2 + Styles) + 1}", (i, "Drawing"));

            int ugmenu = firstColour + 1 + Styles;
            Row("Underground menu window", (ugmenu, "Colours"));

            // poke_win is a cell graphic, so it brings its animation and layout with it.
            int poke = ugmenu + 1;
            if (poke + 3 < fileCount)
                Row("Pokemon window", (poke, "Animation"), (poke + 1, "Layout"),
                    (poke + 2, "Drawing"), (poke + 3, "Colours"));

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }


        /// <summary>
        /// The colours a window frame drawing is meant to be drawn with, from the same naix order the rows
        /// are built from.
        /// </summary>
        public static int WindowFrameColours(int fileIndex)
        {
            var a = Find(DirNames.windowFrames);
            if (a == null) return -1;
            int firstColour = GraphicAssets.FirstPaletteIndex(a);
            const int Styles = 20;
            if (firstColour < 2 + Styles) return -1;

            if (fileIndex < 0 || fileIndex >= firstColour)
            {
                // The one drawing after the colours is the Pokemon window, whose colours follow it.
                int poke = firstColour + 1 + Styles + 1;
                return fileIndex == poke + 2 ? poke + 3 : -1;
            }
            if (fileIndex >= 2 && fileIndex < 2 + Styles) return firstColour + 1 + (fileIndex - 2);

            // The system window and the field menu both use the system colours: window.c's MenuWinPalArcGet
            // returns system_nclr.
            return firstColour;
        }


        // ── archives that simply run drawing, then its pieces ──────────────────────────────────────

        /// <summary>
        /// One row per drawing, taking the colours and arrangements that follow it as belonging to it.
        /// </summary>
        public static List<GraphicAssets.Unit> ByDrawing(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();
            var narc = new ScriptNarc(a.Dir);

            GraphicAssets.Unit open = null;
            int number = 0;

            for (int i = 0; i < fileCount; i++)
            {
                var raw = narc.Get(i);
                var kind = GraphicAssets.Identify(GraphicAssets.Unsqueeze(raw));

                if (kind == GraphicAssets.Kind.TileGraphic)
                {
                    open = new GraphicAssets.Unit { Archive = a, Name = $"{a.Title} {++number}" };
                    open.Parts.Add(Part(a, i, "Drawing"));
                    spokenFor.Add(i);
                    units.Add(open);
                    continue;
                }

                if (open == null) continue;   // anything before the first drawing keeps its own row

                string part = kind switch
                {
                    GraphicAssets.Kind.Palette => "Colours",
                    GraphicAssets.Kind.TileMap => "Arrangement",
                    GraphicAssets.Kind.CellLayout => "As it appears",
                    GraphicAssets.Kind.CellAnimation => "Animation",
                    _ => null,
                };
                if (part == null) continue;

                // More than one arrangement for the same drawing is normal: the same picture laid out
                // several ways. Number them so they can be told apart.
                int already = open.Parts.Count(x => x.Name != null && x.Name.StartsWith(part));
                open.Parts.Add(Part(a, i, already == 0 ? part : $"{part} {already + 1}"));
                spokenFor.Add(i);
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }

        // ── the trainer card ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The card is one drawing arranged two ways, front and back, with a set of colours for each rank
        /// you can reach.
        /// </summary>
        public static List<GraphicAssets.Unit> TrainerCard(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            void Claim(GraphicAssets.Unit u, int index, string what)
            {
                if (index < 0 || index >= fileCount) return;
                if (u.Parts.Exists(x => x.Index == index)) return;
                u.Parts.Add(Part(a, index, what));
                spokenFor.Add(index);
            }

            try
            {
                var card = RomInfo.TrainerCardMembers;
                var u = new GraphicAssets.Unit { Archive = a, Name = "The card itself" };
                Claim(u, card.ncgr, "Drawing");
                Claim(u, card.facaNscr, "Front, arrangement");
                Claim(u, card.backNscr, "Back, arrangement");
                for (int r = 0; r < card.rankPalettes.Length; r++)
                {
                    string rank = r < RomInfo.TrainerCardRankNames.Length
                        ? RomInfo.TrainerCardRankNames[r] : "Rank " + r;
                    Claim(u, card.rankPalettes[r], "Colours, " + rank);
                }
                if (u.Parts.Count > 0) units.Add(u);

                var t = RomInfo.TrainerCardTrainerMembers;
                var p2 = new GraphicAssets.Unit { Archive = a, Name = "The trainer on the card" };
                Claim(p2, t.ncgr, "Drawing");
                Claim(p2, t.maleNscr, "Boy, arrangement");
                Claim(p2, t.femaleNscr, "Girl, arrangement");
                if (p2.Parts.Count > 0) units.Add(p2);
            }
            catch (Exception ex) { AppLogger.Error("GraphicUnits.TrainerCard: " + ex.Message); }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }

        /// <summary>The card's own drawing, for the Trainer Card Editor handing it over.</summary>
        public static int TrainerCardDrawing()
        {
            try { return RomInfo.TrainerCardMembers.ncgr; } catch { return -1; }
        }

        // ── the small pictures in the party and the box ────────────────────────────────────────────

        /// <summary>One row per Pokemon, with its alternate forms' icons under it. </summary>
        public static List<GraphicAssets.Unit> PartyIcons(GraphicAssets.Archive a, int fileCount)
        {
            const int LeadIn = 7;            // DSUtils.cs: the icon for a species is species + 7
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            var lead = new GraphicAssets.Unit
            {
                Archive = a,
                Name = "The colours and the layout every icon shares",
            };
            for (int k = 0; k < Math.Min(LeadIn, fileCount); k++)
            {
                lead.Parts.Add(Part(a, k, "File " + k));
                spokenFor.Add(k);
            }
            if (lead.Parts.Count > 0) units.Add(lead);

            string[] names;
            try { names = RomInfo.GetPokemonNames(); } catch { names = Array.Empty<string>(); }

            // Which forms belong to which Pokemon, from the same table the Pokemon Editor reads.
            var formsOf = new Dictionary<int, List<(string Name, int IconId)>>();
            try
            {
                foreach (var extra in DSPRE.Resources.PokeDatabase.PersonalData.personalExtraFiles)
                {
                    if (!formsOf.TryGetValue(extra.monId, out var list))
                        formsOf[extra.monId] = list = new List<(string, int)>();
                    list.Add((extra.description, extra.iconId));
                }
            }
            catch { }

            for (int species = 0; species < names.Length; species++)
            {
                int file = species + LeadIn;
                if (file >= fileCount) break;
                string who = names[species]?.Trim();
                if (string.IsNullOrEmpty(who) || who.Trim('-').Length == 0) continue;

                var u = new GraphicAssets.Unit { Archive = a, Name = who };
                u.Parts.Add(Part(a, file, "Icon"));
                spokenFor.Add(file);

                if (formsOf.TryGetValue(species, out var forms))
                    foreach (var f in forms)
                    {
                        int at = f.IconId + LeadIn;
                        if (at < 0 || at >= fileCount || spokenFor.Contains(at)) continue;
                        u.Parts.Add(Part(a, at, f.Name));
                        spokenFor.Add(at);
                    }

                units.Add(u);
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }
    }
}
