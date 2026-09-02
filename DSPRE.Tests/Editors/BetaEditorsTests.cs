using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The editors still being tried out: that the switch turns them on and off, that nothing outside
    /// the list is caught by it, and that every name in the list is a window that really exists.
    /// </summary>
    public class BetaEditorsTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly bool _was;

        public BetaEditorsTests(ITestOutputHelper o)
        {
            _out = o;
            _was = BetaEditors.Enabled;
        }

        // The switch is global, so whatever it was before a test has to be put back after it.
        public void Dispose() => BetaEditors.Set(_was);

        private static string Repo =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        [Fact]
        public void WithoutTheSwitchABetaEditorIsNotAllowedAndAnOrdinaryOneIs()
        {
            BetaEditors.Set(false);
            Assert.False(BetaEditors.Allows("FontEditorView"));
            Assert.True(BetaEditors.Allows("MapEditorView"));
            Assert.True(BetaEditors.Allows("PokemonEditorView"));
        }

        [Fact]
        public void WithTheSwitchEverythingIsAllowed()
        {
            BetaEditors.Set(true);
            foreach (string w in BetaEditors.All) Assert.True(BetaEditors.Allows(w));
        }

        [Fact]
        public void TheSwitchIsReadOffTheCommandLine()
        {
            BetaEditors.ReadFrom(new[] { "somefile.nds", "--beta" });
            Assert.True(BetaEditors.Enabled);

            BetaEditors.ReadFrom(new[] { "--BETA" });      // however it is typed
            Assert.True(BetaEditors.Enabled);

#if DEBUG
            // A build made for working on DSPRE always has them, switch or no switch.
            BetaEditors.ReadFrom(new[] { "somefile.nds" });
            Assert.True(BetaEditors.Enabled);
            BetaEditors.ReadFrom(null);
            Assert.True(BetaEditors.Enabled);
#else
            BetaEditors.ReadFrom(new[] { "somefile.nds" });
            Assert.False(BetaEditors.Enabled);
            BetaEditors.ReadFrom(Array.Empty<string>());
            Assert.False(BetaEditors.Enabled);
            BetaEditors.ReadFrom(null);
            Assert.False(BetaEditors.Enabled);
#endif
        }

        [Fact]
        public void ADebugBuildStartsWithThemAvailable()
        {
#if DEBUG
            // No switch given, and they are still available, which is what a developer's run gets.
            BetaEditors.Set(false);
            BetaEditors.ReadFrom(Array.Empty<string>());
            Assert.True(BetaEditors.Enabled);
#else
            BetaEditors.ReadFrom(Array.Empty<string>());
            Assert.False(BetaEditors.Enabled);
#endif
        }

        [Fact]
        public void SomethingNotInTheListIsNeverBeta()
        {
            BetaEditors.Set(false);
            Assert.False(BetaEditors.IsBeta("MapEditorView"));
            Assert.False(BetaEditors.IsBeta(""));
            Assert.False(BetaEditors.IsBeta(null));
            Assert.True(BetaEditors.Allows("MapEditorView"));
            Assert.Null(BetaEditors.WhyNot("MapEditorView"));
        }

        [Fact]
        public void TheReasonNamesTheEditorAndSaysOnlyThatItIsNotReady()
        {
            BetaEditors.Set(false);
            string why = BetaEditors.WhyNot("TilesetBuilderView");
            Assert.Contains("Picture to Background", why);
            Assert.Contains("not available yet", why);
            // How to switch them on is deliberately not said to everybody.
            Assert.DoesNotContain("--beta", why);
            Assert.DoesNotContain("beta", why, StringComparison.OrdinalIgnoreCase);

            BetaEditors.Set(true);
            Assert.Null(BetaEditors.WhyNot("TilesetBuilderView"));
        }

        // ── the list against the code ─────────────────────────────────────────────────────────────

        [Fact]
        public void EveryNameInTheListIsAWindowThatExists()
        {
            string views = Path.Combine(Repo, "DSPRE.Avalonia", "Avalonia", "Views");
            if (!Directory.Exists(views))
            { Assert.Fail($"{views} is not there, so this proved nothing."); return; }

            var onDisk = Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = BetaEditors.All.Where(w => !onDisk.Contains(w)).ToList();
            _out.WriteLine($"{BetaEditors.Count} editors listed, {onDisk.Count} windows on disk.");
            Assert.True(missing.Count == 0,
                "these are in the beta list but have no window: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryMenuEntryForABetaEditorIsActuallyGated()
        {
            string xaml = Path.Combine(Repo, "DSPRE.Avalonia", "Avalonia", "Views", "Shell",
                                       "MainWindowView.axaml");
            if (!File.Exists(xaml))
            { Assert.Fail($"{xaml} is not there, so this proved nothing."); return; }

            string s = File.ReadAllText(xaml);
            // Every entry that names a beta window must also carry a check, either the gate itself or a
            // feature property. An entry naming one with no IsEnabled at all would open it from the menu.
            var loose = new List<string>();
            int seen = 0;
            foreach (Match m in Regex.Matches(s, @"<MenuItem\b[^>]*?/>|<MenuItem\b(?![^>]*?/>)[^>]*?>",
                                              RegexOptions.Singleline))
            {
                string tag = m.Value;
                var key = Regex.Match(tag, @"Beta(?:Note)?\[(\w+)\]");
                if (!key.Success) continue;
                seen++;
                if (!tag.Contains("IsEnabled=")) loose.Add(key.Groups[1].Value);
            }

            _out.WriteLine($"{seen} menu entries name a beta editor.");
            Assert.True(seen > 15, $"only {seen} menu entries were found to be gated, which is too few");
            Assert.True(loose.Count == 0,
                "these name a beta editor but are never disabled: " + string.Join(", ", loose));
        }

        [Fact]
        public void TheGateIsAskedWhereEveryEditorWindowIsShown()
        {
            // Menus are not the only way in: the command palette and buttons inside other editors open
            // windows too, so the one place they all pass through has to ask as well.
            string p = Path.Combine(Repo, "DSPRE.Avalonia", "Avalonia", "WindowPlacement.cs");
            if (!File.Exists(p))
            { Assert.Fail($"{p} is not there, so this proved nothing."); return; }
            string s = File.ReadAllText(p);
            Assert.Contains("BetaEditors.Allows", s);
            Assert.Contains("ShowManaged", s);
        }
    }
}
