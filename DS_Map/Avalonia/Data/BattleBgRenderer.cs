using System;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Decodes a move-effect HAIKEI background (the scrolling full-screen layer used by Surf, Fly, Dig, Cosmic
    /// Power, …) from the battle-background NARC (<see cref="DirNames.battleBg"/> = pl_batt_bg.narc on Platinum).
    /// Each background is a tile set (NCGR), a palette (NCLR) and a tilemap (NSCR), all little NITRO containers;
    /// the NCGR/NSCR are usually LZ10-compressed (0x10 header). Produces a straight-RGBA image (typically 256×256)
    /// that the timeline scrolls + alpha-blends over the battle scene, faithfully to WeSysHaikeiDataIDGet/WE_T02.
    /// </summary>
    public sealed class BattleBgRenderer
    {
        public sealed class BgImage { public byte[] Rgba; public int Width, Height; public int Period; }

        // BG_ID → (chr, pal, scr, scrReverse) file indices in pl_batt_bg.narc, generated from the leaked Platinum
        // Haikei_BG_Table[][5] resolved through pl_batt_bg_def.h. Index = the BG_ID the WEST scripts pass to
        // HAIKEI_CHG / WE_T02 (BG_ID_057 = 48 → Surf). −1 = no reverse-side tilemap.
        private static readonly (int chr, int pal, int scr, int scrRev)[] Table =
        {
            (65,291,62,63), (65,291,62,63), (65,291,62,63), (65,291,62,63), (65,291,62,63), (65,321,62,63),
            (69,292,66,67), (69,325,66,67), (69,328,66,67), (70,293,71,71), (70,293,71,71), (70,319,71,71),
            (70,320,71,71), (70,327,71,71), (76,294,72,72), (76,296,72,72), (76,304,72,72), (76,312,72,72),
            (76,304,72,72), (81,297,82,82), (89,299,86,87), (95,301,92,93), (99,302,96,97), (100,303,101,101),
            (102,305,103,103), (105,306,106,106), (111,307,110,110), (111,339,110,110), (112,308,113,113), (112,309,113,113),
            (112,308,113,113), (119,311,116,117), (119,311,116,117), (119,311,116,117), (124,315,125,125), (129,317,130,130),
            (131,318,132,132), (138,323,136,137), (139,324,140,140), (141,326,142,142), (146,329,143,144), (150,330,147,148),
            (151,331,152,152), (153,332,154,154), (155,333,156,156), (160,334,157,158), (161,335,162,162), (52,286,53,53),
            (163,336,164,165), (163,338,164,165), (166,337,168,-1), (78,295,79,79), (90,300,91,91), (85,298,83,83),
            (114,310,115,115), (122,314,123,123), (120,313,121,121), (134,322,135,135),
        };

        public static bool HasBg(int bgId) => bgId >= 0 && bgId < Table.Length;
        public static int BgCount => Table.Length;

        // The REAL battle-scene backdrops (the scenery behind the platforms), distinct from the move-effect BGs above.
        // client_tool.c: chr = BATTLE_BG00_NCGR_BIN(3) + bg_id, pal = BATT_BG00_D_NCLR(172) + bg_id*3 + timeZone, and a
        // SINGLE shared tilemap BATTLE_BG00_NSCR_BIN(2) for every bg_id. 23 backdrops (BG00..BG22) on Platinum.
        public const int BackdropCount = 23;
        private const int BackdropChr0 = 3, BackdropScr = 2, BackdropPal0 = 172;

        /// <summary>Builds the real battle-scene backdrop for bg_id 0..22 (timeZone 0=day,1=eve,2=night), or null.</summary>
        public BgImage BuildBackdrop(int bgId, int timeZone = 0)
        {
            if (bgId < 0 || bgId >= BackdropCount || !_narc.Available) return null;
            int tz = Math.Clamp(timeZone, 0, 2);
            byte[] chr = Inflate(_narc.Get(BackdropChr0 + bgId));
            byte[] pal = Inflate(_narc.Get(BackdropPal0 + bgId * 3 + tz));
            byte[] scr = Inflate(_narc.Get(BackdropScr));
            if (chr == null || pal == null || scr == null) return null;
            try { return Composite(chr, pal, scr); } catch { return null; }
        }

        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.battleBg);

        /// <summary>Builds the BG image for a BG_ID; reverse=true uses the enemy-side tilemap. Null if unavailable.</summary>
        public BgImage Build(int bgId, bool reverse = false)
        {
            if (!HasBg(bgId) || !_narc.Available) return null;
            var (chrIdx, palIdx, scrIdx, scrRevIdx) = Table[bgId];
            int useScr = reverse && scrRevIdx >= 0 ? scrRevIdx : scrIdx;
            byte[] chr = Inflate(_narc.Get(chrIdx));
            byte[] pal = Inflate(_narc.Get(palIdx));
            byte[] scr = Inflate(_narc.Get(useScr));
            if (chr == null || pal == null || scr == null) return null;
            try { return Composite(chr, pal, scr); } catch { return null; }
        }

        // ── NITRO container parsing (self-contained; the on-disk layouts are fixed) ──────────────────────────────
        private static byte[] Inflate(byte[] b)
        {
            if (b == null) return null;
            if (b.Length > 0 && b[0] == 0x10) { try { return NSMBe4.ROM.LZ77_Decompress(b); } catch { return b; } }
            return b;
        }

        private static int Find(byte[] d, string magic, int from)
        {
            for (int i = from; i + 4 <= d.Length; i += 4)
                if (d[i] == magic[0] && d[i + 1] == magic[1] && d[i + 2] == magic[2] && d[i + 3] == magic[3]) return i;
            return -1;
        }
        private static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);
        private static int U32(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

        private static BgImage Composite(byte[] chr, byte[] pal, byte[] scr)
        {
            // NCLR PLTT: colours are RGB555 u16 starting 0x18 into the block.
            int pltt = Find(pal, "TTLP", 0);
            int palData = pltt >= 0 ? pltt + 0x18 : 0x28;
            int palCount = Math.Min(256, (pal.Length - palData) / 2);
            var colors = new (byte r, byte g, byte b)[Math.Max(256, palCount)];
            for (int i = 0; i < palCount; i++)
            {
                int c = U16(pal, palData + i * 2);
                colors[i] = ((byte)((c & 0x1F) << 3), (byte)(((c >> 5) & 0x1F) << 3), (byte)(((c >> 10) & 0x1F) << 3));
            }

            // NCGR RAHC: bitdepth at +0x0C (3 = 4bpp, 4 = 8bpp); tile bytes at +0x20.
            int rahc = Find(chr, "RAHC", 0);
            int depth = rahc >= 0 ? U32(chr, rahc + 0x0C) : 3;
            bool is8 = depth == 4;
            int tileBytes = rahc >= 0 ? rahc + 0x20 : 0x30;

            // NSCR NRCS: width/height in px at +0x08/+0x0A; map u16 entries at +0x14.
            int nrcs = Find(scr, "NRCS", 0);
            int w = nrcs >= 0 ? U16(scr, nrcs + 0x08) : 256;
            int h = nrcs >= 0 ? U16(scr, nrcs + 0x0A) : 256;
            if (w <= 0 || w > 1024) w = 256;
            if (h <= 0 || h > 1024) h = 256;
            int mapData = nrcs >= 0 ? nrcs + 0x14 : 0x24;

            var rgba = new byte[w * h * 4];
            int cols = w / 8, rows = h / 8, blocksX = (cols + 31) / 32;
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    // NDS BG screen data is stored in 256×256 (32×32-tile) blocks, not a linear grid — a 512-wide
                    // map is [block(0,0)][block(1,0)]… So index by block, then local (x,y) within the block.
                    int mapIdx = ((ty / 32) * blocksX + (tx / 32)) * 1024 + (ty % 32) * 32 + (tx % 32);
                    int mo = mapData + mapIdx * 2;
                    if (mo + 1 >= scr.Length) continue;
                    int e = U16(scr, mo);
                    int tile = e & 0x3FF, palNo = (e >> 12) & 0xF;
                    bool flipH = ((e >> 10) & 1) != 0, flipV = ((e >> 11) & 1) != 0;
                    int tbase = tileBytes + tile * (is8 ? 64 : 32);
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                        {
                            int sx = flipH ? 7 - px : px, sy = flipV ? 7 - py : py;
                            int idx;
                            if (is8) { int off = tbase + sy * 8 + sx; idx = off < chr.Length ? chr[off] : 0; }
                            else { int off = tbase + (sy * 8 + sx) / 2; int bb = off < chr.Length ? chr[off] : 0; idx = ((sx & 1) == 0) ? (bb & 0xF) : (bb >> 4); }
                            if (idx == 0) continue;                       // colour 0 = transparent
                            // A move-effect BG ships ONE 16-colour sub-palette but its tiles reference whatever slot
                            // it was loaded into (WEDEF_BG_DATA_COL_POS — e.g. Surf's tiles use palNo 9). We only have
                            // that one palette, so when palNo·16+idx overflows it, fall back to idx (= the effect
                            // colours). This keeps real multi-sub-palette BGs correct while fixing effect overlays.
                            int ci = is8 ? idx : palNo * 16 + idx;
                            if (ci >= palCount) ci = idx;
                            if (ci >= colors.Length) continue;
                            var (r, g, b) = colors[ci];
                            int dst = ((ty * 8 + py) * w + (tx * 8 + px)) * 4;
                            rgba[dst] = r; rgba[dst + 1] = g; rgba[dst + 2] = b; rgba[dst + 3] = 255;
                        }
                }
            // Detect the vertical repeat period: a seamless-scroll BG stores N identical bands stacked (Surf = 2),
            // so an effect SWEEP should move only ONE band, else it visibly runs N times. period = h/2 if the top
            // and bottom halves are pixel-identical, else h.
            int period = h;
            if ((h & 1) == 0)
            {
                int half = h / 2, bytes = half * w * 4; bool same = true;
                for (int i = 0; i < bytes; i++) if (rgba[i] != rgba[bytes + i]) { same = false; break; }
                if (same) period = half;
            }
            return new BgImage { Rgba = rgba, Width = w, Height = h, Period = period };
        }
    }
}
