using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The particle library finds every particle file and says what each one is for.</summary>
    [Collection("rom")]
    public class ParticleLibraryTests
    {
        private readonly ITestOutputHelper _out;
        public ParticleLibraryTests(ITestOutputHelper o) => _out = o;

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        [SkippableTheory]
        [InlineData("ADAE", "Diamond")]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void EveryParticleFileIsListedUnderWhatItIsFor(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));
            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<RomInfo.DirNames> {
                RomInfo.DirNames.wazaParticle, RomInfo.DirNames.ballParticles,
                RomInfo.DirNames.wazaEffectScripts, RomInfo.DirNames.wazaEffectSub });

            var vm = new ParticleLibraryViewModel();
            vm.Gather();
            vm.Ready();
            var rows = vm.Shown.ToList();
            foreach (var group in rows.GroupBy(r => r.Category))
                _out.WriteLine($"{name}: {group.Key} {group.Count()}, e.g. {group.First().Name}");

            Assert.Contains(ParticleLibraryViewModel.Everything, vm.Categories);
            Assert.Equal(80, rows.Count(r => r.Category == "Ball seals"));
            Assert.All(rows.Where(r => r.Category == "Ball seals"), r => Assert.True(r.Orthographic));
            Assert.Contains(rows, r => r.Category == "Poke Ball bursts" && r.Name.Contains("Master Ball", StringComparison.OrdinalIgnoreCase));
            Assert.True(rows.Count(r => r.Category == "Move animations") > 300, "move particle files should be named by their moves");
            Assert.Contains(rows, r => r.Category == "Move animations" && r.Name.Contains("Ember", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(rows, r => r.Category == "Evolution" || r.Category == "Egg hatching" || r.Category == "Other particles");

            // Opening any row reads the same file the list counted.
            var sample = rows.First(r => r.Category == "Move animations");
            var bytes = sample.Source.Get(sample.Index);
            Assert.Equal(sample.Emitters, bytes[8] | bytes[9] << 8);
        }
    }
}
