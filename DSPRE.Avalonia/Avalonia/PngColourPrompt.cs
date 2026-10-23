using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Asks before a PNG goes into a graphic whose palette lacks some of its colours, instead of quietly swapping
    /// them for the nearest ones.
    /// </summary>
    public static class PngColourPrompt
    {
        public enum Choice { AddColours, AsItIs, Cancel }

        /// <summary>
        /// Checks the PNG and asks when its colours do not all fit; AsItIs when there is nothing to ask. The check
        /// comes back too, so a caller keeping undo steps can remember the palette before it is written.
        /// </summary>
        public static async Task<(Choice Choice, GraphicAssets.PngColourCheck Check)> AskAsync(Window owner, GraphicAssets.Archive archive,
                                                                                            int index, string path, int palette = -1, int paletteBank = -1)
        {
            GraphicAssets.PngColourCheck check = GraphicAssets.CheckPngColours(archive, index, path, palette, paletteBank);
            if (check == null || check.Matches) return (Choice.AsItIs, check);

            Choice picked = Choice.Cancel;
            Window win = new Window
            {
                Title = "Colours not in the palette",
                Width = 540,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            int missing = check.Missing.Count;
            string said = check.KeepsNumbers
                ? (missing == 1 ? "1 of the colours in that PNG differs" : $"{missing} of the colours in that PNG differ")
                  + " from the palette at the same colour numbers."
                : $"That PNG uses {missing} colour{(missing == 1 ? "" : "s")} this graphic's palette doesn't have.";
            StackPanel root = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 8 };
            root.Children.Add(new TextBlock { Text = said, TextWrapping = TextWrapping.Wrap });

            StackPanel rows = new StackPanel { Spacing = 4 };
            // A sprite's new colours can take any free slot, so each gets a choice; a flat drawing keeps the PNG's
            // numbers.
            bool choosing = check.CannotAddBecause == null && !check.KeepsNumbers;
            ComboBox[] picks = new ComboBox[check.Missing.Count];
            for (int i = 0; i < check.Missing.Count; i++)
            {
                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(Swatch(check.Missing[i]));
                if (choosing)
                {
                    // Fifteen at most, so all of them are laid out and the list never has to scroll.
                    ComboBox pick = new ComboBox
                    {
                        Width = 440, MaxDropDownHeight = 640,
                        ItemsPanel = new global::Avalonia.Controls.Templates.FuncTemplate<Panel>(() => new StackPanel()),
                    };
                    for (int slot = 1; slot < 16; slot++)
                    {
                        if (check.Kept[slot]) continue;
                        StackPanel look = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                        ComboBoxItem item = new ComboBoxItem { Content = look, Tag = slot };
                        look.Children.Add(Swatch(check.BankColours[slot]));
                        look.Children.Add(new TextBlock
                        {
                            Text = $"Colour {slot}: " + Users(check.UsersOf[slot]),
                            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 380,
                        });
                        ToolTip.SetTip(item, check.UsersOf[slot] == null ? "Nothing else draws with this colour." : string.Join("\n", check.UsersOf[slot]));
                        global::Avalonia.Automation.AutomationProperties.SetName(item, $"Colour {slot}");
                        pick.Items.Add(item);
                        if (slot == check.Slots[i]) pick.SelectedItem = item;
                    }
                    picks[i] = pick;
                    row.Children.Add(pick);
                }
                else if (check.KeepsNumbers && i < check.Slots.Count)
                {
                    IReadOnlyList<string> users = check.SlotUsers[i];
                    StackPanel note = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 440 };
                    note.Children.Add(new TextBlock
                    {
                        Text = $"Colour {check.Slots[i]}" + (users?.Count == 1 ? ", also used by " + users[0] : ""),
                        TextWrapping = TextWrapping.Wrap,
                    });
                    // A long list of names would bury the colours.
                    if (users?.Count > 1)
                        note.Children.Add(new Expander
                        {
                            Header = $"Also used by {users.Count} other pictures",
                            Content = new TextBlock { Text = string.Join(", ", users), TextWrapping = TextWrapping.Wrap },
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                        });
                    row.Children.Add(note);
                }
                rows.Children.Add(row);
            }
            root.Children.Add(new ScrollViewer { Content = rows, MaxHeight = 320 });
            if (check.CannotAddBecause != null)
                root.Children.Add(new TextBlock { Text = check.CannotAddBecause, TextWrapping = TextWrapping.Wrap });

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
            if (check.CannotAddBecause == null)
            {
                Button add = new Button { Content = "Add to the palette" };
                add.Click += async (_, _) =>
                {
                    if (choosing)
                    {
                        int[] slots = picks.Select(p => p.SelectedItem is Control c && c.Tag is int n ? n : -1).ToArray();
                        if (slots.Any(n => n < 0) || slots.Distinct().Count() != slots.Length)
                        {
                            await DialogHelper.ShowInfoAsync("Pick a different colour number for each new colour.", "Colours not in the palette");
                            return;
                        }
                        check.Slots = slots.ToList();
                    }
                    picked = Choice.AddColours;
                    win.Close();
                };
                buttons.Children.Add(add);
            }
            Button asItIs = new Button { Content = check.KeepsNumbers ? "Keep the palette's colours" : "Use the nearest colours" };
            asItIs.Click += (_, _) => { picked = Choice.AsItIs; win.Close(); };
            buttons.Children.Add(asItIs);
            Button cancel = new Button { Content = "Cancel", IsCancel = true };
            cancel.Click += (_, _) => win.Close();
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);
            win.Content = root;

            if (owner == null) return (Choice.Cancel, check);
            await win.ShowDialog(owner);
            return (picked, check);
        }

        private static string Users(IReadOnlyList<string> users) =>
            users == null ? "nothing else" : users.Count == 1 ? users[0] : $"{users.Count} other pictures";

        private static Border Swatch(uint c) => new Border
        {
            Width = 22, Height = 22, BorderThickness = new global::Avalonia.Thickness(1), BorderBrush = Brushes.Gray,
            Background = new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)),
        };

        public static string Import(GraphicAssets.Archive archive, int index, string path, Choice choice,
                                    GraphicAssets.PngColourCheck check, out string note, int palette = -1, int paletteBank = -1)
        {
            note = null;
            if (choice == Choice.Cancel) return null;
            if (choice == Choice.AddColours)
            {
                string trouble = GraphicAssets.AddPngColours(archive, check);
                if (trouble != null) return trouble;
            }
            return GraphicAssets.ImportPng(archive, index, path, out note, palette, paletteBank);
        }
    }
}
