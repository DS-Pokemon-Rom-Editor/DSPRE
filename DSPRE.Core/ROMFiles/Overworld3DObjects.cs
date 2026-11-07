using System;
using System.Collections.Generic;
using System.IO;
using LibNDSFormats.NSBMD;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Overworld entries the game draws as a 3D model from the field effect archive, not a sprite: signposts, books,
    /// doors, shutters. Members and offsets come from the game's code, so a patched entry previews its new model.
    /// </summary>
    public static class Overworld3DObjects
    {
        /// <summary>One entry: its member in the field effect archive and its offset from its square, in field units (16 per tile).</summary>
        private readonly record struct Kind(int Member, float X, float Y, float Z);

        private const int Fx32One = 0x1000;

        private static Dictionary<uint, Kind> _kinds;
        private static readonly Dictionary<int, byte[]> Files = new Dictionary<int, byte[]>();

        /// <summary>Forgets what was read from the previous ROM.</summary>
        public static void Forget()
        {
            _kinds = null;
            Files.Clear();
        }

        public static bool IsModel(uint entry) => Kinds().ContainsKey(entry);

        /// <summary>Every overworld entry this ROM draws as a model.</summary>
        public static IEnumerable<uint> Entries => Kinds().Keys;

        /// <summary>Where the model is drawn from its square's centre, in field units.</summary>
        public static (float X, float Y, float Z) Offset(uint entry) =>
            Kinds().TryGetValue(entry, out Kind kind) ? (kind.X, kind.Y, kind.Z) : (0f, 0f, 0f);

        /// <summary>
        /// A new model for the entry with its textures bound, or null when the entry is a sprite or its member isn't a
        /// model. Never cached, because building a scene binds into it.
        /// </summary>
        public static NSBMD Load(uint entry)
        {
            if (!Kinds().TryGetValue(entry, out Kind kind)) return null;
            byte[] file = FileFor(kind.Member);
            if (file == null) return null;
            NSBMD model = NSBMDLoader.LoadNSBMD(new MemoryStream(file));
            if (model?.Models == null || model.Models.Length == 0) return null;
            try { model.MatchTextures(); }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException || ex is NullReferenceException)
            {
                AppLogger.Error($"Overworld 3D object {entry} textures: {ex.Message}");
            }
            return model;
        }

        private static byte[] FileFor(int member)
        {
            if (Files.TryGetValue(member, out byte[] cached)) return cached;
            if (!GameDirs.ContainsKey(DirNames.FieldEffectModels)) return null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.FieldEffectModels });
            string path = ArchiveMembers.PathOf(DirNames.FieldEffectModels, member);
            if (!File.Exists(path)) return null;
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 4 || BitConverter.ToUInt32(data, 0) != NSBMD.NdsTypeBmd0) return null;
            Files[member] = data;
            return data;
        }

        private static Dictionary<uint, Kind> Kinds()
        {
            if (_kinds != null) return _kinds;
            Dictionary<uint, Kind> kinds = new Dictionary<uint, Kind>();
            try
            {
                if (GameFamily == GameFamilies.DP) ReadDp(kinds);
                else if (GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.HGSS) ReadTables(kinds);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is ArgumentException)
            {
                AppLogger.WarnOnce("Overworld3DObjects.Kinds", ex);
                kinds.Clear();
            }
            _kinds = kinds;
            return _kinds;
        }

        // Platinum and HGSS: FieldEffect_InitRenderObject loads count (id, member) pairs, and each model is offset
        // from its square by the (id, x, y, z) row with its id.
        private static void ReadTables(Dictionary<uint, Kind> kinds)
        {
            RomLocator.Spot table = RomLocator.Where(RomLocator.Thing.ObjectModelTablePointer);
            RomLocator.Spot count = RomLocator.Where(RomLocator.Thing.ObjectModelCount);
            RomLocator.Spot positions = RomLocator.Where(RomLocator.Thing.ObjectModelPositionsPointer);
            if (table == null || count == null || positions == null) return;

            byte[] ov = RomLocator.ReadFile(RomLocator.Thing.ObjectModelTablePointer);
            if (count.Offset + 2 > ov.Length || table.Offset + 4 > ov.Length || positions.Offset + 4 > ov.Length)
                throw new InvalidDataException($"Overlay {table.Overlay} is too short for the code that reads the overworld model table.");
            ushort movs = BitConverter.ToUInt16(ov, count.Offset);
            if (!ThumbImm8.Is(movs, ThumbImm8.Op.Movs))
                throw new InvalidDataException($"The overworld model count in overlay {table.Overlay} is no longer a movs (0x{movs:X4}).");
            int entries = ThumbImm8.Value(movs);

            byte[] pairs = Pointed(ov, table, BitConverter.ToUInt32(ov, table.Offset), entries * 8, "overworld model table");
            Dictionary<uint, (float X, float Y, float Z)> offsets = Positions(ov, positions);
            for (int i = 0; i < entries; i++)
            {
                uint id = BitConverter.ToUInt32(pairs, 8 * i);
                int member = (int)BitConverter.ToUInt32(pairs, 8 * i + 4);
                (float x, float y, float z) = offsets.TryGetValue(id, out (float X, float Y, float Z) o) ? o : (0f, 0f, 0f);
                kinds[id] = new Kind(member, x, y, z);
            }
        }

        private static Dictionary<uint, (float X, float Y, float Z)> Positions(byte[] ov, RomLocator.Spot pointer)
        {
            const int rowSize = 16, endId = 0xFFFF;
            Dictionary<uint, (float X, float Y, float Z)> offsets = new Dictionary<uint, (float X, float Y, float Z)>();
            uint ram = BitConverter.ToUInt32(ov, pointer.Offset);
            // The table ends at the first id 0xFFFF, so it holds at least one row.
            (byte[] data, int at) = Resolve(ov, pointer, ram, rowSize, "overworld model position table");
            for (int p = at; p + rowSize <= data.Length; p += rowSize)
            {
                uint id = BitConverter.ToUInt32(data, p);
                if (id == endId) return offsets;
                offsets[id] = (BitConverter.ToInt32(data, p + 4) / (float)Fx32One, BitConverter.ToInt32(data, p + 8) / (float)Fx32One,
                    BitConverter.ToInt32(data, p + 12) / (float)Fx32One);
            }
            throw new InvalidDataException("The overworld model positions have no end row.");
        }

        // Diamond and Pearl have no table: signposts 0x5B to 0x60 take members from a six-word list, and the book (0xB7)
        // and door (0xD1) each load a member with a movs.
        private static void ReadDp(Dictionary<uint, Kind> kinds)
        {
            const uint firstSignpost = 0x5B, book = 0xB7, door = 0xD1;
            const int signposts = 6;
            const float signpostZ = -2f;   // pokediamond ov06_0224A1DC draws signposts 2 units nearer the camera

            RomLocator.Spot list = RomLocator.Where(RomLocator.Thing.SignpostModelsPointer);
            RomLocator.Spot bookLoad = RomLocator.Where(RomLocator.Thing.BookModelLoad);
            RomLocator.Spot doorLoad = RomLocator.Where(RomLocator.Thing.DoorModelLoad);
            if (list == null || bookLoad == null || doorLoad == null) return;

            byte[] ov = RomLocator.ReadFile(RomLocator.Thing.SignpostModelsPointer);
            if (list.Offset + 4 > ov.Length || bookLoad.Offset + 2 > ov.Length || doorLoad.Offset + 2 > ov.Length)
                throw new InvalidDataException($"Overlay {list.Overlay} is too short for the overworld model code.");

            byte[] members = Pointed(ov, list, BitConverter.ToUInt32(ov, list.Offset), signposts * 4, "signpost model list");
            for (int i = 0; i < signposts; i++)
                kinds[firstSignpost + (uint)i] = new Kind((int)BitConverter.ToUInt32(members, 4 * i), 0f, 0f, signpostZ);

            AddLoaded(kinds, ov, bookLoad, book);
            AddLoaded(kinds, ov, doorLoad, door);
        }

        private static void AddLoaded(Dictionary<uint, Kind> kinds, byte[] ov, RomLocator.Spot load, uint entry)
        {
            ushort op = BitConverter.ToUInt16(ov, load.Offset);
            if (!ThumbImm8.Is(op, ThumbImm8.Op.Movs) || ThumbImm8.Register(op) != 1)
            {
                AppLogger.WarnOnce("Overworld3DObjects.AddLoaded", new InvalidDataException(
                    $"The model load for overworld 0x{entry:X} in overlay {load.Overlay} is no longer a movs r1 (0x{op:X4})."));
                return;
            }
            kinds[entry] = new Kind(ThumbImm8.Value(op), 0f, 0f, 0f);
        }

        /// <summary>The bytes a pointer points at, in the overlay or the synthetic overlay, cut to <paramref name="length"/>.</summary>
        private static byte[] Pointed(byte[] ov, RomLocator.Spot pointer, uint ram, int length, string what)
        {
            (byte[] data, int at) = Resolve(ov, pointer, ram, length, what);
            byte[] bytes = new byte[length];
            Array.Copy(data, at, bytes, 0, length);
            return bytes;
        }

        private static (byte[] Data, int At) Resolve(byte[] ov, RomLocator.Spot pointer, uint ram, int length, string what)
        {
            byte[] synth = File.Exists(Filesystem.ExpArmPath) ? File.ReadAllBytes(Filesystem.ExpArmPath) : Array.Empty<byte>();
            (int region, int offset) = RamAddress.Find(ram, length, RamAddress.Overlay(pointer.Overlay, ov.Length),
                RamAddress.Synthetic(synth.Length));
            return region switch
            {
                0 => (ov, offset),
                1 => (synth, offset),
                _ => throw new InvalidDataException($"DSPRE can't find the {what} at 0x{ram:X8}."),
            };
        }
    }
}
