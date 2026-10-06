using System;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A move's full-screen effect background (Surf, Fly, Dig, ...). Its NCGR and NSCR are usually LZ10.</summary>
    public sealed class BattleBgRenderer
    {
        public sealed class BgImage { public byte[] Rgba; public int Width, Height; public int Period; }

        // Background id → its battle background archive members (drawing, palette, screen, reversed screen, contest
        // screen), read from the game or the hg-engine checkout. The id is what the move-effect scripts pass to the
        // background-change and background-scroll opcodes (48 → Surf, 44 → Dark Void).
        private static System.Collections.Generic.List<int[]> Table => DSPRE.ROMFiles.MoveBackgroundTable.Current.Rows;

        public static bool HasBg(int bgId) => bgId >= 0 && bgId < Table.Count;
        public static int BgCount => Table.Count;

        // Scenery behind the platforms, not the move-effect BGs above. A wrong palette base still lands on a
        // valid palette, so a mix-up shows wrong colours instead of failing.
        public static int BackdropCount => RomInfo.gameFamily == RomInfo.GameFamilies.DP ? 12 : 23;
        private const int BackdropChr0 = 3, BackdropScr = 2;
        private static int BackdropPal0 => RomInfo.gameFamily switch
        {
            RomInfo.GameFamilies.HGSS => 176,
            RomInfo.GameFamilies.DP => 158,
            _ => 172,
        };

        /// <summary>
        /// The trade sequence's four tile maps (normal, flipped, and both 512 wide) that lay the traded Pokémon's
        /// picture out as a background. The picture is made at runtime, so the archive holds no drawing for them.
        /// </summary>
        public static int TradePokemonMaps0 => RomInfo.gameFamily switch
        {
            RomInfo.GameFamilies.HGSS => 266,
            RomInfo.GameFamilies.DP => 198,
            _ => 262,
        };
        public const int TradePokemonMapCount = 4;

        /// <summary>Which files in the archive make up one backdrop. </summary>
        public static (int Drawing, int Tilemap, int PaletteDay) BackdropFiles(int bgId)
            => (BackdropChr0 + bgId, BackdropScr, BackdropPal0 + bgId * 3);

        /// <summary>Builds the backdrop for a backdrop id below <see cref="BackdropCount"/> (timeZone 0=day, 1=evening, 2=night), or null.</summary>
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

        /// <summary>Builds the BG image for a background id; reverse=true uses the enemy-side tilemap. Null if unavailable.</summary>
        public BgImage Build(int bgId, bool reverse = false) => Build(bgId, reverse ? 1 : 0);

        /// <summary>Builds the BG image with its screen (0), reversed screen (1) or contest screen (2).</summary>
        public BgImage Build(int bgId, int screen)
        {
            return HasBg(bgId) ? Build(Table[bgId], screen) : null;
        }

        /// <summary>Builds a background from a table row that may not be saved yet.</summary>
        public BgImage Build(int[] row, int screen)
        {
            if (row == null || row.Length < 5 || !_narc.Available) return null;
            byte[] chr = Inflate(_narc.Get(row[0]));
            byte[] pal = Inflate(_narc.Get(row[1]));
            byte[] scr = Inflate(_narc.Get(row[2 + Math.Clamp(screen, 0, 2)]));
            if (chr == null || pal == null || scr == null) return null;
            try { return Composite(chr, pal, scr); } catch { return null; }
        }

        // ── NITRO container parsing: delegates to the shared NitroBgCodec ────────────────────────────
        private static byte[] Inflate(byte[] b) => NitroBgCodec.Inflate(b);

        private static BgImage Composite(byte[] chr, byte[] pal, byte[] scr)
        {
            NitroBgCodec.BgImage c = NitroBgCodec.Composite(chr, pal, scr);
            byte[] rgba = c.Rgba; int w = c.Width, h = c.Height;

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
