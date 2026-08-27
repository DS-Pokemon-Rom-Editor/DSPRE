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
    /// The OAM cell layout (NCER) is SHARED across every terrain (GROUND00's mine/enemy cell), only the tiles (NCGR)
    /// and palette (NCLR, day/eve/night) change per <c>GROUND_ID</c>; the game's GroundResourceID_Mine/Enemy/Palette
    /// tables remap the GROUND_ID to a GROUND## graphic set. Renders bank 0 of the shared cell with each terrain's
    /// tiles + palette to a straight-RGBA platform, positioned at the game's GROUND_MINE/ENEMY screen coordinates.
    /// </summary>
    public sealed class BattleGroundRenderer
    {
        public sealed class GroundImage { public byte[] Rgba; public int Width, Height, Left, Top; }

        // GROUND_ID (0..9) → human label.
        public static readonly string[] TerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor" };

        // GROUND_ID → GROUND## graphic set, in the order of the game's GroundResourceID_Mine[]/Enemy[]/Palette[] tables
        // (e.g. GRAVEL(0) uses GROUND02, LAWN(2) uses GROUND00, WATER(7) uses GROUND01).
        private static readonly int[] GroundGfx = { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6 };

        // file indices. GROUND00 has the full M/E set (NCGR,NCER,NANR each); every other GROUND##
        // is just an M_NCGR + E_NCGR pair, packed from index 133. The cell (NCER) is GROUND00's for every terrain.
        private static int MineNcgr(int gg) => gg == 0 ? 127 : 133 + (gg - 1) * 2;
        private static int EnemyNcgr(int gg) => gg == 0 ? 130 : 134 + (gg - 1) * 2;
        private const int MineNcer = 128, EnemyNcer = 131;
        private static int PalDay(int gg) => 1 + gg * 3;   // +0 day, +1 evening, +2 night (BATT_GROUND##_D/E/N_NCLR)

        // GROUND_MINE_X/Y, GROUND_ENEMY_X/Y: the CATS actor screen position (the cell origin). Get_Image
        // draws each OAM at canvasSize/2 + oam.xy, so a 256² render placed at (pos − 128) lands the origin on pos.
        private const int MineX = 64, MineY = 128 + 8, EnemyX = 24 * 8, EnemyY = 8 * 11, Canvas = 256;

        public static int TerrainCount => TerrainNames.Length;

        // Editor convenience: a matching scene backdrop (bg_id 0..22) for a terrain (GROUND_ID). bg_id and ground_id
        // are set independently per-zone in the game data, so there is no canonical 1:1; this reuses the GROUND##
        // graphic number (which parallels the BATTLE_BG## scene numbering) as a sensible default; the Backdrop
        // selector still overrides it. Returns -1 for none.
        public static int BackdropForTerrain(int terrainId)
            => terrainId >= 0 && terrainId < GroundGfx.Length ? Math.Min(GroundGfx[terrainId], 22) : -1;

        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.battleObj);
        public bool Available => _narc.Available;

        /// <summary>Builds the (mine, enemy) ground platforms for a terrain (GROUND_ID 0..9), or (null,null) if the
        /// archive is unmapped/missing. <paramref name="timeZone"/> 0=day,1=evening,2=night selects the palette.</summary>
        public (GroundImage mine, GroundImage enemy) Build(int terrainId, int timeZone = 0)
        {
            if (!_narc.Available || terrainId < 0 || terrainId >= GroundGfx.Length) return (null, null);
            int gg = GroundGfx[terrainId];
            int tz = Math.Clamp(timeZone, 0, 2);
            var mine = Render(MineNcgr(gg), PalDay(gg) + tz, MineNcer, MineX, MineY);
            var enemy = Render(EnemyNcgr(gg), PalDay(gg) + tz, EnemyNcer, EnemyX, EnemyY);
            return (mine, enemy);
        }

        // HP-gauge frames (GaugeObjParam_aa/bb, single battle): the PLAYER gauge = SINGLE_GAGE2 at (192,116),
        // the ENEMY gauge = SINGLE_GAGE1 at (58,36); both use GAGE_PALETTE_NCLR. All in pl_batt_obj. This renders only
        // the static frame cell (bank 0); the HP bar fill + name/level/HP text are drawn at runtime, overlaid in the UI.
        private const int GaugePal = 71;
        public GroundImage BuildGauge(bool player)
        {
            if (!_narc.Available) return null;
            return player ? Render(191, GaugePal, 190, 192, 116)    // SINGLE_GAGE2 NCGR(191)/NCER(190)
                          : Render(188, GaugePal, 187, 58, 36);     // SINGLE_GAGE1 NCGR(188)/NCER(187)
        }

        private GroundImage Render(int ncgrIdx, int nclrIdx, int ncerIdx, int posX, int posY)
        {
            var temps = new List<string>();
            try
            {
                string chr = WriteTemp(_narc.Get(ncgrIdx), temps);
                string pal = WriteTemp(_narc.Get(nclrIdx), temps);
                string cel = WriteTemp(_narc.Get(ncerIdx), temps);
                if (chr == null || pal == null || cel == null) return null;
                var nclr = new NCLR(pal, nclrIdx, Path.GetFileName(pal));
                var ncgr = new NCGR(chr, ncgrIdx, Path.GetFileName(chr));
                var ncer = new NCER(cel, ncerIdx, Path.GetFileName(cel));
                var raw = ncer.Get_RawImage(ncgr, nclr, 0, Canvas, Canvas, trans: true, currOAM: -1, draw_index: null);
                if (raw == null || raw.IsEmpty) return null;
                return new GroundImage { Rgba = ToRgba(raw, Canvas), Width = Canvas, Height = Canvas, Left = posX - Canvas / 2, Top = posY - Canvas / 2 };
            }
            catch (Exception ex) { AppLogger.Error("BattleGroundRenderer.Render failed: " + ex.Message); return null; }
            finally { foreach (var t in temps) { try { File.Delete(t); } catch { } } }
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
