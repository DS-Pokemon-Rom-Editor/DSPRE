using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>What a picker or number box points at, so it can jump to the editor that owns it.</summary>
    public enum LinkKind { None, Pokemon, Item, Move, Trainer, Header, Script, EventFile, Matrix, Map, Text, Wild, AreaData }

    /// <summary>
    /// Attached behaviour (<c>controls:EditorLink.To="Item"</c>) that opens the owning editor from a hover link button, the
    /// context menu or Ctrl+click, using the picker's selected index or the number box's value.
    /// </summary>
    public static class EditorLink
    {
        public static readonly AttachedProperty<LinkKind> ToProperty =
            AvaloniaProperty.RegisterAttached<Control, LinkKind>("To", typeof(EditorLink));

        public static LinkKind GetTo(Control c) => c.GetValue(ToProperty);
        public static void SetTo(Control c, LinkKind v) => c.SetValue(ToProperty, v);

        static EditorLink()
        {
            ToProperty.Changed.AddClassHandler<Control>((c, e) =>
            {
                if (e.OldValue is LinkKind old && old != LinkKind.None) return;   // wired once
                if (e.NewValue is LinkKind k && k != LinkKind.None) Wire(c);
            });
        }

        public static string EditorName(LinkKind k) => k switch
        {
            LinkKind.Pokemon => "Pokémon editor",
            LinkKind.Item => "Item editor",
            LinkKind.Move => "Move data editor",
            LinkKind.Trainer => "Trainer editor",
            LinkKind.Header => "Header editor",
            LinkKind.Script => "Script editor",
            LinkKind.EventFile => "Event editor",
            LinkKind.Matrix => "Matrix editor",
            LinkKind.Map => "Map editor",
            LinkKind.Text => "Text editor",
            LinkKind.Wild => "Wild Pokémon editor",
            LinkKind.AreaData => "Area data editor",
            _ => "",
        };

        private static int IdOf(Control c) => c switch
        {
            FusionAutoCompleteBox f => f.SelectedIndex,
            SelectingItemsControl s => s.SelectedIndex,
            NumericUpDown n => n.Value is decimal d ? (int)d : -1,
            _ => -1,
        };

        public static void Open(LinkKind k, int id)
        {
            if (id < 0) return;
            switch (k)
            {
                case LinkKind.Pokemon: if (id > 0) AvaloniaEditorLauncher.OpenPokemonEditor(id); break;
                case LinkKind.Item: if (id > 0) AvaloniaEditorLauncher.OpenItemEditor(id); break;
                case LinkKind.Move: if (id > 0) AvaloniaEditorLauncher.OpenMoveDataEditor(id); break;
                case LinkKind.Trainer: AvaloniaEditorLauncher.OpenTrainerEditor(id); break;
                case LinkKind.Header: AvaloniaEditorLauncher.OpenHeaderEditor(id); break;
                case LinkKind.Script: AvaloniaEditorLauncher.OpenScriptEditor(id); break;
                case LinkKind.EventFile: AvaloniaEditorLauncher.OpenEventEditor(id); break;
                case LinkKind.Matrix: AvaloniaEditorLauncher.OpenMatrixEditor(id); break;
                case LinkKind.Map: AvaloniaEditorLauncher.OpenMapEditor(id); break;
                case LinkKind.Text: AvaloniaEditorLauncher.OpenTextEditor(id); break;
                case LinkKind.Wild: AvaloniaEditorLauncher.OpenWildEditor(id); break;
                case LinkKind.AreaData: AvaloniaEditorLauncher.OpenAreaDataEditor(id); break;
            }
        }

        private static void Go(Control c) => Open(GetTo(c), IdOf(c));

        // False when another control or the window edge is within the arrow's width to the right of the field.
        private static bool RoomOutside(Control c)
        {
            if (TopLevel.GetTopLevel(c) is not TopLevel top) return false;
            Point? corner = c.TranslatePoint(new Point(c.Bounds.Width, 0), top);
            if (corner == null) return false;
            Rect strip = new Rect(corner.Value.X, corner.Value.Y, 22, c.Bounds.Height);
            if (strip.Right > top.ClientSize.Width) return false;
            HashSet<Visual> above = new HashSet<Visual>(c.GetVisualAncestors());
            foreach (Visual v in top.GetVisualDescendants())
            {
                if (v is not Control k || ReferenceEquals(k, c) || above.Contains(k) || !k.IsEffectivelyVisible || k.TemplatedParent != null) continue;
                if (k is not (TextBlock or TemplatedControl) || k is ScrollViewer || k is ContentControl and not (Button or CheckBox or RadioButton)) continue;
                if (k.GetVisualAncestors().Contains(c) || k.FindAncestorOfType<AdornerLayer>() != null) continue;
                Point? at = k.TranslatePoint(new Point(0, 0), top);
                if (at == null) continue;
                Rect hit = new Rect(at.Value, k.Bounds.Size).Intersect(strip);
                if (hit.Width * hit.Height > 4) return false;
            }
            return true;
        }

        private static void Wire(Control c)
        {
            string name = EditorName(GetTo(c));

            MenuItem item = new MenuItem { Header = "Open in " + name };
            item.Click += (_, _) => Go(c);
            if (c.ContextMenu == null && c.ContextFlyout == null) c.ContextMenu = new ContextMenu { ItemsSource = new[] { item } };
            else if (c.ContextMenu != null) c.ContextMenu.Items.Add(item);

            c.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.GetCurrentPoint(c).Properties.IsLeftButtonPressed)
                {
                    Go(c);
                    e.Handled = true;
                }
            }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Shown on hover, never over the value; RoomOutside picks the side.
            Button arrow = new Button
            {
                Content = Icon.Image("OpenLink"), Padding = new Thickness(2, 0), MinWidth = 0, MinHeight = 0, Width = 20, Height = 20,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, -22, 0), IsVisible = false, Focusable = false,
                Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x30, 0x30, 0x30)),
            };
            ToolTip.SetTip(arrow, $"Open in {name} (or right-click, or Ctrl+click)");
            global::Avalonia.Automation.AutomationProperties.SetName(arrow, $"Open in {name}");
            arrow.Click += (_, _) => Go(c);
            Panel host = new Panel { IsHitTestVisible = true, Background = null, Children = { arrow } };

            bool over = false, overArrow = false;
            void Update()
            {
                bool show = (over || overArrow) && IdOf(c) >= 0;
                if (show && !overArrow) arrow.Margin = RoomOutside(c) ? new Thickness(0, 0, -22, 0) : new Thickness(0, 0, 2, 0);
                arrow.IsVisible = show;
            }
            c.PointerEntered += (_, _) => { over = true; Update(); };
            c.PointerExited += (_, _) => { over = false; global::Avalonia.Threading.DispatcherTimer.RunOnce(Update, TimeSpan.FromMilliseconds(150)); };
            arrow.PointerEntered += (_, _) => { overArrow = true; Update(); };
            arrow.PointerExited += (_, _) => { overArrow = false; Update(); };

            c.AttachedToVisualTree += (_, _) =>
            {
                AdornerLayer layer = AdornerLayer.GetAdornerLayer(c);
                if (layer == null || layer.Children.Contains(host)) return;
                AdornerLayer.SetAdornedElement(host, c);
                AdornerLayer.SetIsClipEnabled(host, false);   // it sits outside the field, so a scroll viewer would clip it
                layer.Children.Add(host);
            };
            c.DetachedFromVisualTree += (_, _) => (host.Parent as Panel)?.Children.Remove(host);
        }
    }
}
