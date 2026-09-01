using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// How the files in each archive group into things.
    ///
    /// Almost nothing in these games is one picture per file. A battle backdrop is a drawing, a tilemap
    /// shared with every other backdrop, and three sets of colours for the three times of day. A move
    /// effect is a drawing, its colours, its layout and its timing, in four different archives at the same
    /// position. Listing those flat is what made this window a wall of numbers.
    ///
    /// Every grouping here comes from somewhere that already knew it: the renderers DSPRE built from the
    /// leaked source, or a table the games carry themselves. Nothing is guessed, and where a file is not
    /// accounted for it still gets a row of its own rather than disappearing.
    /// </summary>
    public static class GraphicUnits
    {
        private static GraphicAssets.UnitPart Part(GraphicAssets.Archive a, int index, string name)
            => new GraphicAssets.UnitPart { Archive = a, Index = index, Name = name };

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
        /// sets of colours. From BattleBgRenderer, which took the mapping from client_tool.c.</summary>
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
            return bg < 0 ? -1 : BattleBgRenderer.BackdropFiles(bg).PaletteDay;
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

        /// <summary>One row per terrain: the two sides' drawings and its three sets of colours. The cell
        /// layouts are shared by every terrain, so they are named but not claimed. From
        /// BattleGroundRenderer, which took the mapping from battle/ground.c.</summary>
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

        /// <summary>One row per drawing, with the colours the game's own table pairs it with. The archive
        /// alternates drawing and colours, but not evenly, and several items share one drawing, so the
        /// table is read rather than the alternation assumed.</summary>
        public static List<GraphicAssets.Unit> ItemIcons(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            for (int i = 0; i < fileCount; i++)
            {
                string name = null;
                try { name = a.NameOf?.Invoke(i); } catch { }
                int colours = -1;
                try { colours = a.ColourEntry?.Invoke(i) ?? -1; } catch { }

                // Only a drawing starts a row. A palette the table pairs with one is folded into it, and a
                // palette nothing points at still gets a row so it does not vanish.
                if (name == null && colours < 0) continue;
                if (colours < 0) continue;

                var u = new GraphicAssets.Unit { Archive = a, Name = name ?? a.Title };
                u.Parts.Add(Part(a, i, "Drawing"));
                spokenFor.Add(i);
                if (colours < fileCount && colours != i)
                {
                    u.Parts.Add(Part(a, colours, "Colours"));
                    // Several items share one set of colours, so it belongs to whichever rows name it and
                    // must not also turn up as a leftover row of its own.
                    spokenFor.Add(colours);
                }
                units.Add(u);
            }

            FillGaps(units, a, fileCount, spokenFor);
            return units;
        }


        // ── the borders drawn around text boxes and menus ──────────────────────────────────────────

        /// <summary>
        /// One row per window frame, named the way the games name them.
        ///
        /// The archive's own index file, include/system/winframe.naix in both leaks, names every entry:
        /// system, fmenu, talk_win00 to talk_win19 and the cursors as drawings, then system, talk_win00
        /// to talk_win19 and ugmenu_win as colours, then the four poke_win files. So drawing 2 goes with
        /// colours 26 in HeartGold, not with colours 2. Pairing them by position would have put the wrong
        /// colours on all twenty text box styles.
        ///
        /// The two games differ only in how many cursors they carry, HeartGold three and Platinum two,
        /// which shifts everything after them. The palettes are found rather than counted so both work.
        ///
        /// The pairing is the engine's own, not a guess. window.c's TalkWinGraphicSet takes the style
        /// number the player picked, CONFIG_GetWindowType, and hands it to both TalkWinCgxArcGet, which
        /// returns talk_win00_ncgr + id, and TalkWinPalArcGet, which returns talk_win00_nclr + id. So the
        /// drawings are a run indexed straight by the setting, and drawing number k always goes with
        /// colours number k. MenuWinPalArcGet returns system_nclr, which is why the field menu shares the
        /// system colours.
        /// </summary>
        public static List<GraphicAssets.Unit> WindowFrames(GraphicAssets.Archive a, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            // Where the drawings stop and the colours start. Everything else follows from it.
            int firstColour = GraphicAssets.FirstPaletteIndex(a);

            // Twenty text box styles is what both games carry. If this archive does not look like the one
            // the leaks describe, leave it flat rather than pairing things up wrongly.
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


        /// <summary>The colours a window frame drawing is meant to be drawn with, from the same naix order
        /// the rows are built from. Without this the frames draw in whatever palette happens to be nearest,
        /// which for these is all black.</summary>
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

            // The system window and the field menu both use the system colours: window.c's
            // MenuWinPalArcGet returns system_nclr.
            //
            // The cursors have no colours of their own and never load any. window.c fetches only their
            // character data, builds them into the tiles of whatever window they sit in, and loads that
            // into the background, so a cursor is drawn in the colours of the box it appears in. The
            // system colours are used here to have something to show it in, not because they are its own.
            return firstColour;
        }


        // ── archives that simply run drawing, then its pieces ──────────────────────────────────────

        /// <summary>
        /// One row per drawing, taking the colours and arrangements that follow it as belonging to it.
        ///
        /// For archives with no index list in the leaks to go by, but which plainly run in that order: a
        /// drawing, then its colours, then the ways it is arranged, then the next drawing. Platinum's
        /// location banner archive is three such runs and HeartGold's is one.
        ///
        /// This is read off the files themselves rather than from anything the games say, so it groups
        /// what is there without claiming to know what the pictures are for.
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

        /// <summary>The card is one drawing arranged two ways, front and back, with a set of colours for
        /// each rank you can reach. The portrait beside it is another drawing arranged two ways, one per
        /// gender. Both layouts are already declared in RomInfo.TrainerCardMembers.</summary>
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

        /// <summary>One row per Pokemon, with its alternate forms' icons under it. A form's icon is not
        /// next to its Pokemon's: it sits past the end of the real species, at the number the game's own
        /// alternate form table gives it.</summary>
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
