using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.ViewModels.Trainers;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Party capsule numbers match the Trainer Editor picker and the Ball Capsules carrier list.</summary>
    [Collection("rom")]
    public class TrainerCapsuleLinkTests
    {
        private readonly ITestOutputHelper _out;
        public TrainerCapsuleLinkTests(ITestOutputHelper o) => _out = o;

        private static string Project(string name) => name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;

        [SkippableTheory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void ThePickerRowIsTheNumberThePartyStoresAndCarriersAreFound(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            SettingsManager.Load();
            new RomInfo(code, Project(name));
            TrainerCapsuleCatalog.Load(again: true);
            Assert.True(TrainerCapsuleCatalog.Available);

            var names = TrainerCapsuleCatalog.Names();
            Assert.Equal(TrainerCapsules.Count + 1, names.Count);
            Assert.Equal("None", names[0]);

            var used = TrainerCapsuleCatalog.UsedBy();
            foreach (var kv in used.OrderBy(k => k.Key).Take(10))
                _out.WriteLine($"{name}: capsule {kv.Key} ({names[kv.Key]}) carried by {string.Join(", ", kv.Value)}");
            Assert.All(used.Keys, k => Assert.InRange(k, 1, TrainerCapsules.Count));

            // Every seal a capsule holds is found in the seal table and lands on the 64 pixel ball.
            int withSeals = Enumerable.Range(1, TrainerCapsules.Count).FirstOrDefault(n => !names[n].EndsWith("empty"));
            Assert.True(withSeals > 0, "no capsule holds any seal");
            var placed = TrainerCapsuleCatalog.Placements(withSeals);
            int stored = TrainerCapsules.ReadAll()[withSeals - 1].Seals.Count(s => s.Seal != 0);
            _out.WriteLine($"{name}: capsule {withSeals} places {placed.Count} of {stored} seals: {string.Join(", ", placed.Select(p => p.Seal.Name))}");
            Assert.Equal(stored, placed.Count);
            Assert.All(placed, p => { Assert.InRange(p.Left, -8, 64); Assert.InRange(p.Top, -8, 64); });

            var mon = new TrainerPartyMonViewModel(0, new(), new(), new(), new string[0], new (int, int)[0], false, false, false, true);
            mon.CapsuleNames = names;
            mon.CapsuleIndex = 5;
            Assert.Equal(5m, mon.BallSeals);
            mon.BallSeals = 0;
            Assert.Equal(0, mon.CapsuleIndex);
            Assert.Empty(mon.CapsuleStickers);
        }
    }
}
