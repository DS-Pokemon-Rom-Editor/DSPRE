using System;
using System.Collections.Generic;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using DSPRE.Avalonia.Views;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// First-time guided tour of the main window: dims the UI and spotlights one region at a
    /// time (header list, map tabs, the main editor menus) with a small callout card. Starts
    /// automatically after the first successful ROM load; replay via Tools &gt; Guided Tour.
    /// The spotlight hole is not dimmed and stays clickable, so users can try the highlighted
    /// control mid-tour.
    /// </summary>
    public sealed class GuidedTour
    {
        private sealed class Step
        {
            public Func<Control> Target;   // null (or a resolver returning null) => centered card, no spotlight
            public string Title;
            public string Body;
            public Action OnEnter;         // runs once when the step becomes current (e.g. switch the live tab)
        }

        private static GuidedTour _active;

        private readonly MainWindowView _main;
        private readonly Canvas _layer;
        private readonly List<Step> _steps;
        private int _index;
        private int _sizeRetries;

        public static bool IsActive => _active != null;

        public static void Start(MainWindowView main)
        {
            if (main == null || _active != null) return;
            var layer = main.FindControl<Canvas>("TourLayer");
            if (layer == null) return;

            // Whether it gets finished or skipped, the tour no longer auto-starts after this.
            if (SettingsManager.Settings != null && !SettingsManager.Settings.guidedTourShown)
            {
                SettingsManager.Settings.guidedTourShown = true;
                SettingsManager.Save();
            }

            _active = new GuidedTour(main, layer);
            _active.Begin();
        }

        private GuidedTour(MainWindowView main, Canvas layer)
        {
            _main = main;
            _layer = layer;
            var maps = main.FindControl<MapsWorkspaceView>("Maps");

            // Helper: a step that switches the Maps workspace to a specific tab and spotlights the
            // tab area, so the tour walks through each embedded editor showing the real thing.
            Step Tab(int index, string title, string body) => new Step
            {
                OnEnter = () =>
                {
                    var tabs = maps?.FindControl<TabControl>("MapTabs");
                    if (tabs != null && index < tabs.ItemCount) tabs.SelectedIndex = index;
                },
                Target = () => maps?.FindControl<Control>("MapTabs"),
                Title = title,
                Body = body,
            };

            _steps = new List<Step>
            {
                new Step
                {
                    Target = () => maps?.FindControl<Control>("Sidebar"),
                    Title = "The header list",
                    Body = "Every location in the game is a map \"header\", and they are all listed here. " +
                           "Click one to load it. Typing in the search box filters the list, which is handy " +
                           "because there are hundreds of them."
                },
                new Step
                {
                    Target = () => maps?.FindControl<Control>("ContextStrip"),
                    Title = "The selected header",
                    Body = "This strip shows which header is selected and which game files it points to: " +
                           "matrix, events, scripts, text and so on. Save header writes just the header's " +
                           "own fields; Save ROM builds a playable .nds from everything saved so far."
                },

                // ── One step per Maps-workspace tab, switching the live tab as we go ──
                Tab(0, "Tab 1 of 9: Header",
                    "The header is this location's settings record: music, weather, camera angle, " +
                    "location name, flags, and which game files (matrix, scripts, text…) it uses."),
                Tab(1, "Tab 2 of 9: Map",
                    "The 3D terrain and buildings. Paint movement collisions and terrain types, move " +
                    "buildings around, and switch the view mode to see all of this header's maps " +
                    "stitched together."),
                Tab(2, "Tab 3 of 9: Events",
                    "Everything placed ON the map: NPCs (overworlds), warps between maps, script " +
                    "triggers and ground items. This is where you populate the world."),
                Tab(3, "Tab 4 of 9: Matrix",
                    "The grid that stitches individual maps into the seamless overworld. Each cell " +
                    "points at one map file and sets its elevation."),
                Tab(4, "Tab 5 of 9: Area Data",
                    "Which texture pack and building set this area draws from; change it to re-skin " +
                    "an area."),
                Tab(5, "Tab 6 of 9: Encounters",
                    "The wild Pokémon of this area: which species appear in grass, water and caves, " +
                    "at what levels and rates."),
                Tab(6, "Tab 7 of 9: Scripts",
                    "Event logic: what happens when the player talks to an NPC or steps on a trigger. " +
                    "Scripts reference the text you'll see in the next tabs."),
                Tab(7, "Tab 8 of 9: Level Scripts",
                    "Scripts that run automatically when the map loads, used for weather, one-time " +
                    "cutscenes and map setup."),
                Tab(8, "Tab 9 of 9: Text",
                    "The dialogue and signs used by this area. Every piece of game text lives in an " +
                    "archive like this one."),

                // ── The menus: what lives where ──
                new Step
                {
                    Target = () => main.FindControl<Control>("PokemonMenu"),
                    Title = "The Pokémon menu",
                    Body = "Edit the creatures themselves here: species stats and learnsets (Pokémon " +
                           "Editor), move data, TM/HM assignments, egg moves, in-game trades, and wild " +
                           "encounters (grass/surf, special encounters, headbutt trees)."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("TrainersMenu"),
                    Title = "The Trainers menu",
                    Body = "Everyone the player battles: parties and properties in the Trainer Editor, " +
                           "and each trainer class's overworld sprite in the Trainer Sprite Editor."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("ItemsMenu"),
                    Title = "The Items menu",
                    Body = "Edit items here: the Item Editor covers each item's data and icon, and Item " +
                           "Tables covers where items come from: the Pickup ability's loot, hidden " +
                           "ground items, and HGSS Rock Smash drops."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("TextMenu"),
                    Title = "The Text menu",
                    Body = "Edit words and logic here: all game text in the Text Editor, and the same " +
                           "Script / Level Script editors you saw as tabs, as standalone windows."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("WorldMenu"),
                    Title = "The World menu",
                    Body = "Edit world structure here: the same map tools you just toured as tabs, plus " +
                           "extras like the Building and Camera editors, fly/spawn points and the " +
                           "Advanced Header Search."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("GraphicsMenu"),
                    Title = "The Graphics menu",
                    Body = "Standalone art editors: the title screen, dungeon cutin splashes, the " +
                           "trainer card, overworld sprites and NSBTX textures."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("ToolsMenu"),
                    Title = "The Tools menu",
                    Body = "Power tools: the ROM Patch Toolbox, Validation & Where-Used, Music & Battle " +
                           "Tables, the Game Icon & Banner editor, NARC utilities and Settings.\n\n" +
                           "Tip: press Ctrl+P anywhere and type an editor's name to open it instantly."
                },
                new Step
                {
                    Target = () => BetaEditors.Enabled
                        ? main.FindControl<Control>("BetaNoticeText") : null,
                    Title = "You are running the unfinished editors",
                    Body = BetaSummary(),
                },
                new Step
                {
                    OnEnter = () =>
                    {
                        var tabs = maps?.FindControl<TabControl>("MapTabs");
                        if (tabs != null) tabs.SelectedIndex = 0;
                    },
                    Target = null,
                    Title = "That's the tour!",
                    Body = "You are ready to start hacking. You can replay this tour anytime from " +
                           "Tools > Guided Tour, and the written guide is under Tools > Welcome & Tutorial."
                },
            };

            // Only when the unfinished editors are switched on. It goes second from last, so it is
            // the thing people read just before they start, and it has the status-bar line to point at.
            if (BetaEditors.Enabled)
            {
                _steps.Insert(_steps.Count - 1, new Step
                {
                    Target = () => main.FindControl<Control>("BetaNoticeText"),
                    Title = "You are running the unfinished editors",
                    Body = BetaSummary(),
                });
            }
        }

        /// <summary>What is switched on, short enough to fit a callout card.</summary>
        private static string BetaSummary()
        {
            var areas = new List<string>();
            foreach (var a in BetaEditors.CountByArea()) areas.Add($"{a.Value} in {a.Key}");

            var features = new List<string>();
            foreach (var f in BetaEditors.Features) features.Add(f.Name);

            return $"{BetaEditors.Count} editors that are normally hidden are available to you: "
                 + string.Join(", ", areas) + ".\n\n"
                 + "Inside editors that are finished, these parts are not: "
                 + string.Join(", ", features).ToLowerInvariant() + ".\n\n"
                 + "They can write a project you cannot open again, so back up first. If you report "
                 + "a problem, please say whether a beta editor or feature was involved. The full "
                 + "list is in Tools > Welcome & Tutorial.";
        }
        private void Begin()
        {
            _index = 0;
            _steps[0].OnEnter?.Invoke();
            _layer.IsVisible = true;
            _main.SizeChanged += OnMainResized;
            _main.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            // The layer was collapsed until now, so its Bounds are still empty; render after
            // the layout pass that IsVisible=true just scheduled.
            global::Avalonia.Threading.Dispatcher.UIThread.Post(Render,
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private void End()
        {
            _main.SizeChanged -= OnMainResized;
            _main.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _layer.Children.Clear();
            _layer.IsVisible = false;
            _active = null;

            // The tab-walk steps switch the live Maps tab; don't leave the user parked on a
            // random one if they bailed out mid-tour.
            var tabs = _main.FindControl<MapsWorkspaceView>("Maps")?.FindControl<TabControl>("MapTabs");
            if (tabs != null) tabs.SelectedIndex = 0;
        }

        private void OnMainResized(object sender, SizeChangedEventArgs e) => Render();

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { End(); e.Handled = true; }
            else if (e.Key == Key.Right || e.Key == Key.Enter) { Next(); e.Handled = true; }
            else if (e.Key == Key.Left) { Back(); e.Handled = true; }
        }

        private void Next()
        {
            if (_index >= _steps.Count - 1) { End(); return; }
            _index++;
            _steps[_index].OnEnter?.Invoke();
            Render();
        }

        private void Back()
        {
            if (_index == 0) return;
            _index--;
            _steps[_index].OnEnter?.Invoke();
            Render();
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        private IBrush Res(string key, Color fallback)
        {
            if (_main.TryFindResource(key, _main.ActualThemeVariant, out var v) && v is IBrush b) return b;
            return new SolidColorBrush(fallback);
        }

        private void Render()
        {
            _layer.Children.Clear();
            var step = _steps[_index];

            var layerSize = _layer.Bounds.Size;
            if (layerSize.Width <= 0 || layerSize.Height <= 0)
            {
                // Layout has not caught up yet; try again on the next loop iteration (bounded).
                if (_active == this && _sizeRetries++ < 10)
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(Render,
                        global::Avalonia.Threading.DispatcherPriority.Background);
                return;
            }
            _sizeRetries = 0;
            var full = new Rect(layerSize);

            // Resolve the spotlight rectangle in layer coordinates (null => centered card only).
            Rect? hole = null;
            var target = step.Target?.Invoke();
            if (target != null && target.IsVisible && target.Bounds.Width > 0)
            {
                var origin = target.TranslatePoint(new Point(0, 0), _layer);
                if (origin.HasValue)
                {
                    hole = new Rect(origin.Value, target.Bounds.Size).Inflate(4)
                        .Intersect(full);
                }
            }

            // Dim everything except the spotlight hole. The dimmed Path is hit-test visible
            // (blocks clicks); the hole is a geometry cutout, so the target stays clickable.
            Geometry dimGeometry = hole.HasValue
                ? new CombinedGeometry(GeometryCombineMode.Exclude,
                    new RectangleGeometry(full), new RectangleGeometry(hole.Value))
                : new RectangleGeometry(full);
            _layer.Children.Add(new Path
            {
                Data = dimGeometry,
                Fill = new SolidColorBrush(Color.FromArgb(0xA8, 0x00, 0x00, 0x00)),
            });

            var accent = Res("SystemAccentColor", Color.FromRgb(0x4F, 0x9D, 0xFF)) is ISolidColorBrush sb
                ? (IBrush)sb : new SolidColorBrush(Color.FromRgb(0x4F, 0x9D, 0xFF));

            if (hole.HasValue)
            {
                var ring = new Border
                {
                    Width = hole.Value.Width,
                    Height = hole.Value.Height,
                    BorderBrush = accent,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(ring, hole.Value.X);
                Canvas.SetTop(ring, hole.Value.Y);
                _layer.Children.Add(ring);
            }

            _layer.Children.Add(BuildCallout(step, full, hole));
        }

        private Control BuildCallout(Step step, Rect full, Rect? hole)
        {
            const double calloutWidth = 330;

            var title = new TextBlock
            {
                Text = step.Title, FontSize = 15, FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
            };
            var body = new TextBlock
            {
                Text = step.Body, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Opacity = 0.95,
            };

            var backBtn = new Button { Content = "← Back", MinWidth = 70, IsEnabled = _index > 0 };
            backBtn.Click += (_, _) => Back();
            bool last = _index == _steps.Count - 1;
            var nextBtn = new Button { Content = last ? "Finish" : "Next →", MinWidth = 70 };
            nextBtn.Click += (_, _) => Next();
            var skipBtn = new Button { Content = "Skip tour", Opacity = 0.75 };
            skipBtn.Click += (_, _) => End();

            var buttons = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var navRight = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            navRight.Children.Add(backBtn);
            navRight.Children.Add(nextBtn);
            DockPanel.SetDock(skipBtn, Dock.Left);
            buttons.Children.Add(skipBtn);
            buttons.Children.Add(navRight);

            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(title);
            stack.Children.Add(body);
            stack.Children.Add(new TextBlock
            {
                Text = $"{_index + 1} / {_steps.Count}", FontSize = 11, Opacity = 0.6,
            });
            stack.Children.Add(buttons);

            var card = new Border
            {
                Width = calloutWidth,
                Background = Res("Editor.PanelBg", Color.FromRgb(0x2B, 0x2B, 0x2B)),
                BorderBrush = Res("Editor.Border", Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    OffsetX = 0, OffsetY = 4, Blur = 18,
                    Color = Color.FromArgb(0x90, 0, 0, 0),
                }),
                Child = stack,
            };

            // Place below the spotlight if it fits, then above; for holes spanning most of the
            // window height (like the sidebar), sit beside them instead. Centered when no target.
            card.Measure(new Size(calloutWidth, double.PositiveInfinity));
            double h = card.DesiredSize.Height;
            double x, y;
            if (hole.HasValue)
            {
                var hv = hole.Value;
                if (hv.Bottom + 10 + h <= full.Height)
                {
                    x = Math.Max(8, Math.Min(hv.X, full.Width - calloutWidth - 8));
                    y = hv.Bottom + 10;
                }
                else if (hv.Y - h - 10 >= 8)
                {
                    x = Math.Max(8, Math.Min(hv.X, full.Width - calloutWidth - 8));
                    y = hv.Y - h - 10;
                }
                else
                {
                    x = hv.Right + 10 + calloutWidth <= full.Width
                        ? hv.Right + 10
                        : Math.Max(8, hv.X - calloutWidth - 10);
                    y = Math.Min(Math.Max(8, hv.Y + 10), Math.Max(8, full.Height - h - 8));
                }
            }
            else
            {
                x = (full.Width - calloutWidth) / 2;
                y = (full.Height - h) / 2;
            }
            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, y);
            return card;
        }
    }
}
