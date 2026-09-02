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
    /// <summary>Every Pokemon's battle sprites are drawn in that Pokemon's own colours.</summary>
    [Collection("rom")]
    public class BattleSpritePaletteTests
    {
        private readonly ITestOutputHelper _out;
        public BattleSpritePaletteTests(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        private const int PerSpecies = 6;      // four sprites, then the colours, then the shiny colours
        private const int NormalPalette = 4;

        [Theory]
        [MemberData(nameof(Games))]
        public void EverySpriteUsesItsOwnPokemonsColours(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var a = GraphicAssets.All.First(x => x.Dir == DirNames.pokemonBattleSprites);
            Assert.NotNull(a.ColourEntry);

            int count = GraphicAssets.Count(a);
            Assert.True(count > 100, $"{game}: the battle sprite archive holds only {count} files");

            int checkedSprites = 0;
            var wrong = new List<string>();
            for (int i = 0; i < count; i++)
            {
                if (i % PerSpecies >= NormalPalette) continue;    // the palettes themselves
                checkedSprites++;
                int want = (i / PerSpecies) * PerSpecies + NormalPalette;
                int got = a.ColourEntry(i);
                if (got != want) wrong.Add($"{i}: took {got}, should take {want}");
            }

            _out.WriteLine($"{game}: {checkedSprites} sprites checked, {wrong.Count} take the wrong colours");
            foreach (var w in wrong.Take(5)) _out.WriteLine("  " + w);
            Assert.Empty(wrong);
        }


        /// <summary>Asking for the shiny colours really changes the picture.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void AskingForShinyChangesThePicture(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            foreach (var a in GraphicAssets.All.Where(x => x.ShinyColourEntry != null))
            {
                int count = GraphicAssets.Count(a);
                int looked = 0, differed = 0;
                var noShiny = new List<string>();
                var unchangedAt = new List<int>();

                for (int i = 0; i < count && looked < 40; i++)
                {
                    var normal = GraphicAssets.Render(a, i, shiny: false);
                    if (normal?.Rgba == null || normal.Width <= 0) continue;
                    // A palette entry is drawn as its own swatch, so shiny means nothing for it.
                    if (normal.Kind == GraphicAssets.Kind.Palette) continue;
                    int shinyEntry = a.ShinyColourEntry(i);
                    if (shinyEntry < 0) { noShiny.Add(i.ToString()); continue; }
                    looked++;

                    var shiny = GraphicAssets.Render(a, i, shiny: true);
                    if (shiny?.Rgba == null) { noShiny.Add(i + " would not draw"); continue; }

                    int diff = 0;
                    for (int k = 0; k < Math.Min(normal.Rgba.Length, shiny.Rgba.Length); k++)
                        if (normal.Rgba[k] != shiny.Rgba[k]) diff++;
                    if (diff > 0) differed++; else unchangedAt.Add(i);
                }

                _out.WriteLine($"{game} / {a.Title}: {looked} drawings, {differed} looked different in "
                             + $"shiny, {unchangedAt.Count} came out identical");
                if (noShiny.Count > 0) _out.WriteLine("  no shiny set: " + string.Join(", ", noShiny.Take(6)));
                if (unchangedAt.Count > 0) _out.WriteLine("  unchanged: " + string.Join(", ", unchangedAt.Take(8)));

                Assert.True(looked >= 10, $"{game} / {a.Title}: only {looked} drawings could be read");

                // The four files of Pokemon number zero are filler: it does not exist, and its two sets of
                // colours are the same as each other, so asking for shiny changes nothing.
                Assert.All(unchangedAt, i => Assert.InRange(i, 0, 3));
                Assert.True(differed + unchangedAt.Count == looked);
            }
        }

        /// <summary>The nearest-palette rule on the same archive, so this check is shown able to fail.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void GuessingTheNearestPaletteGetsTheBackSpritesWrong(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            var a = GraphicAssets.All.First(x => x.Dir == DirNames.pokemonBattleSprites);
            int count = GraphicAssets.Count(a);

            var palettes = Enumerable.Range(0, count).Where(i => i % PerSpecies >= NormalPalette).ToList();

            int Nearest(int index)
            {
                int best = -1, bestGap = int.MaxValue;
                foreach (int i in palettes)
                {
                    int gap = Math.Abs(i - index);
                    if (i < index) gap = gap * 2 - 1;      // the old rule preferred what came before
                    if (gap < bestGap) { bestGap = gap; best = i; }
                }
                return best;
            }

            int backsWrong = 0, frontsWrong = 0, backs = 0, fronts = 0;
            for (int i = 0; i < Math.Min(count, 600); i++)
            {
                int slot = i % PerSpecies;
                if (slot >= NormalPalette) continue;
                int want = (i / PerSpecies) * PerSpecies + NormalPalette;
                bool ok = Nearest(i) == want;
                if (slot < 2) { backs++; if (!ok) backsWrong++; }
                else { fronts++; if (!ok) frontsWrong++; }
            }

            _out.WriteLine($"{game}: guessing got {backsWrong} of {backs} back sprites wrong and "
                         + $"{frontsWrong} of {fronts} front sprites wrong");
            Assert.True(backs > 50 && fronts > 50, $"{game}: too few sprites to compare");

            // Guessing gets every back sprite wrong but the very first Pokemon's two, which have no earlier
            // palette to be misled by.
            Assert.Equal(backs - 2, backsWrong);
            Assert.Equal(0, frontsWrong);
        }
    }
}
