using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using Images;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Taking a graphic out of the game and putting one back.
    ///
    /// A drawing in these games is a grid of numbers with a separate list of colours. Saving it as an
    /// ordinary picture throws the numbers away, and then putting it back means guessing which colour was
    /// which. So everything goes out as a PNG that keeps the numbers and the list, which is what an
    /// indexed PNG is, and comes back in the same way. Anything that is not a drawing says so instead.
    /// </summary>
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
        public static Indexed ReadIndexed(Archive a, int index, out string whynot, bool shiny = false)
        {
            whynot = null;
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) { whynot = "This game does not have this archive."; return null; }

            byte[] rawStored = narc.Get(index);
            var kind = Identify(rawStored);
            if (kind != Kind.TileGraphic)
            {
                whynot = kind == Kind.Palette
                    ? "This entry is a set of colours, not a drawing. The colours can be changed but there "
                      + "are no pixels here to paint."
                    : "This entry is not a drawing, so it has no pixels to paint.";
                return null;
            }

            byte[] pal = FindColours(a, narc, index, shiny);
            if (pal == null) { whynot = "No colours could be found for this drawing."; return null; }

            byte[] raw = Unsqueeze(rawStored);
            var colours = NitroBgCodec.ReadPalette(Unsqueeze(pal), out int count);
            if (colours == null || count == 0) { whynot = "The colours could not be read."; return null; }

            var shape = ReadShape(raw, a.PixelWidth);
            if (shape == null)
            {
                whynot = "This drawing is stored in a way DSPRE cannot take apart yet.";
                return null;
            }

            var (dataOff, dataSize, bpp, width, height, tiled) = shape.Value;
            if (a.ScrambledPixels) Unscramble(raw, dataOff, dataSize);

            int pixels = width * height;
            var idx = new byte[pixels];

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
            var straight = tiled ? Untile(idx, width, height) : idx;

            // A sixteen colour drawing takes one bank out of a palette that usually holds several, and the
            // archive says which bank this entry wants. Reading from the start regardless painted the party
            // icons in the first Pokemon's colours, so Venusaur came out yellow.
            int bankSize = bpp == 8 ? 256 : 16;
            int bank = a.ColourBank?.Invoke(index) ?? 0;
            int from = bank * bankSize;
            if (from < 0 || from >= count) from = 0;
            int have = Math.Min(bankSize, count - from);

            var palette = new uint[have];
            for (int i = 0; i < have; i++)
            {
                var c = colours[from + i];
                palette[i] = (uint)((i == 0 ? 0x00000000 : 0xFF000000) | (c.r << 16) | (c.g << 8) | c.b);
            }

            return new Indexed
            {
                Indices = straight, Palette = palette, Width = width, Height = height,
                ColourCount = have, BitsPerPixel = bpp,
            };
        }

        /// <summary>
        /// Undoes the scrambling on a Pokemon battle sprite's pixels, in place.
        ///
        /// These entries are ordinary drawings whose pixels have been run through a rolling key, so read
        /// straight they come out as coloured noise. The key starts from the first two bytes and steps on
        /// with the same multiply and add the games use; Diamond and Pearl run it backwards from the last
        /// two bytes instead. Same routine as PokemonSpriteEditorViewModel.MakeImage, which is where these
        /// numbers come from.
        /// </summary>
        private static void Unscramble(byte[] data, int off, int size)
        {
            int words = size / 2;
            if (words <= 0 || off < 0 || off + words * 2 > data.Length) return;

            ushort At(int i) => (ushort)(data[off + i * 2] | (data[off + i * 2 + 1] << 8));
            void Put(int i, ushort v) { data[off + i * 2] = (byte)(v & 0xFF); data[off + i * 2 + 1] = (byte)(v >> 8); }

            unchecked
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.DP)
                {
                    uint key = At(0);
                    for (int i = 0; i < words; i++)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
                else
                {
                    uint key = At(words - 1);
                    for (int i = words - 1; i >= 0; i--)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
            }
        }

        /// <summary>Puts the scrambling back on, so an edited sprite reads the same way the game expects.
        ///
        /// The key starts from the sprite's own first two bytes, which means those two bytes have to come
        /// back out unchanged or every pixel after them decodes to nonsense. Reading always turns them into
        /// four see-through pixels for the same reason, so they are put back that way here. In practice
        /// that corner of a battle sprite is empty anyway.</summary>
        private static void Scramble(byte[] data, int off, int size, ushort seed)
        {
            int words = size / 2;
            if (words <= 0 || off < 0 || off + words * 2 > data.Length) return;

            ushort At(int i) => (ushort)(data[off + i * 2] | (data[off + i * 2 + 1] << 8));
            void Put(int i, ushort v) { data[off + i * 2] = (byte)(v & 0xFF); data[off + i * 2 + 1] = (byte)(v >> 8); }

            Put(RomInfo.gameFamily != RomInfo.GameFamilies.DP ? 0 : words - 1, 0);

            unchecked
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.DP)
                {
                    uint key = seed;
                    for (int i = 0; i < words; i++)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
                else
                {
                    uint key = seed;
                    for (int i = words - 1; i >= 0; i--)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
            }
        }

        /// <summary>The word the scrambling was seeded from, which has to be kept to put it back.</summary>
        private static ushort ScrambleSeed(byte[] data, int off, int size)
        {
            int words = size / 2;
            if (words <= 0) return 0;
            int at = RomInfo.gameFamily != RomInfo.GameFamilies.DP ? 0 : words - 1;
            return (ushort)(data[off + at * 2] | (data[off + at * 2 + 1] << 8));
        }

        /// <summary>Where the pixels are in a drawing, what shape they make, and whether they are stored
        /// in eight by eight blocks. Laid out to match the reader in Images/Images/NCGR.cs, which is what
        /// the rest of DSPRE uses: after the sixteen byte file header the CHAR section runs id, size,
        /// tiles down, tiles across, depth, two spare, the tiled flag, the size of the pixels and one more
        /// spare, so the pixels themselves begin thirty two bytes into the section.</summary>
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

            // A width the archive is known to use beats anything worked out from the bytes. Most of these
            // files record nothing, and the ones that do sometimes record the whole run rather than the
            // shape the game draws.
            if (declaredWidth >= 8 && declaredWidth % 8 == 0) { acrossTiles = declaredWidth / 8; downTiles = 0; }

            if (acrossTiles == 0 && downTiles > 0) acrossTiles = (numTiles + downTiles - 1) / downTiles;
            if (acrossTiles == 0)
            {
                // Nothing recorded and nothing known, which is common. Lay the tiles out as close to square
                // as they go, in TILES rather than pixels: a thirty two tile icon becomes four by eight
                // tiles, which is 32 by 64 and looks like something. Laying them in one long row instead
                // reported a party icon as 256 by 8, which is true of the bytes and useless to look at.
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
            var outp = new byte[width * height];
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
            var outp = new byte[width * height];
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
            var ix = ReadIndexed(a, index, out string whynot);
            if (ix != null)
            {
                // Only the colours this drawing is allowed. A sixteen colour drawing sits next to a list of
                // two hundred and fifty six, and writing all of them out made the picture refuse to go back
                // in for having too many.
                var allowed = ix.Palette.Length > ix.ColourCount
                    ? ix.Palette.Take(ix.ColourCount).ToArray()
                    : ix.Palette;
                File.WriteAllBytes(path, IndexedPng.Write(ix.Indices, allowed, ix.Width, ix.Height));
                return null;
            }

            // Not a drawing on its own, but it may still have a picture: a background, a sprite made of
            // pieces, or a set of colours. Save what can be shown.
            var p = Render(a, index);
            if (p.Rgba == null) return whynot ?? p.Whynot ?? "There is no picture in this entry to save.";

            var seen = new Dictionary<uint, byte>();
            var pal = new List<uint>();
            var idx = new byte[p.Width * p.Height];
            for (int i = 0; i < idx.Length; i++)
            {
                uint argb = (uint)((p.Rgba[i * 4 + 3] << 24) | (p.Rgba[i * 4] << 16)
                                 | (p.Rgba[i * 4 + 1] << 8) | p.Rgba[i * 4 + 2]);
                if (!seen.TryGetValue(argb, out byte v))
                {
                    if (pal.Count >= 256) v = 0;
                    else { v = (byte)pal.Count; pal.Add(argb); seen[argb] = v; }
                }
                idx[i] = v;
            }
            if (pal.Count == 0) return "There is no picture in this entry to save.";
            File.WriteAllBytes(path, IndexedPng.Write(idx, pal.ToArray(), p.Width, p.Height));
            return null;
        }

        /// <summary>Saves the entry exactly as it sits in the ROM. Always possible, and the only way to
        /// keep everything about an entry that is not a picture.</summary>
        public static string ExportRaw(Archive a, int index, string path)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";
            var b = narc.Get(index);
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

            // A whole picture put together from pieces goes back through the pieces, not straight into
            // the file: an assembled sprite through its layout, a background through its arrangement.
            string whole = PutWholePictureBack(a, index, path, out note);
            if (whole != Skipped) return whole;

            var ix = ReadIndexed(a, index, out string whynot);
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

            return WriteIndices(a, index, indices, ix);
        }

        /// <summary>Says this entry is not one of the assembled kinds, so the ordinary path should run.</summary>
        private const string Skipped = "\u0000not assembled";

        /// <summary>
        /// Puts a PNG of a whole assembled picture back through the pieces it is drawn from, when the
        /// entry is one of those. Comes back with Skipped when it is an ordinary flat drawing.
        /// </summary>
        private static string PutWholePictureBack(Archive a, int index, string path, out string note)
        {
            note = null;
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return Skipped;

            byte[] raw = narc.Get(index);
            var kind = Identify(raw);

            bool assembledSprite = kind == Kind.CellLayout;
            bool wholeBackground = false;
            if (kind == Kind.TileGraphic)
            {
                int arranged = -1;
                try { arranged = a.ArrangementEntry?.Invoke(index) ?? -1; } catch { }
                wholeBackground = arranged >= 0;
            }
            if (!assembledSprite && !wholeBackground) return Skipped;

            var shown = Render(a, index);
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
            var painted = Flatten(indices, pal, w, h);

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
            var narc = new ScriptNarc(a.Dir);
            byte[] storedRaw = narc.Get(index);
            if (storedRaw == null) return "This entry could not be read.";

            // Some of these are kept squeezed down. Work on the opened-out file and squeeze it again at
            // the end, so the edit lands in the same shape the game reads.
            byte marker = SqueezeMarker(storedRaw);
            byte[] stored = marker != 0 ? Unsqueeze(storedRaw) : storedRaw;
            if (marker == 0x11)
                return "This drawing is squeezed down in a way DSPRE cannot put back yet, so nothing was "
                     + "changed.";

            var shape = ReadShape(stored, a.PixelWidth);
            if (shape == null) return "This drawing could not be taken apart, so nothing was changed.";
            var (dataOff, dataSize, bpp, width, height, isTiled) = shape.Value;

            var tiled = isTiled ? Retile(straightIndices, width, height) : straightIndices;
            var outp = (byte[])stored.Clone();
            ushort seed = a.ScrambledPixels ? ScrambleSeed(stored, dataOff, dataSize) : (ushort)0;

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

            if (a.ScrambledPixels) Scramble(outp, dataOff, dataSize, seed);

            if (marker != 0)
            {
                var packed = Squeeze(outp, marker);
                if (packed == null)
                    return "This drawing could not be squeezed back down, so nothing was changed.";
                outp = packed;
            }

            narc.Put(index, outp);
            return null;
        }

        /// <summary>Changes the colours a drawing is painted with. Every pixel using a colour changes with
        /// it, because the pixels hold numbers and the numbers point here.</summary>
        /// <summary>Turns the numbers and the colours into plain pixels. Colour zero is the see-through one
        /// in these games, so it is left clear.</summary>
        public static byte[] Flatten(byte[] indices, uint[] palette, int width, int height)
        {
            var rgba = new byte[width * height * 4];
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

        public static string WritePalette(Archive a, int index, uint[] palette)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";

            // Where in the file this entry's colours start, matching what ReadIndexed took out.
            int startAt = (a.ColourBank?.Invoke(index) ?? 0) * 16;

            // Find the very file the colours came from, so the right one is written back.
            byte[] palStored = null;
            int palIndex = -1;
            if (a.Colours == Pairing.SameIndexInOtherArchive && a.ColourArchive != null)
            {
                var other = new ScriptNarc(a.ColourArchive.Value);
                if (!other.Available) return "The colours for this drawing are in an archive this game does not have.";
                palStored = other.Get(index); palIndex = index;
                if (palStored == null) return "The colours for this drawing could not be found.";
                string err = PatchPalette(ref palStored, palette, startAt);
                if (err != null) return err;
                other.Put(palIndex, palStored);
                return null;
            }

            palIndex = FindColourIndex(a, narc, index);
            if (palIndex < 0) return "The colours for this drawing could not be found.";
            palStored = narc.Get(palIndex);
            string e = PatchPalette(ref palStored, palette, startAt);
            if (e != null) return e;
            narc.Put(palIndex, palStored);
            return null;
        }

        private static string PatchPalette(ref byte[] nclr, uint[] palette, int startAt = 0)
        {
            if (nclr == null) return "The colours could not be read.";

            byte marker = SqueezeMarker(nclr);
            if (marker == 0x11)
                return "These colours are squeezed down in a way DSPRE cannot put back yet.";
            if (marker != 0) nclr = Unsqueeze(nclr);

            int p = NitroBgCodec.Find(nclr, "TTLP", 0);
            if (p < 0) return "These colours are not stored in a way DSPRE can write back.";
            int size = NitroBgCodec.U32(nclr, p + 0x10);
            int off = p + 0x10 + 4 + NitroBgCodec.U32(nclr, p + 0x14);
            if (off < 0 || off + size > nclr.Length) return "These colours are not stored in a way DSPRE can write back.";

            // Write into the same bank the colours were read from. These files often hold many banks side
            // by side, so starting at the front would repaint whatever owns the first one.
            int room = size / 2 - startAt;
            if (startAt < 0 || room <= 0)
                return "These colours sit outside what this entry holds.";
            if (palette.Length > room)
                return $"This entry holds {room} colours here and {palette.Length} were given.";

            var outp = (byte[])nclr.Clone();
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
                var packed = Squeeze(outp, marker);
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
                var first = narc.Get(0);
                return first != null && Identify(first) == Kind.Palette ? 0 : -1;
            }
            var palettes = PaletteIndexes(a.Dir, narc);
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
