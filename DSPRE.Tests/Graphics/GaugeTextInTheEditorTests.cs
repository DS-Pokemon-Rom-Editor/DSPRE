using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Battle;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The Battle Screen editor draws every game's gauge letters from its own ROM.</summary>
    [Collection("rom")]
    public class GaugeTextInTheEditorTests
    {
        private readonly ITestOutputHelper _out;
        public GaugeTextInTheEditorTests(ITestOutputHelper o) => _out = o;

        private static bool Open(string code, string project)
        {
            if (!Directory.Exists(project)) return false;
            SettingsManager.Load();
            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>
                { RomInfo.DirNames.fonts, RomInfo.DirNames.battleObj });
            BattleGaugeTextRenderer.Reset();
            return true;
        }

        [SkippableTheory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        [InlineData("ADAE", "Diamond")]
        public void TheEditorDrawsTheGamesOwnLetters(string code, string name)
        {
            string project = name switch { "Platinum" => TestRoms.Platinum, "Diamond" => TestRoms.Diamond, _ => TestRoms.HeartGold };
            Skip.If(!Open(code, project), $"{name} not unpacked here");

            _out.WriteLine($"{name}: {BattleGaugeTextRenderer.Unavailable ?? "can be drawn"}, {BattleGaugeText.Where()}");
            Assert.True(BattleGaugeTextRenderer.IsAvailable, BattleGaugeTextRenderer.Unavailable);
            Assert.NotNull(BattleGaugeTextRenderer.Name("CHIMCHAR"));
            Assert.NotNull(BattleGaugeTextRenderer.LevelWithGender(5, BattleGaugeText.Gender.Male));
            Assert.NotNull(BattleGaugeTextRenderer.HealthNumbers(10, 20));
            Assert.NotNull(BattleGaugeTextRenderer.StatusWord(BattleGaugeText.Status.Burn));

            var vm = new BattleScreenEditorViewModel();
            Assert.True(vm.GaugeTextIsReal, $"{name}: the editor will not draw the real letters");
            Assert.False(vm.HasGaugeTextNote, $"{name}: the editor shows a fallback note");
        }
    }
}
