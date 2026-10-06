using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DSPRE
{
    /// <summary>
    /// The external Trainer Class Metadata patch v1.0.0 by MrHam88 (PR #272, MIT licence), for US HeartGold and SoulSilver.
    /// The armips source was assembled once and kept as original and patched bytes for every native write, plus the
    /// payload and the places that refer to where it sits, so it can be placed anywhere in the synthetic overlay.
    /// Building a/1/5/5 from the game's own tables is a port of the patch's build_trainer_class_metadata_narc.py.
    /// </summary>
    public static class TrainerClassMetadataPatch
    {
        public const int PayloadSize = 0x2068;
        public const uint DefaultPayloadOffset = 0x12200;
        public const uint SyntheticBase = 0x023C8000;

        public static bool SupportsCurrentRom =>
            RomInfo.gameLanguage == RomInfo.GameLanguages.English && (RomInfo.romID == "IPKE" || RomInfo.romID == "IPGE");

        private sealed class Reloc { public byte Kind; public bool Blx; public uint Offset, Value; }
        private sealed class Span { public uint Offset; public byte[] Original, Patched; }
        private sealed class Target { public string Path; public List<Span> Spans = new(); public List<Reloc> Relocs = new(); }
        private sealed class Data { public byte[] Payload; public List<Reloc> PayloadRelocs = new(); public List<Target> Targets = new(); }

        private static Data _data;

        private static Data Load()
        {
            if (_data != null) return _data;
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("DSPRE.Resources.ROMToolboxDB.TrainerClassMetadataPatch.bin")
                ?? throw new InvalidOperationException("The trainer class metadata patch data is missing from this build.");
            using var r = new BinaryReader(s);
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "TCMP" || r.ReadUInt16() != 1) throw new InvalidDataException("Unknown trainer class metadata patch data.");
            var d = new Data { Payload = r.ReadBytes(r.ReadInt32()) };
            Reloc ReadReloc() { var x = new Reloc { Kind = r.ReadByte(), Blx = r.ReadByte() != 0 }; r.ReadUInt16(); x.Offset = r.ReadUInt32(); x.Value = r.ReadUInt32(); return x; }
            for (int i = r.ReadInt32(); i > 0; i--) d.PayloadRelocs.Add(ReadReloc());
            for (int f = r.ReadUInt16(); f > 0; f--)
            {
                var t = new Target { Path = Encoding.ASCII.GetString(r.ReadBytes(r.ReadUInt16())) };
                for (int i = r.ReadInt32(); i > 0; i--)
                {
                    uint off = r.ReadUInt32(); int len = r.ReadInt32();
                    t.Spans.Add(new Span { Offset = off, Original = r.ReadBytes(len), Patched = r.ReadBytes(len) });
                }
                for (int i = r.ReadInt32(); i > 0; i--) t.Relocs.Add(ReadReloc());
                d.Targets.Add(t);
            }
            return _data = d;
        }

        private static string PathOf(string target)
        {
            if (target == "arm9/arm9.bin") return RomInfo.arm9Path;
            int overlay = int.Parse(Path.GetFileNameWithoutExtension(target).Substring(2));
            return OverlayUtils.GetPath(overlay);
        }

        private static int OverlayOf(string target) =>
            target.StartsWith("arm9_overlays/", StringComparison.Ordinal) ? int.Parse(Path.GetFileNameWithoutExtension(target).Substring(2)) : -1;

        /// <summary>Thumb BL, or BLX into ARM code, from <paramref name="source"/> to <paramref name="target"/>.</summary>
        private static byte[] Branch(uint source, uint target, bool blx)
        {
            int offset = blx ? (int)(target - ((source + 4) & ~3u)) : (int)(target - (source + 4));
            ushort hi = (ushort)(0xF000 | ((offset >> 12) & 0x7FF));
            ushort lo = (ushort)((blx ? 0xE800 : 0xF800) | ((offset >> 1) & 0x7FF));
            return BitConverter.GetBytes(hi).Concat(BitConverter.GetBytes(lo)).ToArray();
        }

        /// <summary>The payload for a runtime address.</summary>
        public static byte[] BuildPayload(uint address)
        {
            var d = Load();
            byte[] p = (byte[])d.Payload.Clone();
            foreach (var x in d.PayloadRelocs)
            {
                byte[] bytes = x.Kind == 0 ? Branch(address + x.Offset, x.Value, x.Blx) : BitConverter.GetBytes(address + x.Value);
                bytes.CopyTo(p, (int)x.Offset);
            }
            return p;
        }

        /// <summary>
        /// Where an installed copy's routines sit in the synthetic overlay, read back from the first ARM9 pointer the
        /// install wrote (the payload address plus a known offset); null when it can't be told.
        /// </summary>
        public static uint? InstalledOffset()
        {
            try
            {
                Target arm9 = Load().Targets.FirstOrDefault(t => t.Path == "arm9/arm9.bin");
                Reloc pointer = arm9?.Relocs.FirstOrDefault(x => x.Kind != 2);
                if (pointer == null) return null;
                byte[] file = File.ReadAllBytes(RomInfo.arm9Path);
                if (pointer.Offset + 4 > file.Length) return null;
                uint address = BitConverter.ToUInt32(file, (int)pointer.Offset) - pointer.Value;
                return address >= SyntheticBase && address - SyntheticBase + PayloadSize <= 0x16000 ? address - SyntheticBase : null;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException) { return null; }
        }

        /// <summary>The payload with everything that depends on its address zeroed, for the range check.</summary>
        public static byte[] Footprint => new byte[PayloadSize];

        /// <summary>Why the project can't take the patch as it stands, or null.</summary>
        public static string WhyNotApplicable()
        {
            var d = Load();
            foreach (var t in d.Targets)
            {
                int overlay = OverlayOf(t.Path);
                if (overlay >= 0 && OverlayUtils.IsCompressed(overlay)) OverlayUtils.Decompress(overlay);
                byte[] file = File.ReadAllBytes(PathOf(t.Path));
                foreach (var s in t.Spans)
                    if (s.Offset + s.Original.Length > file.Length || !file.AsSpan((int)s.Offset, s.Original.Length).SequenceEqual(s.Original))
                        return $"{t.Path} at 0x{s.Offset:X} is not the unmodified US HeartGold/SoulSilver code the patch hooks. " +
                            "Another patch or edit has changed it.";
            }
            try { BuildRecords(); }
            catch (InvalidDataException ex) { return ex.Message; }
            return null;
        }

        /// <summary>Builds a/1/5/5 from the game's tables, then writes the payload and every hook.</summary>
        public static void Apply(uint payloadOffset)
        {
            var d = Load();
            List<byte[]> records = BuildRecords();   // before the hooks retire the tables it reads
            uint address = SyntheticBase + payloadOffset;
            foreach (var t in d.Targets)
            {
                string path = PathOf(t.Path);
                byte[] file = File.ReadAllBytes(path);
                foreach (var s in t.Spans) s.Patched.CopyTo(file, (int)s.Offset);
                uint fileBase = t.Path == "arm9/arm9.bin" ? ARM9.address : OverlayUtils.OverlayTable.GetRAMAddress(OverlayOf(t.Path));
                foreach (var x in t.Relocs)
                {
                    byte[] bytes = x.Kind == 2 ? Branch(fileBase + x.Offset, address + x.Value, x.Blx) : BitConverter.GetBytes(address + x.Value);
                    bytes.CopyTo(file, (int)x.Offset);
                }
                File.WriteAllBytes(path, file);
            }
            DSUtils.WriteToFile(Filesystem.expArmPath, BuildPayload(address), payloadOffset);
            WriteArchive(records);
            TrainerClassMetadataStore.Reset();
        }

        // ── a/1/5/5 from the game's tables (port of build_trainer_class_metadata_narc.py) ──────────────────
        private const int ClassCount = 129, RecordSize = 0x34;
        private const uint Ov12Base = 0x022378C0, Ov80Base = 0x02229EE0, Ov115Base = 0x0225F020, Ov117Base = 0x0225F020;
        private const int GenderOffset = 0xFFB90, TrainerComboOffset = 0xFC3CA, ComboTableOffset = 0xFC40A, EyeMusicOffset = 0xFC61C;
        private const uint PrizeTableAddress = 0x0226C4C4, FrontierBrainTableAddress = 0x0223DB98;
        private const int TrainerComboCount = 32, ComboCount = 45, EyeMusicCount = 44, DefaultCombo = 0x29;
        private const ushort DefaultEyeMusic = 1108;

        private static readonly ushort[] Style0Small = { 0, 4, 6, 5 }, Style0Large = { 0, 7, 9, 8 };
        private static readonly ushort[] StandardSymbols = { 0x3B, 0x3C, 0x3D, 0x3E }, FrontierSymbols = { 0x3B, 0xCC, 0xCD, 0xCE };
        private static readonly ushort[] ExpectedTrainerComboRows =
        {
            66, 1091, 2118, 3144, 4170, 5195, 6217, 7244, 8290, 9319, 10344, 11369, 12394, 13419, 14444, 15470,
            16471, 17497, 18544, 19544, 20566, 21527, 21623, 32884, 33906, 31861, 30838, 34940, 29751, 29758, 44079, 45165,
        };
        private static readonly ushort[] ExpectedComboEffects =
        {
            12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 29, 30, 31, 32, 33, 28, 0xFFFF, 0xFFFF, 0xFFFF,
            35, 36, 34, 34, 39, 40, 41, 42, 43, 44, 37, 37, 38, 38, 37, 38, 0xFFFF, 0xFFFF, 45, 46,
        };
        private static readonly ushort[] ExpectedEyeMusicClasses =
        {
            2, 3, 4, 5, 6, 8, 9, 11, 14, 20, 21, 24, 25, 69, 31, 34, 36, 38, 42, 43, 46, 47, 49, 52, 55, 56, 60, 62, 63, 64,
            65, 68, 77, 78, 79, 82, 113, 115, 121, 122, 116, 114, 117, 118,
        };
        private static readonly Dictionary<int, uint> Vs20RecordByEffect = new()
        {
            [12] = 0x022603B0, [13] = 0x022603C4, [14] = 0x022603D8, [15] = 0x022603EC, [16] = 0x02260400, [17] = 0x02260414,
            [18] = 0x02260428, [19] = 0x0226043C, [20] = 0x02260450, [21] = 0x02260464, [22] = 0x02260478, [23] = 0x0226048C,
            [24] = 0x022604A0, [25] = 0x022604B4, [26] = 0x022604C8, [27] = 0x022604DC, [28] = 0x02260374,
        };
        private static readonly Dictionary<int, uint> Vs8RecordByEffect = new()
        {
            [29] = 0x02260388, [30] = 0x02260390, [31] = 0x02260398, [32] = 0x022603A0, [33] = 0x022603A8,
        };
        private static readonly Dictionary<int, ushort[]> Vs8BackgroundByEffect = new()
        {
            [29] = new ushort[] { 0x2F, 0x30, 0x31, 0x32 }, [30] = new ushort[] { 0x33, 0x30, 0x31, 0x32 }, [31] = new ushort[] { 0x34, 0x30, 0x31, 0x32 },
            [32] = new ushort[] { 0x35, 0x30, 0x31, 0x32 }, [33] = new ushort[] { 0x36, 0x30, 0x31, 0x32 },
        };
        private static readonly Dictionary<int, uint> RocketAdminRecordByEffect = new()
        {
            [40] = 0x0225FACC, [41] = 0x0225FAD4, [42] = 0x0225FADC, [43] = 0x0225FAE4, [44] = 0x0225FAEC,
        };
        private static readonly Dictionary<int, int> FrontierBrainRowByClass = new() { [97] = 0, [99] = 4, [100] = 1, [101] = 5, [102] = 3 };

        private static ushort U16(byte[] d, int at) => BitConverter.ToUInt16(d, at);
        private static uint U32(byte[] d, int at) => BitConverter.ToUInt32(d, at);
        private static void Put16(byte[] r, int at, int v) => BitConverter.GetBytes(checked((ushort)v)).CopyTo(r, at);
        private static void Put32(byte[] r, int at, uint v) => BitConverter.GetBytes(v).CopyTo(r, at);
        private static void Quartet(byte[] r, int at, IReadOnlyList<int> v) { for (int i = 0; i < 4; i++) Put16(r, at + i * 2, v[i]); }
        private static void Quartet(byte[] r, int at, ushort[] v) => Quartet(r, at, v.Select(x => (int)x).ToArray());

        private static byte[] Slice(byte[] d, long at, int size, string what)
        {
            if (at < 0 || at + size > d.Length) throw new InvalidDataException($"The {what} lies outside its file.");
            return d.Skip((int)at).Take(size).ToArray();
        }

        private static List<byte[]> BuildRecords()
        {
            byte[] arm9 = File.ReadAllBytes(RomInfo.arm9Path);
            byte[] Ov(int n) { if (OverlayUtils.IsCompressed(n)) OverlayUtils.Decompress(n); return File.ReadAllBytes(OverlayUtils.GetPath(n)); }
            byte[] ov12 = Ov(12), ov80 = Ov(80), ov115 = Ov(115), ov117 = Ov(117);

            byte[] genders = Slice(arm9, GenderOffset, ClassCount, "gender table");
            byte[] comboMap = Slice(arm9, TrainerComboOffset, TrainerComboCount * 2, "trainer-to-combo table");
            var comboRows = Enumerable.Range(0, TrainerComboCount).Select(i => U16(comboMap, i * 2)).ToArray();
            if (!comboRows.SequenceEqual(ExpectedTrainerComboRows))
                throw new InvalidDataException("The trainer-to-combo table is not the retail US HeartGold/SoulSilver one, which the patch's builder needs.");
            var comboByClass = comboRows.ToDictionary(r => r & 0x3FF, r => r >> 10);

            byte[] comboBlob = Slice(arm9, ComboTableOffset, ComboCount * 4, "combo table");
            var combos = Enumerable.Range(0, ComboCount).Select(i => (Effect: U16(comboBlob, i * 4), Music: U16(comboBlob, i * 4 + 2))).ToArray();
            if (!combos.Select(c => c.Effect).SequenceEqual(ExpectedComboEffects))
                throw new InvalidDataException("The combo table's effects are not the retail US HeartGold/SoulSilver ones.");

            byte[] eyeBlob = Slice(arm9, EyeMusicOffset, EyeMusicCount * 6, "eye-contact music table");
            var eyeRows = Enumerable.Range(0, EyeMusicCount).Select(i => (Class: U16(eyeBlob, i * 6), Main: U16(eyeBlob, i * 6 + 2), Alt: U16(eyeBlob, i * 6 + 4))).ToArray();
            if (!eyeRows.Select(e => e.Class).SequenceEqual(ExpectedEyeMusicClasses))
                throw new InvalidDataException("The eye-contact music table's classes are not the retail US HeartGold/SoulSilver ones.");
            var eyeByClass = eyeRows.ToDictionary(e => (int)e.Class, e => (e.Main, e.Alt));

            byte[] prizeBlob = Slice(ov12, PrizeTableAddress - Ov12Base, ClassCount * 4, "prize table");
            var prizeRows = Enumerable.Range(0, ClassCount).Select(i => (Class: (int)U16(prizeBlob, i * 4), Coefficient: U16(prizeBlob, i * 4 + 2))).ToArray();
            if (!prizeRows.Select(p => p.Class).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, ClassCount)))
                throw new InvalidDataException("The prize table does not hold each trainer class 0 to 128 once.");
            var prizeByClass = prizeRows.ToDictionary(p => p.Class, p => p.Coefficient);

            byte[] brainTable = Slice(ov80, FrontierBrainTableAddress - Ov80Base, 6 * 0x0C, "Frontier Brain table");

            var records = new List<byte[]>();
            for (int c = 0; c < ClassCount; c++)
            {
                var r = new byte[RecordSize];
                Put16(r, 0x00, genders[c]);
                Put16(r, 0x02, prizeByClass[c]);
                var (eyeMain, eyeAlt) = eyeByClass.TryGetValue(c, out var e) ? e : (DefaultEyeMusic, DefaultEyeMusic);
                Put16(r, 0x04, eyeMain);
                Put16(r, 0x06, eyeAlt);
                int comboId = comboByClass.TryGetValue(c, out int cb) ? cb : DefaultCombo;
                var (effect, battleMusic) = combos[comboId];
                Put16(r, 0x08, battleMusic);

                if (Vs20RecordByEffect.TryGetValue(effect, out uint vs20))
                {
                    byte[] s = Slice(ov115, vs20 - Ov115Base, 0x14, "style 1 recipe");
                    Put16(r, 0x0A, 1);
                    bool savedRival = U16(s, 0x08) == 0x17;
                    Put16(r, 0x0C, savedRival ? 0 : (int)U32(s, 0x04));
                    r[0x0E] = (byte)(savedRival ? 1 : 0);
                    Put32(r, 0x10, U32(s, 0x00));
                    Quartet(r, 0x24, new[] { (int)s[0x0C], s[0x0D], s[0x0E], s[0x0F] });
                    Put16(r, 0x16, s[0x10]); Put16(r, 0x18, s[0x11]); Put16(r, 0x1E, s[0x12]);
                    Quartet(r, 0x2C, StandardSymbols);
                }
                else if (Vs8RecordByEffect.TryGetValue(effect, out uint vs8))
                {
                    byte[] s = Slice(ov115, vs8 - Ov115Base, 0x08, "style 2 recipe");
                    Put16(r, 0x0A, 2);
                    Put16(r, 0x0C, U16(s, 0x06));
                    Put16(r, 0x14, s[0x03]);
                    int portrait = U16(s, 0x00);
                    Quartet(r, 0x24, new[] { portrait, portrait + 1, portrait + 2, portrait + 3 });
                    Quartet(r, 0x16, Vs8BackgroundByEffect[effect]);
                    Quartet(r, 0x2C, StandardSymbols);
                }
                else if (RocketAdminRecordByEffect.TryGetValue(effect, out uint admin))
                {
                    byte[] s = Slice(ov117, admin - Ov117Base, 0x08, "style 3 recipe");
                    Put16(r, 0x0A, 3);
                    Put16(r, 0x0C, (int)U32(s, 0x04));
                    Quartet(r, 0x24, new[] { (int)s[0], s[1], s[2], s[3] });
                    Put16(r, 0x16, 0xD7); Put16(r, 0x18, 0xD8); Put16(r, 0x1E, 0xD9); Put16(r, 0x20, 0xDA); Put16(r, 0x22, 0xDD);
                }
                else if (c == 47 && effect == 45) { Put16(r, 0x0A, 4); Put16(r, 0x16, 0xA6); Put16(r, 0x18, 0xA7); Put16(r, 0x1E, 0xA8); }
                else if (c == 109 && effect == 46) { Put16(r, 0x0A, 5); Quartet(r, 0x16, new[] { 0x01, 0x0A, 0x0C, 0x0B }); }
                else if ((c == 55 || c == 62) && effect == 39) { Put16(r, 0x0A, 6); Quartet(r, 0x16, new[] { 0x03, 0x9C, 0x9E, 0x9D }); }
                else { Quartet(r, 0x16, Style0Small); Quartet(r, 0x24, Style0Large); }

                if (FrontierBrainRowByClass.TryGetValue(c, out int row))
                {
                    Array.Clear(r, 0x0A, RecordSize - 0x0A);
                    byte[] s = brainTable.Skip(row * 0x0C).Take(0x0C).ToArray();
                    Put16(r, 0x0A, 13);
                    Put16(r, 0x0C, (int)U32(s, 0x00));
                    Quartet(r, 0x24, new[] { (int)s[4], s[5], s[6], s[7] });
                    Put16(r, 0x16, s[8]); Put16(r, 0x18, s[9]); Put16(r, 0x1E, s[10]);
                    Quartet(r, 0x2C, FrontierSymbols);
                }
                records.Add(r);
            }
            return records;
        }

        /// <summary>Packs the records the way the builder does and writes both the packed and the unpacked archive.</summary>
        private static void WriteArchive(List<byte[]> records)
        {
            var gmif = new List<byte>();
            var fat = new List<byte>();
            foreach (var rec in records)
            {
                int start = gmif.Count;
                gmif.AddRange(rec);
                fat.AddRange(BitConverter.GetBytes(start));
                fat.AddRange(BitConverter.GetBytes(gmif.Count));
                while (gmif.Count % 4 != 0) gmif.Add(0xFF);
            }
            var btaf = Encoding.ASCII.GetBytes("BTAF").Concat(BitConverter.GetBytes(12 + fat.Count)).Concat(BitConverter.GetBytes((ushort)records.Count))
                .Concat(BitConverter.GetBytes((ushort)0)).Concat(fat).ToArray();
            var btnf = Encoding.ASCII.GetBytes("BTNF").Concat(BitConverter.GetBytes(16)).Concat(BitConverter.GetBytes(4)).Concat(BitConverter.GetBytes(0x00010000)).ToArray();
            var gmifChunk = Encoding.ASCII.GetBytes("GMIF").Concat(BitConverter.GetBytes(8 + gmif.Count)).Concat(gmif).ToArray();
            int total = 16 + btaf.Length + btnf.Length + gmifChunk.Length;
            var narc = Encoding.ASCII.GetBytes("NARC").Concat(BitConverter.GetBytes((ushort)0xFFFE)).Concat(BitConverter.GetBytes((ushort)0x0100))
                .Concat(BitConverter.GetBytes(total)).Concat(BitConverter.GetBytes((ushort)16)).Concat(BitConverter.GetBytes((ushort)3))
                .Concat(btaf).Concat(btnf).Concat(gmifChunk).ToArray();

            var (packed, unpacked) = RomInfo.gameDirs[RomInfo.DirNames.trainerClassMetadata];
            File.WriteAllBytes(packed, narc);
            if (Directory.Exists(unpacked))
            {
                foreach (var f in Directory.GetFiles(unpacked)) File.Delete(f);
                for (int i = 0; i < records.Count; i++) File.WriteAllBytes(Path.Combine(unpacked, i.ToString("D4")), records[i]);
            }
        }
    }
}
