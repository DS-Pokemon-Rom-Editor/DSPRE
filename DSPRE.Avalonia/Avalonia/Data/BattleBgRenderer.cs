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

        // BG_ID → (chr, pal, scr, scrReverse) file indices in the battle-background NARC, one table per game
        // family: Platinum uses pl_batt_bg.narc, HGSS uses batt_bg_gs.narc (retail a/0/0/7, 351 entries).
        // Index = the BG_ID the move-effect scripts pass to the background-change / background-scroll opcodes
        // (BG_ID 48 → Surf; BG_ID 44 → Dark Void). −1 = no reverse-side tilemap. The two families' file layouts
        // differ throughout, so using one family's table on the other decodes entirely wrong entries.
        private static readonly (int chr, int pal, int scr, int scrRev)[] PlatTable =
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

        private static readonly (int chr, int pal, int scr, int scrRev)[] HgssTable =
        {
            (59,295,56,57), (59,295,56,57), (119,319,120,-1), (59,295,56,57), (59,295,56,57), (59,330,56,57),
            (63,296,60,61), (142,334,143,144), (63,337,60,61), (64,297,65,-1), (64,297,65,-1), (119,320,120,-1),
            (64,329,65,-1), (64,336,65,-1), (119,318,120,-1), (70,300,66,-1), (70,308,66,-1), (119,317,120,-1),
            (70,308,66,-1), (75,301,76,-1), (83,303,80,81), (89,305,86,87), (93,306,90,91), (94,307,95,-1),
            (99,310,100,-1), (102,311,103,-1), (108,312,107,-1), (108,348,107,-1), (109,313,110,-1), (111,314,112,-1),
            (109,313,110,-1), (118,316,115,116), (118,316,115,116), (118,316,115,116), (125,324,126,-1), (130,326,131,-1),
            (132,327,133,-1), (139,332,137,138), (140,333,141,-1), (145,335,146,-1), (150,338,147,148), (154,339,151,152),
            (155,340,156,-1), (157,341,158,-1), (159,342,160,-1), (164,343,161,162), (165,344,166,-1), (46,290,47,-1),
            (167,345,168,169), (167,347,168,169), (170,346,172,171), (72,299,73,-1), (84,304,85,-1), (79,302,77,78),
            (113,315,114,-1), (123,323,124,-1), (121,322,122,-1), (135,331,136,-1), (98,309,96,97),
        };

        private static (int chr, int pal, int scr, int scrRev)[] Table =>
            RomInfo.gameFamily == GameFamilies.HGSS ? HgssTable : PlatTable;

        public static bool HasBg(int bgId) => bgId >= 0 && bgId < Table.Length;
        public static int BgCount => Table.Length;

        // The real battle-scene backdrops (scenery behind the platforms), distinct from the move-effect BGs
        // above. Character file = base graphic index (3) + bg_id, one shared tilemap (index 2) for every
        // bg_id, 23 backdrops (BG00..BG22). Day/eve/night palette base is per-family: Platinum pl_batt_bg
        // = 172..240, HGSS a/0/0/7 = 176..244. The wrong base still lands on some valid palette, so mixing
        // them up renders wrong colours instead of failing loudly.
        public const int BackdropCount = 23;
        private const int BackdropChr0 = 3, BackdropScr = 2;
        private static int BackdropPal0 => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS ? 176 : 172;

        /// <summary>Which files in the archive make up one backdrop. Every backdrop shares one tilemap and
        /// has three sets of colours, one for each time of day. Said here so the Graphics window can list a
        /// backdrop as one thing rather than as five unrelated files.</summary>
        public static (int Drawing, int Tilemap, int PaletteDay) BackdropFiles(int bgId)
            => (BackdropChr0 + bgId, BackdropScr, BackdropPal0 + bgId * 3);

        /// <summary>Builds the real battle-scene backdrop for bg_id 0..22 (timeZone 0=day,1=eve,2=night), or null.</summary>
        public BgImage BuildBackdrop(int bgId, int timeZone = 0)
        {
            if (bgId < 0 || bgId >= BackdropCount || !_narc.Available) return null;
            int tz = Math.Clamp(timeZone, 0, 2);
            byte[] chr = Inflate(_narc.Get(BackdropChr0 + bgId));
            byte[] pal = Inflate(_narc.Get(BackdropPal0 + bgId * 3 + tz));
            byte[] scr = Inflate(_narc.Get(BackdropScr));
            // Guard against a wrong palette index quietly reading non-palette bytes (e.g. another NSCR) as
            // colours, which renders as garbled noise instead of failing; fall back to placeholder art instead.
            if (chr == null || pal == null || scr == null || NitroBgCodec.Find(pal, "TTLP", 0) < 0) return null;
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

        // ── NITRO container parsing: delegates to the shared NitroBgCodec ────────────────────────────
        private static byte[] Inflate(byte[] b) => NitroBgCodec.Inflate(b);

        private static BgImage Composite(byte[] chr, byte[] pal, byte[] scr)
        {
            var c = NitroBgCodec.Composite(chr, pal, scr);
            var rgba = c.Rgba; int w = c.Width, h = c.Height;

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
