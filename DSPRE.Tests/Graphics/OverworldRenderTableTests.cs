using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The render-properties table marks 3D overworlds on Diamond, Pearl and Platinum; HeartGold has none.</summary>
    [Collection("rom")]
    public class OverworldRenderTableTests
    {
        private readonly ITestOutputHelper _out;
        public OverworldRenderTableTests(ITestOutputHelper o) => _out = o;

        private static void Load(string code, string path)
        {
            new RomInfo(code, path);
            RomInfo.SetOWtable();
            RomInfo.Set3DOverworldsDict();
            RomInfo.ReadOWTable();
        }

        [SkippableTheory]
        [InlineData("ADAE", "Diamond", 193, new uint[] { 91, 92, 93, 94, 95, 96, 118, 183, 209 })]
        [InlineData("CPUE", "Platinum", 259, new uint[] { 91, 92, 93, 94, 95, 96, 118, 183, 209, 262 })]
        public void TheRenderTableIsFoundAndNamesTheModels(string code, string name, int rows, uint[] models)
        {
            string path = name == "Diamond" ? TestRoms.Diamond : TestRoms.Platinum;
            Skip.IfNot(Directory.Exists(path), $"{name} is not unpacked here");
            Load(code, path);

            Assert.True(OverworldSpriteTableExpansion.IsRenderTableAvailable, $"{name}: no render table found");
            var states = OverworldSpriteTableExpansion.ReadRenderStates();
            var threeD = states.Where(s => s.State.DrawType == 2).Select(s => s.Id).OrderBy(x => x).ToArray();
            _out.WriteLine($"{name}: {states.Count} rows, 3D ids {string.Join(",", threeD)}");

            Assert.Equal(rows, states.Count);
            Assert.Equal(models, threeD);
            Assert.True(OverworldSpriteTableExpansion.TryReadRenderState(states[0].Id, out _), $"{name}: the table's first row is not read");

            foreach (uint id in models) Assert.True(RomInfo.ow3DSpriteDict.ContainsKey(id), $"{name}: model {id} is not offered");
            for (uint id = 101; id <= 116; id++) Assert.True(RomInfo.ow3DSpriteDict.ContainsKey(id), $"{name}: variable id {id} is not offered");
            Assert.False(RomInfo.ow3DSpriteDict.ContainsKey(117));
            Assert.Equal(models.Length + 16, RomInfo.ow3DSpriteDict.Count);
        }

        [SkippableFact]
        public void HeartGoldHasNoRenderTableAndNoPhantomIds()
        {
            Skip.IfNot(Directory.Exists(TestRoms.HeartGold), "HeartGold is not unpacked here");
            Load("IPKE", TestRoms.HeartGold);

            Assert.False(OverworldSpriteTableExpansion.IsRenderTableAvailable);
            Assert.Empty(RomInfo.ow3DSpriteDict);
            Assert.DoesNotContain(RomInfo.OverworldTable.Values, v => v.spriteID == 0x3D3D);
        }
    }
}
