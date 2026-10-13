using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>Right-click a menu item to pin it beside the menus; right-click a pinned button to unpin it.</summary>
    public partial class MainWindowView
    {
        private readonly Dictionary<string, MenuItem> _menuByPath = new Dictionary<string, MenuItem>();

        private void SetupFavourites()
        {
            foreach (MenuItem item in Leaves(MainMenu))
            {
                string path = PathOf(item);
                if (path == null || _menuByPath.ContainsKey(path)) continue;
                _menuByPath[path] = item;
                // On the press: the menu closes on a right press, before any release reaches the item.
                item.AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (!e.GetCurrentPoint(item).Properties.IsRightButtonPressed) return;
                    TogglePin(path);
                    e.Handled = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
            FavouritesStar.Source = DSPRE.Avalonia.Controls.Icon.Get("star");
            // Drawn rather than typed, so the chevrons look the same in every font.
            global::Avalonia.Controls.Shapes.Path chevrons = new global::Avalonia.Controls.Shapes.Path
            {
                Data = global::Avalonia.Media.Geometry.Parse("M0,0 L4,4 L0,8 M5,0 L9,4 L5,8"),
                StrokeThickness = 1.6, Width = 10, Height = 9, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            };
            chevrons.Bind(global::Avalonia.Controls.Shapes.Shape.StrokeProperty, this.GetResourceObservable("Editor.Text"));
            Button more = new Button { Content = chevrons, Padding = new Thickness(8, 2), Margin = new Thickness(2, 0) };
            ToolTip.SetTip(more, "More pinned items");
            global::Avalonia.Automation.AutomationProperties.SetName(more, "More pinned items");
            more.Click += (_, _) => ShowHiddenPins(more);
            FavouritesBar.OverflowButton = more;
            SeedDefaultFavourites();
            RebuildFavourites();
        }

        // The pins the row had no room for, as a menu under the overflow button.
        private void ShowHiddenPins(Button anchor)
        {
            MenuFlyout flyout = new MenuFlyout();
            foreach (Control hidden in FavouritesBar.Hidden)
            {
                if (hidden.Tag is not string path || !_menuByPath.TryGetValue(path, out MenuItem item)) continue;
                MenuItem entry = new MenuItem { Header = Label(item), IsEnabled = Usable(item) };
                ToolTip.SetTip(entry, path + "\nRight-click to unpin");
                entry.Click += (_, _) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                entry.AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (!e.GetCurrentPoint(entry).Properties.IsRightButtonPressed) return;
                    flyout.Hide();
                    TogglePin(path);
                    e.Handled = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
                flyout.Items.Add(entry);
            }
            if (flyout.Items.Count > 0) flyout.ShowAt(anchor);
        }

        // The editors most work starts in, pinned once on a first run; anyone who already pinned keeps their own.
        private static readonly string[] DefaultFavourites =
        {
            "Pokémon > Pokémon editor", "Trainers > Trainer editor", "World > Map editor",
            "World > Event editor", "Text > Script editor", "Text > Text editor",
        };

        private void SeedDefaultFavourites()
        {
            if (SettingsManager.Settings.favouritesSeeded) return;
            List<string> pins = SettingsManager.Settings.favouriteMenuItems ??= new List<string>();
            if (pins.Count == 0) pins.AddRange(DefaultFavourites.Where(_menuByPath.ContainsKey));
            SettingsManager.Settings.favouritesSeeded = true;
            SettingsManager.Save();
        }

        // Items that run something: no submenu, not the recent-projects list that rebuilds itself.
        private IEnumerable<MenuItem> Leaves(ItemsControl parent)
        {
            foreach (object o in parent.Items)
            {
                if (o is not MenuItem item || ReferenceEquals(item, RecentMenu)) continue;
                if (item.Items.OfType<MenuItem>().Any())
                    foreach (MenuItem leaf in Leaves(item)) yield return leaf;
                else if (item.Header is string) yield return item;
            }
        }

        private static string Label(MenuItem item) => (item.Header as string)?.Replace("_", "").TrimEnd('…', '.').Trim();

        private static string PathOf(MenuItem item)
        {
            List<string> parts = new List<string>();
            for (ILogical node = item; node is MenuItem m; node = node.LogicalParent) parts.Insert(0, Label(m));
            return parts.Any(string.IsNullOrEmpty) ? null : string.Join(" > ", parts);
        }

        private void TogglePin(string path)
        {
            List<string> pins = SettingsManager.Settings.favouriteMenuItems ??= new List<string>();
            if (!pins.Remove(path)) pins.Add(path);
            SettingsManager.Save();
            RebuildFavourites();
        }

        // The item and every menu above it.
        private static IEnumerable<MenuItem> Gates(MenuItem item)
        {
            for (ILogical node = item; node is MenuItem m; node = node.LogicalParent) yield return m;
        }

        private static bool Usable(MenuItem item) => Gates(item).All(m => m.IsEnabled);

        private void RebuildFavourites()
        {
            foreach (Control pin in FavouritesBar.Children.Where(c => !ReferenceEquals(c, FavouritesBar.OverflowButton)).ToList())
                FavouritesBar.Children.Remove(pin);
            List<string> pins = SettingsManager.Settings.favouriteMenuItems ?? new List<string>();
            // A star beside a menu item says it is already pinned.
            foreach (KeyValuePair<string, MenuItem> kv in _menuByPath)
                kv.Value.Icon = pins.Contains(kv.Key) ? DSPRE.Avalonia.Controls.Icon.Image("star") : null;
            NoFavouritesText.IsVisible = !pins.Any(_menuByPath.ContainsKey);
            int at = 0;
            foreach (string path in pins)
            {
                if (!_menuByPath.TryGetValue(path, out MenuItem item)) continue;
                Button button = new Button { Content = Label(item), Padding = new Thickness(8, 2), Margin = new Thickness(2, 0), Tag = path };
                ToolTip.SetTip(button, path + "\nRight-click to unpin");
                // Enabled and shown exactly when the menu item is, so a pinned editor follows the ROM and the beta gate.
                // A submenu item's own state ignores its menus, which are what turn off most editors without a ROM.
                button.IsEnabled = Usable(item);
                foreach (MenuItem gate in Gates(item))
                    gate.PropertyChanged += (_, e) => { if (e.Property == IsEnabledProperty) button.IsEnabled = Usable(item); };
                button.Bind(IsVisibleProperty, item.GetObservable(IsVisibleProperty));
                button.Click += (_, _) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                button.AddHandler(PointerReleasedEvent, (_, e) =>
                {
                    if (e.InitialPressMouseButton != MouseButton.Right) return;
                    TogglePin(path);
                    e.Handled = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
                FavouritesBar.Children.Insert(at++, button);
            }
        }
    }
}
