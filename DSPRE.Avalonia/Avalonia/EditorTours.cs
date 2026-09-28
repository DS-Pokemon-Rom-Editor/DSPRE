using System;
using System.Collections.Generic;
using System.Linq;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.LogicalTree;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// One step of an editor's tour. <see cref="Target"/> is null (centred card), "toolbar", "tabs", "list",
    /// "name:X", "type:X", or "tab:Name" optionally followed by "&gt;" and a target inside that tab.
    /// </summary>
    public sealed record TourStep(string Target, string Title, string Body);

    /// <summary>First-open tours of every editor, keyed by the editor's view class.</summary>
    public static partial class EditorTours
    {
        private sealed record Tour(string Name, TourStep[] Steps);
        private static readonly Dictionary<string, Tour> All = new();

        static EditorTours()
        {
            RegisterWorld();
            RegisterPokemon();
            RegisterTrainers();
            RegisterItems();
            RegisterText();
            RegisterGraphics();
            RegisterTools();
        }

        static partial void RegisterWorld();
        static partial void RegisterPokemon();
        static partial void RegisterTrainers();
        static partial void RegisterItems();
        static partial void RegisterText();
        static partial void RegisterGraphics();
        static partial void RegisterTools();

        /// <param name="key">The editor's view class name.</param>
        private static void Add(string key, string name, params TourStep[] steps) => All[key] = new Tour(name, steps);

        private static TourStep S(string target, string title, string body) => new(target, title, body);

        public static bool Has(string key) => key != null && All.ContainsKey(key);

        /// <summary>Plays <paramref name="key"/>'s tour over <paramref name="root"/> now.</summary>
        public static void Start(Control root, string key)
        {
            if (root == null || !All.TryGetValue(key ?? "", out var tour)) return;
            var steps = new List<(Func<Control>, string, string, Action)>();
            foreach (var step in tour.Steps)
            {
                string target = step.Target;
                TabItem tab = null;
                if (target != null && target.StartsWith("tab:"))
                {
                    string rest = target.Substring(4);
                    int split = rest.IndexOf('>');
                    string tabName = split < 0 ? rest : rest.Substring(0, split);
                    target = split < 0 ? null : rest.Substring(split + 1);
                    tab = FindTab(root, tabName);
                    if (tab == null || !tab.IsVisible || !tab.IsEnabled) continue;
                }
                // Outside a tab the controls already exist, so one only another game shows can be skipped now.
                if (tab == null && target != null && target.Contains(':') && Resolve(root, target) == null) continue;
                var capturedTab = tab;
                string inner = target;
                string title = step.Title;
                Func<Control> resolve = () =>
                {
                    Control scope = capturedTab != null ? (Control)capturedTab.Content ?? capturedTab : root;
                    if (inner == null) return capturedTab?.Parent as Control;
                    var found = Resolve(scope, inner) ?? (scope != root ? Resolve(root, inner) : null);
                    if (found == null) AppLogger.Info($"Tour {key}: nothing to point at for \"{title}\" ({inner})");
                    return found;
                };
                Action onEnter = capturedTab == null ? null : () =>
                {
                    // A tab inside another tab needs its outer tabs picked too.
                    foreach (var t in capturedTab.GetLogicalAncestors().OfType<TabItem>().Reverse().Append(capturedTab))
                        if (t.Parent is TabControl tc) tc.SelectedItem = t;
                };
                steps.Add((resolve, step.Title, step.Body, onEnter));
            }
            GuidedTour.StartSteps(root, steps);
        }

        /// <summary>Wires an editor window or embedded editor to its tour: a "?" button, F1, and a one-time offer.</summary>
        public static void Attach(Control root, string key)
        {
            if (root == null || !Has(key) || Design.IsDesignMode) return;
            if (Attached.TryGetValue(root, out _)) return;
            Attached.AddOrUpdate(root, null);

            root.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.F1),
                Command = new EditorWindowChrome.RelayCommand(() => Start(root, key)),
            });

            bool buttonAdded = false;
            int visibleTicks = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (_, _) =>
            {
                if (!buttonAdded) buttonAdded = AddButton(root, key);
                if (GuidedTour.IsActive || !root.IsEffectivelyVisible || root.Bounds.Width < 200 || BusyOverlay.IsBusy(root) || StillLoading(root)) { visibleTicks = 0; return; }
                // Give the editor a moment to fill in before offering.
                if (++visibleTicks < 3) return;
                timer.Stop();
                var settings = SettingsManager.Settings;
                if (settings == null) return;
                settings.editorToursShown ??= new List<string>();
                if (settings.editorToursShown.Contains(key)) return;
                settings.editorToursShown.Add(key);
                SettingsManager.Save();
                Offer(root, key);
            };
            root.AttachedToVisualTree += (_, _) => timer.Start();
            root.DetachedFromVisualTree += (_, _) => timer.Stop();
            if (TopLevel.GetTopLevel(root) != null) timer.Start();
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> HasButtonTable = new();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Attached = new();

        /// <summary>Attaches tours to every window as it opens; a host window is keyed by the editor inside it.</summary>
        public static void Install()
        {
            Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) =>
            {
                string key = w is DSPRE.Avalonia.Views.Shell.EditorHostWindow { Content: Control hosted } ? hosted.GetType().Name : w.GetType().Name;
                Attach(w, key);
            });
        }
        private static class HasButton
        {
            public static void Add(Control c) => HasButtonTable.AddOrUpdate(c, null);
            public static bool Contains(Control c) => HasButtonTable.TryGetValue(c, out _);
        }

        /// <summary>The view model's IsLoading or IsBusy flag, so the offer never points at a half-built window.</summary>
        private static bool StillLoading(Control root)
        {
            object vm = root is DSPRE.Avalonia.Views.Shell.EditorHostWindow { Content: Control hosted } ? hosted.DataContext : root.DataContext;
            if (vm == null) return false;
            foreach (string name in new[] { "IsLoading", "IsBusy" })
                if (vm.GetType().GetProperty(name)?.GetValue(vm) is true) return true;
            return false;
        }

        /// <summary>Puts "?" at the end of the editor's first row of toolbar buttons, once the toolbar exists.</summary>
        private static bool AddButton(Control root, string key)
        {
            var toolbar = Toolbar(root);
            if (toolbar == null) return false;
            var rows = toolbar.GetVisualDescendants().OfType<Panel>().Prepend(toolbar.Child as Panel).Where(p => p != null).ToList();
            if (rows.Any(p => p.Children.OfType<Button>().Any(b => b.Content as string == "?"))) { HasButton.Add(root); return true; }
            static bool IsRow(Panel p) => p is WrapPanel || p is StackPanel { Orientation: Orientation.Horizontal };
            // The toolbar's own row, or the first row of a stacked toolbar; otherwise the last button row that is showing.
            var top = toolbar.Child as Panel;
            var row = top != null && IsRow(top) ? top
                    : (top as StackPanel)?.Orientation == Orientation.Vertical ? top.Children.OfType<Panel>().FirstOrDefault(p => IsRow(p) && p.IsVisible)
                    : rows.LastOrDefault(p => IsRow(p) && p.IsVisible && p.Children.OfType<Button>().Any());
            if (row == null) return true;
            HasButton.Add(root);
            var help = new Button { Content = "?", Padding = new Thickness(8, 2) };
            ToolTip.SetTip(help, "Tour (F1)");
            help.Click += (_, _) => Start(root, key);
            row.Children.Add(help);
            return true;
        }

        /// <summary>A small card in the editor's corner asking whether to show the tour.</summary>
        private static void Offer(Control root, string key)
        {
            var layer = OverlayLayer.GetOverlayLayer(root);
            if (layer == null) return;
            IBrush Res(string k, IBrush fallback) => root.TryFindResource(k, root.ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

            var show = new Button { Content = "Show me around", IsDefault = false };
            var later = new Button { Content = "Not now" };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(later);
            buttons.Children.Add(show);
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock { Text = "New to the " + All[key].Name + "?", FontWeight = FontWeight.SemiBold, FontSize = 14 });
            stack.Children.Add(new TextBlock { Text = "A short tour shows what each part does. " + (HasButton.Contains(root) ? "F1 or ? shows it again later." : "F1 shows it again later."), TextWrapping = TextWrapping.Wrap, Opacity = 0.9 });
            stack.Children.Add(buttons);
            var card = new Border
            {
                Width = 300,
                Background = Res("Editor.PanelBg", Brushes.Gray),
                BorderBrush = Res("SystemAccentColor", Res("Editor.Border", Brushes.DimGray)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 12),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 18, Color = Color.FromArgb(0x90, 0, 0, 0) }),
                Child = stack,
            };

            // Anchored to the editor's corner, not the window's; no background so clicks outside reach the editor.
            var holder = new Canvas { Background = null };
            holder.Children.Add(card);
            void Place()
            {
                holder.Width = layer.Bounds.Width; holder.Height = layer.Bounds.Height;
                var origin = root.TranslatePoint(new Point(0, 0), layer) ?? new Point(0, 0);
                card.Measure(new Size(300, double.PositiveInfinity));
                Canvas.SetLeft(card, Math.Max(8, origin.X + root.Bounds.Width - 300 - 16));
                Canvas.SetTop(card, Math.Max(8, origin.Y + root.Bounds.Height - card.DesiredSize.Height - 16));
            }
            void Close()
            {
                layer.Children.Remove(holder);
                root.SizeChanged -= Resized;
                root.DetachedFromVisualTree -= Detached;
            }
            void Resized(object s, SizeChangedEventArgs e) => Place();
            void Detached(object s, VisualTreeAttachmentEventArgs e) => Close();
            show.Click += (_, _) => { Close(); Start(root, key); };
            later.Click += (_, _) => Close();
            root.SizeChanged += Resized;
            root.DetachedFromVisualTree += Detached;
            layer.Children.Add(holder);
            Place();
            Dispatcher.UIThread.Post(Place, DispatcherPriority.Loaded);
        }

        // ── Finding things in an editor ───────────────────────────────────────────

        private static IEnumerable<Control> Descendants(Control root) =>
            root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible);

        private static Border Toolbar(Control root)
        {
            if (!root.TryFindResource("Editor.ToolbarBg", root.ActualThemeVariant, out var bg)) return null;
            return root.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => ReferenceEquals(b.Background, bg) && b.IsEffectivelyVisible);
        }

        private static TabItem FindTab(Control root, string name)
        {
            foreach (var tab in root.GetLogicalDescendants().OfType<TabItem>())
            {
                if (tab.Header is string s && s == name) return tab;
                if (AutomationProperties.GetName(tab) == name) return tab;
                if (tab.Header is Control header && header.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == name)) return tab;
            }
            return null;
        }

        private static Control Resolve(Control scope, string target)
        {
            if (target == "toolbar") return Toolbar(scope);
            if (target == "tabs") return Descendants(scope).OfType<TabControl>().FirstOrDefault();
            if (target == "list")
                return Descendants(scope).FirstOrDefault(c => c is ListBox || c is TreeView || c is DataGrid);
            if (target.StartsWith("name:"))
            {
                string n = target.Substring(5);
                return scope.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == n && c.IsEffectivelyVisible)
                    ?? scope.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == n && c.IsEffectivelyVisible);
            }
            if (target.StartsWith("type:"))
            {
                string t = target.Substring(5);
                return Descendants(scope).FirstOrDefault(c => c.GetType().Name == t);
            }
            return null;
        }
    }
}
