using System;
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
    /// <summary>
    /// Diamond and Pearl keep the gauge's letters at different tile numbers, so reading them the way
    /// Platinum and HeartGold are read gives nonsense. The editor has to fall back to the written
    /// sample: a preview that looks like a gauge but holds the wrong pictures is believed.
    /// </summary>
    [Collection("rom")]
    public class DiamondFallsBackTests
    {
        private readonly ITestOutputHelper _out;
        public DiamondFallsBackTests(ITestOutputHelper o) => _out = o;

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

        [Fact]
        public void DiamondSaysItCannotAndKeepsTheWrittenSample()
        {
            if (!Open("ADAE", TestRoms.Diamond))
            { _out.WriteLine("Diamond not unpacked here, skipped"); return; }

            _out.WriteLine("Diamond: " + (BattleGaugeTextRenderer.Unavailable ?? "reported as available"));
            Assert.False(BattleGaugeTextRenderer.IsAvailable,
                         "Diamond must not be read with the other games' tile numbers");
            Assert.False(string.IsNullOrWhiteSpace(BattleGaugeTextRenderer.Unavailable),
                         "it has to say why, so the editor can tell the user");

            // Nothing is handed out, so nothing wrong can be drawn.
            Assert.Null(BattleGaugeTextRenderer.Name("CHIMCHAR"));
            Assert.Null(BattleGaugeTextRenderer.LevelWithGender(5, BattleGaugeText.Gender.Male));
            Assert.Null(BattleGaugeTextRenderer.HealthNumbers(10, 20));
            Assert.Null(BattleGaugeTextRenderer.StatusWord(BattleGaugeText.Status.Burn));

            // And the editor falls back rather than showing a blank gauge.
            var vm = new BattleScreenEditorViewModel();
            _out.WriteLine($"the editor says real={vm.GaugeTextIsReal}, note=\"{vm.GaugeTextNote}\"");
            Assert.False(vm.GaugeTextIsReal, "the editor thinks it can draw Diamond's gauge letters");
            Assert.True(vm.HasGaugeTextNote, "the editor shows no reason why");
            Assert.False(string.IsNullOrWhiteSpace(vm.SampleLevelText),
                         "the written sample is gone too, so the gauge would show nothing at all");
        }

        /// <summary>
        /// The other two do work, so the test above is about Diamond rather than about nothing working.
        /// Without this a broken reader would make Diamond's test pass for the wrong reason.
        /// </summary>
        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void TheOtherTwoDoDrawIt(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }

            _out.WriteLine($"{name}: {BattleGaugeTextRenderer.Unavailable ?? "can be drawn"}");
            Assert.True(BattleGaugeTextRenderer.IsAvailable, BattleGaugeTextRenderer.Unavailable);
            Assert.NotNull(BattleGaugeTextRenderer.Name("CHIMCHAR"));

            var vm = new BattleScreenEditorViewModel();
            Assert.True(vm.GaugeTextIsReal, $"{name}: the editor will not draw the real letters");
            Assert.False(vm.HasGaugeTextNote, $"{name}: the editor is making excuses it should not need");
        }
    }
}
