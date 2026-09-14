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
                // The bridge borrows both sides from other terrains, so it has no files of its own to show.
                if (spokenFor.Contains(mine) && spokenFor.Contains(enemy)) continue;

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


        // ── the Ball Capsule editor's seals and screens ────────────────────────────────────────────

        /// <summary>One row per seal, drawn through its layout, plus the capsule editor's screens.</summary>
        public static List<GraphicAssets.Unit> SealGraphics(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();
            void Row(string name, bool claim, params (int Index, string Part)[] parts)
            {
                var u = new GraphicAssets.Unit { Archive = a, Name = name };
                foreach (var (index, part) in parts)
                {
                    if (index < 0 || index >= fileCount) continue;
                    u.Parts.Add(Part(a, index, part));
                    if (claim) spokenFor.Add(index);
                }
                if (u.Parts.Count > 0) units.Add(u);
            }

            bool johto = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            int layout = johto ? 38 : 93, animation = johto ? 36 : 1, colours = johto ? 6 : 293;
            IReadOnlyList<DSPRE.ROMFiles.BallSeal> seals;
            try { seals = DSPRE.ROMFiles.BallSeals.Read(); } catch { seals = Array.Empty<DSPRE.ROMFiles.BallSeal>(); }
            foreach (var seal in seals)
            {
                if (seal == null || seal.Sprite >= fileCount) continue;
                // Every sticker shares one layout, so each row's view of it names that seal's own drawing and colours.
                int sprite = seal.Sprite;
                var own = new GraphicAssets.Archive
                {
                    Dir = a.Dir, Title = a.Title, In = a.In, What = a.What, DeepEditor = a.DeepEditor,
                    DrawingEntry = i => i == layout ? sprite : -1,
                    ColourEntry = i => i == layout || i == sprite ? colours : (a.ColourEntry?.Invoke(i) ?? -1),
                };
                var u = new GraphicAssets.Unit { Archive = a, Name = $"{seal.Name} sticker" };
                if (layout < fileCount)
                    u.Parts.Add(new GraphicAssets.UnitPart { Archive = own, Index = layout, Name = "As it appears", Kind = GraphicAssets.Kind.CellLayout });
                u.Parts.Add(Part(a, sprite, "Drawing"));
                if (animation < fileCount) u.Parts.Add(Part(a, animation, "Animation, shared"));
                if (colours < fileCount) u.Parts.Add(Part(a, colours, "Colours, shared"));
                units.Add(u);
                spokenFor.Add(sprite);
            }
            foreach (int shared in new[] { layout, animation, colours }) spokenFor.Add(shared);

            if (!johto)
            {
                Row("Capsule editor, lower screen", true, (267, "Drawing"), (283, "Capsule arrangement"),
                    (282, "Seal case arrangement"), (287, "Colours"));
                Row("Capsule editor, top screen", true, (268, "Drawing"), (284, "Arrangement"),
                    (269, "Second drawing"), (285, "Second arrangement"), (288, "Colours"));
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
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

        /// <summary>The drawing a card or pose arrangement is laid out from; the nearest drawing in the archive is a different picture.</summary>
        public static int TrainerCardDrawingFor(int index)
        {
            try
            {
                var card = RomInfo.TrainerCardMembers;
                if (index == card.facaNscr || index == card.backNscr) return card.ncgr;
                var pose = RomInfo.TrainerCardTrainerMembers;
                if (index == pose.maleNscr || index == pose.femaleNscr) return pose.ncgr;
            }
            catch { }
            return -1;
        }

        /// <summary>The card and the pose are both drawn in the Normal rank's colours.</summary>
        public static int TrainerCardColoursFor(int index)
        {
            try
            {
                var card = RomInfo.TrainerCardMembers;
                var pose = RomInfo.TrainerCardTrainerMembers;
                if (index == card.ncgr || index == card.facaNscr || index == card.backNscr
                    || index == pose.ncgr || index == pose.maleNscr || index == pose.femaleNscr)
                    return card.rankPalettes[0];
            }
            catch { }
            return -1;
        }

        // ── the banner shown on entering a place ───────────────────────────────────────────────────

        /// <summary>One row per banner style, a drawing and its colours, named as the Header Editor names it.</summary>
        public static List<GraphicAssets.Unit> AreaWindows(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();
            for (int style = 0; style * 2 + 1 < fileCount; style++)
            {
                var u = new GraphicAssets.Unit { Archive = a, Name = AreaWindowName(style) };
                u.Parts.Add(Part(a, style * 2, "Drawing"));
                u.Parts.Add(Part(a, style * 2 + 1, "Colours"));
                spokenFor.Add(style * 2);
                spokenFor.Add(style * 2 + 1);
                units.Add(u);
            }
            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }

        // Area icon 0 shows no banner, so style k is the header's area icon k + 1.
        private static string AreaWindowName(int style)
        {
            string label = null;
            try
            {
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                    DSPRE.Resources.PokeDatabase.Area.HGSSAreaIconsDict.TryGetValue((byte)(style + 1), out label);
                else if (style + 1 < DSPRE.Resources.PokeDatabase.Area.PtAreaIconValues.Length)
                    label = DSPRE.Resources.PokeDatabase.Area.PtAreaIconValues[style + 1];
            }
            catch { }
            if (label != null && label.StartsWith("[") && label.Contains(']')) label = label.Substring(label.IndexOf(']') + 1).Trim();
            return string.IsNullOrEmpty(label) ? $"Banner {style + 1}" : $"{label} banner";
        }

        // ── the small pictures in the party and the box ────────────────────────────────────────────

        /// <summary>One row per Pokemon, with its alternate forms' icons under it, and one for the eggs. </summary>
        public static List<GraphicAssets.Unit> PartyIcons(GraphicAssets.Archive a, int fileCount)
        {
            const int LeadIn = DSPRE.ROMFiles.PokemonIconFiles.SharedFiles;
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

            // Files run species by species, then eggs and forms, so every species has its row before its forms.
            var rowOf = new Dictionary<int, GraphicAssets.Unit>();
            GraphicAssets.Unit eggs = null;
            for (int file = LeadIn; file < fileCount; file++)
            {
                var icon = DSPRE.ROMFiles.PokemonIconFiles.Describe(file);
                if (icon == null) continue;

                if (icon.IsEgg)
                {
                    if (eggs == null) units.Add(eggs = new GraphicAssets.Unit { Archive = a, Name = "Eggs" });
                    eggs.Parts.Add(Part(a, file, DSPRE.ROMFiles.PokemonIconFiles.Label(icon, names)));
                    spokenFor.Add(file);
                    continue;
                }

                if (!rowOf.TryGetValue(icon.Species, out var u))
                {
                    string who = DSPRE.ROMFiles.PokemonIconFiles.Label(new DSPRE.ROMFiles.PokemonIconFiles.Icon { Species = icon.Species }, names);
                    units.Add(u = new GraphicAssets.Unit { Archive = a, Name = who });
                    rowOf[icon.Species] = u;
                }
                u.Parts.Add(Part(a, file, icon.Form ?? "Icon"));
                spokenFor.Add(file);
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }
    }
}
