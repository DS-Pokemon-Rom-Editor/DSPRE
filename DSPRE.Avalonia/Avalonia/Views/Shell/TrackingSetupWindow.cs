using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.Tracking;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>Help me set it up: git, then GitHub sign-in, then a repo. Steps already done show as done.</summary>
    public sealed class TrackingSetupWindow : Window
    {
        private static TrackingSetupWindow _open;

        private readonly ChangesViewModel _vm;
        private readonly StackPanel _steps = new StackPanel { Spacing = 14, Margin = new global::Avalonia.Thickness(16) };
        private string _account, _code, _remote;
        private readonly Button _skip = new Button { Content = "Skip GitHub, local only" };
        private bool _busy;

        public static async Task ShowAsync(Window owner, ChangesViewModel vm)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }
            if (owner == null) return;
            _open = new TrackingSetupWindow(vm);
            _open.Closed += (_, _) => _open = null;
            await _open.ShowDialog(owner);
        }

        private TrackingSetupWindow(ChangesViewModel vm)
        {
            _vm = vm;
            Title = "Set up change tracking";
            Width = 620;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Button skip = _skip;
            skip.Click += async (_, _) =>
            {
                if (!_vm.IsTracking) await TrackingActions.StartAsync(this, _vm);
                Close();
            };
            Button close = new Button { Content = "Close", MinWidth = 80, IsCancel = true };
            close.Click += (_, _) => Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new global::Avalonia.Thickness(16, 0, 16, 16) };
            buttons.Children.Add(skip);
            buttons.Children.Add(close);
            ScrollViewer scroll = new ScrollViewer { Content = _steps, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            Content = DialogSizing.Body(scroll, buttons);
            DialogSizing.Grow(this, scroll);
            Opened += async (_, _) => await RebuildAsync();
        }

        private async Task RebuildAsync()
        {
            string git = GitCli.IsAvailable ? await GitCli.VersionAsync() : null;
            string gh = GitHubCli.IsAvailable ? await GitHubCli.VersionAsync() : null;
            if (gh != null) _account = await GitHubCli.AccountAsync();
            (string Name, string Email)? identity = git != null ? await GitCli.IdentityAsync(_vm.FolderPath) : null;
            _remote = git != null && _vm.IsRepository ? await GitCli.RemoteUrlAsync(_vm.FolderPath) : null;

            _steps.Children.Clear();
            _steps.Children.Add(Step(git != null && identity != null, "git",
                git == null ? "git isn't installed, or DSPRE can't find it." : identity == null ? $"git {git}. Commits need your name and email." : $"git {git}, committing as {identity.Value.Name}.",
                git == null
                    ? new Control[] { Link("Get git", "https://git-scm.com/downloads"), Locate("Locate git…", "git", p => SettingsManager.Settings.GitPath = p), Again() }
                    : identity == null ? new Control[] { Action("Set name and email…", async () => await IdentityDialog.AskAsync(this)) } : Array.Empty<Control>()));

            Control[] ghButtons;
            string ghText;
            if (gh == null)
            {
                ghText = "Uses GitHub CLI, which isn't installed or can't be found.";
                ghButtons = new Control[] { Link("Get GitHub CLI", "https://cli.github.com"), Locate("Locate gh…", "gh", p => SettingsManager.Settings.GitHubCliPath = p), Again() };
            }
            else if (_account == null)
            {
                ghText = _code == null ? "Opens your browser to sign in." : $"Enter the code {_code} in your browser.";
                List<Control> list = new List<Control> { Action("Sign in", SignInAsync) };
                if (_code != null) list.Add(Action("Copy code", async () => { if (Clipboard != null) await Clipboard.SetTextAsync(_code); }));
                ghButtons = list.ToArray();
            }
            else
            {
                ghText = $"Signed in as {_account}.";
                ghButtons = Array.Empty<Control>();
            }
            _steps.Children.Add(Step(_account != null, "Sign in to GitHub", ghText, ghButtons));

            _steps.Children.Add(Step(_vm.IsTracking, "Track this ROM",
                _vm.IsTracking ? "Tracking into " + _vm.FolderName + "." : "Make a changes folder for this ROM, or clone one in the next step.",
                _vm.IsTracking ? Array.Empty<Control>() : new Control[] { Action("Start tracking", async () => await TrackingActions.StartAsync(this, _vm)) }));

            string repoText;
            List<Control> repoButtons = new List<Control>();
            bool repoDone = false;
            if (_remote != null)
            {
                repoText = _remote;
                repoDone = true;
            }
            else if (_vm.IsTracking && !_vm.IsRepository)
            {
                repoText = "The changes folder isn't a git repo yet.";
                if (git != null) repoButtons.Add(Action("Make it one", async () => await GitCli.InitAsync(_vm.FolderPath)));
            }
            else
            {
                repoText = "Create a new private repo, or pick one of yours.";
                if (_account != null) repoButtons.Add(Action("Choose…", ChooseRepoAsync));
                repoButtons.Add(Action("Add by address…", _vm.IsTracking ? AddRemoteAsync : async () => await TrackingActions.CloneAsync(this, _vm)));
            }
            _steps.Children.Add(Step(repoDone, "Choose a repo", repoText, repoButtons.ToArray()));
            _skip.IsVisible = _remote == null;
            await _vm.RefreshViewAsync();
        }

        private async Task SignInAsync()
        {
            CliRunner.Result r = await GitHubCli.SignInAsync(code => global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                _code = code;
                await RebuildAsync();
            }));
            _code = null;
            if (!r.Success) await DialogHelper.ShowErrorAsync(r.Message, "Sign in to GitHub", this);
        }

        private async Task ChooseRepoAsync()
        {
            GitHubCli.Repo picked = await RepoPicker.AskAsync(this, _vm);
            if (picked == null) return;
            if (!_vm.IsTracking)
            {
                await TrackingActions.CloneAsync(this, _vm, picked.Url);
                return;
            }
            await ConnectAsync(picked.Url + ".git", picked.IsEmpty);
        }

        private async Task AddRemoteAsync()
        {
            string url = await DialogHelper.PromptTextAsync("Repo address", "Add a remote", "", this);
            if (!string.IsNullOrWhiteSpace(url)) await ConnectAsync(url.Trim(), true);
        }

        private async Task ConnectAsync(string url, bool empty)
        {
            if (!empty)
            {
                await DialogHelper.ShowErrorAsync("That repo already has files. Pick an empty one.", "Choose a repo", this);
                return;
            }
            CliRunner.Result r = await GitCli.SetRemoteAsync(_vm.FolderPath, url);
            if (r.Success && _vm.CanCommit) await TrackingActions.CommitAsync(this, _vm);
            if (r.Success) r = await GitCli.PushAsync(_vm.FolderPath);
            if (!r.Success) await DialogHelper.ShowErrorAsync(r.Message, "Choose a repo", this);
        }

        private Control Step(bool done, string title, string text, Control[] buttons)
        {
            Border pill = new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(3),
                Padding = new global::Avalonia.Thickness(6, 2),
                MinWidth = 48,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = done ? "DONE" : "TO DO", FontSize = 10, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            };
            pill.Bind(Border.BackgroundProperty, this.GetResourceObservable(done ? "Editor.Good" : "Editor.StatusBg"));
            StackPanel body = new StackPanel { Spacing = 4, Margin = new global::Avalonia.Thickness(12, 0, 0, 0) };
            body.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
            body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            if (buttons.Length > 0)
            {
                WrapPanel row = new WrapPanel { Margin = new global::Avalonia.Thickness(0, 4, 0, 0) };
                foreach (Control b in buttons)
                {
                    b.Margin = new global::Avalonia.Thickness(0, 0, 6, 4);
                    row.Children.Add(b);
                }
                body.Children.Add(row);
            }
            Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            grid.Children.Add(pill);
            Grid.SetColumn(body, 1);
            grid.Children.Add(body);
            return grid;
        }

        private Button Action(string label, Func<Task> run)
        {
            Button b = new Button { Content = label, IsEnabled = !_busy };
            b.Click += async (_, _) =>
            {
                _busy = true;
                b.IsEnabled = false;
                // Listing repos and signing in take seconds, so the button shows it's working.
                StackPanel working = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                working.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 36, MinWidth = 36, Height = 4, VerticalAlignment = VerticalAlignment.Center });
                working.Children.Add(new TextBlock { Text = label });
                b.Content = working;
                try { await run(); }
                catch (Exception ex)
                {
                    CrashReporter.LogHandled(ex);
                    await DialogHelper.ShowErrorAsync(ex.Message, "Set up change tracking", this);
                }
                finally
                {
                    _busy = false;
                    await RebuildAsync();
                }
            };
            return b;
        }

        private Button Link(string label, string url) => Action(label, () =>
        {
            SystemShell.OpenWithDefaultApp(url);
            return Task.CompletedTask;
        });

        private Button Again() => Action("Check again", () => Task.CompletedTask);

        private Button Locate(string label, string name, Action<string> set) => Action(label, async () =>
        {
            string path = await DialogHelper.OpenFileAsync(this, "Where is " + name + "?",
                OperatingSystem.IsWindows() ? new[] { new FilePickerFileType(name) { Patterns = new[] { name + ".exe" } } } : null);
            if (path == null || SettingsManager.Settings == null) return;
            set(path);
            SettingsManager.Save();
        });
    }

    /// <summary>The signed-in account's repos, with a row to create a new one.</summary>
    public static class RepoPicker
    {
        public static async Task<GitHubCli.Repo> AskAsync(Window owner, ChangesViewModel vm)
        {
            IReadOnlyList<GitHubCli.Repo> repos;
            try { repos = await GitHubCli.ListReposAsync(); }
            catch (InvalidOperationException ex)
            {
                await DialogHelper.ShowErrorAsync(ex.Message, "Your repos", owner);
                return null;
            }
            TaskCompletionSource<GitHubCli.Repo> result = new TaskCompletionSource<GitHubCli.Repo>();
            Window win = new Window { Title = "Choose a repo", Width = 560, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            TextBox search = new TextBox { PlaceholderText = "Search your repos", Margin = new global::Avalonia.Thickness(12, 12, 12, 6) };
            string defaultName = Path.GetFileName(vm.FolderPath ?? TrackingSetup.DefaultFolder()).Replace(' ', '-');
            TextBox newName = new TextBox { Text = defaultName, Width = 260 };
            CheckBox isPrivate = new CheckBox { Content = "Private", IsChecked = true };
            Button create = new Button { Content = "Create", IsEnabled = vm.IsTracking && vm.IsRepository };
            ToolTip.SetTip(create, vm.IsTracking ? null : "Start tracking first");
            ToolTip.SetShowOnDisabled(create, true);
            StackPanel createRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new global::Avalonia.Thickness(12, 0, 12, 8) };
            createRow.Children.Add(new TextBlock { Text = "New", VerticalAlignment = VerticalAlignment.Center });
            createRow.Children.Add(newName);
            createRow.Children.Add(isPrivate);
            createRow.Children.Add(create);
            ListBox list = new ListBox { Margin = new global::Avalonia.Thickness(12, 0) };
            void Fill()
            {
                string q = search.Text ?? "";
                list.ItemsSource = repos.Where(r => r.NameWithOwner.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Select(r => new ListBoxItem
                    {
                        Content = $"{r.NameWithOwner}   {(r.IsPrivate ? "private" : "public")}{(r.IsEmpty ? ", empty" : "")}",
                        Tag = r,
                    }).ToList();
            }
            search.TextChanged += (_, _) => Fill();
            Fill();
            Button use = new Button { Content = "Use", MinWidth = 80 }, cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
            use.Click += async (_, _) =>
            {
                if ((list.SelectedItem as ListBoxItem)?.Tag is not GitHubCli.Repo r) return;
                if (!r.IsEmpty && await GitHubCli.ManifestOfAsync(r.NameWithOwner) == null)
                {
                    await DialogHelper.ShowErrorAsync($"{r.NameWithOwner} isn't a changes repo and isn't empty.", "Choose a repo", win);
                    return;
                }
                result.TrySetResult(r);
                win.Close();
            };
            create.Click += async (_, _) =>
            {
                string name = (newName.Text ?? "").Trim();
                if (name.Length == 0) return;
                if (vm.CanCommit) await TrackingActions.CommitAsync(win, vm);
                CliRunner.Result r = await GitHubCli.CreateAsync(name, isPrivate.IsChecked == true, vm.FolderPath);
                if (!r.Success)
                {
                    await DialogHelper.ShowErrorAsync(r.Message, "Create a repo", win);
                    return;
                }
                SaveNotice.Show("Created " + name);
                await vm.RefreshViewAsync();
                result.TrySetResult(null);
                win.Close();
            };
            cancel.Click += (_, _) => { result.TrySetResult(null); win.Close(); };
            win.Closed += (_, _) => result.TrySetResult(null);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new global::Avalonia.Thickness(12) };
            buttons.Children.Add(use);
            buttons.Children.Add(cancel);
            win.Content = DialogSizing.Body(list, buttons, createRow, search);
            DialogSizing.Grow(win, list);
            await win.ShowDialog(owner);
            return await result.Task;
        }
    }
}
