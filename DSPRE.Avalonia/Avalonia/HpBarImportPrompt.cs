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
    /// Shows how a battle will draw an imported HP bar, with the colours and pieces fitted to it, and asks
    /// whether to import it that way.
    /// </summary>
    public static class HpBarImportPrompt
    {
        public enum Choice { Fit, PictureOnly, Cancel }

        private sealed class Shown
        {
            public HpBarFit.Plan Plan;
            public List<(string Label, byte[] Rgba)> Preview;
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
                Width = 600,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };
            StackPanel root = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 10 };
            StackPanel body = new StackPanel { Spacing = 10 };
            root.Children.Add(body);

            Show(body, plain);

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
            root.Children.Add(buttons);
            win.Content = root;

            if (owner == null) return (Choice.Cancel, plain.Plan);
            await win.ShowDialog(owner);
            return (picked, plain.Plan);
        }

        private static void Show(StackPanel body, Shown shown)
        {
            body.Children.Clear();
            if (shown.Preview == null)
            {
                body.Children.Add(new TextBlock { Text = shown.WhyNot ?? "This bar can't be fitted.", TextWrapping = TextWrapping.Wrap });
                return;
            }
            HpBarFit.Plan plan = shown.Plan;
            StackPanel shots = new StackPanel { Spacing = 8 };
            foreach ((string label, byte[] rgba) in shown.Preview)
            {
                shots.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold });
                shots.Children.Add(new Border
                {
                    BorderBrush = Brushes.Gray, BorderThickness = new global::Avalonia.Thickness(1), HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new Image
                    {
                        Source = ImageConverter.FromRgba(rgba, BattleScreenRenderer.ScreenWidth, BattleScreenRenderer.ScreenHeight),
                        Width = BattleScreenRenderer.ScreenWidth * 2, Height = BattleScreenRenderer.ScreenHeight * 2,
                        Stretch = Stretch.Fill,
                    }.Also(i => RenderOptions.SetBitmapInterpolationMode(i, global::Avalonia.Media.Imaging.BitmapInterpolationMode.None)),
                });
            }
            body.Children.Add(new ScrollViewer { Content = shots, MaxHeight = 820 });

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
            body.Children.Add(text);
            foreach (string note in plan.Notes)
                body.Children.Add(new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        }

        private static T Also<T>(this T value, System.Action<T> act) { act(value); return value; }

        private static Border Swatch(uint c) => new Border
        {
            Width = 22, Height = 22, BorderThickness = new global::Avalonia.Thickness(1), BorderBrush = Brushes.Gray,
            Background = new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)),
        };
    }
}
