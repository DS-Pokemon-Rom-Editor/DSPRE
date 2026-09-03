using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Shell;
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

        // ── what the welcome guide and the tour say about it ──────────────────────────

        /// <summary>
        /// The welcome guide gains a page about the unfinished editors, and only in that mode. The
        /// pages used to be a static array, so this also pins that the count and the indexes follow.
        /// </summary>
        [Fact]
        public void TheWelcomeGuideExplainsBetaOnlyWhenItApplies()
            {
            BetaEditors.Set(false);
            var off = new WelcomeViewModel();
            int plain = CountPages(off);
            Assert.False(PageTitles(off).Contains("unfinished", StringComparison.OrdinalIgnoreCase),
                         "the plain guide should not mention the unfinished editors");

            BetaEditors.Set(true);
            var on = new WelcomeViewModel();
            Assert.Equal(plain + 1, CountPages(on));

            // Second, right after the greeting, not buried at the end.
            on.PageIndex = 1;
            Assert.Contains("unfinished", on.PageTitle, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("beta editor or feature was involved", on.PageBody);
            Assert.Contains($"{BetaEditors.Count} editors", on.PageBody);
            Assert.Equal($"2 / {plain + 1}", on.PageLabel);

            // Every page is still reachable at the longer length.
            for (int k = 0; k < plain + 1; k++)
            {
                on.PageIndex = k;
                Assert.False(string.IsNullOrWhiteSpace(on.PageTitle), $"page {k} has no title");
            }
        }

        /// <summary>Every in-editor feature that is gated is named, so the guide cannot go stale.</summary>
        [Fact]
        public void EveryGatedFeatureInsideAnEditorIsWrittenDown()
        {
            string views = Path.Combine(Repo, "DSPRE.Avalonia", "Avalonia", "Views");
            if (!Directory.Exists(views))
            { Assert.Fail($"{views} is not there, so this proved nothing."); return; }

            int bound = Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories)
                .Sum(f => Regex.Matches(File.ReadAllText(f), "ShowBetaFeatures").Count);

            _out.WriteLine($"{bound} controls bound to ShowBetaFeatures, "
                         + $"{BetaEditors.Features.Count} features written down");
            Assert.True(bound > 0, "nothing is bound to ShowBetaFeatures any more");
            Assert.True(BetaEditors.Features.Count > 0, "no in-editor features are written down");
            Assert.All(BetaEditors.Features, f =>
            {
                Assert.False(string.IsNullOrWhiteSpace(f.Name));
                Assert.False(string.IsNullOrWhiteSpace(f.Where));
            });
        }

        /// <summary>The status bar says which mode you are in either way, so the tour has a target.</summary>
        [Fact]
        public void TheStatusBarNamesTheModeEitherWay()
        {
            string xaml = Path.Combine(Repo, "DSPRE.Avalonia", "Avalonia", "Views", "Shell",
                                       "MainWindowView.axaml");
            if (!File.Exists(xaml))
            { Assert.Fail($"{xaml} is not there, so this proved nothing."); return; }
            Assert.Contains("BetaNoticeText", File.ReadAllText(xaml), StringComparison.Ordinal);
        }

        private static int CountPages(WelcomeViewModel vm)
        {
            int n = 0;
            while (true)
            {
                vm.PageIndex = n;
                if (vm.PageIndex != n) break;
                n++;
            }
            return n;
        }

        private static string PageTitles(WelcomeViewModel vm)
        {
            var all = new List<string>();
            for (int k = 0; k < CountPages(vm); k++) { vm.PageIndex = k; all.Add(vm.PageTitle); }
            return string.Join(" | ", all);
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
            Assert.True(seen > 0, "no menu entry is gated at all, which cannot be right");
            Assert.True(loose.Count == 0,
                "these name a beta editor but are never disabled: " + string.Join(", ", loose));

            // A binding naming a window that is no longer gated is litter: it always reads true and
            // quietly suggests the editor is still being held back. Thirty one of these were left
            // behind when the list was trimmed from 47 to 16.
            var stale = new List<string>();
            foreach (Match m in Regex.Matches(s, @"Beta(?:Note)?\[(\w+)\]"))
            {
                string window = m.Groups[1].Value;
                if (!BetaEditors.IsBeta(window)) stale.Add(window);
            }
            Assert.True(stale.Count == 0,
                "these menu bindings name a window that is not gated any more: "
                + string.Join(", ", stale.Distinct()));
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

        /// <summary>
        /// The gate only works if every editor actually goes through it. The check above cannot see
        /// that: it reads WindowPlacement and passes whether or not anything calls it. Nine listed
        /// editors were opened with a plain Show() and skipped the gate entirely while it was green.
        /// </summary>
        [Fact]
        public void NoEditorWindowIsOpenedWithAPlainShow()
        {
            var roots = new[] { Path.Combine(Repo, "DSPRE.Avalonia"), Path.Combine(Repo, "DS_Map") };
            var files = roots.Where(Directory.Exists)
                             .SelectMany(r => Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories))
                             .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                      && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                             .ToList();
            Assert.True(files.Count > 100, $"only {files.Count} source files were read, so this proved nothing");

            // `new SomethingView(...).Show()`, across line breaks, which is how they are usually written.
            var call = new Regex(@"new\s+[\w.]*?(\w+View)\s*\((?:[^()]|\([^()]*\))*\)\s*\.Show\(\)",
                                 RegexOptions.Singleline);
            var loose = new List<string>();
            int seen = 0;
            foreach (string f in files)
            {
                string text = File.ReadAllText(f);
                foreach (Match m in call.Matches(text))
                {
                    string view = m.Groups[1].Value;
                    // The shell's own main window is not an editor: it is not gated and must not cascade.
                    if (view == "MainWindowView") continue;
                    seen++;
                    loose.Add($"{Path.GetFileName(f)}: {view}");
                }
            }

            _out.WriteLine($"{files.Count} source files read, {seen} plain Show() calls on an editor view");
            Assert.True(loose.Count == 0,
                "these open an editor without asking the gate, use ShowManaged(): " + string.Join(", ", loose));
        }


        /// <summary>
        /// Walking the map, the animated preview and the drag gizmos live inside editors that are not
        /// themselves gated, so the window gate cannot reach them. They hang off ShowBetaFeatures
        /// instead, and this is what stops that binding being dropped by accident.
        /// </summary>
        [Theory]
        [InlineData("World/EventEditorView.axaml", "Pegman")]
        [InlineData("World/EventEditorView.axaml", "StepInHere_Click")]
        [InlineData("World/EventEditorView.axaml", "AnimatedPreview_Click")]
        [InlineData("World/MapEditorView.axaml", "AnimatedPreview_Click")]
        public void TheUnfinishedPartsOfAFinishedEditorAreGated(string view, string marker)
        {
            string path = Path.Combine(Repo, "DSPRE.Avalonia", "Avalonia", "Views",
                                       view.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            { Assert.Fail($"{path} is not there, so this proved nothing."); return; }

            string xaml = File.ReadAllText(path);
            int at = xaml.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(at >= 0, $"{view} no longer mentions {marker}");

            // Walk back to the start of the element the marker sits in, then read that whole tag.
            int open = xaml.LastIndexOf('<', at);
            int close = xaml.IndexOf('>', at);
            Assert.True(open >= 0 && close > open, $"could not read the element around {marker}");
            string tag = xaml.Substring(open, close - open);

            Assert.True(tag.Contains("ShowBetaFeatures", StringComparison.Ordinal),
                $"{view}: the element carrying {marker} is not bound to ShowBetaFeatures, so it shows "
                + "in a normal build.");
        }

        /// <summary>The check above proves able to fail: the pattern it looks for really does match.</summary>
        [Fact]
        public void ThePlainShowCheckRecognisesTheShapeItLooksFor()
        {
            var call = new Regex(@"new\s+[\w.]*?(\w+View)\s*\((?:[^()]|\([^()]*\))*\)\s*\.Show\(\)",
                                 RegexOptions.Singleline);
            Assert.Matches(call, "new FontEditorView().Show()");
            Assert.Matches(call, "new Views.World.SpawnEditorView(a, b(c), d)" + "\n    .Show()");
            Assert.DoesNotMatch(call, "new FontEditorView().ShowManaged()");
        }
    }
}
