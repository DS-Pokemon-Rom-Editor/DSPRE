using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    [Collection("rom")]
    public class BuildingModelTextureSetRomTests
    {
        [SkippableFact]
        public void PlatinumTablesAssociateModelsWithTextureSets()
            => Check("CPUE", TestRoms.Platinum, expectIndoor: false);

        [SkippableFact]
        public void HeartGoldTablesAssociateOutdoorAndIndoorModelsWithTextureSets()
            => Check("IPKE", TestRoms.HeartGold, expectIndoor: true);

        [SkippableFact]
        public void PlatinumBrowserStartsWithTheRomsSetInsteadOfEveryPack()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Platinum), "The Platinum test ROM project is not available.");
            new RomInfo("CPUE", TestRoms.Platinum);
            var use = BuildingModelTextureSets.ReadCurrentRom()
                .First(s => !s.Indoor && s.ModelIds.Count > 0);
            int modelId = use.ModelIds[0];

            var browser = new ModelBrowserViewModel();
            browser.Scan();
            browser.Publish();
            browser.Selected = browser.Shown.First(i =>
                i.Archive.Dir == RomInfo.DirNames.exteriorBuildingModels && i.Index == modelId);

            Assert.True(browser.TextureChoices.Count > 1);
            Assert.StartsWith($"ROM uses set {use.TextureSetId}", browser.TextureChoices[1]);
            Assert.Equal(1, browser.TextureChoice);
            Assert.True(browser.TextureChoices.Count < ModelAssets.TextureSetCount(
                ModelAssets.All.First(a => a.Dir == RomInfo.DirNames.exteriorBuildingModels)) + 1,
                "The browser still offered every texture pack.");

            browser.Selected = null;
            Assert.Empty(browser.TextureChoices);
            Assert.Empty(browser.AnimationChoices);
            Assert.Empty(browser.SlideChoices);
            Assert.Equal("Pick something on the left to see it.", browser.Details);
        }

        /// <summary>Diamond's set 43 is the no-buildings stand-in, used only by area 47, and can still be replaced.</summary>
        [SkippableFact]
        public void DiamondSetFortyThreeIsTheNoBuildingsStandIn()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Diamond), "The Diamond test ROM project is not available.");
            new RomInfo("ADAE", TestRoms.Diamond);

            var textures = ModelAssets.All.First(a => a.Dir == RomInfo.DirNames.buildingTextures);
            var narc = new ScriptNarc(textures.Dir);
            Assert.True(narc.Available);
            byte[] set43 = narc.Get(43);

            Assert.True(BuildingModelTextureSets.IsNoTexturesStandIn(set43));
            Assert.Equal(new[] { 47 }, BuildingModelTextureSets.AreasThatNeverReadSet(43));
            Assert.Equal(ModelAssets.Kind.Empty, ModelAssets.Identify(set43));
            Assert.Null(ModelAssets.CannotImportBecause(textures, 43));
            Assert.Contains("area 47", ModelAssets.WhatCanBeDone(textures, 43).ShowNote);

            // Every other set is a real texture set.
            for (int i = 0; i < narc.Count; i++)
                if (i != 43) Assert.Equal(ModelAssets.Kind.TextureBundle, ModelAssets.Identify(narc.Get(i)));
        }

        [SkippableFact]
        public void PlatinumReusedSetFortyThreeForRealTextures()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Platinum), "The Platinum test ROM project is not available.");
            new RomInfo("CPUE", TestRoms.Platinum);
            Assert.Empty(BuildingModelTextureSets.AreasThatNeverReadSet(43));
        }

        private static void Check(string code, string path, bool expectIndoor)
        {
            Skip.IfNot(Directory.Exists(path), $"The {code} test ROM project is not available.");
            new RomInfo(code, path);

            var sets = BuildingModelTextureSets.ReadCurrentRom();
            var withModels = sets.Where(s => s.ModelIds.Count > 0).ToList();

            Assert.NotEmpty(sets);
            Assert.True(withModels.Sum(s => s.ModelIds.Count) > 100, "Too few model associations were read.");
            Assert.All(sets, s =>
            {
                Assert.NotEmpty(s.AreaIds);
                Assert.True(File.Exists(Filesystem.GetBuildingTexturePath(s.TextureSetId)));
            });
            if (expectIndoor) Assert.Contains(withModels, s => s.Indoor);
        }
    }
}
