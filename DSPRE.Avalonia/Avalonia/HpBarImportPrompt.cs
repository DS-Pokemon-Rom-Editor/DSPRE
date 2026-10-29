using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Shows the battle with the current HP bar and the imported one, with the colours and pieces fitted to it,
    /// and asks whether to import it that way.
    /// </summary>
    public static class HpBarImportPrompt
    {
        public enum Choice { Fit, PictureOnly, Cancel }

        private sealed class Shown
        {
            public HpBarFit.Plan Plan;
            public List<(string Label, byte[] Now, byte[] After)> Preview;
            public string WhyNot;
        }

        private static Shown Make(GraphicAssets.Archive archive, int index, BattleGaugeComposer.Kind kind, string path)
        {
            Shown made = new Shown { Plan = HpBarImport.Plan(kind, path, out string whynot) };
            made.Preview = made.Plan == null ? null : HpBarImport.Preview(archive, index, path, made.Plan, out whynot);
            made.WhyNot = whynot;
            return made;
        }

        public static async Task<(Choice Choice, HpBarFit.Plan Plan)> AskAsync(Window owner, GraphicAssets.Archive archive, int index,
                                                                             BattleGaugeComposer.Kind kind, string path,
                                                                             bool offerPictureOnly = true)
        {
            Shown plain = Make(archive, index, kind, path);

            Choice picked = Choice.Cancel;
            Window win = new Window
            {
                Title = "Import HP bar",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
            if (plain.Preview != null)
            {
                Button fit = new Button { Content = "Import" };
                ToolTip.SetTip(fit, "Import the picture and set the text colours and bar pieces from it");
                fit.Click += (_, _) => { picked = Choice.Fit; win.Close(); };
                buttons.Children.Add(fit);
            }
            if (offerPictureOnly)
            {
                Button only = new Button { Content = "Picture only" };
                ToolTip.SetTip(only, "Import the picture and leave the text colours and bar pieces as they are");
                only.Click += (_, _) => { picked = Choice.PictureOnly; win.Close(); };
                buttons.Children.Add(only);
            }
            Button cancel = new Button { Content = "Cancel", IsCancel = true };
            cancel.Click += (_, _) => win.Close();
            buttons.Children.Add(cancel);

            if (plain.Preview == null)
            {
                win.Width = 460;
                win.SizeToContent = SizeToContent.Height;
                StackPanel root = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 10 };
                root.Children.Add(new TextBlock { Text = plain.WhyNot ?? "This bar can't be fitted.", TextWrapping = TextWrapping.Wrap });
                root.Children.Add(buttons);
                win.Content = root;
            }
            else
            {
                StackPanel bottom = Details(plain.Plan);
                bottom.Children.Add(buttons);
                DockPanel body = DialogSizing.Body(Comparison(plain.Preview), bottom);
                body.Margin = new global::Avalonia.Thickness(16);
                win.Content = body;
                // Each preview opens at about twice the DS size, smaller when the screen is short.
                int rows = plain.Preview.Count;
                win.Width = 1100;
                win.Height = rows == 1 ? 560 : 900;
                if (owner?.Screens?.ScreenFromWindow(owner) is { } screen)
                    win.Height = Math.Min(win.Height, screen.WorkingArea.Height / screen.Scaling * 0.9);
                win.MinWidth = 640;
                win.MinHeight = rows == 1 ? 360 : 480;
            }

            if (owner == null) return (Choice.Cancel, plain.Plan);
            await win.ShowDialog(owner);
            return (picked, plain.Plan);
        }

        private static Grid Comparison(List<(string Label, byte[] Now, byte[] After)> pages)
        {
            Grid grid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
            grid.ColumnDefinitions = new ColumnDefinitions("Auto,*,*");
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Add(grid, Heading("Current bar"), 0, 1);
            Add(grid, Heading("Imported bar"), 0, 2);
            for (int i = 0; i < pages.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
                Add(grid, new TextBlock { Text = pages[i].Label, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, i + 1, 0);
                Add(grid, Screen(pages[i].Now), i + 1, 1);
                Add(grid, Screen(pages[i].After), i + 1, 2);
            }
            return grid;
        }

        private static TextBlock Heading(string text) => new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };

        private static void Add(Grid grid, Control c, int row, int column)
        {
            Grid.SetRow(c, row);
            Grid.SetColumn(c, column);
            grid.Children.Add(c);
        }

        private static Viewbox Screen(byte[] rgba)
        {
            Image image = new Image
            {
                Source = ImageConverter.FromRgba(rgba, BattleScreenRenderer.ScreenWidth, BattleScreenRenderer.ScreenHeight),
                Width = BattleScreenRenderer.ScreenWidth, Height = BattleScreenRenderer.ScreenHeight,
            };
            RenderOptions.SetBitmapInterpolationMode(image, global::Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            return new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = new Border { BorderBrush = Brushes.Gray, BorderThickness = new global::Avalonia.Thickness(1), Child = image },
            };
        }

        private static StackPanel Details(HpBarFit.Plan plan)
        {
            StackPanel details = new StackPanel { Spacing = 8, Margin = new global::Avalonia.Thickness(0, 10, 0, 0) };
            // With the patch nothing is painted behind the text, so its background colour isn't shown.
            StackPanel text = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            List<(string, uint)> swatches = new List<(string, uint)>();
            if (!plan.ClearTextBackground) swatches.Add(("Text background", plan.Background));
            swatches.Add(("Letters", plan.Ink));
            swatches.Add(("Shadow", plan.Shadow));
            foreach ((string label, uint colour) in swatches)
            {
                StackPanel one = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                one.Children.Add(Swatch(colour));
                one.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                text.Children.Add(one);
            }
            details.Children.Add(text);
            foreach (string note in plan.Notes)
                details.Children.Add(new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            return details;
        }

        private static Border Swatch(uint c) => new Border
        {
            Width = 22, Height = 22, BorderThickness = new global::Avalonia.Thickness(1), BorderBrush = Brushes.Gray,
            Background = new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)),
        };
    }
}
