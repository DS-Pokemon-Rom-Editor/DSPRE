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
    /// Every editor's toolbar: the same height, padding, background and border, and the same order, so Save, Undo or
    /// Import is always found in the same place. The file or record picker comes first, then Save, Discard, Undo and
    /// Redo, then Add and Remove, then Import and Export, then whatever the editor adds. A control's slot comes from
    /// <see cref="SlotProperty"/>, or from its label when that is not set; a label in front of a picker goes with it.
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

        private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
            Child = _panel;
            Items.CollectionChanged += (_, _) => Arrange();
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
            if (label.StartsWith("Add", StringComparison.OrdinalIgnoreCase) || label.StartsWith("New", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Add;
            if (label.StartsWith("Remove", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Remove;
            if (label.StartsWith("Import", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Import;
            if (label.StartsWith("Export", StringComparison.OrdinalIgnoreCase)) return ToolbarSlot.Export;
            return ToolbarSlot.Extra;
        }

        private readonly List<StackPanel> _captioned = new();

        private void Arrange()
        {
            foreach ((Control item, int _) in _placed) item.PropertyChanged -= ChildChanged;
            _panel.Children.Clear();
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

            int lastGroup = -1;
            foreach ((Control item, ToolbarSlot slot, int _) in ordered)
            {
                int group = GroupOf(slot);
                if (lastGroup >= 0 && group != lastGroup)
                {
                    Control divider = new global::Avalonia.Controls.Shapes.Rectangle { Width = 1, Margin = new Thickness(3, 4, 9, 4), VerticalAlignment = VerticalAlignment.Stretch };
                    divider.Bind(global::Avalonia.Controls.Shapes.Shape.FillProperty, this.GetResourceObservable("Editor.Separator"));
                    _panel.Children.Add(divider);
                    _dividers.Add((divider, group));
                }
                if (item.Margin == default) item.Margin = new Thickness(0, 2, 6, 2);
                if (item.VerticalAlignment == VerticalAlignment.Stretch) item.VerticalAlignment = VerticalAlignment.Center;
                _panel.Children.Add(item);
                _placed.Add((item, group));
                item.PropertyChanged += ChildChanged;
                lastGroup = group;
            }
            UpdateDividers();
        }

        private void ChildChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty) UpdateDividers();
        }

        /// <summary>A divider shows only between two groups that both have something visible.</summary>
        private void UpdateDividers()
        {
            foreach ((Control divider, int group) in _dividers)
            {
                bool before = _placed.Any(p => p.Group < group && p.Item.IsVisible);
                bool here = _placed.Any(p => p.Group == group && p.Item.IsVisible);
                divider.IsVisible = before && here;
            }
        }
    }
}
