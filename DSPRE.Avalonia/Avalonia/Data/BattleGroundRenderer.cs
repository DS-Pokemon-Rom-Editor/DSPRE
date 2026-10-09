using System;
using System.Collections.Generic;
using System.IO;
using Ekona.Images;
using Images;   // NCGR / NCLR / NCER readers
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Decodes the real in-game battle ground "tray" the Pokémon stand on (the terrain platforms) from
    /// <c>battle/graphic/pl_batt_obj.narc</c> (<see cref="DirNames.battleObj"/>, Platinum).
    /// The OAM cell layout (NCER) is SHARED across every terrain (platform 0's mine/enemy cell), only the tiles (NCGR)
    /// and palette (NCLR, day/eve/night) change per <c>terrain id</c>; the game's own tables
    /// remap the terrain id to a platform graphic set. Renders bank 0 of the shared cell with each terrain's
    /// tiles + palette to a straight-RGBA platform, positioned at the game's platform screen coordinates.
    /// </summary>
    public sealed class BattleGroundRenderer
    {
        public sealed class GroundImage { public byte[] Rgba; public int Width, Height, Left, Top; }

        // terrain id to label.
        private static readonly string[] DpTerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor", "Marsh" };
        private static readonly string[] PtTerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor", "Marsh",
              "Bridge", "Aaron", "Bertha", "Flint", "Lucian", "Cynthia", "Distortion World",
              "Battle Tower", "Battle Factory", "Battle Arcade", "Battle Castle", "Battle Hall", "Giratina" };
        private static readonly string[] HgssTerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor", "Marsh",
              "Unused", "Will", "Koga", "Bruno", "Karen", "Lance", "Distortion World",
              "Battle Tower", "Battle Factory", "Battle Arcade", "Battle Castle", "Battle Hall", "Giratina" };
        public static string[] TerrainNames => gameFamily switch
        {
            GameFamilies.DP => DpTerrainNames,
            GameFamilies.HGSS => HgssTerrainNames,
            _ => PtTerrainNames,
        };

        // terrain id to platform graphic set per side. Id 11 draws your side from platform 10 and theirs, with its colours, from platform 8.
        private static readonly int[] DpGroundGfx = { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6, 8 };
        private static readonly int[] PtHgssMineGfx =
            { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6, 8, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
        private static readonly int[] PtHgssEnemyGfx =
            { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6, 8, 8, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
        private static int[] MineGfx => gameFamily == GameFamilies.DP ? DpGroundGfx : PtHgssMineGfx;
        private static int[] EnemyGfx => gameFamily == GameFamilies.DP ? DpGroundGfx : PtHgssEnemyGfx;


        // Cell origin on screen; a 256 canvas drawn at pos - 128 puts its centre there.
        private const int MineX = 64, MineY = 128 + 8, EnemyX = 24 * 8, EnemyY = 8 * 11, Canvas = 256;

        public static int TerrainCount => TerrainNames.Length;

        /// <summary>
        /// Which files make up the ground one Pokemon stands on, found by the names the game gives
        /// them. The numbers differ per game, so a constant is right for one family and wrong for the
        /// others. platform 0 carries the layout every terrain is drawn with.
        /// </summary>
        public static (int MineDrawing, int EnemyDrawing, int MineLayout, int EnemyLayout, int PaletteDay)?
            TerrainFiles(int terrainId)
        {
            if (terrainId < 0 || terrainId >= MineGfx.Length) return null;
            int mine = MineGfx[terrainId], enemy = EnemyGfx[terrainId];
            int mineDraw = BattleObjects.Find($"Platform.{mine}.Yours", "Drawing");
            int enemyDraw = BattleObjects.Find($"Platform.{enemy}.Theirs", "Drawing");
            int mineLayout = BattleObjects.Find("Platform.0.Yours", "As it appears");
            int enemyLayout = BattleObjects.Find("Platform.0.Theirs", "As it appears");
            int palDay = BattleObjects.Find($"Platform.{enemy}.Day", "Colours");
            if (mineDraw < 0 || enemyDraw < 0 || mineLayout < 0 || enemyLayout < 0 || palDay < 0) return null;
            return (mineDraw, enemyDraw, mineLayout, enemyLayout, palDay);
        }

        // A default only: backdrop and ground are set independently per zone, and platform numbering parallels backdrop numbering.
        public static int BackdropForTerrain(int terrainId)
        {
            if (terrainId < 0 || terrainId >= MineGfx.Length) return -1;
            // Ice platforms are drawn as set 3, the forest's number, but ice floors are indoors: Snowpoint Gym uses Indoors 1.
            if (terrainId == TerrainIce) return Math.Min(BackdropIndoors1, BattleBgRenderer.BackdropCount - 1);
            return Math.Min(MineGfx[terrainId], BattleBgRenderer.BackdropCount - 1);
        }

        private const int TerrainIce = 8, BackdropIndoors1 = 6;

        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.battleObj);
        public bool Available => _narc.Available;

        /// <summary>Builds the (mine, enemy) ground platforms for a terrain (a terrain id), or (null,null) if the
        /// archive is unmapped/missing. <paramref name="timeZone"/> 0=day,1=evening,2=night selects the palette.</summary>
        public (GroundImage mine, GroundImage enemy) Build(int terrainId, int timeZone = 0)
        {
            if (!_narc.Available) return (null, null);
            (int MineDrawing, int EnemyDrawing, int MineLayout, int EnemyLayout, int PaletteDay)? files = TerrainFiles(terrainId);
            if (files == null) return (null, null);
            int tz = Math.Clamp(timeZone, 0, 2);
            (int MineDrawing, int EnemyDrawing, int MineLayout, int EnemyLayout, int PaletteDay) f = files.Value;
            GroundImage mine = Render(f.MineDrawing, f.PaletteDay + tz, f.MineLayout, MineX, MineY);
            GroundImage enemy = Render(f.EnemyDrawing, f.PaletteDay + tz, f.EnemyLayout, EnemyX, EnemyY);
            return (mine, enemy);
        }

        // Only the static single-battle gauge frame; the fill and text are overlaid in the UI.
        public GroundImage BuildGauge(bool player)
        {
            if (!_narc.Available) return null;
            // Found by name because file numbers differ per game.
            string thing = player ? "HpBar.Yours" : "HpBar.Theirs";
            int drawing = BattleObjects.Find(thing, "Drawing");
            int layout = BattleObjects.Find(thing, "As it appears");
            int colours = BattleObjects.Find("HpBar.Shared", "Colours");
            if (drawing < 0 || layout < 0 || colours < 0) return null;
            (int X, int Y) at = BattleGaugeComposer.CentreOf(player ? BattleGaugeComposer.Kind.PlayerSingle : BattleGaugeComposer.Kind.OpponentSingle);
            return Render(drawing, colours, layout, at.X, at.Y);
        }

        private GroundImage Render(int ncgrIdx, int nclrIdx, int ncerIdx, int posX, int posY)
        {
            List<string> temps = new List<string>();
            try
            {
                string chr = WriteTemp(_narc.Get(ncgrIdx), temps);
                string pal = WriteTemp(_narc.Get(nclrIdx), temps);
                string cel = WriteTemp(_narc.Get(ncerIdx), temps);
                if (chr == null || pal == null || cel == null) return null;
                NCLR nclr = new NCLR(pal, nclrIdx, Path.GetFileName(pal));
                NCGR ncgr = new NCGR(chr, ncgrIdx, Path.GetFileName(chr));
                NCER ncer = new NCER(cel, ncerIdx, Path.GetFileName(cel));
                RawImage raw = ncer.Get_RawImage(ncgr, nclr, 0, Canvas, Canvas, trans: true, currOAM: -1, draw_index: null);
                if (raw == null || raw.IsEmpty) return null;
                return new GroundImage { Rgba = ToRgba(raw, Canvas), Width = Canvas, Height = Canvas, Left = posX - Canvas / 2, Top = posY - Canvas / 2 };
            }
            catch (Exception ex) { AppLogger.Error("BattleGroundRenderer.Render failed: " + ex.Message); return null; }
            finally { foreach (string t in temps) { try { File.Delete(t); } catch { } } }
        }

        // The clact readers take a file path; materialise the NARC bytes (LZ10-decompressed if 0x10) to a temp file.
        private static string WriteTemp(byte[] bytes, List<string> temps)
        {
            if (bytes == null) return null;
            if (bytes.Length >= 4 && bytes[0] == 0x10) { try { bytes = NSMBe4.ROM.LZ77_Decompress(bytes); } catch { } }
            string tmp = Path.Combine(Path.GetTempPath(), "dspre_grd_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(tmp, bytes);
            temps.Add(tmp);
            return tmp;
        }

        // RawImage BGRA → straight RGBA byte[s*s*4].
        private static byte[] ToRgba(DSPRE.RawImage raw, int s)
        {
            byte[] outp = new byte[s * s * 4];
            if (raw == null || raw.IsEmpty) return outp;
            int bw = Math.Min(s, raw.Width), bh = Math.Min(s, raw.Height);
            for (int y = 0; y < bh; y++)
            {
                for (int x = 0; x < bw; x++)
                {
                    int si = (y * raw.Width + x) * 4, di = (y * s + x) * 4;   // BGRA → RGBA
                    outp[di + 0] = raw.Bgra[si + 2]; outp[di + 1] = raw.Bgra[si + 1];
                    outp[di + 2] = raw.Bgra[si + 0]; outp[di + 3] = raw.Bgra[si + 3];
                }
            }
            return outp;
        }
    }
}
