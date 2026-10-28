using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The place-name banner's frame size, time on screen and slide speed, and its position once patched. Platinum draws a 17 by 5
    /// frame from tile 0; HGSS draws a 17 by 4 frame from tile 1 in a window one row taller.
    /// </summary>
    public sealed class PlaceNameBanner
    {
        public sealed record Look(int Across, int Down, int Width, int Height, int Time, int Speed, int SpeedOut);

        // A style's sheet holds 96 tiles, and the window has room for 5 rows.
        public const int SheetTiles = 96, WindowRows = 5, ScreenColumns = 32;

        private readonly RomInfo.PlaceNameBannerSites _s;
        private readonly ScreenTables _tables;
        private readonly Dictionary<uint, int> _at = new Dictionary<uint, int>();
        private readonly byte[] _data;

        // Takes only the instructions this class changes, so a save never overwrites the motion patch's calls between them.
        private PlaceNameBanner(RomInfo.PlaceNameBannerSites s, ScreenTables tables)
        {
            _s = s;
            _tables = tables;
            int window = tables.Take(s.Overlay, (int)s.Window, 0x1C);
            for (uint i = 0; i < 0x1C; i += 2) _at[s.Window + i] = window + (int)i;
            uint[] sites = new[] { s.Mod, SecondMod, s.Count, s.Task, SlideIn, SlideOut }.Concat(Hidden).ToArray();
            foreach (uint site in s.Centre != 0 ? sites.Append(s.Centre) : sites) _at[site] = tables.Take(s.Overlay, (int)site, 2);
            _data = tables.Finish();
        }

        public static bool SupportsCurrentRom => RomLocator.PlaceNameBannerSites != null;

        /// <summary>Null when this ROM's banner code isn't known or doesn't look like the game's.</summary>
        public static PlaceNameBanner Read()
        {
            RomInfo.PlaceNameBannerSites s = RomLocator.PlaceNameBannerSites;
            if (s == null) return null;
            ScreenTables tables = new ScreenTables();
            if (tables.FileOf(s.Overlay) == null) return null;
            PlaceNameBanner banner = new PlaceNameBanner(s, tables);
            return tables.Missing || !banner.LooksRight() ? null : banner;
        }

        private ushort Op(uint site) => BitConverter.ToUInt16(_data, _at[site]);
        private void Put(uint site, int op) => BitConverter.TryWriteBytes(_data.AsSpan(_at[site]), (ushort)op);

        private static bool Mov(ushort op, int reg) => op >> 8 == (0x20 | reg);
        private static bool Cmp(ushort op, int reg) => op >> 8 == (0x28 | reg);

        private static RomLocator.BannerTables D => RomLocator.BannerDistances;
        private uint At(int distance) => (uint)(_s.Task + distance);
        // The slide task's "sub r0, r0, #4" and "add r0, r0, #4"; Set writes "sub r0, #n" and "add r0, #n" there.
        private uint SlideIn => At(D.SlideIn);
        private uint SlideOut => At(D.SlideOut);
        private uint SecondMod => (uint)(_s.Mod + D.SecondMod);
        private uint[] Hidden => D.Hidden.Select(At).Append(_s.Show).Append((uint)(_s.Show + D.ShowRepeat)).ToArray();
        private uint WindowHeight => Patched ? _s.Window + 8 : _s.Window + 0x0A;

        private bool LooksRight() =>
            Cmp(Op(_s.Task), 0) && Mov(Op(_s.Mod), 1) && Mov(Op(SecondMod), 1) && Cmp(Op(_s.Count), 4) && Mov(Op(WindowHeight), Patched ? 2 : 0)
            && Hidden.All(h => (Op(h) & 0xFF) == (Op(_s.Show) & 0xFF)) && Speed(Op(SlideIn)) > 0 && Speed(Op(SlideOut)) > 0
            && (Patched || Op(_s.Window) == 0x2300);

        private static int Speed(ushort op) => op >> 9 == 0b0001111 || op >> 9 == 0b0001110 ? op >> 6 & 7 : op >> 11 is 0b00111 or 0b00110 ? op & 0xFF : 0;

        // ── the position patch ──

        // Storing y, width, height and palette in pairs frees an instruction to set x.
        private static readonly int[] PatchedShape = { 0x2200, 0x2320, 0xA900, 0xC10C, 0x2200, 0x2300, 0xC10C, 0x4A00, 0x600A, 0x2300, 0x1C01, 0x3120, 0x69C0, 0x2203 };
        private static readonly int[] PatchedMask = { 0xFF00, 0xFF00, 0xFFFF, 0xFFFF, 0xFF00, 0xFF00, 0xFFFF, 0xFF00, 0xFFFF, 0xFF00, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF };

        public bool Patched => Enumerable.Range(0, PatchedShape.Length).All(i => (Op(_s.Window + (uint)i * 2) & PatchedMask[i]) == (PatchedShape[i] & PatchedMask[i]));

        /// <summary>Rewrites the window function for <see cref="BannerPositionPatch"/>. The banner stays where the game puts it until moved.</summary>
        internal void WritePositionCode()
        {
            uint w = _s.Window;
            int height = Op(w + 0x0A) & 0xFF, palette = Op(w + 0x0E) & 0xFF;
            // The base tile's literal stays where it is; only the load that reads it moves up two instructions.
            uint literal = ((w + 0x12 + 4) & ~3u) + (uint)(Op(w + 0x12) & 0xFF) * 4;
            int k = (int)(literal - ((w + 0x0E + 4) & ~3u)) / 4;
            int[] code = { 0x2200, 0x2320, 0xA900, 0xC10C, 0x2200 | height, 0x2300 | palette, 0xC10C, 0x4A00 | k, 0x600A, 0x2300, 0x1C01, 0x3120, 0x69C0, 0x2203 };
            for (int i = 0; i < code.Length; i++) Put(w + (uint)i * 2, code[i]);
            _tables.Write(_data);
        }

        // ── values ──

        public Look Current
        {
            get
            {
                int width = Op(_s.Mod) & 0xFF;
                int rows = width == 0 ? 0 : (Op(_s.Count) & 0xFF) / width;
                return new Look(Patched ? Op(_s.Window + 18) & 0xFF : 0, Patched ? Op(_s.Window) & 0xFF : 0, width, rows,
                                Op(_s.Task) & 0xFF, Speed(Op(SlideIn)), Speed(Op(SlideOut)));
            }
        }

        private int ExtraRows => (Op(WindowHeight) & 0xFF) - Current.Height;
        private int FirstTile => _s.Centre != 0 ? 1 : 0;

        /// <summary>The tallest frame, and the widest frame at a given height, that fit the sheet and the window.</summary>
        public int MostRows => WindowRows - ExtraRows;
        public int MostWidth(int rows) => Math.Min(ScreenColumns, rows <= 0 ? ScreenColumns : (SheetTiles - FirstTile) / rows);

        /// <summary>How far the game scrolls the banner's layer up to hide it.</summary>
        public int HiddenScroll(Look l) => 8 * (l.Down + l.Height + ExtraRows) - 2;

        public string WhyNot(Look l)
        {
            if (!Patched && (l.Across != 0 || l.Down != 0)) return "Moving the banner needs the position patch.";
            if (l.Height < 1 || l.Height > MostRows) return $"The banner can be 1 to {MostRows} tiles tall.";
            if (l.Width < 1 || l.Width > MostWidth(l.Height)) return $"At {l.Height} tiles tall the banner can be 1 to {MostWidth(l.Height)} tiles wide.";
            if (l.Across + l.Width > ScreenColumns) return "The banner runs off the right edge.";
            if (HiddenScroll(l) > 255) return "The banner sits too low to slide out of sight.";
            if (l.Time < 0 || l.Time > 255) return "The banner can stay on screen for 0 to 255 steps.";
            if (l.Speed < 1 || l.Speed > 255 || l.SpeedOut < 1 || l.SpeedOut > 255) return "The banner can slide 1 to 255 pixels a step.";
            return null;
        }

        public void Set(Look l)
        {
            if (WhyNot(l) is string why) throw new InvalidOperationException(why);
            int extra = ExtraRows;
            Put(_s.Mod, 0x2100 | l.Width);
            Put(SecondMod, 0x2100 | l.Width);
            Put(_s.Count, 0x2C00 | l.Width * l.Height);
            Put(WindowHeight, (Patched ? 0x2200 : 0x2000) | l.Height + extra);
            foreach (uint h in Hidden) Put(h, Op(h) & 0xFF00 | HiddenScroll(l));
            Put(_s.Task, 0x2800 | l.Time);
            Put(SlideIn, 0x3800 | l.Speed);
            Put(SlideOut, 0x3000 | l.SpeedOut);
            if (_s.Centre != 0) Put(_s.Centre, 0x2200 | 8 * l.Width - 4);
            if (!Patched) return;
            Put(_s.Window, 0x2200 | l.Down);
            Put(_s.Window + 2, 0x2300 | ScreenColumns - l.Across);
            Put(_s.Window + 18, 0x2300 | l.Across);
        }

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        /// <summary>Writes the code and, when the size changed, stretches every style's frame to it.</summary>
        public void WriteFile(byte[] bytes)
        {
            PlaceNameBanner before = Read();
            _tables.Write(bytes);
            PlaceNameBanner after = Read();
            if (before == null || after == null) return;
            // The motion tables depend on the banner's place and size, so write them again after it changes.
            if (BannerMotionPatch.Read() is BannerMotionPatch.Style style) BannerMotionPatch.WriteStyle(style, after.Current, after.HiddenScroll(after.Current));
            (int w0, int h0) = (before.Current.Width, before.Current.Height);
            (int w1, int h1) = (after.Current.Width, after.Current.Height);
            if (w0 == w1 && h0 == h1) return;
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.AreaWindowGraphics });
            for (int member = 0; ; member += 2)
            {
                string path = ArchiveMembers.PathOf(RomInfo.DirNames.AreaWindowGraphics, member);
                if (!File.Exists(path)) break;
                byte[] sheet = File.ReadAllBytes(path);
                if (Stretch(sheet, FirstTile, w0, h0, w1, h1) is byte[] stretched) ArchiveMembers.Put(RomInfo.DirNames.AreaWindowGraphics, member, stretched);
            }
        }

        /// <summary>Redraws the frame at the new size by repeating its middle. The two edge columns and the top and bottom rows stay as they are.</summary>
        public static byte[] Stretch(byte[] sheet, int first, int w0, int h0, int w1, int h1)
        {
            if (sheet == null || w0 < 1 || h0 < 1 || w1 < 1 || h1 < 1) return null;
            int rahc = -1;
            for (int i = 0; i + 4 <= sheet.Length; i += 4)
                if (sheet[i] == 'R' && sheet[i + 1] == 'A' && sheet[i + 2] == 'H' && sheet[i + 3] == 'C') { rahc = i; break; }
            if (rahc < 0 || BitConverter.ToInt32(sheet, rahc + 0x0C) != 3) return null;
            int data = rahc + 8 + BitConverter.ToInt32(sheet, rahc + 0x1C), tiles = BitConverter.ToInt32(sheet, rahc + 0x18) / 32;
            if (first + w0 * h0 > tiles || first + w1 * h1 > tiles) return null;
            byte[] old = sheet.Skip(data + first * 32).Take(w0 * h0 * 32).ToArray();
            byte[] stretched = (byte[])sheet.Clone();
            int From(int at, int size, int was, int edge)
            {
                if (at < edge) return Math.Min(at, was - 1);
                if (at >= size - edge) return Math.Max(0, was - (size - at));
                int middle = Math.Max(1, was - 2 * edge);
                return Math.Min(was - 1, edge + (at - edge) % middle);
            }
            for (int y = 0; y < h1; y++)
                for (int x = 0; x < w1; x++)
                {
                    int ox = From(x, w1, w0, 2), oy = From(y, h1, h0, 1);
                    Buffer.BlockCopy(old, (oy * w0 + ox) * 32, stretched, data + (first + y * w1 + x) * 32, 32);
                }
            return stretched;
        }
    }
}
