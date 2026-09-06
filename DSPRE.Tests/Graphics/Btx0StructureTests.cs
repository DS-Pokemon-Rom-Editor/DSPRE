using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.LibNDSFormats;
using Xunit;

namespace DSPRE.Tests
{
    [Collection("rom")]
    public class Btx0StructureTests
    {
        [Fact]
        public void Inspect_ReportsDictionaryReuseAndImportSheet()
        {
            byte[] file = BuildFile(secondTextureOffsetUnits: 0);

            Assert.True(Btx0Structure.TryInspect(file, out Btx0Structure structure, out string error), error);
            Assert.Equal(2, structure.Textures.Count);
            Assert.Equal(1, structure.UniqueTextureBlockCount);
            Assert.Equal(0, structure.Textures[0].OffsetGroup);
            Assert.Equal(0, structure.Textures[1].OffsetGroup);
            Assert.Equal(8, structure.Textures[0].Width);
            Assert.Equal(8, structure.Textures[0].Height);
            Assert.Equal(3, structure.Textures[0].Format);
            Assert.Equal("frame0", structure.Textures[0].Name);
            Assert.Equal(8, structure.SheetWidth);
            Assert.Equal(16, structure.SheetHeight);
            Assert.Single(structure.Palettes);
            Assert.Equal(16, structure.Palettes[0].ColorCapacity);
        }

        [Fact]
        public void ProfileComparison_DistinguishesOffsetReuse()
        {
            Assert.True(Btx0Structure.TryInspect(BuildFile(0), out Btx0Structure reused, out string reusedError), reusedError);
            Assert.True(Btx0Structure.TryInspect(BuildFile(4), out Btx0Structure distinct, out string distinctError), distinctError);

            Assert.False(reused.HasSameProfileAs(distinct));
            Assert.False(distinct.HasSameProfileAs(reused));
            Assert.Equal(1, reused.UniqueTextureBlockCount);
            Assert.Equal(2, distinct.UniqueTextureBlockCount);
        }

        [Fact]
        public void Inspect_RejectsTextureOutsideDeclaredData()
        {
            byte[] file = BuildFile(secondTextureOffsetUnits: 9);

            Assert.False(Btx0Structure.TryInspect(file, out Btx0Structure structure, out string error));
            Assert.Null(structure);
            Assert.Contains("Texture 1", error);
        }

        [Fact]
        public void Inspect_RejectsTruncatedDictionary()
        {
            byte[] file = BuildFile(secondTextureOffsetUnits: 0);
            Array.Resize(ref file, 130);

            Assert.False(Btx0Structure.TryInspect(file, out _, out string error));
            Assert.Contains("truncated", error, StringComparison.OrdinalIgnoreCase);
        }

        [SkippableFact]
        public void InspectorAcceptsEveryReferencedPlatinumOverworldBtx() =>
            InspectReferencedOverworlds("CPUE", TestRoms.Platinum, "Platinum");

        [SkippableFact]
        public void InspectorAcceptsEveryReferencedHeartGoldOverworldBtx() =>
            InspectReferencedOverworlds("IPKE", TestRoms.HeartGold, "HeartGold");

        private static void InspectReferencedOverworlds(string code, string project, string game)
        {
            Skip.IfNot(Directory.Exists(project), $"The {game} test ROM project is not available.");
            new RomInfo(code, project);
            RomInfo.Set3DOverworldsDict();
            RomInfo.SetOWtable();
            RomInfo.ReadOWTable();

            string directory = RomInfo.gameDirs[RomInfo.DirNames.OWSprites].unpackedDir;
            var failures = new List<string>();
            int checkedCount = 0;
            foreach (uint member in RomInfo.OverworldTable.Values.Select(v => v.spriteID).Distinct())
            {
                string path = Path.Combine(directory, member.ToString("D4"));
                if (!File.Exists(path)) continue;
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 4 || data[0] != 'B' || data[1] != 'T' || data[2] != 'X' || data[3] != '0')
                    continue;

                checkedCount++;
                if (!Btx0Structure.TryInspect(data, out _, out string error))
                    failures.Add($"member {member}: {error}");
            }

            Assert.True(checkedCount > 0, $"{game}: no referenced BTX files were checked.");
            Assert.True(failures.Count == 0, $"{game}: {string.Join("; ", failures.Take(8))}");
        }

        private static byte[] BuildFile(ushort secondTextureOffsetUnits)
        {
            const int tex0 = 20;
            const int textureDictionary = 80;
            const int paletteDictionary = 152;
            const int textureData = 192;
            const int paletteData = 256;
            const int fileSize = 308;

            var file = new byte[fileSize];
            WriteId(file, 0, "BTX0");
            WriteU32(file, 8, fileSize);
            WriteU16(file, 12, 16);
            WriteU16(file, 14, 1);
            WriteU32(file, 16, tex0);

            WriteId(file, tex0, "TEX0");
            WriteU32(file, tex0 + 4, fileSize - tex0);
            WriteU16(file, tex0 + 12, 8); // 64 bytes of 4bpp texture data.
            WriteU16(file, tex0 + 14, textureDictionary);
            WriteU32(file, tex0 + 20, textureData);
            WriteU16(file, tex0 + 48, 4); // 32 bytes, or 16 colours.
            WriteU16(file, tex0 + 52, paletteDictionary);
            WriteU32(file, tex0 + 56, paletteData);

            int texDictionary = tex0 + textureDictionary;
            file[texDictionary + 1] = 2;
            int texEntries = texDictionary + 16 + 2 * 4;
            WriteU16(file, texEntries, 0);
            WriteU16(file, texEntries + 2, 3 << 10);
            WriteU16(file, texEntries + 8, secondTextureOffsetUnits);
            WriteU16(file, texEntries + 10, 3 << 10);
            WriteName(file, texEntries + 16, "frame0");
            WriteName(file, texEntries + 32, "frame1");

            int palDictionary = tex0 + paletteDictionary;
            file[palDictionary + 1] = 1;
            int palEntries = palDictionary + 16 + 4;
            WriteU16(file, palEntries, 0);
            WriteName(file, palEntries + 4, "palette0");
            return file;
        }

        private static void WriteId(byte[] destination, int offset, string id)
        {
            for (int i = 0; i < id.Length; i++)
                destination[offset + i] = (byte)id[i];
        }

        private static void WriteName(byte[] destination, int offset, string name)
        {
            for (int i = 0; i < name.Length; i++)
                destination[offset + i] = (byte)name[i];
        }

        private static void WriteU16(byte[] destination, int offset, int value) =>
            BitConverter.GetBytes((ushort)value).CopyTo(destination, offset);

        private static void WriteU32(byte[] destination, int offset, int value) =>
            BitConverter.GetBytes((uint)value).CopyTo(destination, offset);
    }
}
