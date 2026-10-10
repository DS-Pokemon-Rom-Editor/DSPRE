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
            RebuildFavourites();
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

        private void RebuildFavourites()
        {
            FavouritesBar.Children.Clear();
            foreach (string path in SettingsManager.Settings.favouriteMenuItems ?? new List<string>())
            {
                if (!_menuByPath.TryGetValue(path, out MenuItem item)) continue;
                Button button = new Button { Content = Label(item), Padding = new Thickness(8, 2), Margin = new Thickness(2, 0) };
                ToolTip.SetTip(button, path + "\nRight-click to unpin");
                // Enabled and shown exactly when the menu item is, so a pinned editor follows the ROM and the beta gate.
                button.Bind(IsEnabledProperty, item.GetObservable(IsEffectivelyEnabledProperty));
                button.Bind(IsVisibleProperty, item.GetObservable(IsVisibleProperty));
                button.Click += (_, _) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                button.AddHandler(PointerReleasedEvent, (_, e) =>
                {
                    if (e.InitialPressMouseButton != MouseButton.Right) return;
                    TogglePin(path);
                    e.Handled = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
                FavouritesBar.Children.Add(button);
            }
        }
    }
}
