using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    public static partial class GraphicAssets
    {
        /// <summary>
        /// What a PNG would lose going into a graphic: the colours its palette does not hold, and where they could
        /// go instead. An import that finds some asks first rather than swapping them for the nearest colours.
        /// </summary>
        public sealed class PngColourCheck
        {
            public List<uint> Missing = new();          // 0xRRGGBB, as the DS would keep them
            public List<int> Slots = new();             // colour number each missing colour would take, in order
            public List<IReadOnlyList<string>> SlotUsers = new();   // what else draws with that number, or null
            public int PaletteEntry = -1;
            public int Bank;                            // which sixteen of the palette the graphic draws with
            public string CannotAddBecause;             // null when the colours can be put into the palette
            public bool KeepsNumbers;                   // a flat drawing takes the PNG's numbers, not its colours
            public IReadOnlyList<string>[] UsersOf = new IReadOnlyList<string>[16];   // what else draws with each colour number of the bank, or null
            public uint[] BankColours = new uint[16];
            public bool[] Kept = new bool[16];          // numbers the PNG already paints with, so not free to take

            public bool Matches => Missing.Count == 0;
        }

        /// <summary>Null when it cannot be told; the import then reports the problem itself.</summary>
        public static PngColourCheck CheckPngColours(Archive a, int index, string path, int paletteIndex = -1, int paletteBank = -1)
        {
            try
            {
                ScriptNarc narc = new ScriptNarc(a.Dir);
                if (!narc.Available) return null;
                byte[] raw = narc.Get(index);
                Kind kind = Identify(raw);
                if (!IndexedPng.TryRead(File.ReadAllBytes(path), out byte[] indices, out uint[] pal, out int w, out int h)) return null;

                if (kind == Kind.TileGraphic && (a.ArrangementEntry?.Invoke(index) ?? -1) < 0) return CheckNumbers(a, index, indices, pal, paletteIndex, paletteBank);
                if (kind != Kind.CellLayout && kind != Kind.TileGraphic) return null;

                int paletteAt = paletteIndex >= 0 ? paletteIndex : FindColourIndex(a, narc, index);
                byte[] nclr = paletteAt >= 0 ? narc.Get(paletteAt) : null;
                if (nclr == null) return null;
                (byte r, byte g, byte b)[] all = NitroBgCodec.ReadPalette(Unsqueeze(nclr), out int count);

                PngColourCheck check = new PngColourCheck { PaletteEntry = paletteAt };
                List<int> banks = kind == Kind.CellLayout ? CellPlacement.Banks(Unsqueeze(raw)) : null;
                if (banks == null || banks.Count != 1)
                    check.CannotAddBecause = "DSPRE cannot tell which set of sixteen colours new ones belong in for this picture.";
                check.Bank = banks?.FirstOrDefault() ?? 0;

                // Each square of a whole background keeps its own row, so a colour in any of its rows is not new.
                if (kind == Kind.TileGraphic)
                {
                    HashSet<uint> drawable = ArrangedColours(a, narc, index, all, count);
                    if (drawable != null)
                    {
                        foreach (uint c in PaintedColours(indices, pal))
                            if (!drawable.Contains(c) && !check.Missing.Contains(c)) check.Missing.Add(c);
                        return check;
                    }
                }

                uint[] bank = Enumerable.Range(0, 16)
                    .Select(i => check.Bank * 16 + i < count ? Pack(all[check.Bank * 16 + i]) : 0xFFFFFFFFu).ToArray();
                bool[] wanted = new bool[16];
                foreach (uint c in PaintedColours(indices, pal))
                {
                    int at = Array.FindIndex(bank, 1, x => x == c);
                    if (at >= 1) wanted[at] = true;
                    else if (!check.Missing.Contains(c)) check.Missing.Add(c);
                }
                if (check.Matches || check.CannotAddBecause != null) return check;

                Dictionary<int, List<string>> users = SlotUsers(a, narc, index, paletteAt, check.Bank);
                for (int s = 0; s < 16; s++)
                {
                    check.BankColours[s] = bank[s];
                    check.Kept[s] = wanted[s] || bank[s] == 0xFFFFFFFFu;
                    check.UsersOf[s] = users.TryGetValue(s, out List<string> u) && u.Count > 0 ? u : null;
                }
                // Graphics the game never loads do not count against a colour.
                int Weight(int slot) => users.TryGetValue(slot, out List<string> u) ? u.Count(x => !x.Contains("unused")) : 0;
                List<int> free = Enumerable.Range(1, 15).Where(s => !check.Kept[s]).OrderBy(Weight).ThenBy(s => s).ToList();
                if (free.Count < check.Missing.Count)
                {
                    check.CannotAddBecause = $"This picture needs {check.Missing.Count} new colours and only {free.Count} of the sixteen "
                                           + "are not already taken by colours it keeps.";
                    return check;
                }
                foreach (int s in free.Take(check.Missing.Count))
                {
                    check.Slots.Add(s);
                    check.SlotUsers.Add(check.UsersOf[s]);
                }
                return check;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("The PNG's colours could not be checked: " + ex.Message);
                return null;
            }
        }

        /// <summary>A flat drawing keeps the PNG's colour numbers, so what matters is the colour each number has.</summary>
        private static PngColourCheck CheckNumbers(Archive a, int index, byte[] indices, uint[] pal, int paletteIndex, int paletteBank)
        {
            Indexed ix = ReadIndexed(a, index, out _, paletteIndex: paletteIndex, paletteBank: paletteBank);
            if (ix?.Palette == null || pal == null) return null;
            PngColourCheck check = new PngColourCheck { KeepsNumbers = true };
            ScriptNarc narc = new ScriptNarc(a.Dir);
            check.PaletteEntry = paletteIndex >= 0 ? paletteIndex : FindColourIndex(a, narc, index);
            if (ix.BitsPerPixel == 4 && paletteBank >= 0) check.Bank = paletteBank;
            else if (ix.BitsPerPixel == 4) { try { check.Bank = a.ColourBank?.Invoke(index) ?? 0; } catch { } }   // without a known bank the first bank is used
            Dictionary<int, List<string>> users = ix.BitsPerPixel == 4 && check.PaletteEntry >= 0
                ? SlotUsers(a, narc, index, check.PaletteEntry, check.Bank) : new Dictionary<int, List<string>>();
            bool[] used = new bool[256];
            foreach (byte v in indices) used[v] = true;
            for (int i = 1; i < used.Length; i++)
            {
                if (!used[i] || i >= pal.Length || i >= ix.Palette.Length) continue;
                uint want = Ds(pal[i] & 0xFFFFFF), have = Ds(ix.Palette[i] & 0xFFFFFF);
                if (want == have) continue;
                check.Missing.Add(want);
                check.Slots.Add(i);
                check.SlotUsers.Add(users.TryGetValue(i, out List<string> u) && u.Count > 0 ? u : null);
            }
            if (check.PaletteEntry < 0) check.CannotAddBecause = "DSPRE does not know which colours this drawing is painted with.";
            return check;
        }

        public static string AddPngColours(Archive a, PngColourCheck check)
        {
            if (check == null || check.CannotAddBecause != null || check.PaletteEntry < 0) return check?.CannotAddBecause;
            for (int i = 0; i < check.Missing.Count; i++)
            {
                uint c = check.Missing[i];
                string trouble = WritePaletteColour(a, check.PaletteEntry, check.Bank * 16 + check.Slots[i],
                                                    (byte)(c >> 16), (byte)(c >> 8), (byte)c);
                if (trouble != null) return trouble;
            }
            return null;
        }

        /// <summary>Every opaque colour a PNG paints with, at the DS's five bits a channel.</summary>
        private static IEnumerable<uint> PaintedColours(byte[] indices, uint[] pal)
        {
            bool[] seen = new bool[256];
            foreach (byte v in indices) seen[v] = true;
            for (int i = 0; i < seen.Length && i < pal.Length; i++)
                if (seen[i] && (pal[i] >> 24) >= 128) yield return Ds(pal[i] & 0xFFFFFF);
        }

        /// <summary>Every row the arrangement's squares name, or the whole file for a 256-colour drawing; null without an arrangement.</summary>
        private static HashSet<uint> ArrangedColours(Archive a, ScriptNarc narc, int index, (byte r, byte g, byte b)[] all, int count)
        {
            int arranged = -1;
            try { arranged = a.ArrangementEntry?.Invoke(index) ?? -1; } catch { }   // without an arrangement the row check applies
            byte[] scr = arranged >= 0 ? narc.Get(arranged) : null;
            if (scr == null) return null;
            scr = Unsqueeze(scr);
            bool eightBit = NitroBgCodec.ReadTileHeader(Unsqueeze(narc.Get(index))).EightBit;
            HashSet<int> rows = new HashSet<int>();
            if (!eightBit)
            {
                (int w, int h, int mapAt) = NitroBgCodec.ReadScreenHeader(scr);
                int entryBytes = NitroBgCodec.EntryBytes(scr);
                for (int square = 0; square < NitroBgCodec.SquareCount(w / 8, h / 8); square++)
                {
                    int e = NitroBgCodec.EntryAt(scr, mapAt, square, entryBytes);
                    if (e >= 0 && entryBytes == 2) rows.Add(e >> 12);
                }
                if (entryBytes == 1) rows.Add(0);
            }
            HashSet<uint> colours = new HashSet<uint>();
            for (int i = 0; i < count && i < all.Length; i++)
                if (eightBit ? i != 0 : i % 16 != 0 && rows.Contains(i / 16)) colours.Add(Pack(all[i]));
            return colours;
        }

        private static uint Pack((byte r, byte g, byte b) c) => Ds((uint)(c.r << 16 | c.g << 8 | c.b));

        private static uint Ds(uint rgb) => rgb & 0xF8F8F8;

        /// <summary>What else draws with each colour number of a bank: drawings in the archive sharing the palette,
        /// and whatever the archive itself names.</summary>
        private static Dictionary<int, List<string>> SlotUsers(Archive a, ScriptNarc narc, int index, int paletteAt, int bankNumber)
        {
            Dictionary<int, List<string>> users = new();
            void Use(int slot, string who)
            {
                if (!users.TryGetValue(slot, out List<string> list)) users[slot] = list = new List<string>();
                if (!list.Contains(who)) list.Add(who);
            }

            int own = Identify(narc.Get(index)) == Kind.TileGraphic ? index : -1;
            if (own < 0) { try { own = a.DrawingEntry?.Invoke(index) ?? -1; } catch { } }   // without the game's pairing the drawing is found by searching
            if (own < 0) own = FindDrawingIndex(narc, index);

            for (int i = 0; i < narc.Count; i++)
            {
                if (i == own) continue;
                byte[] b = narc.Get(i);
                if (Identify(b) != Kind.TileGraphic || FindColourIndex(a, narc, i) != paletteAt) continue;
                byte[] open = Unsqueeze(b);
                int at = NitroBgCodec.ReadTileHeader(open).TilesAt;
                if (at <= 0 || at >= open.Length) continue;
                string who = a.NameOf?.Invoke(i) ?? $"file {i}";
                bool[] seen = new bool[16];
                for (int k = at; k < open.Length; k++) { seen[open[k] & 0xF] = true; seen[open[k] >> 4] = true; }
                for (int s = 1; s < 16; s++) if (seen[s]) Use(s, who);
            }

            if (a.AlsoUsesColours != null)
                foreach ((int slot, string who) in a.AlsoUsesColours(paletteAt, bankNumber)) Use(slot, who);
            return users;
        }
    }
}
