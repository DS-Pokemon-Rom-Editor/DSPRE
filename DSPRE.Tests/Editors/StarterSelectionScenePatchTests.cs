using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The DP/Pt selection scene patch shifts Thumb code, so saving the starters a second time must not
    /// shift it again. Works on an in-memory copy of the overlay; the project is never written.
    /// </summary>
    [Collection("rom")]
    public class StarterSelectionScenePatchTests
    {
        private readonly ITestOutputHelper _out;
        public StarterSelectionScenePatchTests(ITestOutputHelper o) => _out = o;

        private static readonly int[] First = { 1, 300, 493 };
        private static readonly int[] Second = { 387, 390, 393 };

        [SkippableFact]
        public void PatchingTwiceMatchesPatchingOnceAndUpdatesTheSpecies()
        {
            string platinum = TestRoms.Platinum;
            Skip.If(!Directory.Exists(platinum), "Platinum not unpacked here");
            SettingsManager.Load();
            new RomInfo("CPUE", platinum);
            Skip.If(RomInfo.starterOverlayNumber < 0, "no starter overlay for this ROM");

            byte[] original = ReadOverlayCopy(RomInfo.starterOverlayNumber);
            int codeOffset = FindCodeOffset(original);
            _out.WriteLine($"routine at 0x{codeOffset:X}, already patched: {IsPatched(original, codeOffset)}");

            byte[] once = (byte[])original.Clone();
            StarterPokemonData.PatchDpPtSelectionSceneAsm(once, First);
            Assert.True(IsPatched(once, codeOffset), "the patch did not apply");
            Assert.Equal(First, DecodeSpecies(once, codeOffset));

            byte[] twice = (byte[])original.Clone();
            StarterPokemonData.PatchDpPtSelectionSceneAsm(twice, First);
            StarterPokemonData.PatchDpPtSelectionSceneAsm(twice, First);
            Assert.Equal(once, twice);

            StarterPokemonData.PatchDpPtSelectionSceneAsm(twice, Second);
            Assert.Equal(Second, DecodeSpecies(twice, codeOffset));

            StarterPokemonData.PatchDpPtSelectionSceneAsm(twice, First);
            Assert.Equal(once, twice);
        }

        private static byte[] ReadOverlayCopy(int overlay)
        {
            string path = OverlayUtils.GetPath(overlay);
            Skip.If(!File.Exists(path), "starter overlay not found");
            if (!OverlayUtils.IsCompressed(overlay)) return File.ReadAllBytes(path);

            string temp = Path.Combine(Path.GetTempPath(), $"dspre_starter_ov_{Guid.NewGuid():N}.bin");
            try
            {
                File.Copy(path, temp);
                Skip.If(OverlayUtils.Decompress(temp, makeBackup: false) != 0, "could not decompress the overlay copy");
                return File.ReadAllBytes(temp);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static int FindCodeOffset(byte[] data)
        {
            byte[] prefix = DSUtils.StringToByteArray(RomInfo.starterGraphicsPrefix);
            var matches = DSUtils.SearchBytes(data, prefix);
            Assert.NotEmpty(matches);
            return matches[0] + prefix.Length;
        }

        private static bool IsPatched(byte[] data, int offset) =>
            data[offset] == 0x2D && data[offset + 1] == 0x18 && data[offset + 2] == 0x28 && data[offset + 3] == 0x68;

        private static int[] DecodeSpecies(byte[] data, int codeOffset)
        {
            int offset = codeOffset + 0x16 + 0xA;
            var species = new int[3];
            for (int i = 0; i < 3; i++)
            {
                int instr1 = data[offset + 4] | (data[offset + 5] << 8);
                int instr2 = data[offset + 8] | (data[offset + 9] << 8);
                int diff = instr1 & 0xFF;
                if ((instr1 & 0x800) != 0) diff = -diff;
                species[i] = 4 * (i + 1) + diff + (instr2 & 0xFF);
                offset += 0xE;
            }
            return species;
        }
    }
}
