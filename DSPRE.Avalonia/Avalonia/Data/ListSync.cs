using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Updates combo labels in place, so a bound <c>SelectedIndex</c> keeps the user's selection when labels or names
    /// refresh.
    /// </summary>
    public static class ListSync
    {
        public static void Apply(ObservableCollection<string> target, IReadOnlyList<string> items)
        {
            if (target == null || items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                if (i < target.Count) { if (target[i] != items[i]) target[i] = items[i]; }
                else target.Add(items[i]);
            }
            while (target.Count > items.Count) target.RemoveAt(target.Count - 1);
        }
    }
}
