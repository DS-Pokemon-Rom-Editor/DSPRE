using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Ekona.Images;
using Images;
using static DSPRE.RomInfo;

namespace DSPRE.Editors.Utils
{
    // Decodes the real battle ground platforms from pl_batt_obj.narc (DirNames.battleObj). The OAM cell
    // (NCER) is shared across every terrain; only the tiles/palette change per GROUND_ID.
    public sealed class BattleGroundRenderer
    {
        public sealed class GroundImage { public Bitmap Image; public int Left, Top; }

        // GROUND_ID (battle/, 0..9) → human label.
        public static readonly string[] TerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor" };

        // GROUND_ID → GROUND## graphic set (e.g. GRAVEL(0) uses GROUND02, LAWN(2) uses GROUND00).
        private static readonly int[] GroundGfx = { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6 };

        // GROUND00 has the full M/E set; every other GROUND## is an M_NCGR+E_NCGR pair from index 133.
        // The cell (NCER) is GROUND00's for every terrain.
        private static int MineNcgr(int gg) => gg == 0 ? 127 : 133 + (gg - 1) * 2;
        private static int EnemyNcgr(int gg) => gg == 0 ? 130 : 134 + (gg - 1) * 2;
        private const int MineNcer = 128, EnemyNcer = 131;
        private static int PalDay(int gg) => 1 + gg * 3;   // +0 day, +1 evening, +2 night

        // GROUND_MINE/ENEMY_X/Y screen position; a 256² render placed at (pos − 128) centers on it.
        private const int MineX = 64, MineY = 128 + 8, EnemyX = 24 * 8, EnemyY = 8 * 11, Canvas = 256;

        public const int LawnTerrainId = 2;

        // bg_id and ground_id aren't canonically paired; this reuses the GROUND## number as a default.
        public static int BackdropForTerrain(int terrainId)
            => terrainId >= 0 && terrainId < GroundGfx.Length ? Math.Min(GroundGfx[terrainId], 22) : -1;

        private readonly EntryNarc _narc = new EntryNarc(DirNames.battleObj);
        public bool Available => _narc.Available;

        public (GroundImage mine, GroundImage enemy) Build(int terrainId, int timeZone = 0)
        {
            if (!_narc.Available || terrainId < 0 || terrainId >= GroundGfx.Length) return (null, null);
            int gg = GroundGfx[terrainId];
            int tz = Math.Max(0, Math.Min(2, timeZone));
            var mine = Render(MineNcgr(gg), PalDay(gg) + tz, MineNcer, MineX, MineY);
            var enemy = Render(EnemyNcgr(gg), PalDay(gg) + tz, EnemyNcer, EnemyX, EnemyY);
            return (mine, enemy);
        }

        // Real HP-gauge frame graphic (single-battle, bank 0 only; fill + text drawn on top by the caller).
        private const int GaugePal = 71;
        public GroundImage BuildGauge(bool player)
        {
            if (!_narc.Available) return null;
            return player ? Render(191, GaugePal, 190, 192, 116)
                          : Render(188, GaugePal, 187, 58, 36);
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
                Image img = ncer.Get_Image(ncgr, nclr, 0, Canvas, Canvas, false, false, false, true, true, -1, null);
                if (img == null) return null;
                return new GroundImage { Image = (Bitmap)img, Left = posX - Canvas / 2, Top = posY - Canvas / 2 };
            }
            catch (Exception ex) { AppLogger.Error("BattleGroundRenderer.Render failed: " + ex.Message); return null; }
            finally { foreach (var t in temps) { try { File.Delete(t); } catch { } } }
        }

        // clact readers take a file path; materialise the NARC bytes (LZ10-decompressed) to a temp file.
        private static string WriteTemp(byte[] bytes, List<string> temps)
        {
            if (bytes == null) return null;
            if (bytes.Length >= 4 && bytes[0] == 0x10) { try { bytes = NSMBe4.ROM.LZ77_Decompress(bytes); } catch { } }
            string tmp = Path.Combine(Path.GetTempPath(), "dspre_grd_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(tmp, bytes);
            temps.Add(tmp);
            return tmp;
        }
    }
}
