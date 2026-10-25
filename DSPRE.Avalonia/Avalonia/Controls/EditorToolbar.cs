using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Where a control sits on an editor's toolbar. Groups appear in this order with a divider between them.</summary>
    public enum ToolbarSlot { Auto = -1, Picker = 0, Save = 1, Discard = 2, Undo = 3, Redo = 4, Add = 5, Remove = 6, Import = 7, Export = 8, Extra = 9 }

    /// <summary>
    /// Every editor's toolbar. The first row has the picker, Save, Discard, Undo and Redo; the second has Add, Remove,
    /// Import, Export and the editor's other controls. A control's slot comes from <see cref="SlotProperty"/>, or else from its label.
    /// </summary>
    public class EditorToolbar : Border
    {
        public static readonly AttachedProperty<ToolbarSlot> SlotProperty =
            AvaloniaProperty.RegisterAttached<EditorToolbar, Control, ToolbarSlot>("Slot", ToolbarSlot.Auto);

        public static ToolbarSlot GetSlot(Control c) => c.GetValue(SlotProperty);
        public static void SetSlot(Control c, ToolbarSlot value) => c.SetValue(SlotProperty, value);

        [Content]
        public global::Avalonia.Controls.Controls Items { get; } = new();

        private Button _popOut;
        /// <summary>Set by a host embedding this editor: a button beside the picker opens the same file in its own window.</summary>
        public Action PopOut
        {
            set
            {
                if (_popOut != null) Items.Remove(_popOut);
                _popOut = null;
                if (value == null) return;
                _popOut = new Button();
                Icon.SetKey(_popOut, "popout");
                SetSlot(_popOut, ToolbarSlot.Picker);
                ToolTip.SetTip(_popOut, "Open in its own window, on the same file");
                global::Avalonia.Automation.AutomationProperties.SetName(_popOut, "Open in window");
                _popOut.Click += (_, _) => value();
                // Beside the file it opens: right after the file picker, not after every box in the picker group.
                int anchor = -1;
                for (int i = 0; i < Items.Count; i++)
                    if (Items[i].Name is "FilePicker" or "MapPicker") { anchor = i; break; }
                if (anchor >= 0) Items.Insert(anchor + 1, _popOut); else Items.Add(_popOut);
            }
        }

        // Both rows always show, so every toolbar is the same height.
        private readonly WrapPanel _row1 = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, MinHeight = 30 };
        private readonly WrapPanel _row2 = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, MinHeight = 30 };
        private WrapPanel[] Rows => new[] { _row1, _row2 };
        private static int RowOf(int group) => group <= 1 ? 0 : 1;
        private readonly List<(Control Divider, int Group)> _dividers = new();
        private readonly List<(Control Item, int Group)> _placed = new();

        public EditorToolbar()
        {
            Padding = new Thickness(6, 4);
            BorderThickness = new Thickness(0, 0, 0, 1);
            MinHeight = 38;
            Bind(BackgroundProperty, this.GetResourceObservable("Editor.ToolbarBg"));
            Bind(BorderBrushProperty, this.GetResourceObservable("Editor.Border"));
            DockPanel.SetDock(this, Dock.Top);
            Child = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Children = { _row1, _row2 } };
            Items.CollectionChanged += (_, _) => Arrange();
            Loaded += (_, _) => FitWindowOnce();
            SizeChanged += (_, _) =>
            {
                if (_fitWhenArranged) { _fitWhenArranged = false; global::Avalonia.Threading.Dispatcher.UIThread.Post(FitWindow, global::Avalonia.Threading.DispatcherPriority.Background); }
                global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateCompact, global::Avalonia.Threading.DispatcherPriority.Background);
            };
            // The tour's "?" button arrives after the window opens, so refit when a row changes soon after opening.
            foreach (WrapPanel row in Rows)
                row.Children.CollectionChanged += (_, _) =>
                {
                    if (_fitted && DateTime.UtcNow - _fittedAt < TimeSpan.FromSeconds(5))
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(FitWindow, global::Avalonia.Threading.DispatcherPriority.Background);
                };
        }

        private bool _fitted, _fitWhenArranged;
        private DateTime _fittedAt;

        /// <summary>
        /// Widens the editor's window on first open so each toolbar row fits on one line, up to the screen width.
        /// A toolbar embedded in the main window leaves that window alone.
        /// </summary>
        private void FitWindowOnce()
        {
            if (_fitted) return;
            _fitted = true;
            _fittedAt = DateTime.UtcNow;
            FitWindow();
        }

        private void FitWindow()
        {
            if (TopLevel.GetTopLevel(this) is not Window window || window is Views.Shell.MainWindowView) return;
            // Inside a tab the toolbar can load with no width yet, so it is fitted once it is laid out.
            if (Bounds.Width <= 0) { _fitWhenArranged = true; return; }
            double needed = Padding.Left + Padding.Right + Rows.Max(RowWidth);
            double shortBy = Math.Ceiling(needed - Bounds.Width);
            if (shortBy <= 0) return;
            double screen = window.Screens.ScreenFromWindow(window)?.WorkingArea.Width / window.RenderScaling ?? double.MaxValue;
            double width = Math.Min(window.Bounds.Width + shortBy, screen - 32);
            if (width > window.Bounds.Width) window.Width = width;
        }

        private static double RowWidth(WrapPanel row)
        {
            double width = 0;
            foreach (Control child in row.Children)
            {
                if (!child.IsVisible) continue;
                child.Measure(Size.Infinity);
                width += child.DesiredSize.Width;
            }
            return width;
        }

        // ── Narrow windows: buttons with an icon drop their text before the row has to wrap ───────────────────
        private bool _compact;
        // A label's width while shown, so the full row can be worked out while labels are hidden.
        private readonly Dictionary<TextBlock, double> _labelWidths = new();
        // Tooltips this toolbar set while compact, to take back when the labels return.
        private readonly HashSet<Control> _tipsAdded = new();

        /// <summary>The text beside an icon on a button, or null for a button with no icon or no text.</summary>
        private static TextBlock IconLabel(Control c)
        {
            if ((c as ContentControl)?.Content is not StackPanel panel || panel.Children.Count != 2) return null;
            bool hasIcon = panel.Children.Any(x => x is global::Avalonia.Controls.Image);
            return hasIcon ? panel.Children.OfType<TextBlock>().FirstOrDefault(t => !string.IsNullOrEmpty(t.Text)) : null;
        }

        private IEnumerable<(Control Button, TextBlock Label)> IconButtons()
        {
            foreach ((Control item, int _) in _placed)
            {
                if (item is StackPanel pair && _captioned.Contains(pair)) continue;
                if (item is UndoRedoButtons undoRedo)
                {
                    foreach (Control button in undoRedo.Children)
                        if (IconLabel(button) is TextBlock inner) yield return (button, inner);
                    continue;
                }
                if (IconLabel(item) is TextBlock label) yield return (item, label);
            }
        }

        private void UpdateCompact()
        {
            if (_placed.Count == 0 || Bounds.Width <= 0) return;
            double available = Bounds.Width - Padding.Left - Padding.Right;
            double[] full = Rows.Select(RowWidth).ToArray();
            foreach ((Control button, TextBlock label) in IconButtons())
            {
                if (label.IsVisible) { label.Measure(Size.Infinity); _labelWidths[label] = label.DesiredSize.Width + 5; }
                else if (_labelWidths.TryGetValue(label, out double w)) full[RowOf(GroupOfPlaced(button))] += w;
            }
            bool compact = full.Max() > available + 0.5;
            if (compact == _compact) return;
            _compact = compact;
            foreach ((Control button, TextBlock label) in IconButtons())
            {
                label.IsVisible = !compact;
                if (compact && ToolTip.GetTip(button) == null) { ToolTip.SetTip(button, label.Text); _tipsAdded.Add(button); }
                else if (!compact && _tipsAdded.Remove(button)) ToolTip.SetTip(button, null);
            }
        }

        // XAML adds a child before setting its label, so the last child was slotted unlabelled until something re-arranged.
        protected override void OnInitialized()
        {
            base.OnInitialized();
            Arrange();
        }

        /// <summary>The group a control belongs to, from its slot or, failing that, from what its label says.</summary>
        private static int GroupOf(ToolbarSlot slot) => slot switch
        {
            ToolbarSlot.Picker => 0,
            ToolbarSlot.Save or ToolbarSlot.Discard or ToolbarSlot.Undo or ToolbarSlot.Redo => 1,
            ToolbarSlot.Add or ToolbarSlot.Remove => 2,
            ToolbarSlot.Import or ToolbarSlot.Export => 3,
            _ => 4,
        };

        private static string LabelOf(Control c)
        {
            string name = global::Avalonia.Automation.AutomationProperties.GetName(c);
            if (!string.IsNullOrEmpty(name)) return name;
            object content = (c as ContentControl)?.Content;
            if (content is string s) return s;
            if (content is Control inner)
            {
                foreach (ILogical l in inner.GetLogicalDescendants()) if (l is TextBlock t && !string.IsNullOrEmpty(t.Text)) return t.Text;
                if (inner is TextBlock tb) return tb.Text;
            }
            return null;
        }

        public static ToolbarSlot Infer(Control c)
        {
            ToolbarSlot set = GetSlot(c);
            if (set != ToolbarSlot.Auto) return set;
            if (c is UndoRedoButtons) return ToolbarSlot.Undo;
            // On a toolbar a list or a number box picks the record being edited.
            if (c is ComboBox or AutoCompleteBox or FusionAutoCompleteBox or NumericUpDown) return ToolbarSlot.Picker;
            if (c is not Button and not SplitButton and not DropDownButton) return ToolbarSlot.Extra;
            string label = (LabelOf(c) ?? "").Trim().TrimEnd('…', '.').Trim();
            if (label.Equals("Save", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Save ", StringComparison.OrdinalIgnoreCase) && !label.Contains("ROM", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Save;
            if (label is "Discard" or "Reset" or "Revert") return ToolbarSlot.Discard;
            if (label == "Undo") return ToolbarSlot.Undo;
            if (label == "Redo") return ToolbarSlot.Redo;
            if (label.StartsWith("Add", StringComparison.OrdinalIgnoreCase) || label.StartsWith("New", StringComparison.OrdinalIgnoreCase)
                || label.Equals("Duplicate", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Add;
            if (label.StartsWith("Remove", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Remove;
            if (label.StartsWith("Import", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Import;
            if (label.StartsWith("Export", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Export;
            return ToolbarSlot.Extra;
        }

        private readonly List<StackPanel> _captioned = new();
        private readonly List<StackPanel> _groupPanels = new();

        private void Arrange()
        {
            // Labels hidden for a narrow row come back before the row is rebuilt; the check runs again after.
            foreach ((Control button, TextBlock label) in IconButtons())
            {
                label.IsVisible = true;
                if (_tipsAdded.Remove(button)) ToolTip.SetTip(button, null);
            }
            foreach ((Control item, int _) in _placed) item.PropertyChanged -= ChildChanged;
            foreach (WrapPanel row in Rows) row.Children.Clear();
            foreach (StackPanel g in _groupPanels) g.Children.Clear();
            _groupPanels.Clear();
            foreach (StackPanel pair in _captioned) pair.Children.Clear();
            _captioned.Clear();
            _dividers.Clear();
            _placed.Clear();

            // A text label in front of a control is its caption: the two go in one piece so a wrap never splits them.
            List<(Control Item, ToolbarSlot Slot, int Order)> slotted = new();
            for (int i = 0; i < Items.Count; i++)
            {
                Control c = Items[i];
                if (c is TextBlock && GetSlot(c) == ToolbarSlot.Auto && i + 1 < Items.Count && Items[i + 1] is not TextBlock)
                {
                    Control target = Items[i + 1];
                    StackPanel pair = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                    pair.Bind(IsVisibleProperty, target.GetObservable(IsVisibleProperty));
                    if (c.VerticalAlignment == VerticalAlignment.Stretch) c.VerticalAlignment = VerticalAlignment.Center;
                    target.Margin = default;
                    pair.Children.Add(c);
                    pair.Children.Add(target);
                    _captioned.Add(pair);
                    slotted.Add((pair, Infer(target), i));
                    i++;
                    continue;
                }
                slotted.Add((c, Infer(c), i));
            }
            IOrderedEnumerable<(Control Item, ToolbarSlot Slot, int Order)> ordered =
                slotted.OrderBy(x => GroupOf(x.Slot)).ThenBy(x => x.Slot == ToolbarSlot.Extra || x.Slot == ToolbarSlot.Picker ? 0 : (int)x.Slot).ThenBy(x => x.Order);

            // Each group stays in one piece, so a wrapping toolbar never splits Add from Remove.
            int lastGroup = -1;
            StackPanel groupPanel = null;
            foreach ((Control item, ToolbarSlot slot, int _) in ordered)
            {
                int group = GroupOf(slot);
                if (group != lastGroup)
                {
                    WrapPanel row = Rows[RowOf(group)];
                    if (lastGroup >= 0 && RowOf(lastGroup) == RowOf(group))
                    {
                        Control divider = new global::Avalonia.Controls.Shapes.Rectangle { Width = 1, Margin = new Thickness(3, 4, 9, 4), VerticalAlignment = VerticalAlignment.Stretch };
                        divider.Bind(global::Avalonia.Controls.Shapes.Shape.FillProperty, this.GetResourceObservable("Editor.Separator"));
                        row.Children.Add(divider);
                        _dividers.Add((divider, group));
                    }
                    groupPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    _groupPanels.Add(groupPanel);
                    row.Children.Add(groupPanel);
                }
                if (item.Margin == default) item.Margin = new Thickness(0, 2, 6, 2);
                if (item.VerticalAlignment == VerticalAlignment.Stretch) item.VerticalAlignment = VerticalAlignment.Center;
                groupPanel.Children.Add(item);
                _placed.Add((item, group));
                item.PropertyChanged += ChildChanged;
                lastGroup = group;
            }
            UpdateDividers();
            _compact = false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateCompact, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private int GroupOfPlaced(Control c)
        {
            foreach ((Control item, int group) in _placed)
                if (ReferenceEquals(item, c) || item is UndoRedoButtons pair && pair.Children.Contains(c)) return group;
            return 4;
        }

        private void ChildChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty) UpdateDividers();
        }

        /// <summary>A divider shows only between two groups on the same row that both have something visible.</summary>
        private void UpdateDividers()
        {
            foreach ((Control divider, int group) in _dividers)
            {
                bool before = _placed.Any(p => p.Group < group && RowOf(p.Group) == RowOf(group) && p.Item.IsVisible);
                bool here = _placed.Any(p => p.Group == group && p.Item.IsVisible);
                divider.IsVisible = before && here;
            }
        }
    }
}
