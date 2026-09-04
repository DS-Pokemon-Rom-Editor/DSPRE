using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Three editors draw a battle, and all three have to draw the gauge with the game's own pictures.
    /// Two of them used to write the name and level in a desktop font, which never looked like the game.
    ///
    /// The check that matters here is the one a build cannot make: a view asks for a picture by name, and
    /// a name that does not exist on the view model binds to nothing and silently draws an empty box.
    /// </summary>
    [Collection("rom")]
    public class AllThreeBattlePreviewsTests
    {
        private readonly ITestOutputHelper _out;
        public AllThreeBattlePreviewsTests(ITestOutputHelper o) => _out = o;

        private static string Repo
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DS_Map.sln"))) dir = dir.Parent;
                return dir?.FullName;
            }
        }

        /// <summary>The three views that draw a battle, and the view model each one binds to.</summary>
        private static readonly (string view, string viewModel)[] Previews =
        {
            (@"DSPRE.Avalonia\Avalonia\Views\Battle\BattleScreenEditorView.axaml",
             "DSPRE.Avalonia.ViewModels.Battle.BattleScreenEditorViewModel"),
            (@"DSPRE.Avalonia\Avalonia\Views\Battle\BattleSceneControl.axaml",
             "DSPRE.Avalonia.ViewModels.Battle.BattleDisplayEditorViewModel"),
            (@"DSPRE.Avalonia\Avalonia\Views\Battle\BattleScriptEditorView.axaml",
             "DSPRE.Avalonia.ViewModels.Battle.BattleScriptEditorViewModel"),
        };

        /// <summary>
        /// All three build their bars through the composer, so the name and level end up inside the
        /// bar's own picture. Drawing them over the top instead was what put a flat block of panel
        /// colour across the bar's slanted edge.
        /// </summary>
        [Fact]
        public void EveryBattlePreviewBuildsItsBarWithTheWritingInside()
        {
            Assert.NotNull(Repo);
            var models = new[]
            {
                @"DSPRE.Avalonia\Avalonia\Data\BattleScreenRenderer.cs",
                @"DSPRE.Avalonia\Avalonia\ViewModels\Battle\BattleDisplayEditorViewModel.cs",
                @"DSPRE.Avalonia\Avalonia\ViewModels\Battle\BattleScriptEditorViewModel.cs",
            };
            foreach (string m in models)
            {
                string code = File.ReadAllText(Path.Combine(Repo, m));
                Assert.True(code.Contains("BattleGaugeComposer", StringComparison.Ordinal)
                         || code.Contains("GaugeTextImages.Bar", StringComparison.Ordinal),
                            $"{Path.GetFileName(m)} still builds a bar with no writing in it");
            }

            // And nothing writes over the bars any more.
            foreach (var (view, _) in Previews)
            {
                string markup = File.ReadAllText(Path.Combine(Repo, view));
                foreach (string gone in new[] { "NameImage", "LevelImage", "HealthImage", "StatusImage" })
                    Assert.False(markup.Contains(gone, StringComparison.Ordinal),
                                 $"{Path.GetFileName(view)} still draws {gone} over the bar");
            }
        }

        /// <summary>
        /// The writing really lands in the bar: a bar built with a name differs from the same bar built
        /// with none, and only in the rows the name sits in. Comparing against the plain bar the older
        /// code drew would not show this, since that one has no writing at all either.
        /// </summary>
        [SkippableFact]
        public void ABarWithANameDiffersFromAnEmptyOneWhereTheNameGoes()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum not unpacked here");

            SettingsManager.Load();
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>
                { RomInfo.DirNames.battleObj, RomInfo.DirNames.fonts });
            BattleGaugeText.Reset();
            Assert.True(BattleGaugeText.IsAvailable, BattleGaugeText.Unavailable);

            var empty = BattleGaugeComposer.Build(BattleGaugeComposer.Kind.OpponentSingle,
                new BattleGaugeComposer.Showing { Name = "", Level = 5 });
            var named = BattleGaugeComposer.Build(BattleGaugeComposer.Kind.OpponentSingle,
                new BattleGaugeComposer.Showing { Name = "CHIMCHAR", Level = 5 });
            Assert.NotNull(empty);
            Assert.NotNull(named);
            Assert.Equal(empty.Width, named.Width);

            int changed = 0, outsideTheName = 0;
            for (int y = 0; y < named.Height; y++)
                for (int x = 0; x < named.Width; x++)
                {
                    int at = (y * named.Width + x) * 4;
                    if (empty.Rgba[at] == named.Rgba[at]
                     && empty.Rgba[at + 1] == named.Rgba[at + 1]
                     && empty.Rgba[at + 2] == named.Rgba[at + 2]) continue;
                    changed++;
                    int screenY = named.Top + y;
                    // The name block is eight tiles across and two down, so it covers rows 24 to 39.
                    if (screenY < 24 || screenY > 39) outsideTheName++;
                }

            _out.WriteLine($"{changed} pixels changed, {outsideTheName} of them outside the name's rows");
            Assert.True(changed > 150, $"only {changed} pixels changed, so the name did not go in");
            Assert.Equal(0, outsideTheName);
        }

        /// <summary>
        /// A double battle shows two bars a side instead of one, and they are different pictures in
        /// different places. Every one of them has to draw.
        /// </summary>
        [SkippableFact]
        public void ADoubleBattleShowsFourBarsAndASingleShowsTwo()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum not unpacked here");

            SettingsManager.Load();
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>
                { RomInfo.DirNames.battleObj, RomInfo.DirNames.fonts, RomInfo.DirNames.battleBg });
            BattleGaugeText.Reset();

            var renderer = new BattleScreenRenderer();
            if (!renderer.Available) { Assert.Fail("the battle backgrounds did not load, so this proved nothing"); }

            foreach (bool doubles in new[] { false, true })
            {
                var pieces = renderer.Build(new BattleScreenRenderer.Options
                {
                    PokemonName = "CHIMCHAR", Level = 5, Doubles = doubles,
                });
                var bars = pieces.Where(p => p.Name.StartsWith("HP bar", StringComparison.Ordinal)).ToList();

                Assert.Equal(doubles ? 4 : 2, bars.Count);
                foreach (var b in bars)
                    Assert.True(b.Rgba != null, $"{b.Name} did not draw: {b.Whynot}");

                // Two bars in the same place would mean one of the layouts was never used.
                var places = bars.Select(b => (b.PaintedLeft, b.PaintedTop)).ToList();
                Assert.Equal(places.Count, places.Distinct().Count());

                _out.WriteLine($"{(doubles ? "double" : "single")}: "
                             + string.Join(", ", bars.Select(b => $"{b.Name} at {b.PaintedLeft},{b.PaintedTop}")));
            }
        }

        /// <summary>
        /// Every picture a battle view asks for by name exists on the view model it binds to. A build
        /// never catches this: Avalonia resolves bindings when the window opens, and a missing one just
        /// leaves a blank where the name should be.
        /// </summary>
        [Fact]
        public void EveryPictureABattleViewAsksForExists()
        {
            Assert.NotNull(Repo);
            var assembly = typeof(BattleGaugeTextRenderer).Assembly;
            int checkedNames = 0;

            foreach (var (view, viewModelName) in Previews)
            {
                var type = assembly.GetType(viewModelName);
                Assert.True(type != null, viewModelName + " is not in the assembly");

                string markup = File.ReadAllText(Path.Combine(Repo, view));

                // Inside a list's template the bindings name the row, not the view model, so those
                // parts are taken out before looking at what is asked for.
                markup = Regex.Replace(markup, "<DataTemplate.*?</DataTemplate>", "",
                                       RegexOptions.Singleline);

                // Every plain binding, not only the ones already named like a picture: an earlier
                // version of this only looked at names ending in "Image", so a typo that changed the
                // ending sailed past it. Bindings with a dot or a bracket are left out, since those
                // reach into something else rather than naming a property here.
                var asked = Regex.Matches(markup, @"\{Binding (!?)([A-Za-z][A-Za-z0-9]*)\}")
                                 .Select(m => m.Groups[2].Value)
                                 .Distinct()
                                 .ToList();
                Assert.True(asked.Count > 0, $"{Path.GetFileName(view)} asks for no pictures at all");

                foreach (string name in asked)
                {
                    var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    Assert.True(property != null,
                                $"{Path.GetFileName(view)} asks for {name}, which {type.Name} does not have");
                    checkedNames++;
                }
                _out.WriteLine($"{Path.GetFileName(view)} -> {type.Name}: {string.Join(", ", asked)}");
            }

            Assert.True(checkedNames >= 30, $"only {checkedNames} names were checked across three views");
            _out.WriteLine($"{checkedNames} names checked, all of them present");
        }
    }
}
