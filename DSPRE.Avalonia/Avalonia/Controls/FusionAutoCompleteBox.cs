using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// An editable, searchable selector with Qt Fusion's combo-box interaction model.
    /// Text is only committed when it identifies an item in ItemsSource.
    /// </summary>
    public class FusionAutoCompleteBox : AutoCompleteBox
    {
        public static readonly StyledProperty<int> SelectedIndexProperty =
            AvaloniaProperty.Register<FusionAutoCompleteBox, int>(
                nameof(SelectedIndex),
                -1,
                defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

        private bool _editingText;
        private bool _showAllItems;
        private bool _refreshingDropDown;
        private bool _textChangedSinceLastOpen;
        private object _lastCommittedItem;
        private INotifyCollectionChanged _itemsSourceCollection;

        static FusionAutoCompleteBox()
        {
            SelectedIndexProperty.Changed.AddClassHandler<FusionAutoCompleteBox>(
                (control, _) => control.ApplySelectedIndex());
        }

        public FusionAutoCompleteBox()
        {
            MinimumPrefixLength = 0;
            IsTextCompletionEnabled = false;
            TextFilter = FilterText;
        }

        /// <summary>
        /// Gets or sets the zero-based item index. This mirrors ComboBox.SelectedIndex so existing
        /// DSPRE view models can adopt the searchable control without a second selection property.
        /// </summary>
        public int SelectedIndex
        {
            get => GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty)
            {
                OnSelectedItemChanged(change.GetNewValue<object>());
            }
            else if (change.Property == ItemsSourceProperty)
            {
                UpdateItemsSourceCollectionSubscription();

                if (_lastCommittedItem != null && FindItemIndex(_lastCommittedItem) < 0)
                {
                    _lastCommittedItem = null;
                }

                ApplySelectedIndex();
            }
            else if (change.Property == IsDropDownOpenProperty)
            {
                OnDropDownStateChanged(change.GetNewValue<bool>());
            }
        }

        private void UpdateItemsSourceCollectionSubscription()
        {
            var collection = ItemsSource as INotifyCollectionChanged;
            if (ReferenceEquals(collection, _itemsSourceCollection))
            {
                return;
            }

            if (_itemsSourceCollection != null)
            {
                _itemsSourceCollection.CollectionChanged -= OnItemsSourceCollectionChanged;
            }

            _itemsSourceCollection = collection;
            if (_itemsSourceCollection != null)
            {
                _itemsSourceCollection.CollectionChanged += OnItemsSourceCollectionChanged;
            }
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ApplySelectedIndex();
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            bool matchesSelectedItem = SelectedItem != null &&
                string.Equals(Text, FormatValue(SelectedItem), StringComparison.OrdinalIgnoreCase);

            if (!matchesSelectedItem && IsKeyboardFocusWithin)
            {
                _editingText = true;
                _showAllItems = false;
                _textChangedSinceLastOpen = true;
                SetCurrentValue(TextFilterProperty, FilterText);
            }

            base.OnTextChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                CommitText();
                e.Handled = true;
            }
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            CommitText();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (e.Handled || IsDropDownOpen || !IsKeyboardFocusWithin || ItemsSource == null)
            {
                return;
            }

            int step = e.Delta.Y < 0 ? 1 : e.Delta.Y > 0 ? -1 : 0;
            if (step == 0)
            {
                return;
            }

            int itemCount = 0;
            foreach (var item in ItemsSource)
            {
                itemCount++;
            }

            int nextIndex = SelectedIndex < 0 && step > 0
                ? 0
                : SelectedIndex + step;
            if (nextIndex < 0 || nextIndex >= itemCount || nextIndex == SelectedIndex)
            {
                return;
            }

            SetCurrentValue(SelectedIndexProperty, nextIndex);
            e.Handled = true;
        }

        private void OnSelectedItemChanged(object item)
        {
            if (item != null)
            {
                _lastCommittedItem = item;
                _editingText = false;

                int index = FindItemIndex(item);
                if (index >= 0 && index != SelectedIndex)
                {
                    SetCurrentValue(SelectedIndexProperty, index);
                }
            }
            // AutoCompleteBox clears SelectedItem while the user has only typed a prefix. Keep the
            // index and last committed value until CommitText decides whether that edit is valid.
        }

        private void ApplySelectedIndex()
        {
            int index = SelectedIndex;
            object item = GetItemAt(index);

            if (item == null)
            {
                if (index < 0)
                {
                    _lastCommittedItem = null;
                }

                if (index < 0 && SelectedItem != null)
                {
                    SetCurrentValue(SelectedItemProperty, null);
                }

                return;
            }

            if (!ReferenceEquals(SelectedItem, item))
            {
                SetCurrentValue(SelectedItemProperty, item);
            }
        }

        private void OnDropDownStateChanged(bool isOpen)
        {
            if (!isOpen)
            {
                if (!_refreshingDropDown)
                {
                    _showAllItems = false;
                    _textChangedSinceLastOpen = false;
                    SetCurrentValue(TextFilterProperty, FilterText);
                }

                return;
            }

            if (_refreshingDropDown || _textChangedSinceLastOpen)
            {
                _textChangedSinceLastOpen = false;
                return;
            }

            // AutoCompleteBox normally populates only after text changes. The arrow must also make
            // the complete source list available, including when the current text is already a
            // selected item's full name.
            _showAllItems = true;
            SetCurrentValue(TextFilterProperty, FilterText);
            _refreshingDropDown = true;
            PopulateComplete();
            SetCurrentValue(IsDropDownOpenProperty, true);
            _refreshingDropDown = false;
        }

        private void CommitText()
        {
            if (SelectedItem != null && FindItemIndex(SelectedItem) >= 0)
            {
                _lastCommittedItem = SelectedItem;
                _editingText = false;
                return;
            }

            string text = Text ?? string.Empty;
            object exactItem = FindExactItem(text);

            if (exactItem != null)
            {
                SetCurrentValue(SelectedItemProperty, exactItem);
            }
            else if (text.Length == 0)
            {
                SetCurrentValue(SelectedItemProperty, null);
                SetCurrentValue(SelectedIndexProperty, -1);
                _lastCommittedItem = null;
            }
            else if (_lastCommittedItem != null && FindItemIndex(_lastCommittedItem) >= 0)
            {
                SetCurrentValue(SelectedItemProperty, _lastCommittedItem);
            }
            else
            {
                SetCurrentValue(TextProperty, string.Empty);
                SetCurrentValue(SelectedItemProperty, null);
                SetCurrentValue(SelectedIndexProperty, -1);
                _lastCommittedItem = null;
            }

            _editingText = false;
        }

        private bool FilterText(string searchText, string itemText)
        {
            if (_showAllItems || string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            if (string.IsNullOrEmpty(itemText) || itemText.IndexOf(searchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return !string.IsNullOrEmpty(itemText);
            }

            string query = searchText.Trim();
            if (query.Length < 3)
            {
                return false;
            }

            int threshold = Math.Max(1, query.Length / 4);
            foreach (string token in itemText.Split(new[] { ' ', '_', '-', '.', ',', '[', ']', '(', ')', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (global::DSPRE.CoreExtensions.Levenshtein(query.ToLowerInvariant(), token.ToLowerInvariant()) <= threshold)
                {
                    return true;
                }
            }

            return false;
        }

        private object GetItemAt(int index)
        {
            if (index < 0 || ItemsSource == null)
            {
                return null;
            }

            int currentIndex = 0;
            foreach (object item in ItemsSource)
            {
                if (currentIndex++ == index)
                {
                    return item;
                }
            }

            return null;
        }

        private int FindItemIndex(object target)
        {
            if (target == null || ItemsSource == null)
            {
                return -1;
            }

            int index = 0;
            foreach (object item in ItemsSource)
            {
                if (ReferenceEquals(item, target) || Equals(item, target))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private object FindExactItem(string text)
        {
            if (ItemsSource == null)
            {
                return null;
            }

            foreach (object item in ItemsSource)
            {
                if (string.Equals(FormatValue(item), text, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
