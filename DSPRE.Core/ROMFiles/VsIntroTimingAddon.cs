using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// DSPRE's own per-class VS intro timings for HeartGold and SoulSilver with the trainer class metadata patch. A
    /// marked block in the synthetic overlay holds a small helper and a table of eight timings for each of 256 classes;
    /// hooks in the intro code ask the helper for the battle's class, read from the metadata patch's active-class word.
    /// A timing of 0 keeps the game's own value. Source: Resources/ROMToolboxDB/VsIntroTiming/vs_timing.s, built with arm-none-eabi-as -mcpu=arm946e-s -mthumb.
    /// </summary>
    public sealed class VsIntroTimingAddon
    {
        public const string Marker = "VSTIMINGADD1";
        public const int Classes = 256, FieldsPerClass = 8;

        public enum Field { FlashCount, GymSlide, VsShrink, VsGap, EmblemFlight, DoorsClose, DoorsHold, DoorsOpen }

        /// <summary>The game's own value of each field, which a 0 in the table keeps.</summary>
        public static readonly int[] GameValues = { 2, 4, 6, 3, 8, 18, 5, 18 };

        /// <summary>The game's own value for a class of <paramref name="vsStyle"/>: the gym, League and executive intros flash once.</summary>
        public static int GameValue(Field field, int vsStyle) =>
            field == Field.FlashCount && vsStyle is 1 or 2 or 3 ? 1 : GameValues[(int)field];


        // Assembled from vs_timing.s: header, the GetTiming helper, the class word and the hooks.
        private static readonly byte[] Code =
        {
            0x44, 0x53, 0x56, 0x54, 0x02, 0x00, 0x54, 0x01, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x05, 0x4A, 0x12, 0x68, 0xFF, 0x2A, 0x05, 0xD8, 0xD2, 0x00, 0x12, 0x18, 0x4B, 0xA3, 0x98, 0x5C,
            0x00, 0x28, 0x00, 0xD1, 0x08, 0x00, 0x70, 0x47, 0x00, 0x00, 0x00, 0x00, 0x0D, 0xB5, 0x04, 0x20, 0x08, 0x21, 0xFF, 0xF7,
            0xED, 0xFF, 0x01, 0x00, 0x04, 0x91, 0x0D, 0xBD, 0x0E, 0xB5, 0x04, 0x20, 0x08, 0x21, 0xFF, 0xF7, 0xE5, 0xFF, 0x04, 0x90,
            0x0E, 0xBC, 0xF2, 0x20, 0x00, 0xBD, 0xC0, 0x46, 0x05, 0xB5, 0x04, 0x20, 0x08, 0x21, 0xFF, 0xF7, 0xDB, 0xFF, 0x03, 0x00,
            0x00, 0x21, 0x05, 0xBD, 0x03, 0xB5, 0x02, 0x20, 0x06, 0x21, 0xFF, 0xF7, 0xD3, 0xFF, 0x03, 0x00, 0x03, 0xBC, 0x4A, 0x08,
            0x00, 0xBD, 0xC0, 0x46, 0x03, 0xB5, 0x02, 0x20, 0x06, 0x21, 0xFF, 0xF7, 0xC9, 0xFF, 0x03, 0x00, 0x03, 0xBC, 0x0A, 0x1C,
            0x00, 0xBD, 0xC0, 0x46, 0x03, 0xB5, 0x03, 0x20, 0x03, 0x21, 0xFF, 0xF7, 0xBF, 0xFF, 0x02, 0x00, 0x03, 0xBC, 0x0A, 0x80,
            0x00, 0xBD, 0xC0, 0x46, 0x00, 0xB5, 0x01, 0x20, 0x04, 0x21, 0xFF, 0xF7, 0xB5, 0xFF, 0x01, 0x90, 0x00, 0xBD, 0xC0, 0x46,
            0x00, 0xB5, 0x05, 0x20, 0x12, 0x21, 0xFF, 0xF7, 0xAD, 0xFF, 0x02, 0x21, 0x00, 0xBD, 0xC0, 0x46, 0x00, 0xB5, 0x07, 0x20,
            0x12, 0x21, 0xFF, 0xF7, 0xA5, 0xFF, 0x02, 0x22, 0x00, 0xBD, 0xC0, 0x46, 0x50, 0x1C, 0xA8, 0x61, 0x06, 0xB5, 0x06, 0x20,
            0x05, 0x21, 0xFF, 0xF7, 0x9B, 0xFF, 0x06, 0xBC, 0x82, 0x42, 0x01, 0xD9, 0x06, 0x22, 0x00, 0xE0, 0x00, 0x22, 0xA8, 0x69,
            0x00, 0xBD, 0xC0, 0x46, 0x00, 0xB5, 0x00, 0x20, 0x02, 0x21, 0xFF, 0xF7, 0x8D, 0xFF, 0x01, 0x90, 0x00, 0xBD, 0xC0, 0x46,
            0x0A, 0xB5, 0x00, 0x20, 0x02, 0x21, 0xFF, 0xF7, 0x85, 0xFF, 0x03, 0x90, 0x0A, 0xBC, 0x0A, 0x1C, 0x00, 0xBD, 0xC0, 0x46,
            0x03, 0xB5, 0x00, 0x20, 0x01, 0x21, 0xFF, 0xF7, 0x7B, 0xFF, 0x03, 0x90, 0x03, 0xBC, 0x0A, 0x1C, 0x00, 0xBD, 0xC0, 0x46,
            0x01, 0xB5, 0x00, 0x20, 0x01, 0x21, 0xFF, 0xF7, 0x71, 0xFF, 0x02, 0x90, 0x01, 0xBC, 0x10, 0x21, 0x00, 0xBD, 0xC0, 0x46,
            0x07, 0xB5, 0x00, 0x20, 0x01, 0x21, 0xFF, 0xF7, 0x67, 0xFF, 0x04, 0x90, 0x07, 0xBC, 0x33, 0x1D, 0x00, 0xBD, 0xC0, 0x46
        };
        private const int ClassWordAt = 0x30, TableAt = 0x154;
        private const int ActiveClassInPayload = 0x78;   // TCM_CurrentTrainerClass in the v1.0.0 payload

        private sealed record Site(int Overlay, int Offset, byte[] Original, int Hook);

        // Each hook replaces four bytes of the game's code; vs_timing.s says which registers each keeps.
        private static readonly Site[] Sites =
        {
            // Overlay 117, Team Rocket grunt: the four moves of an emblem's flight, the opening flash, and the executive's.
            new(117, 0x11A, new byte[] { 0x08, 0x21, 0x00, 0x91 }, 0x34),
            new(117, 0x142, new byte[] { 0x08, 0x21, 0x00, 0x91 }, 0x34),
            new(117, 0x16C, new byte[] { 0xF2, 0x20, 0x00, 0x91 }, 0x44),
            new(117, 0x19A, new byte[] { 0x00, 0x21, 0x08, 0x23 }, 0x58),
            new(117, 0x0C0, new byte[] { 0x02, 0x20, 0x00, 0x90 }, 0xF4),
            new(117, 0x6FA, new byte[] { 0x0A, 0x1C, 0x00, 0x90 }, 0x118),
            // Overlay 115, gym leaders and the League: VS mark shrink and gap, face slide, opening flashes.
            new(115, 0x054, new byte[] { 0x4A, 0x08, 0x06, 0x23 }, 0x68),
            new(115, 0x064, new byte[] { 0x0A, 0x1C, 0x06, 0x23 }, 0x7C),
            new(115, 0x0C2, new byte[] { 0x03, 0x22, 0x0A, 0x80 }, 0x90),
            new(115, 0x47E, new byte[] { 0x04, 0x20, 0x00, 0x90 }, 0xA4),
            new(115, 0x3A2, new byte[] { 0x10, 0x21, 0x00, 0x90 }, 0x12C),
            new(115, 0xB94, new byte[] { 0x33, 0x1D, 0x00, 0x90 }, 0x140),
            // Overlay 118, Kimono Girls: the doors closing, held shut and opening, and the opening flash.
            new(118, 0x130, new byte[] { 0x12, 0x20, 0x02, 0x21 }, 0xB4),
            new(118, 0x1AC, new byte[] { 0x12, 0x20, 0x02, 0x22 }, 0xC4),
            new(118, 0x190, new byte[] { 0x50, 0x1C, 0xA8, 0x61 }, 0xD4),
            new(118, 0x10C, new byte[] { 0x02, 0x20, 0x00, 0x90 }, 0xF4),
            // Overlay 119, the six Poke Ball intros: their opening flashes.
            new(119, 0x0EC, new byte[] { 0x02, 0x20, 0x00, 0x90 }, 0xF4),
            new(119, 0x73E, new byte[] { 0x02, 0x20, 0x00, 0x90 }, 0xF4),
            new(119, 0x1034, new byte[] { 0x02, 0x20, 0x00, 0x90 }, 0xF4),
            new(119, 0x43E, new byte[] { 0x0A, 0x1C, 0x00, 0x90 }, 0x104),
            new(119, 0xB5A, new byte[] { 0x0A, 0x1C, 0x00, 0x90 }, 0x104),
            new(119, 0x136E, new byte[] { 0x0A, 0x1C, 0x00, 0x90 }, 0x104),
        };

        public static int BlockLength => (SyntheticOverlaySpace.HeaderSize + TableAt + Classes * FieldsPerClass + 3) & ~3;

        private readonly int _block;
        private readonly byte[] _table, _saved;

        private VsIntroTimingAddon(int block, byte[] table)
        {
            _block = block; _table = table; _saved = (byte[])table.Clone();
        }

        /// <summary>Why the add-on can't be used on this ROM, or null.</summary>
        public static string WhyNot()
        {
            if (gameLanguage != GameLanguages.English || (romID != "IPKE" && romID != "IPGE"))
                return "Intro timings are for English HeartGold and SoulSilver.";
            if (TrainerClassMetadataPatch.InstalledOffset() == null) return "Apply the trainer class metadata patch first.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion in the ROM Patch Toolbox first.";
            if (!ActiveClassWordMatches()) return "The trainer class metadata patch isn't the version these timings were built for.";
            return null;
        }

        // The helper reads a word inside the patch's payload, so the payload must be the one it was measured on.
        private static bool ActiveClassWordMatches()
        {
            uint? offset = TrainerClassMetadataPatch.InstalledOffset();
            if (offset == null) return false;
            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            byte[] expected = TrainerClassMetadataPatch.BuildPayload(TrainerClassMetadataPatch.SyntheticBase + offset.Value);
            return offset.Value + ActiveClassInPayload <= synth.Length
                && synth.AsSpan((int)offset.Value, ActiveClassInPayload).SequenceEqual(expected.AsSpan(0, ActiveClassInPayload));
        }

        public static int HookCount => Sites.Length;
        public static IEnumerable<int> HookedOverlays => Sites.Select(s => s.Overlay).Distinct();

        /// <summary>An overlay's file, decompressed first, as the hooks write it.</summary>
        public static string OverlayFilePath(int ov) => OverlayFile(ov);

        private static string OverlayFile(int ov)
        {
            if (OverlayUtils.IsStillCompressed(ov)) OverlayUtils.Decompress(ov);
            return OverlayUtils.GetPath(ov);
        }

        private static int FindBlock(byte[] synth) =>
            SyntheticOverlaySpace.Blocks(synth, Marker).Select(r => (int)r.Start).DefaultIfEmpty(-1).First();

        /// <summary>The installed add-on, or null when it isn't installed.</summary>
        public static VsIntroTimingAddon Load()
        {
            if (WhyNot() != null) return null;
            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            int block = FindBlock(synth);
            if (block < 0) return null;
            int at = block + SyntheticOverlaySpace.HeaderSize + TableAt;
            return new VsIntroTimingAddon(block, synth.AsSpan(at, Classes * FieldsPerClass).ToArray());
        }

        /// <summary>Places the helper and table, then points every hook site at it. Writes at once.</summary>
        public static VsIntroTimingAddon Install()
        {
            string why = WhyNot();
            if (why != null) throw new InvalidOperationException(why);
            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            if (FindBlock(synth) >= 0) return Load();

            Dictionary<int, byte[]> overlays = Sites.Select(s => s.Overlay).Distinct().ToDictionary(ov => ov, ov => File.ReadAllBytes(OverlayFile(ov)));
            foreach (Site s in Sites)
                if (!overlays[s.Overlay].AsSpan(s.Offset, s.Original.Length).SequenceEqual(s.Original))
                    throw new InvalidOperationException($"Overlay {s.Overlay} doesn't hold the game's code at 0x{s.Offset:X}, so the timings can't hook it.");

            byte[] block = new byte[BlockLength];
            Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            Code.CopyTo(block, SyntheticOverlaySpace.HeaderSize);
            uint activeClass = TrainerClassMetadataPatch.SyntheticBase + TrainerClassMetadataPatch.InstalledOffset().Value + ActiveClassInPayload;
            BitConverter.GetBytes(activeClass).CopyTo(block, SyntheticOverlaySpace.HeaderSize + ClassWordAt);

            int at = SyntheticOverlaySpace.Place(synth, block.Length, SyntheticOverlaySpace.Reserved(synth));
            if (at < 0) throw new InvalidOperationException("No free space was found in the expanded ARM9 area for the intro timings.");
            uint code = synthOverlayLoadAddress + (uint)(at + SyntheticOverlaySpace.HeaderSize);

            byte[] synthBefore = (byte[])synth.Clone();
            Dictionary<int, byte[]> overlaysBefore = overlays.ToDictionary(kv => kv.Key, kv => (byte[])kv.Value.Clone());
            block.CopyTo(synth, at);
            foreach (Site s in Sites)
            {
                uint source = OverlayUtils.OverlayTable.GetRAMAddress(s.Overlay) + (uint)s.Offset;
                PatchToolboxLogic.BuildThumbBl(source, code + (uint)s.Hook).CopyTo(overlays[s.Overlay], s.Offset);
            }
            try
            {
                File.WriteAllBytes(Filesystem.expArmPath, synth);
                foreach (KeyValuePair<int, byte[]> kv in overlays) File.WriteAllBytes(OverlayUtils.GetPath(kv.Key), kv.Value);
            }
            catch
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                foreach (KeyValuePair<int, byte[]> kv in overlaysBefore) File.WriteAllBytes(OverlayUtils.GetPath(kv.Key), kv.Value);
                throw;
            }
            return Load();
        }

        public int Get(int trainerClass, Field field) =>
            trainerClass >= 0 && trainerClass < Classes ? _table[trainerClass * FieldsPerClass + (int)field] : 0;

        public void Set(int trainerClass, Field field, int value)
        {
            if (trainerClass < 0 || trainerClass >= Classes) return;
            _table[trainerClass * FieldsPerClass + (int)field] = (byte)Math.Clamp(value, 0, 255);
        }

        public bool HasChanges => !_table.SequenceEqual(_saved);

        public byte[] Snapshot() => (byte[])_table.Clone();
        public void Restore(byte[] state) { if (state?.Length == _table.Length) state.CopyTo(_table, 0); }

        public void Save()
        {
            if (!HasChanges) return;
            DSUtils.WriteToFile(Filesystem.expArmPath, _table, (uint)(_block + SyntheticOverlaySpace.HeaderSize + TableAt));
            _table.CopyTo(_saved, 0);
        }
    }
}
