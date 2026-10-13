using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>Picks which events of an imported file go into the open one.</summary>
    public static class EventImportDialog
    {
        public static async Task<EventImportChoice> ShowAsync(EventFile source, Window owner)
        {
            EventImportChoice result = null;
            Window win = new Window
            {
                Title = "Import events",
                Width = 980, Height = 520, MinWidth = 640, MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            Grid columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 8, Margin = new global::Avalonia.Thickness(12) };
            List<CheckBox> ow = Column(columns, 0, "Overworlds", source.overworlds,
                                       o => $"ID {o.owID}, {DSPRE.Avalonia.Data.OverworldLabels.Of(o.overlayTableEntry)}");
            List<CheckBox> warps = Column(columns, 1, "Warps", source.warps, w => w.ToString());
            List<CheckBox> trigs = Column(columns, 2, "Triggers", source.triggers, t => t.ToString());
            List<CheckBox> spawns = Column(columns, 3, "Spawnables", source.spawnables, s => s.ToString());

            CheckBox clear = new CheckBox { Content = "Clear this file's events first", VerticalAlignment = VerticalAlignment.Center };
            TextBlock note = new TextBlock
            {
                Text = "Overworlds whose ID is already used here get a free ID, so scripts that use them may need updating.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 12, Opacity = 0.8,
                Margin = new global::Avalonia.Thickness(12, 0, 12, 0),
            };
            Button ok = new Button { Content = "Import", MinWidth = 80, IsDefault = true };
            Button cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
            ok.Click += (_, _) =>
            {
                result = new EventImportChoice
                {
                    ClearFirst = clear.IsChecked == true,
                    Overworlds = Ticked(ow), Warps = Ticked(warps), Triggers = Ticked(trigs), Spawnables = Ticked(spawns),
                };
                win.Close();
            };
            cancel.Click += (_, _) => win.Close();

            void SyncOk() => ok.IsEnabled = ow.Concat(warps).Concat(trigs).Concat(spawns).Any(b => b.IsChecked == true) || clear.IsChecked == true;
            foreach (CheckBox b in ow.Concat(warps).Concat(trigs).Concat(spawns)) b.IsCheckedChanged += (_, _) => SyncOk();
            clear.IsCheckedChanged += (_, _) => SyncOk();
            SyncOk();

            DockPanel bottom = new DockPanel { Margin = new global::Avalonia.Thickness(12, 0, 12, 12) };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Right);
            bottom.Children.Add(buttons);
            bottom.Children.Add(clear);

            DockPanel root = new DockPanel();
            DockPanel.SetDock(bottom, Dock.Bottom);
            DockPanel.SetDock(note, Dock.Bottom);
            root.Children.Add(bottom);
            root.Children.Add(note);
            root.Children.Add(columns);
            win.Content = root;

            if (owner == null) return null;
            await win.ShowDialog(owner);
            return result;
        }

        private static List<CheckBox> Column<T>(Grid grid, int column, string title, IList<T> events, System.Func<T, string> describe)
        {
            List<CheckBox> boxes = new List<CheckBox>();
            for (int i = 0; i < events.Count; i++)
                boxes.Add(new CheckBox { Content = $"{i:D2}  {describe(events[i])}", IsChecked = true });

            StackPanel list = new StackPanel();
            foreach (CheckBox b in boxes) list.Children.Add(b);
            Button all = new Button { Content = "Check all", IsEnabled = boxes.Count > 0 };
            Button none = new Button { Content = "Uncheck all", IsEnabled = boxes.Count > 0 };
            all.Click += (_, _) => boxes.ForEach(b => b.IsChecked = true);
            none.Click += (_, _) => boxes.ForEach(b => b.IsChecked = false);

            StackPanel head = new StackPanel { Spacing = 4 };
            head.Children.Add(new TextBlock { Text = $"{title} ({events.Count})", FontWeight = global::Avalonia.Media.FontWeight.Bold });
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(all);
            row.Children.Add(none);
            head.Children.Add(row);

            DockPanel panel = new DockPanel();
            DockPanel.SetDock(head, Dock.Top);
            panel.Children.Add(head);
            panel.Children.Add(new ScrollViewer { Content = list, Margin = new global::Avalonia.Thickness(0, 6, 0, 0) });
            Grid.SetColumn(panel, column);
            grid.Children.Add(panel);
            return boxes;
        }

        private static int[] Ticked(List<CheckBox> boxes) =>
            Enumerable.Range(0, boxes.Count).Where(i => boxes[i].IsChecked == true).ToArray();
    }
}
