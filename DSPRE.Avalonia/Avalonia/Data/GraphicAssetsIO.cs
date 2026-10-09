using System;
using DSPRE.HgEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using Images;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Taking a graphic out of the game and putting one back.</summary>
    public static partial class GraphicAssets
    {
        /// <summary>The numbers and the colours of one entry, which is what a drawing really is.</summary>
        public sealed class Indexed
        {
            public byte[] Indices;    // one number per pixel
            public uint[] Palette;    // 0xAARRGGBB, looked up by that number
            public int Width, Height;
            public int ColourCount;   // how many colours this drawing is allowed
            public int BitsPerPixel;  // 4 means sixteen colours, 8 means two hundred and fifty six
        }

        /// <summary>Pulls one entry apart into its numbers and its colours, or says why it cannot be.</summary>
        /// <param name="source">Where to read the archive from, for a caller holding the ROM's own bytes
        /// rather than the unpacked copy.</param>
        /// <param name="paletteIndex">The colours to use, for a caller that knows which file the game pairs with
        /// this drawing; otherwise they are found from the archive's layout.</param>
        public static Indexed ReadIndexed(Archive a, int index, out string whynot, bool shiny = false,
                                          ScriptNarc source = null, int paletteIndex = -1)
        {
            whynot = null;
            ScriptNarc narc = source ?? new ScriptNarc(a.Dir);
            if (!narc.Available) { whynot = "This game does not have this archive."; return null; }

            if (a.Pokewalker is { } pw)
            {
                byte[] pwPixels = DSPRE.ROMFiles.PokewalkerImage.Decode(Unsqueeze(narc.Get(index)), pw.Width, pw.Height);
                if (pwPixels == null) { whynot = $"This entry isn't a {pw.Width} by {pw.Height} Pokéwalker picture."; return null; }
                return new Indexed
                {
                    Indices = pwPixels, Palette = (uint[])DSPRE.ROMFiles.PokewalkerImage.Shades.Clone(),
                    Width = pw.Width, Height = pw.Height, ColourCount = 4, BitsPerPixel = 2,
                };
            }

            byte[] rawStored = narc.Get(index);
            Kind kind = Identify(rawStored);
            if (kind != Kind.TileGraphic)
            {
                whynot = kind == Kind.Palette
                    ? "This entry is a set of colours, not a drawing. The colours can be changed but there "
                      + "are no pixels here to paint."
                    : "This entry is not a drawing, so it has no pixels to paint.";
                return null;
            }

            byte[] pal = paletteIndex >= 0 ? narc.Get(paletteIndex) : FindColours(a, narc, index, shiny);
            if (pal == null) { whynot = "No colours could be found for this drawing."; return null; }

            byte[] raw = Unsqueeze(rawStored);
            (byte r, byte g, byte b)[] colours = NitroBgCodec.ReadPalette(Unsqueeze(pal), out int count);
            if (colours == null || count == 0) { whynot = "The colours could not be read."; return null; }

            (int dataOff, int dataSize, int bpp, int width, int height, bool tiled)? shape = ReadShape(raw, WidthFor(a, index));
            if (shape == null)
            {
                whynot = "This drawing is stored in a way DSPRE cannot take apart yet.";
                return null;
            }

            (int dataOff, int dataSize, int bpp, int width, int height, bool tiled) = shape.Value;
            if (Scrambled(a, index)) SpriteScrambling.Unscramble(raw, dataOff, dataSize, FromEnd(a));

            int pixels = width * height;
            byte[] idx = new byte[pixels];

            if (bpp == 8)
            {
                for (int i = 0; i < pixels && dataOff + i < raw.Length; i++) idx[i] = raw[dataOff + i];
            }
            else
            {
                for (int i = 0; i < pixels; i++)
                {
                    int at = dataOff + i / 2;
                    if (at >= raw.Length) break;
                    idx[i] = (byte)((i % 2 == 0) ? (raw[at] & 0x0F) : (raw[at] >> 4));
                }
            }

            // Most drawings store their pixels in eight by eight blocks laid left to right. Some are stored
            // plainly, row after row, and the drawing says which it is. Straighten out only the blocked ones.
            // A drawing a cell places is laid out the way that cell puts its tiles.
            byte[] straight;
            List<CellPlacement.Piece> pieces = tiled ? PiecesFor(a, narc, index, bpp) : null;
            if (pieces != null && CellPlacement.Place(idx, pieces, out byte[] placed, out int pw2, out int ph2))
            {
                straight = placed;
                width = pw2;
                height = ph2;
            }
            else straight = tiled ? Untile(idx, width, height) : idx;

            // A sixteen colour drawing takes one bank out of a palette that usually holds several, and the
            // archive says which bank this entry wants.
            int bankSize = bpp == 8 ? 256 : 16;
            int bank = a.ColourBank?.Invoke(index) ?? 0;
            int from = bank * bankSize;
            if (from < 0 || from >= count) from = 0;
            int have = Math.Min(bankSize, count - from);

            uint[] palette = new uint[have];
            for (int i = 0; i < have; i++)
            {
                (byte r, byte g, byte b) c = colours[from + i];
                palette[i] = (uint)((i == 0 ? 0x00000000 : 0xFF000000) | (c.r << 16) | (c.g << 8) | c.b);
            }

            return new Indexed
            {
                Indices = straight, Palette = palette, Width = width, Height = height,
                ColourCount = have, BitsPerPixel = bpp,
            };
        }

        /// <summary>The pieces of the cell that places this drawing, or null when nothing does.</summary>
        private static List<CellPlacement.Piece> PiecesFor(Archive a, ScriptNarc narc, int index, int bpp)
        {
            int layout = -1;
            try { layout = a.CellPlacementEntry?.Invoke(index) ?? -1; } catch { }
            byte[] ncer = layout >= 0 && layout != index ? narc.Get(layout) : null;
            return ncer == null ? null : CellPlacement.Pieces(Unsqueeze(ncer), bpp);
        }

        /// <summary>Whether this archive's pixels are scrambled in the game being edited.</summary>
        private static bool Scrambled(Archive a, int index) =>
            a.ScrambledEntry?.Invoke(index) ?? a.ScrambledNow?.Invoke() ?? a.ScrambledPixels;

        private static bool FromEnd(Archive a) =>
            a.ScrambleFromEnd?.Invoke() ?? RomInfo.gameFamily == RomInfo.GameFamilies.DP;

        /// <summary>
        /// Where the pixels are in a drawing, what shape they make, and whether they are stored in eight by
        /// eight blocks.
        /// </summary>
        /// <summary>The width this entry is known to be drawn at, if any.</summary>
        private static int WidthFor(Archive a, int index)
        {
            int said = 0;
            try { said = a.PixelWidthOf?.Invoke(index) ?? 0; } catch { }
            if (said > 0) return said;
            return a.PixelWidth > 0 ? a.PixelWidth : SourcePngWidth(a, index);
        }

        // hg-engine tiles a drawing from its PNG row by row, so the PNG's width is the drawing's width.
        private static int SourcePngWidth(Archive a, int index)
        {
            if (!HgEngineProject.IsActive) return 0;
            HgEngineBuiltPngs.Source source = HgEngineBuiltPngs.For(HgEngineOwnedFiles.ArchiveOf(a.Dir), index, new ScriptNarc(a.Dir).Count);
            if (source?.Part != HgEngineBuiltPngs.Part.Pixels || !File.Exists(source.Path)) return 0;
            try
            {
                using FileStream f = File.OpenRead(source.Path);
                byte[] head = new byte[24];
                if (f.Read(head, 0, 24) < 24 || head[12] != 'I' || head[13] != 'H' || head[14] != 'D' || head[15] != 'R') return 0;
                return (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            }
            catch (IOException) { return 0; }
        }

        private static (int dataOff, int dataSize, int bpp, int width, int height, bool tiled)? ReadShape(byte[] ncgr, int declaredWidth = 0)
        {
            if (ncgr == null) return null;
            int c = NitroBgCodec.Find(ncgr, "RAHC", 0);
            if (c < 0 || c + 0x20 > ncgr.Length) return null;

            // Read as SIGNED, which is how Nds4j (the library behind NitroViewer) reads them: a game that
            // does not record a dimension writes 0xFFFF, which is -1, not 65535.
            short tilesDown = (short)NitroBgCodec.U16(ncgr, c + 0x08);
            short tilesAcross = (short)NitroBgCodec.U16(ncgr, c + 0x0A);
            int depth = NitroBgCodec.U32(ncgr, c + 0x0C);          // 3 = sixteen colours, 4 = two fifty six
            int tiledFlag = NitroBgCodec.U32(ncgr, c + 0x14);
            int dataSize = NitroBgCodec.U32(ncgr, c + 0x18);
            int dataOff = c + 0x20;
            if (dataOff >= ncgr.Length) return null;
            if (dataSize <= 0 || dataOff + dataSize > ncgr.Length) dataSize = ncgr.Length - dataOff;

            int bpp = depth == 4 ? 8 : 4;
            bool tiled = (tiledFlag & 0xFF) == 0;
            int bytesPerTile = 64 * bpp / 8;
            int numTiles = bytesPerTile > 0 ? dataSize / bytesPerTile : 0;
            if (numTiles <= 0) return null;

            // Width is recorded far more often than height. When only the height is missing, work it out
            // from how many tiles there are rather than falling back to a guess about the whole shape.
            int acrossTiles = tilesAcross > 0 ? tilesAcross : 0;
            int downTiles = tilesDown > 0 ? tilesDown : 0;

            // A width the archive is known to use beats anything worked out from the bytes.
            if (declaredWidth >= 8 && declaredWidth % 8 == 0) { acrossTiles = declaredWidth / 8; downTiles = 0; }

            if (acrossTiles == 0 && downTiles > 0) acrossTiles = (numTiles + downTiles - 1) / downTiles;
            if (acrossTiles == 0)
            {
                // Nothing recorded and nothing known, which is common.
                acrossTiles = Math.Max(1, (int)Math.Round(Math.Sqrt(numTiles)));
                while (acrossTiles > 1 && numTiles % acrossTiles != 0) acrossTiles--;
                if (acrossTiles <= 0) acrossTiles = 1;
            }
            if (downTiles == 0) downTiles = (numTiles + acrossTiles - 1) / acrossTiles;

            int width = acrossTiles * 8, height = downTiles * 8;

            // Never claim more pixels than are actually stored.
            int roomForPixels = numTiles * 64;
            if ((long)width * height > roomForPixels)
            {
                height = roomForPixels / Math.Max(1, width);
                if (tiled) height -= height % 8;
                if (height <= 0) return null;
            }
            if (width <= 0 || height <= 0) return null;
            return (dataOff, dataSize, bpp, width, height, tiled);
        }

        /// <summary>Eight by eight blocks in a row, into a plain left to right picture.</summary>
        private static byte[] Untile(byte[] tiled, int width, int height)
        {
            byte[] outp = new byte[width * height];
            int across = Math.Max(1, width / 8);
            for (int i = 0; i < tiled.Length && i < outp.Length; i++)
            {
                int tile = i / 64, inTile = i % 64;
                int tx = (tile % across) * 8 + inTile % 8;
                int ty = (tile / across) * 8 + inTile / 8;
                if (tx < width && ty < height) outp[ty * width + tx] = tiled[i];
            }
            return outp;
        }

        /// <summary>A plain picture back into eight by eight blocks.</summary>
        private static byte[] Retile(byte[] straight, int width, int height)
        {
            byte[] outp = new byte[width * height];
            int across = Math.Max(1, width / 8);
            for (int i = 0; i < outp.Length; i++)
            {
                int tile = i / 64, inTile = i % 64;
                int tx = (tile % across) * 8 + inTile % 8;
                int ty = (tile / across) * 8 + inTile / 8;
                if (tx < width && ty < height) outp[i] = straight[ty * width + tx];
            }
            return outp;
        }

        // ── out ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Saves an entry as a PNG that keeps its numbers and its colours, so it can come back in
        /// unchanged. Returns the reason when there is no picture to save, and writes nothing then.</summary>
        public static string ExportPng(Archive a, int index, string path)
        {
            // A picture Replace takes as a whole is saved whole too, so the two sizes always agree.
            string whynot = null;
            Indexed ix = SavedAsWholePicture(a, index) ? null : ReadIndexed(a, index, out whynot);
            if (ix != null)
            {
                // Only the colours this drawing is allowed.
                uint[] allowed = ix.Palette.Length > ix.ColourCount
                    ? ix.Palette.Take(ix.ColourCount).ToArray()
                    : ix.Palette;
                File.WriteAllBytes(path, IndexedPng.Write(ix.Indices, allowed, ix.Width, ix.Height));
                return null;
            }

            // Not a drawing on its own, but it may still have a picture: a background, a sprite made of
            // pieces, or a set of colours. Save what can be shown.
            Preview p = Render(a, index);
            if (p.Rgba == null) return whynot ?? p.Whynot ?? "There is no picture in this entry to save.";

            // Replace reads colour 0 as see-through, so clear pixels take it and nothing else does. The
            // rest follow the game's own colour order, keeping the exact colours that were drawn.
            Dictionary<int, int> gameOrder = new Dictionary<int, int>();
            try
            {
                ScriptNarc narc = new ScriptNarc(a.Dir);
                byte[] palFile = narc.Available ? FindColours(a, narc, index) : null;
                int count = 0;
                (byte r, byte g, byte b)[] colours = palFile != null ? NitroBgCodec.ReadPalette(Unsqueeze(palFile), out count) : null;
                for (int i = 0; colours != null && i < count && i < colours.Length; i++)
                    gameOrder.TryAdd(Key555(colours[i].r, colours[i].g, colours[i].b), i);
            }
            catch { }

            int pixels = p.Width * p.Height;
            List<uint> drawn = new List<uint>();
            HashSet<uint> seen = new HashSet<uint>();
            for (int i = 0; i < pixels; i++)
            {
                if (p.Rgba[i * 4 + 3] == 0) continue;
                uint argb = 0xFF000000u | (uint)((p.Rgba[i * 4] << 16) | (p.Rgba[i * 4 + 1] << 8) | p.Rgba[i * 4 + 2]);
                if (seen.Add(argb)) drawn.Add(argb);
            }
            if (drawn.Count == 0) return "There is no picture in this entry to save.";
            if (drawn.Count > 255) return $"This picture has {drawn.Count} colours, more than a PNG can number.";

            List<uint> ordered = drawn
                .Select((c, first) => (c, first, at: gameOrder.TryGetValue(Key555((byte)(c >> 16), (byte)(c >> 8), (byte)c), out int g) ? g : int.MaxValue))
                .OrderBy(t => t.at).ThenBy(t => t.first)
                .Select(t => t.c)
                .ToList();
            uint[] pal = new uint[ordered.Count + 1];
            Dictionary<uint, byte> number = new Dictionary<uint, byte>();
            for (int i = 0; i < ordered.Count; i++) { pal[i + 1] = ordered[i]; number[ordered[i]] = (byte)(i + 1); }

            byte[] idx = new byte[pixels];
            for (int i = 0; i < pixels; i++)
            {
                if (p.Rgba[i * 4 + 3] == 0) continue;
                uint argb = 0xFF000000u | (uint)((p.Rgba[i * 4] << 16) | (p.Rgba[i * 4 + 1] << 8) | p.Rgba[i * 4 + 2]);
                idx[i] = number[argb];
            }
            File.WriteAllBytes(path, IndexedPng.Write(idx, pal, p.Width, p.Height));
            return null;
        }

        private static int Key555(byte r, byte g, byte b) => (r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10);

        /// <summary>Whether Replace takes this entry as the whole picture it draws rather than its tiles.</summary>
        private static bool SavedAsWholePicture(Archive a, int index)
        {
            try
            {
                ScriptNarc narc = new ScriptNarc(a.Dir);
                if (!narc.Available) return false;
                Kind kind = Identify(narc.Get(index));
                if (kind == Kind.CellLayout) return true;
                return kind == Kind.TileGraphic && (a.ArrangementEntry?.Invoke(index) ?? -1) >= 0;
            }
            catch { return false; }
        }

        /// <summary>Saves the entry exactly as it sits in the ROM. Always possible, and the only way to
        /// keep everything about an entry that is not a picture.</summary>
        public static string ExportRaw(Archive a, int index, string path)
        {
            ScriptNarc narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";
            byte[] b = narc.Get(index);
            if (b == null || b.Length == 0) return "This entry is empty.";
            File.WriteAllBytes(path, b);
            return null;
        }

        // ── back in ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Puts a PNG back in. Every check happens before anything is written, so a refusal
        /// leaves the game exactly as it was. Returns the reason, or null when it went in.</summary>
        public static string ImportPng(Archive a, int index, string path)
            => ImportPng(a, index, path, out _);

        /// <param name="note">What the caller should tell somebody afterwards, when there is something
        /// worth saying: a background that shares its tiles changes in more places than were painted.</param>
        public static string ImportPng(Archive a, int index, string path, out string note)
        {
            note = null;
            if (a.CannotImportBecause != null) return a.CannotImportBecause;

            string builtByHgEngine = HgEngineSourceAssets.CannotImportBecause(a.Dir, index)
                ?? BuiltPngSources.CannotWrite(a.Dir, HgEngineOwnedFiles.ArchiveOf(a.Dir), index, new ScriptNarc(a.Dir).Count);
            if (builtByHgEngine != null) return builtByHgEngine;

            // A whole picture put together from pieces goes back through the pieces, not straight into
            // the file: an assembled sprite through its layout, a background through its arrangement.
            string whole = PutWholePictureBack(a, index, path, out note);
            if (whole != Skipped) return whole;

            Indexed ix = ReadIndexed(a, index, out string whynot);
            if (ix == null) return whynot ?? "This entry is not a drawing, so a PNG cannot take its place.";

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { return "That file could not be read: " + ex.Message; }

            if (!IndexedPng.TryRead(file, out byte[] indices, out uint[] pal, out int w, out int h))
                return "That PNG does not keep its colours in a numbered list. Export this entry first and "
                     + "paint over what comes out, or save yours as an indexed PNG.";

            if (w != ix.Width || h != ix.Height)
                return $"This drawing is {ix.Width} by {ix.Height} and that PNG is {w} by {h}. Export this "
                     + "entry first to get one the right size.";

            int highest = 0;
            foreach (byte v in indices) if (v > highest) highest = v;
            if (highest >= ix.ColourCount)
                return $"This drawing is allowed {ix.ColourCount} colours and that PNG uses colour number "
                     + $"{highest}. Reduce it to {ix.ColourCount} colours and try again.";

            string trouble = WriteIndices(a, index, indices, ix);
            if (trouble == null) note = PaletteDiffers(indices, pal, ix);
            return trouble;
        }

        /// <summary>Only the pixel numbers go back, so a PNG recoloured in its palette alone changes nothing; this says so.</summary>
        private static string PaletteDiffers(byte[] indices, uint[] pal, Indexed ix)
        {
            if (pal == null || ix.Palette == null) return null;
            bool[] used = new bool[256];
            foreach (byte v in indices) used[v] = true;
            int differ = 0, counted = 0;
            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i] || i >= pal.Length || i >= ix.Palette.Length) continue;
                counted++;
                if ((pal[i] & 0xFFFFFF) != (ix.Palette[i] & 0xFFFFFF)) differ++;
            }
            if (differ == 0) return null;
            return $"That PNG's colours differ from this entry's palette for {differ} of the {counted} colours it uses. "
                 + "Only the pixel numbers were taken; the colours stay as the palette entry has them.";
        }

        /// <summary>Says this entry is not one of the assembled kinds, so the ordinary path should run.</summary>
        private const string Skipped = "\u0000not assembled";

        /// <summary>
        /// Puts a PNG of a whole assembled picture back through the pieces it is drawn from, when the entry
        /// is one of those.
        /// </summary>
        private static string PutWholePictureBack(Archive a, int index, string path, out string note)
        {
            note = null;
            ScriptNarc narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return Skipped;

            byte[] raw = narc.Get(index);
            Kind kind = Identify(raw);

            bool assembledSprite = kind == Kind.CellLayout;
            bool wholeBackground = false;
            if (kind == Kind.TileGraphic)
            {
                int arranged = -1;
                try { arranged = a.ArrangementEntry?.Invoke(index) ?? -1; } catch { }
                wholeBackground = arranged >= 0;
            }
            if (!assembledSprite && !wholeBackground) return Skipped;

            Preview shown = Render(a, index);
            if (shown.Rgba == null || shown.Width <= 0)
                return shown.Whynot ?? "This entry could not be drawn, so nothing can be put back into it.";

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { return "That file could not be read: " + ex.Message; }

            if (!IndexedPng.TryRead(file, out byte[] indices, out uint[] pal, out int w, out int h))
                return "That PNG does not keep its colours in a numbered list. Save this one first and "
                     + "paint over what comes out, or save yours as an indexed PNG.";
            if (w != shown.Width || h != shown.Height)
                return $"This is {shown.Width} by {shown.Height} and that PNG is {w} by {h}. Save this one "
                     + "first to get one the right size.";

            // The painter and the browser both work in plain pixels, so turn the PNG's numbers back into
            // colours and let the piece readers work out which numbers those are in each piece's own bank.
            byte[] painted = Flatten(indices, pal, w, h);

            if (assembledSprite) return PutAssembledBack(a, index, painted, w, h);

            string why = PutBackgroundBack(a, index, painted, w, h, out int changed, out int shared,
                                           out int fought);
            if (why != null) return why;
            if (shared > 0)
                note = $"{changed} squares changed, and {shared} of them are drawn from a piece that is "
                     + "used elsewhere in this background, so those places changed too. That is how "
                     + "backgrounds save room rather than something going wrong.";
            if (fought > 0)
                note = (note == null ? "" : note + " ")
                     + $"{fought} pixels were asked to be two colours at once because the places sharing "
                     + "a piece were painted differently. A piece can only be one thing, so the last one "
                     + "won.";
            return null;
        }

        /// <summary>Writes new pixel numbers into an entry, leaving everything else about it alone. Shared
        /// by putting a PNG back and by painting.</summary>
        public static string WriteIndices(Archive a, int index, byte[] straightIndices, Indexed ix)
        {
            ScriptNarc narc = new ScriptNarc(a.Dir);
            byte[] storedRaw = narc.Get(index);
            if (storedRaw == null) return "This entry could not be read.";

            if (a.Pokewalker is { } pw)
            {
                byte[] plain = DSPRE.ROMFiles.PokewalkerImage.Encode(straightIndices, pw.Width, pw.Height);
                if (plain == null) return "A Pokéwalker picture has four shades, numbered 0 to 3.";
                byte pwMarker = SqueezeMarker(storedRaw);
                byte[] packed = pwMarker != 0 ? Squeeze(plain, pwMarker) : plain;
                if (packed == null) return "This picture could not be squeezed back down, so nothing was changed.";
                narc.Put(index, packed);
                return null;
            }

            // Some of these are kept squeezed down. Work on the opened-out file and squeeze it again at
            // the end, so the edit lands in the same shape the game reads.
            byte marker = SqueezeMarker(storedRaw);
            byte[] stored = marker != 0 ? Unsqueeze(storedRaw) : storedRaw;
            if (marker == 0x11)
                return "This drawing is squeezed down in a way DSPRE cannot put back yet, so nothing was "
                     + "changed.";

            (int dataOff, int dataSize, int bpp, int width, int height, bool tiled)? shape = ReadShape(stored, WidthFor(a, index));
            if (shape == null) return "This drawing could not be taken apart, so nothing was changed.";
            (int dataOff, int dataSize, int bpp, int width, int height, bool isTiled) = shape.Value;

            byte[] tiled;
            List<CellPlacement.Piece> pieces = isTiled ? PiecesFor(a, narc, index, bpp) : null;
            if (pieces != null)
            {
                // Start from the tiles as they are, so tiles no piece shows keep their pixels.
                byte[] current = (byte[])stored.Clone();
                if (Scrambled(a, index)) SpriteScrambling.Unscramble(current, dataOff, dataSize, FromEnd(a));
                tiled = new byte[width * height];
                for (int i = 0; i < tiled.Length; i++)
                {
                    int at = dataOff + (bpp == 8 ? i : i / 2);
                    if (at >= current.Length) break;
                    tiled[i] = bpp == 8 ? current[at] : (byte)(i % 2 == 0 ? current[at] & 0x0F : current[at] >> 4);
                }
                CellPlacement.Place(tiled, pieces, out _, out int placedWidth, out int placedHeight);
                if (straightIndices.Length != placedWidth * placedHeight
                    || !CellPlacement.Unplace(straightIndices, placedWidth, pieces, tiled))
                    return "That picture doesn't match the cell this drawing is placed by, so nothing was changed.";
            }
            else tiled = isTiled ? Retile(straightIndices, width, height) : straightIndices;
            byte[] outp = (byte[])stored.Clone();
            ushort seed = Scrambled(a, index) ? SpriteScrambling.Seed(stored, dataOff, dataSize, FromEnd(a)) : (ushort)0;

            if (bpp == 8)
            {
                if (tiled.Length > dataSize) return "That picture has more pixels than this entry holds.";
                Array.Copy(tiled, 0, outp, dataOff, tiled.Length);
            }
            else
            {
                if ((tiled.Length + 1) / 2 > dataSize) return "That picture has more pixels than this entry holds.";
                for (int i = 0; i + 1 < tiled.Length; i += 2)
                    outp[dataOff + i / 2] = (byte)((tiled[i] & 0x0F) | ((tiled[i + 1] & 0x0F) << 4));
            }

            if (Scrambled(a, index)) SpriteScrambling.Scramble(outp, dataOff, dataSize, seed, FromEnd(a));

            if (marker != 0)
            {
                byte[] packed = Squeeze(outp, marker);
                if (packed == null)
                    return "This drawing could not be squeezed back down, so nothing was changed.";
                outp = packed;
            }

            narc.Put(index, outp);
            return null;
        }

        /// <summary>Changes the colours a drawing is painted with. </summary>
        /// <summary>Turns the numbers and the colours into plain pixels. Colour zero is the see-through one
        /// in these games, so it is left clear.</summary>
        public static byte[] Flatten(byte[] indices, uint[] palette, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < indices.Length && i * 4 + 3 < rgba.Length; i++)
            {
                byte n = indices[i];
                uint c = n < palette.Length ? palette[n] : 0u;
                rgba[i * 4] = (byte)((c >> 16) & 0xFF);
                rgba[i * 4 + 1] = (byte)((c >> 8) & 0xFF);
                rgba[i * 4 + 2] = (byte)(c & 0xFF);
                rgba[i * 4 + 3] = n == 0 ? (byte)0 : (byte)255;
            }
            return rgba;
        }

        public static byte[] Flatten(Indexed art) => Flatten(art.Indices, art.Palette, art.Width, art.Height);

        public static string WritePalette(Archive a, int index, uint[] palette, int paletteIndex = -1)
        {
            ScriptNarc narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";

            // Where in the file this entry's colours start, matching what ReadIndexed took out: a bank is
            // 256 colours for an 8bpp drawing.
            byte[] drawing = narc.Get(index);
            (int dataOff, int dataSize, int bpp, int width, int height, bool tiled)? shape = drawing != null ? ReadShape(Unsqueeze(drawing), WidthFor(a, index)) : null;
            int bankSize = shape?.bpp == 8 ? 256 : 16;
            int startAt = (a.ColourBank?.Invoke(index) ?? 0) * bankSize;

            // Find the very file the colours came from, so the right one is written back.
            byte[] palStored = null;
            int palIndex = -1;
            if (a.Colours == Pairing.SameIndexInOtherArchive && a.ColourArchive != null)
            {
                ScriptNarc other = new ScriptNarc(a.ColourArchive.Value);
                if (!other.Available) return "The colours for this drawing are in an archive this game does not have.";
                palStored = other.Get(index); palIndex = index;
                if (palStored == null) return "The colours for this drawing could not be found.";
                string err = PatchPalette(ref palStored, palette, startAt);
                if (err != null) return err;
                other.Put(palIndex, palStored);
                return null;
            }

            palIndex = paletteIndex >= 0 ? paletteIndex : FindColourIndex(a, narc, index);
            if (palIndex < 0) return "The colours for this drawing could not be found.";
            palStored = narc.Get(palIndex);
            string e = PatchPalette(ref palStored, palette, startAt);
            if (e != null) return e;
            narc.Put(palIndex, palStored);
            return null;
        }

        /// <summary>The colours a palette entry holds, in file order, or null when it is not a palette.</summary>
        public static (byte r, byte g, byte b)[] ReadPaletteEntry(Archive a, int index)
        {
            ScriptNarc narc = new ScriptNarc(a.Dir);
            byte[] raw = narc.Available ? narc.Get(index) : null;
            if (raw == null || Identify(raw) != Kind.Palette) return null;
            (byte r, byte g, byte b)[] colours = NitroBgCodec.ReadPalette(Unsqueeze(raw), out int count);
            return colours == null ? null : colours.Take(Math.Min(count, colours.Length)).ToArray();
        }

        /// <summary>Changes one colour of a palette entry in place, keeping every other colour and bank.</summary>
        public static string WritePaletteColour(Archive a, int index, int number, byte r, byte g, byte b)
        {
            ScriptNarc narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";
            byte[] raw = narc.Get(index);
            if (raw == null || Identify(raw) != Kind.Palette) return "This entry is not a set of colours.";
            string err = PatchPalette(ref raw, new[] { (uint)(r << 16 | g << 8 | b) }, number);
            if (err != null) return err;
            narc.Put(index, raw);
            return null;
        }

        internal static string PatchPalette(ref byte[] nclr, uint[] palette, int startAt = 0)
        {
            if (nclr == null) return "The colours could not be read.";

            byte marker = SqueezeMarker(nclr);
            if (marker == 0x11)
                return "These colours are squeezed down in a way DSPRE cannot put back yet.";
            if (marker != 0) nclr = Unsqueeze(nclr);

            int p = NitroBgCodec.Find(nclr, "TTLP", 0);
            if (p < 0) return "These colours are not stored in a way DSPRE can write back.";
            // Colours start 0x18 into the block, where NitroBgCodec.ReadPalette reads them.
            int off = p + 0x18;
            int size = Math.Min(NitroBgCodec.U32(nclr, p + 0x10), nclr.Length - off);
            if (size <= 0) return "These colours are not stored in a way DSPRE can write back.";

            // Write into the same bank the colours were read from. These files often hold many banks side
            // by side, so starting at the front would repaint whatever owns the first one.
            int room = size / 2 - startAt;
            if (startAt < 0 || room <= 0)
                return "These colours sit outside what this entry holds.";
            if (palette.Length > room)
                return $"This entry holds {room} colours here and {palette.Length} were given.";

            byte[] outp = (byte[])nclr.Clone();
            for (int i = 0; i < palette.Length; i++)
            {
                uint c = palette[i];
                int r = (int)((c >> 16) & 0xFF) >> 3, g = (int)((c >> 8) & 0xFF) >> 3, b = (int)(c & 0xFF) >> 3;
                ushort v = (ushort)(r | (g << 5) | (b << 10));
                outp[off + (startAt + i) * 2] = (byte)(v & 0xFF);
                outp[off + (startAt + i) * 2 + 1] = (byte)(v >> 8);
            }
            if (marker != 0)
            {
                byte[] packed = Squeeze(outp, marker);
                if (packed == null) return "These colours could not be squeezed back down.";
                outp = packed;
            }

            nclr = outp;
            return null;
        }

        /// <summary>Which entry holds the colours for a drawing, following the archive's rule.</summary>
        private static int FindColourIndex(Archive a, ScriptNarc narc, int index)
        {
            int told = a.ColourEntry?.Invoke(index) ?? -1;
            if (told >= 0 && Identify(narc.Get(told)) == Kind.Palette) return told;

            if (a.Colours == Pairing.OnePaletteForAll)
            {
                byte[] first = narc.Get(0);
                return first != null && Identify(first) == Kind.Palette ? 0 : -1;
            }
            List<int> palettes = PaletteIndexes(a.Dir, narc);
            int best = -1, bestGap = int.MaxValue;
            foreach (int i in palettes)
            {
                int gap = Math.Abs(i - index);
                if (i < index) gap = gap * 2 - 1;
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return best;
        }
    }
}
