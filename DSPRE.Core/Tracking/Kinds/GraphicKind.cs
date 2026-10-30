using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// A graphics or asset member by what it is: colours as a list, a drawing as an indexed PNG, a screen as rows;
    /// cells, animations, models, textures and particles as their own file next to the JSON.
    /// </summary>
    public sealed class GraphicKind : FieldKind
    {
        private readonly DirNames _dir;
        private readonly string _title;
        private string _ext = ".bin";

        public GraphicKind(DirNames dir, bool graphics)
        {
            _dir = dir;
            Name = graphics ? "graphic" : "asset";
            Folder = (graphics ? "graphics/" : "assets/") + dir;
            _title = dir.ToString();
        }

        public override string Name { get; }
        public override string Folder { get; }
        internal static readonly string[] SpeciesParts = { "female back", "male back", "female front", "male front", "colours", "shiny colours" };
        internal static readonly string[] ClassParts = { "drawing", "colours", "cells", "animation", "plain copy" };

        public override string Title(string id, ChangeContext ctx)
        {
            int member = Number(id);
            string owner = _dir switch
            {
                DirNames.PokemonBattleSprites => ctx?.Names?.Species(member / 6) is string mon ? mon + " " + SpeciesParts[member % 6] : null,
                DirNames.TrainerGraphics when TrainerGraphicsLayout.Stride == ClassParts.Length
                    => ctx?.Names?.TrainerClass(TrainerGraphicsLayout.ClassOf(member)) is string cls ? cls + " " + ClassParts[member % ClassParts.Length] : null,
                _ => null,
            };
            return owner == null ? _title + " " + id : _title + " " + id + " " + owner;
        }

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == _dir ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(_dir, Number(id)) };

        protected override string BlobExtension(string key) => key == "image" ? ".png" : _ext;

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] stored = read(Files(id, ctx)[0]);
            if (stored == null) return null;
            byte[] bytes = NitroBgCodec.Inflate(stored);
            string format = Format(bytes);
            JsonObject json = new JsonObject { ["format"] = format };
            if (!ReferenceEquals(bytes, stored)) json["compressed"] = true;
            switch (format)
            {
                case "NCLR":
                    JsonObject colours = new JsonObject();
                    ushort[] all = NitroBgCodec.ReadColours(bytes);
                    for (int i = 0; i < all.Length; i++) colours[i.ToString(CultureInfo.InvariantCulture)] = ChangesJson.Colour(all[i]);
                    json["colors"] = colours;
                    return json;
                case "NCGR" when Drawing(bytes) is (int w, int h, bool eight, bool tiled, int at):
                    json["bpp"] = eight ? 8 : 4;
                    json["layout"] = tiled ? "tiled" : "linear";
                    json["width"] = w;
                    json["height"] = h;
                    byte[] plain = bytes;
                    if (Scrambling(Number(id)) is bool fromEnd)
                    {
                        plain = (byte[])bytes.Clone();
                        SpriteScrambling.Unscramble(plain, at, w * h * (eight ? 8 : 4) / 8, fromEnd);
                    }
                    json["image"] = Blob(ToPng(plain, w, h, eight, tiled, at, Colours(Number(id), eight ? 256 : 16, ctx)));
                    return json;
                case "NSCR" when Screen(bytes) is (int cols, int rows, int at, int entryBytes):
                    JsonObject lines = new JsonObject();
                    for (int y = 0; y < rows; y++)
                    {
                        StringBuilder line = new StringBuilder();
                        for (int x = 0; x < cols; x++)
                        {
                            if (x > 0) line.Append(' ');
                            int entry = NitroBgCodec.EntryAt(bytes, at, y * cols + x, entryBytes);
                            line.Append(entry.ToString(entryBytes == 1 ? "X2" : "X4", CultureInfo.InvariantCulture));
                        }
                        lines[y.ToString(CultureInfo.InvariantCulture)] = line.ToString();
                    }
                    json["entries"] = lines;
                    return json;
                default:
                    // Kept as stored, compressed or not, since nothing in it is read as fields.
                    _ext = format == "data" ? ".bin" : "." + format.ToLowerInvariant();
                    json.Remove("compressed");
                    json["content"] = Blob(stored);
                    return json;
            }
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            if (record["content"] is JsonNode content)
                return new Dictionary<TrackedFile, byte[]> { [file] = BlobBytes(content.GetValue<string>()) };
            byte[] stored = start(file) ?? throw new InvalidOperationException("A new graphic needs a file to build from.");
            bool compressed = record["compressed"]?.GetValue<bool>() == true;
            byte[] bytes = (byte[])(compressed ? NitroBgCodec.Inflate(stored) : stored).Clone();
            string format = record["format"]?.GetValue<string>();
            if (format != Format(bytes)) throw new InvalidDataException($"The ROM's file is {Format(bytes)}, the change is for {format}.");
            switch (format)
            {
                case "NCLR":
                    foreach (KeyValuePair<string, JsonNode> c in (JsonObject)record["colors"])
                        if (!NitroBgCodec.PutColour(bytes, int.Parse(c.Key, CultureInfo.InvariantCulture), ChangesJson.ParseColour(c.Value.GetValue<string>())))
                            throw new InvalidDataException($"Colour {c.Key} is past the end of this palette.");
                    break;
                case "NCGR":
                    if (Drawing(bytes) is not (int w, int h, bool eight, bool tiled, int at)
                        || w != record["width"].GetValue<int>() || h != record["height"].GetValue<int>() || (eight ? 8 : 4) != record["bpp"].GetValue<int>())
                        throw new InvalidDataException("The ROM's drawing has another size or colour depth.");
                    int size = w * h * (eight ? 8 : 4) / 8;
                    bool? scrambled = Scrambling(Number(id));
                    ushort seed = scrambled is bool end ? SpriteScrambling.Seed(bytes, at, size, end) : (ushort)0;
                    if (scrambled is bool before) SpriteScrambling.Unscramble(bytes, at, size, before);
                    FromPng(bytes, BlobBytes(record["image"].GetValue<string>()), w, h, eight, tiled, at);
                    if (scrambled is bool after) SpriteScrambling.Scramble(bytes, at, size, seed, after);
                    break;
                case "NSCR":
                    if (Screen(bytes) is not (int cols, int rows, int mapAt, int entryBytes)) throw new InvalidDataException("The ROM's screen can't be read.");
                    JsonObject lines = (JsonObject)record["entries"];
                    if (lines.Count != rows) throw new InvalidDataException("The ROM's screen has another size.");
                    for (int y = 0; y < rows; y++)
                    {
                        string[] cells = lines[y.ToString(CultureInfo.InvariantCulture)].GetValue<string>().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (cells.Length != cols) throw new InvalidDataException("The ROM's screen has another size.");
                        for (int x = 0; x < cols; x++)
                            if (!NitroBgCodec.PutEntry(bytes, mapAt, y * cols + x, entryBytes, int.Parse(cells[x], NumberStyles.HexNumber, CultureInfo.InvariantCulture)))
                                throw new InvalidDataException("A screen entry doesn't fit.");
                    }
                    break;
            }
            return new Dictionary<TrackedFile, byte[]> { [file] = compressed ? Lz77.Compress(bytes) : bytes };
        }

        private static string Format(byte[] b)
        {
            if (b.Length < 4) return "data";
            return Encoding.ASCII.GetString(b, 0, 4) switch
            {
                "RLCN" => "NCLR",
                "RGCN" => "NCGR",
                "RCSN" => "NSCR",
                "RECN" => "NCER",
                "RNAN" => "NANR",
                "BMD0" => "NSBMD",
                "BTX0" => "NSBTX",
                "BCA0" => "NSBCA",
                "BTA0" => "NSBTA",
                "BTP0" => "NSBTP",
                "BMA0" => "NSBMA",
                "BVA0" => "NSBVA",
                " APS" => "SPA",
                "RTFN" => "NFTR",
                _ => "data",
            };
        }

        /// <summary>Whether the member's pixels are stored scrambled, and if so whether keyed from the end; null when plain.</summary>
        private bool? Scrambling(int member)
        {
            switch (_dir)
            {
                case DirNames.PokemonBattleSprites:
                case DirNames.OtherPokemonBattleSprites:
                    return GameFamily == GameFamilies.DP;
                case DirNames.TrainerGraphics:
                case DirNames.TrainerBackGraphics:
                    bool scrambled = TrainerGraphicsLayout.PixelsAreScrambled
                        || TrainerGraphicsLayout.HasCells && TrainerGraphicsLayout.ScanEntry(TrainerGraphicsLayout.ClassOf(member)) == member;
                    return scrambled ? TrainerGraphicsLayout.ScanScrambledFromEnd : null;
                default:
                    return null;
            }
        }

        private static (int W, int H, bool Eight, bool Tiled, int At)? Drawing(byte[] ncgr)
        {
            if (NitroBgCodec.ReadDrawingHeader(ncgr) is not NitroBgCodec.DrawingHeader head) return null;
            (int w, int h) = NitroBgCodec.DrawingSize(head);
            int bytes = w * h * (head.EightBit ? 8 : 4) / 8;
            if (w % 8 != 0 || head.Tiled && h % 8 != 0 || bytes != head.DataSize || head.DataAt + bytes > ncgr.Length) return null;
            return (w, h, head.EightBit, head.Tiled, head.DataAt);
        }

        private static (int Cols, int Rows, int At, int EntryBytes)? Screen(byte[] nscr)
        {
            int nrcs = NitroBgCodec.Find(nscr, "NRCS", 0);
            if (nrcs < 0 || nrcs + 0x14 > nscr.Length) return null;
            int cols = NitroBgCodec.U16(nscr, nrcs + 0x08) / 8, rows = NitroBgCodec.U16(nscr, nrcs + 0x0A) / 8;
            int entryBytes = NitroBgCodec.EntryBytes(nscr);
            int size = NitroBgCodec.U32(nscr, nrcs + 0x10);
            if (cols <= 0 || rows <= 0 || cols * rows * entryBytes != size || nrcs + 0x14 + size > nscr.Length) return null;
            return (cols, rows, nrcs + 0x14, entryBytes);
        }

        private static int PixelOffset(int x, int y, int w, bool tiled) =>
            tiled ? ((y / 8) * (w / 8) + x / 8) * 64 + (y % 8) * 8 + x % 8 : y * w + x;

        /// <summary>The member holding a drawing's colours, or -1 where the archive doesn't pair them.</summary>
        private int ColoursMember(int member) => _dir switch
        {
            DirNames.PokemonBattleSprites => member % 6 < 4 ? member - member % 6 + 4 : -1,
            DirNames.TrainerGraphics or DirNames.TrainerBackGraphics => TrainerGraphicsLayout.ColoursEntry(TrainerGraphicsLayout.ClassOf(member)),
            _ => -1,
        };

        // The original game's colours, so a palette edit never changes a drawing's PNG and both sides of a change compare alike.
        private uint[] Colours(int member, int count, ChangeContext ctx)
        {
            int pal = ColoursMember(member);
            if (pal < 0 || pal == member || NitroBgCodec.Inflate(ctx.Base?.Read(TrackedFile.Member(_dir, pal))) is not byte[] nclr || Format(nclr) != "NCLR") return Greys(count);
            ushort[] bgr = NitroBgCodec.ReadColours(nclr);
            uint[] palette = Greys(count);
            for (int i = 0; i < count && i < bgr.Length; i++)
            {
                uint r = (uint)(bgr[i] & 0x1F) << 3, g = (uint)(bgr[i] >> 5 & 0x1F) << 3, b = (uint)(bgr[i] >> 10 & 0x1F) << 3;
                palette[i] = (i == 0 ? 0u : 0xFF000000u) | r << 16 | g << 8 | b;
            }
            return palette;
        }

        private static byte[] ToPng(byte[] ncgr, int w, int h, bool eight, bool tiled, int at, uint[] palette)
        {
            byte[] indices = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = PixelOffset(x, y, w, tiled);
                    indices[y * w + x] = eight ? ncgr[at + p] : (byte)((ncgr[at + p / 2] >> (p % 2 == 0 ? 0 : 4)) & 0xF);
                }
            return IndexedPng.Write(indices, palette, w, h, eight ? 8 : 4);
        }

        private static void FromPng(byte[] ncgr, byte[] png, int w, int h, bool eight, bool tiled, int at)
        {
            if (!IndexedPng.TryRead(png, out byte[] indices, out _, out int pw, out int ph) || pw != w || ph != h)
                throw new InvalidDataException($"The PNG must be an indexed {w}x{h} image.");
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = PixelOffset(x, y, w, tiled);
                    byte v = indices[y * w + x];
                    if (eight) ncgr[at + p] = v;
                    else if (v > 15) throw new InvalidDataException("A 16-colour drawing's PNG uses colour " + v + ".");
                    else
                    {
                        int shift = p % 2 == 0 ? 0 : 4;
                        ncgr[at + p / 2] = (byte)(ncgr[at + p / 2] & ~(0xF << shift) | v << shift);
                    }
                }
        }

        // Pixel values are the exact colour numbers; the colours only make the PNG viewable.
        private static uint[] Greys(int count)
        {
            uint[] palette = new uint[count];
            for (int i = 0; i < count; i++)
            {
                uint g = (uint)(i * 255 / (count - 1));
                palette[i] = 0xFF000000u | g << 16 | g << 8 | g;
            }
            return palette;
        }
    }
}
