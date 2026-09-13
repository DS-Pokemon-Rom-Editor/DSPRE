using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>The graphics browser's names for the location banner, fonts and Substitute doll match their files.</summary>
    [Collection("rom")]
    public class NamedGraphicsTests
    {
        private readonly ITestOutputHelper _out;
        public NamedGraphicsTests(ITestOutputHelper o) => _out = o;

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "ADAE", "Diamond" },
            new object[] { "CPUE", "Platinum" },
            new object[] { "IPKE", "HeartGold" },
        };

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        private static GraphicAssets.Kind? KindFor(string part) => part switch
        {
            "Drawing" => GraphicAssets.Kind.TileGraphic,
            "Colours" => GraphicAssets.Kind.Palette,
            "Arrangement" => GraphicAssets.Kind.TileMap,
            _ => null,
        };

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void TheLocationBannerIsTheGamesOwnBannerArchive(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));

            if (name == "Diamond")
            {
                Assert.False(gameDirs.ContainsKey(DirNames.areaWindowGraphics), "Diamond keeps its banner as a loose file");
                return;
            }

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.areaWindowGraphics);
            var narc = new ScriptNarc(DirNames.areaWindowGraphics);
            Assert.True(narc.Available);
            Assert.Equal(18, narc.Count);

            var units = GraphicUnits.AreaWindows(archive, narc.Count);
            Assert.Equal(9, units.Count);
            foreach (var u in units)
                foreach (var p in u.Parts)
                    Assert.Equal(KindFor(p.Name), GraphicAssets.Identify(GraphicAssets.Unsqueeze(narc.Get(p.Index))));
            _out.WriteLine($"{name}: " + string.Join(", ", units.Select(u => u.Name)));
            Assert.DoesNotContain(GraphicAssets.All, a => a.Dir == DirNames.dynamicHeaders);
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void TheFontNamesMatchTheFontFiles(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));

            var narc = new ScriptNarc(DirNames.fonts);
            Assert.True(narc.Available);
            var names = NamedArchives.Names(DirNames.fonts);
            Assert.Equal(narc.Count, names.Count);

            int checkedKinds = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var (thing, part) = BattleObjects.Split(names[i]);
                var wanted = KindFor(part);
                if (wanted == null) continue;
                Assert.Equal(wanted, GraphicAssets.Identify(GraphicAssets.Unsqueeze(narc.Get(i))));
                Assert.False(string.IsNullOrEmpty(NamedArchives.Friendly(DirNames.fonts, thing)));
                checkedKinds++;
            }
            _out.WriteLine($"{name}: {names.Count} font files, {checkedKinds} drawings and colours checked");
            // HeartGold keeps one more set of font colours than Diamond, Pearl and Platinum.
            Assert.Equal(name == "HeartGold" ? 5 : 4, checkedKinds);
            Assert.True(NamedArchives.ColoursFor(DirNames.fonts, names.ToList().FindIndex(n => n == "font_special_chars_NCGR")) >= 0);
        }

        [SkippableTheory]
        [InlineData("ADAE", "Diamond", 208)]
        [InlineData("CPUE", "Platinum", 248)]
        [InlineData("IPKE", "HeartGold", 256)]
        public void TheSubstituteDollAndShadowAreNamedButNotForms(string code, string name, int doll)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));

            var back = AlternateFormSprites.WhoOwns(doll);
            Assert.Equal("Substitute doll", back?.Form.Name);
            Assert.Equal("Back", back?.Part);
            Assert.Equal(doll + 2, AlternateFormSprites.ColoursFor(doll, false));
            Assert.Equal(doll + 2, AlternateFormSprites.ColoursFor(doll + 1, false));

            var shadow = AlternateFormSprites.WhoOwns(doll + 3);
            Assert.Equal(("Shadow", "Drawing"), (shadow?.Form.Name, shadow?.Part));
            Assert.Equal(doll + 4, AlternateFormSprites.ColoursFor(doll + 3, false));

            Assert.DoesNotContain(AlternateFormSprites.ForCurrentGame(), f => f.Name == "Substitute doll" || f.Name == "Shadow");

            var narc = new ScriptNarc(DirNames.otherPokemonBattleSprites);
            Assert.True(narc.Available);
            Assert.Equal(doll + 5, narc.Count);
        }
    }
}
