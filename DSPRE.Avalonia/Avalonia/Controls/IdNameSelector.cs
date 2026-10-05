using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// A value that has names: a number box and a searchable list side by side, kept in step. Typing a number picks its
    /// name, picking a name sets the number, and a number with no name leaves the list empty rather than lying. Every
    /// editor picks named numbers this way (camera, weather, music, light type, move, species, battle type).
    /// <see cref="Keys"/> maps list rows to values when the rows are not simply 0, 1, 2 and so on.
    /// </summary>
    public class IdNameSelector : Grid
    {
        public static readonly StyledProperty<decimal?> ValueProperty =
            AvaloniaProperty.Register<IdNameSelector, decimal?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<IEnumerable> NamesProperty =
            AvaloniaProperty.Register<IdNameSelector, IEnumerable>(nameof(Names));
        public static readonly StyledProperty<IList<int>> KeysProperty =
            AvaloniaProperty.Register<IdNameSelector, IList<int>>(nameof(Keys));
        public static readonly StyledProperty<decimal> MaximumProperty =
            AvaloniaProperty.Register<IdNameSelector, decimal>(nameof(Maximum), 65535);
        public static readonly StyledProperty<decimal> MinimumProperty =
            AvaloniaProperty.Register<IdNameSelector, decimal>(nameof(Minimum), 0);
        public static readonly StyledProperty<double> NumberWidthProperty =
            AvaloniaProperty.Register<IdNameSelector, double>(nameof(NumberWidth), 90);

        public decimal? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public IEnumerable Names { get => GetValue(NamesProperty); set => SetValue(NamesProperty, value); }
        public IList<int> Keys { get => GetValue(KeysProperty); set => SetValue(KeysProperty, value); }
        public decimal Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public decimal Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double NumberWidth { get => GetValue(NumberWidthProperty); set => SetValue(NumberWidthProperty, value); }

        private readonly NumericUpDown _number = new() { FormatString = "0", VerticalAlignment = VerticalAlignment.Center };
        private readonly FusionAutoCompleteBox _names = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        private bool _syncing;

        public IdNameSelector()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,6,*");
            SetColumn(_names, 2);
            Children.Add(_number);
            Children.Add(_names);
            _number.Width = NumberWidth;
            _number.Minimum = Minimum;
            _number.Maximum = Maximum;
            _number.ValueChanged += (_, e) =>
            {
                if (_syncing) return;
                Value = e.NewValue;
            };
            _names.PropertyChanged += (_, e) =>
            {
                if (_syncing || e.Property != FusionAutoCompleteBox.SelectedIndexProperty) return;
                int row = _names.SelectedIndex;
                if (row < 0) return;
                IList<int> keys = Keys;
                Value = keys != null ? (row < keys.Count ? keys[row] : Value) : row;
            };
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == NamesProperty)
            {
                if (change.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= NamesChanged;
                if (change.NewValue is INotifyCollectionChanged now) now.CollectionChanged += NamesChanged;
                _names.ItemsSource = Names;
                Sync();
            }
            else if (change.Property == ValueProperty || change.Property == KeysProperty) Sync();
            else if (change.Property == MaximumProperty) _number.Maximum = Maximum;
            else if (change.Property == MinimumProperty) _number.Minimum = Minimum;
            else if (change.Property == NumberWidthProperty) _number.Width = NumberWidth;
        }

        // Lists are often filled after the value is set, row by row with their keys beside them; settle once they are done.
        private bool _resyncQueued;
        private void NamesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_resyncQueued) return;
            _resyncQueued = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { _resyncQueued = false; Sync(); });
        }

        private void Sync()
        {
            _syncing = true;
            try
            {
                _number.Value = Value;
                int row = -1;
                if (Value is decimal v)
                {
                    IList<int> keys = Keys;
                    int count = 0;
                    if (Names is ICollection c) count = c.Count;
                    else if (Names != null) foreach (object _ in Names) count++;
                    row = keys != null ? keys.IndexOf((int)v) : v >= 0 && v < count ? (int)v : -1;
                }
                _names.SelectedIndex = row;
            }
            finally { _syncing = false; }
        }
    }
}
