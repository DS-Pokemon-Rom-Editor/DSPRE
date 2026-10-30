using System;
using System.Collections.Generic;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using DSPRE.Avalonia.Views;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// The main window's tour after the first ROM load, replayed from Tools &gt; Guided tour. It dims the window and lights
    /// one part at a time, which stays clickable. Editors run their own tours (<see cref="StartEditor"/>).
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

        private readonly Control _root;
        private readonly MainWindowView _main;   // null for an editor tour
        private readonly Canvas _layer;
        private TopLevel _host;
        private readonly List<Step> _steps;
        private int _index;
        private int _sizeRetries;

        public static bool IsActive => _active != null;

        public static void Start(MainWindowView main)
        {
            if (main == null || _active != null) return;
            Canvas layer = main.FindControl<Canvas>("TourLayer");
            if (layer == null) return;

            // Whether it gets finished or skipped, the tour no longer auto-starts after this.
            if (SettingsManager.Settings != null && !SettingsManager.Settings.GuidedTourShown)
            {
                SettingsManager.Settings.GuidedTourShown = true;
                SettingsManager.Save();
            }

            _active = new GuidedTour(main, layer);
            _active.Begin();
        }

        /// <summary>Plays an editor's tour over <paramref name="root"/>, in that editor's own window.</summary>
        public static void StartSteps(Control root, List<(Func<Control> target, string title, string body, Action onEnter)> steps)
        {
            if (root == null || _active != null || steps.Count == 0) return;
            OverlayLayer overlay = OverlayLayer.GetOverlayLayer(root);
            if (overlay == null) return;
            Canvas layer = new Canvas { Background = null };
            overlay.Children.Add(layer);

            List<Step> list = new List<Step>();
            foreach ((Func<Control> target, string title, string body, Action onEnter) in steps) list.Add(new Step { Target = target, Title = title, Body = body, OnEnter = onEnter });
            _active = new GuidedTour(root, layer, list);
            _active.Begin();
        }

        private GuidedTour(Control root, Canvas layer, List<Step> steps)
        {
            _root = root;
            _layer = layer;
            _steps = steps;
        }

        private GuidedTour(MainWindowView main, Canvas layer)
        {
            _root = main;
            _main = main;
            _layer = layer;
            MapsWorkspaceView maps = main.FindControl<MapsWorkspaceView>("Maps");

            // Helper: a step that switches the Maps workspace to a specific tab and spotlights the
            // tab area, so the tour walks through each embedded editor showing the real thing.
            Step Tab(int index, string title, string body) => new Step
            {
                OnEnter = () =>
                {
                    TabControl tabs = maps?.FindControl<TabControl>("MapTabs");
                    if (tabs != null && index < tabs.ItemCount) tabs.SelectedIndex = index;
                },
                Target = () => maps?.FindControl<Control>("MapTabs"),
                Title = title,
                Body = body,
            };

            // Beta editors are hidden from the menus when off, so the menu steps only name them when on.
            bool beta = BetaEditors.Enabled;

            _steps = new List<Step>
            {
                new Step
                {
                    Target = () => maps?.FindControl<Control>("Sidebar"),
                    Title = "The header list",
                    Body = "Every place in the game is a map header, all listed here. Click one to load it, or type in " +
                           "the search box to find one."
                },
                new Step
                {
                    Target = () => maps?.FindControl<Control>("ContextStrip"),
                    Title = "The selected header",
                    Body = "The picked header and the files it points to: matrix, events, scripts, text and the rest. " +
                           "Save header writes the header's fields; Save ROM builds a playable .nds from everything saved."
                },

                // ── One step per Maps-workspace tab, switching the live tab as we go ──
                Tab(0, "Tab 1 of 9: Header",
                    "This place's settings: music, weather, camera, name, flags, and the files it uses."),
                Tab(1, "Tab 2 of 9: Map",
                    "The 3D terrain and buildings. Paint where the player can walk, move buildings, and switch " +
                    "the view to see all of this header's maps together."),
                Tab(2, "Tab 3 of 9: Events",
                    "Everything placed on the map: people, warps, triggers and items on the ground.\n\n" +
                    "A field that shows a link button, here or in other editors, opens the editor it refers to: " +
                    "right-click and pick Open, or Ctrl+click."),
                Tab(3, "Tab 4 of 9: Matrix",
                    "The grid that joins maps into one world. Each cell picks a map and its height."),
                Tab(4, "Tab 5 of 9: Area data",
                    "Which texture pack and building set this area uses; change it to reskin the area."),
                Tab(5, "Tab 6 of 9: Encounters",
                    "This area's wild Pokémon in grass, water and caves, with their levels and rates."),
                Tab(6, "Tab 7 of 9: Scripts",
                    "What happens when the player talks to someone or steps on a trigger. Scripts show the text " +
                    "from the Text tab."),
                Tab(7, "Tab 8 of 9: Level scripts",
                    "Scripts that run by themselves when the map loads, for weather, one-time cutscenes and setup."),
                Tab(8, "Tab 9 of 9: Text",
                    "This area's dialogue and signs. All game text lives in archives like this one."),

                // ── The menus: what lives where ──
                new Step
                {
                    Target = () => main.FindControl<Control>("FileMenu"),
                    Title = "The File menu",
                    Body = "Open a ROM or project, save a playable .nds with Save ROM, or build and run it in your " +
                           "emulator with F5. hg-engine projects have a submenu here to link, compile and review patches."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("PokemonMenu"),
                    Title = "The Pokémon menu",
                    Body = "Species with their learnsets and sprites, move data" + (beta ? " and animations" : "") +
                           ", TMs, egg moves, trades, starters, wild Pokémon, and special encounters like headbutt " +
                           "trees, the Bug Contest and the Safari Zone."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("TrainersMenu"),
                    Title = "The Trainers menu",
                    Body = beta
                        ? "Everyone the player battles: parties, trainer sprites, rematches, the Pokégear phone book, " +
                          "trainer flags and the Battle Tower."
                        : "Everyone the player battles: parties, rematches, trainer flags and the Battle Tower."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("ItemsMenu"),
                    Title = "The Items menu",
                    Body = $"Each item's data and icon, {(beta ? "what the marts sell, " : "")}and where items come from: " +
                           "Pickup, hidden items and Rock Smash in HeartGold and SoulSilver."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("TextMenu"),
                    Title = "The Text menu",
                    Body = "All game text, the script editors you saw as tabs, custom script commands, " +
                           $"{(beta ? "the font and " : "")}the character map."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("WorldMenu"),
                    Title = "The World menu",
                    Body = "The map tabs as full windows, plus buildings, Fly and spawn points, " +
                           $"{(beta ? "Platinum's Distortion World " : "")}and Advanced header search."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("GraphicsMenu"),
                    Title = "The Graphics menu",
                    Body = beta
                        ? "Every picture and model in the game, plus battle scenes, the title screen, the trainer card, " +
                          "the game icon, overworld sprites, particles and more."
                        : "Every picture and model in the game, plus the overworld sprites and the textures " +
                          "of maps and buildings."
                },
                new Step
                {
                    Target = () => main.FindControl<Control>("ToolsMenu"),
                    Title = "The Tools menu",
                    Body = $"The ROM patch toolbox, {(beta ? "validation and where used, " : "")}overlays, music and battle tables, " +
                           "exports, archive tools and Settings.\n\n" +
                           "Press Ctrl+P anywhere and type an editor's name to open it. Right-click a menu item to pin it " +
                           "beside the menus, and right-click the pin to take it off."
                },
                new Step
                {
                    // The bar is empty after the prompt is declined, so the card sits centred instead.
                    Target = () => main.FindControl<Control>("TrackingBar") is { Bounds.Height: > 0 } bar ? bar : null,
                    Title = "Track changes",
                    Body = "This bar shows whether your edits are being written down. " +
                           "Tools > Welcome & tutorial has a page that explains how it works."
                },
                new Step
                {
                    OnEnter = () =>
                    {
                        TabControl tabs = maps?.FindControl<TabControl>("MapTabs");
                        if (tabs != null) tabs.SelectedIndex = 0;
                    },
                    Target = null,
                    Title = "That's the tour",
                    Body = "You're ready to start. Replay the tour from Tools > Guided tour; the written guide is in " +
                           "Tools > Welcome & tutorial."
                },
            };

            // Beta only, second from last so it's read just before starting, pointing at the status bar line.
            if (beta)
            {
                // The Audio menu holds only the Audio editor, so it is hidden with it.
                _steps.Insert(_steps.FindIndex(s => s.Title == "The Tools menu"), new Step
                {
                    Target = () => main.FindControl<Control>("AudioMenu"),
                    Title = "The Audio menu",
                    Body = "Cries, music, fanfares and sound effects: listen, export and replace them."
                });
                _steps.Insert(_steps.Count - 1, new Step
                {
                    Target = () => main.FindControl<Control>("BetaNoticeText"),
                    Title = "Unfinished editors are on",
                    Body = BetaSummary(),
                });
            }
        }

        /// <summary>What is switched on, short enough to fit a callout card.</summary>
        private static string BetaSummary()
        {
            List<string> areas = new List<string>();
            foreach (KeyValuePair<string, int> a in BetaEditors.CountByArea()) areas.Add($"{a.Value} in {a.Key}");

            List<string> features = new List<string>();
            foreach (BetaEditors.BetaFeature f in BetaEditors.Features) features.Add(f.Name);

            return $"{BetaEditors.Count} unfinished editors are on: " + string.Join(", ", areas) + ".\n\n"
                 + "These parts of finished editors are unfinished too: "
                 + string.Join(", ", features).ToLowerInvariant() + ".\n\n"
                 + "They can break a project, so back up first, and when you report a problem, say whether you "
                 + "used one of them. The full list is in Tools > Welcome & tutorial.";
        }
        private void Begin()
        {
            _index = 0;
            _steps[0].OnEnter?.Invoke();
            _layer.IsVisible = true;
            _host = TopLevel.GetTopLevel(_root);
            if (_host != null)
            {
                _host.SizeChanged += OnMainResized;
                _host.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            }
            _root.DetachedFromVisualTree += OnRootDetached;
            // The layer was collapsed until now, so its Bounds are still empty; render after
            // the layout pass that IsVisible=true just scheduled.
            global::Avalonia.Threading.Dispatcher.UIThread.Post(Render,
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private void End()
        {
            if (_host != null)
            {
                _host.SizeChanged -= OnMainResized;
                _host.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            }
            _root.DetachedFromVisualTree -= OnRootDetached;
            _layer.Children.Clear();
            _layer.IsVisible = false;
            if (_main == null) (_layer.Parent as Panel)?.Children.Remove(_layer);
            _active = null;
            if (_main == null) return;

            // The tab-walk steps switch the live Maps tab; don't leave the user parked on a
            // random one if they bailed out mid-tour.
            TabControl tabs = _main.FindControl<MapsWorkspaceView>("Maps")?.FindControl<TabControl>("MapTabs");
            if (tabs != null) tabs.SelectedIndex = 0;
        }

        private void OnMainResized(object sender, SizeChangedEventArgs e) => Render();

        private void OnRootDetached(object sender, VisualTreeAttachmentEventArgs e) { if (_active == this) End(); }

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
            RenderSettled();
        }

        private void Back()
        {
            if (_index == 0) return;
            _index--;
            _steps[_index].OnEnter?.Invoke();
            RenderSettled();
        }

        // A step that switched tabs points at controls not laid out yet, so draw again once they are.
        private void RenderSettled()
        {
            Render();
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_active == this) Render(); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        private IBrush Res(string key, Color fallback)
        {
            if (_root.TryFindResource(key, _root.ActualThemeVariant, out object v) && v is IBrush b) return b;
            return new SolidColorBrush(fallback);
        }

        private void Render()
        {
            _layer.Children.Clear();
            Step step = _steps[_index];

            // An editor tour's canvas sits in the window's overlay layer, which does not stretch its children.
            if (_main == null && _layer.Parent is Control overlayHost)
            {
                _layer.Width = overlayHost.Bounds.Width;
                _layer.Height = overlayHost.Bounds.Height;
            }
            Size layerSize = _main == null ? new Size(_layer.Width, _layer.Height) : _layer.Bounds.Size;
            if (double.IsNaN(layerSize.Width)) layerSize = default;
            if (layerSize.Width <= 0 || layerSize.Height <= 0)
            {
                // Layout has not caught up yet; try again on the next loop iteration (bounded).
                if (_active == this && _sizeRetries++ < 10)
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(Render,
                        global::Avalonia.Threading.DispatcherPriority.Background);
                return;
            }
            _sizeRetries = 0;
            Rect full = new Rect(layerSize);

            // Resolve the spotlight rectangle in layer coordinates (null => centered card only).
            Rect? hole = null;
            Control target = step.Target?.Invoke();
            if (target != null && target.IsVisible && target.Bounds.Width > 0)
            {
                Point? origin = target.TranslatePoint(new Point(0, 0), _layer);
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

            IBrush accent = Res("SystemAccentColor", Color.FromRgb(0x4F, 0x9D, 0xFF)) is ISolidColorBrush sb
                ? (IBrush)sb : new SolidColorBrush(Color.FromRgb(0x4F, 0x9D, 0xFF));

            if (hole.HasValue)
            {
                Border ring = new Border
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

            TextBlock title = new TextBlock
            {
                Text = step.Title, FontSize = 15, FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
            };
            TextBlock body = new TextBlock
            {
                Text = step.Body, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Opacity = 0.95,
            };

            Button backBtn = new Button { Content = Controls.Icon.Content("arrowleft", "Back"), MinWidth = 70, IsEnabled = _index > 0 };
            global::Avalonia.Automation.AutomationProperties.SetName(backBtn, "Back");
            backBtn.Click += (_, _) => Back();
            bool last = _index == _steps.Count - 1;
            Button nextBtn = new Button { Content = last ? "Finish" : Controls.Icon.Content("arrowright", "Next", after: true), MinWidth = 70 };
            global::Avalonia.Automation.AutomationProperties.SetName(nextBtn, last ? "Finish" : "Next");
            nextBtn.Click += (_, _) => Next();
            Button skipBtn = new Button { Content = "Skip tour", Opacity = 0.75 };
            skipBtn.Click += (_, _) => End();

            DockPanel buttons = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            StackPanel navRight = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            navRight.Children.Add(backBtn);
            navRight.Children.Add(nextBtn);
            DockPanel.SetDock(skipBtn, Dock.Left);
            buttons.Children.Add(skipBtn);
            buttons.Children.Add(navRight);

            StackPanel stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(title);
            stack.Children.Add(body);
            stack.Children.Add(new TextBlock
            {
                Text = $"{_index + 1} / {_steps.Count}", FontSize = 11, Opacity = 0.6,
            });
            stack.Children.Add(buttons);

            Border card = new Border
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
                Rect hv = hole.Value;
                if (hv.Bottom + 10 + h <= full.Height)
                {
                    x = Math.Max(8, Math.Min(hv.X, full.Width - calloutWidth - 8));
                    y = hv.Bottom + 10;
                }
                else if (hv.Y - h - 34 >= 8)
                {
                    // Keeps the 24 px margin of the clamp below, since the height measures short before the card shows.
                    x = Math.Max(8, Math.Min(hv.X, full.Width - calloutWidth - 8));
                    y = hv.Y - h - 34;
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
            // The height measures short before the card is shown, so keep a margin for its buttons.
            y = Math.Max(8, Math.Min(y, full.Height - h - 24));
            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, y);
            return card;
        }
    }
}
