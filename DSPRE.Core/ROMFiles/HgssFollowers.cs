using DSPRE.HgEngine;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// HGSS walking Pokémon. Each follower model has a row in the overlay 1 object graphics table, whose third
    /// u16 picks the sprite size and shadow, and a 4-byte entry in a/1/4/1: byte 1 keeps it out of maps that
    /// restrict tall followers, byte 2 is its motion (0, 0x10, 0x11 or 0x01 in retail).
    /// </summary>
    public static class HgssFollowers
    {
        /// <summary>SPRITE_FOLLOWER_MON_BULBASAUR; the follower sprites follow one per model.</summary>
        public const int FirstSprite = 428;
        public const int SpeciesCount = 493;

        public const ushort SmallBits = 0x4E27, SmallNoShadowBits = 0x4E26, LargeBits = 0x5208;
        public enum Size { Small, SmallNoShadow, Large, Other }

        public const byte Walks = 0x00, Hovers = 0x10, Flies = 0x11, Unknown01 = 0x01;
        public const string FemaleLabel = "Female";

        public static Size SizeOf(ushort bits) => bits switch
        {
            SmallBits => Size.Small,
            SmallNoShadowBits => Size.SmallNoShadow,
            LargeBits => Size.Large,
            _ => Size.Other,
        };

        public static ushort BitsOf(Size size, ushort other) => size switch
        {
            Size.Small => SmallBits,
            Size.SmallNoShadow => SmallNoShadowBits,
            Size.Large => LargeBits,
            _ => other,
        };

        public class Model
        {
            public int Index;
            public string Label;
            public ushort Bits;
            public bool TooTall;
            public byte Motion;
            public byte[] Param;
            public int TextureWidth, TextureHeight;
        }

        /// <summary>Null when this ROM's follower data can be edited, otherwise why not.</summary>
        public static string WhyNot()
        {
            if (gameFamily != GameFamilies.HGSS) return "Followers exist only in HeartGold and SoulSilver.";
            if (HgEngineProject.IsActive || IsHgEngineBaseProject) return "hg-engine keeps follower data in its source.";
            if (romID != "IPKE") return "Follower editing supports HeartGold (US) only.";
            if (FollowerModelTableOffset < 0 || !File.Exists(arm9Path)) return "Follower tables not found.";
            if (ARM9.CheckCompressionMark()) return "arm9 is compressed.";
            // The tables must still read as the retail ones: Bulbasaur 0, Ivysaur 1, Venusaur 2 with a female model, Unown 27 forms.
            if (Lut(FollowerModelTableOffset, 4) is not [0, 0, 1, 2]
                || Lut(FollowerFormCountTableOffset + 200 * 2, 1)[0] != 27
                || Lut(FollowerFemaleTableOffset + 2 * 2, 1)[0] != 1) return "The follower tables have been moved.";
            // A path kept from the last ROM would point at its table.
            SetOWtable();
            if (RowOffset(FirstSprite) < 0) return "Follower sprites are missing from the overworld table.";
            return null;
        }

        private static ushort[] Lut(int offset, int count)
        {
            var bytes = DSUtils.ReadFromFile(arm9Path, offset, count * 2);
            var values = new ushort[count];
            for (int i = 0; i < count; i++) values[i] = BitConverter.ToUInt16(bytes, i * 2);
            return values;
        }

        /// <summary>The models a species walks as: the base one, then its female or form models.</summary>
        public static List<(int index, string label)> ModelsOf(int species)
        {
            var list = new List<(int, string)>();
            if (species < 1 || species > SpeciesCount) return list;
            int first = Lut(FollowerModelTableOffset + species * 2, 1)[0];
            list.Add((first, "Normal"));
            if (Lut(FollowerFemaleTableOffset + (species - 1) * 2, 1)[0] != 0) list.Add((first + 1, FemaleLabel));
            else
                for (int f = 1, forms = Lut(FollowerFormCountTableOffset + (species - 1) * 2, 1)[0]; f <= forms; f++)
                    list.Add((first + f, $"Form {f}"));
            return list;
        }

        // Where the row for a sprite sits in the file holding the object graphics table, or -1.
        private static long RowOffset(int sprite)
        {
            if (string.IsNullOrEmpty(OWtablePath) || !File.Exists(OWtablePath)) return -1;
            using var reader = new BinaryReader(File.OpenRead(OWtablePath));
            reader.BaseStream.Position = OWTableOffset;
            while (reader.BaseStream.Position + 6 <= reader.BaseStream.Length)
            {
                long at = reader.BaseStream.Position;
                ushort id = reader.ReadUInt16();
                if (id == 0xFFFF) break;
                if (id == sprite) return at;
                reader.BaseStream.Position += 4;
            }
            return -1;
        }

        private static List<long> RowsUsingModelFile(ushort modelFile)
        {
            var rows = new List<long>();
            using var reader = new BinaryReader(File.OpenRead(OWtablePath));
            reader.BaseStream.Position = OWTableOffset;
            while (reader.BaseStream.Position + 6 <= reader.BaseStream.Length)
            {
                long at = reader.BaseStream.Position;
                if (reader.ReadUInt16() == 0xFFFF) break;
                if (reader.ReadUInt16() == modelFile) rows.Add(at);
                reader.BaseStream.Position += 2;
            }
            return rows;
        }

        private static string ParamPath(int model)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.followerParams });
            return Path.Combine(gameDirs[DirNames.followerParams].unpackedDir, model.ToString("D4"));
        }

        public static Model Read(int model, string label)
        {
            long row = RowOffset(FirstSprite + model);
            if (row < 0) return null;
            var rowBytes = DSUtils.ReadFromFile(OWtablePath, row, 6);
            string paramPath = ParamPath(model);
            var param = File.Exists(paramPath) ? File.ReadAllBytes(paramPath) : new byte[4];
            if (param.Length < 4) Array.Resize(ref param, 4);

            var m = new Model
            {
                Index = model, Label = label, Bits = BitConverter.ToUInt16(rowBytes, 4),
                TooTall = param[1] != 0, Motion = param[2], Param = param,
            };
            try
            {
                string art = Filesystem.GetOWSpritePath(BitConverter.ToUInt16(rowBytes, 2));
                if (File.Exists(art))
                {
                    NSBTXLoader.LoadNsbtx(new FileInfo(art), out var texs, out _);
                    if (texs?.Count > 0) { m.TextureWidth = texs[0].width; m.TextureHeight = texs[0].height; }
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is EndOfStreamException) { }
            return m;
        }

        public static void Write(Model m)
        {
            long row = RowOffset(FirstSprite + m.Index);
            if (row < 0) throw new InvalidOperationException($"No overworld row for follower model {m.Index}.");
            DSUtils.WriteToFile(OWtablePath, BitConverter.GetBytes(m.Bits), (uint)(row + 4));
            // The standing-still sprites used in scenes have rows of their own on the same model file.
            ushort modelFile = BitConverter.ToUInt16(DSUtils.ReadFromFile(OWtablePath, row + 2, 2), 0);
            foreach (long other in RowsUsingModelFile(modelFile))
                if (other != row) DSUtils.WriteToFile(OWtablePath, BitConverter.GetBytes(m.Bits), (uint)(other + 4));

            var param = (byte[])m.Param.Clone();
            param[1] = (byte)(m.TooTall ? 1 : 0);
            param[2] = m.Motion;
            File.WriteAllBytes(ParamPath(m.Index), param);
            m.Param = param;
        }
    }
}
