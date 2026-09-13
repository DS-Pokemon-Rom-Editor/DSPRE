using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Every party icon file is named for what it shows and opens the Pokémon Editor entry that owns it.</summary>
    [Collection("rom")]
    public class PokemonIconFilesTests
    {
        private readonly ITestOutputHelper _out;
        public PokemonIconFilesTests(ITestOutputHelper o) => _out = o;

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "ADAE", "Diamond", 540 },
            new object[] { "CPUE", "Platinum", 547 },
            new object[] { "IPKE", "HeartGold", 551 },
        };

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void EveryIconFileIsSomethingTheEditorCanOpen(string code, string name, int files)
        {
            string path = Project(name);
            Skip.IfNot(Directory.Exists(path), $"{name} is not unpacked here");
            new RomInfo(code, path);
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons, DirNames.personalPokeData });

            var narc = new ScriptNarc(DirNames.monIcons);
            Assert.True(narc.Available, $"{name}: no icon archive");
            Assert.Equal(files, narc.Count);

            int entries = GetPokemonNamesWithForms(GetPersonalFilesCount()).Length;
            var names = GetPokemonNames();
            for (int file = PokemonIconFiles.SharedFiles + 1; file < narc.Count; file++)
            {
                var icon = PokemonIconFiles.Describe(file);
                Assert.True(icon != null, $"{name}: icon file {file} is not described");
                Assert.InRange(icon.EditorId, 0, entries - 1);
            }
            Assert.Null(PokemonIconFiles.Describe(narc.Count));

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.monIcons);
            var units = GraphicUnits.PartyIcons(archive, narc.Count);
            var unnamed = units.Where(u => u.Name == archive.Title).SelectMany(u => u.Parts).Select(p => p.Index).ToList();
            _out.WriteLine($"{name}: {narc.Count} icons, {units.Count} rows, {entries} editor entries, unnamed files: {string.Join(",", unnamed)}");
            Assert.Equal(new[] { PokemonIconFiles.SharedFiles }, unnamed);
        }

        [SkippableFact]
        public void DiamondFormsAndEggsOpenTheirOwners()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Diamond), "Diamond is not unpacked here");
            new RomInfo("ADAE", TestRoms.Diamond);

            var unownB = PokemonIconFiles.Describe(507);
            Assert.Equal((201, "B", 201), (unownB.Species, unownB.Form, unownB.EditorId));
            Assert.Equal(500, PokemonIconFiles.Describe(537).EditorId);   // Wormadam Trash
            Assert.Equal(412, PokemonIconFiles.Describe(535).EditorId);   // Burmy Trash has no entry of its own
            var manaphyEgg = PokemonIconFiles.Describe(502);
            Assert.True(manaphyEgg.IsEgg);
            Assert.Equal(490, manaphyEgg.EditorId);
            Assert.Null(PokemonIconFiles.Describe(540));                   // Giratina's form came with Platinum
        }

        [SkippableFact]
        public void PlatinumAndHeartGoldAddTheirForms()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Platinum) && Directory.Exists(TestRoms.HeartGold), "Platinum and HeartGold are both needed");

            new RomInfo("CPUE", TestRoms.Platinum);
            Assert.Equal(501, PokemonIconFiles.Describe(540).EditorId);   // Giratina Origin
            var mow = PokemonIconFiles.Describe(546);
            Assert.Equal((479, "Mow", 507), (mow.Species, mow.Form, mow.EditorId));
            Assert.Null(PokemonIconFiles.Describe(547));

            new RomInfo("IPKE", TestRoms.HeartGold);
            var sunshine = PokemonIconFiles.Describe(550);
            Assert.Equal((421, "Sunshine", 421), (sunshine.Species, sunshine.Form, sunshine.EditorId));
            Assert.Equal("Sunny", PokemonIconFiles.Describe(547).Form);
        }
    }
}
