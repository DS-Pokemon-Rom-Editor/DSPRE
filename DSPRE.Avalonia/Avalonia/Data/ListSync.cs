using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Updates an <see cref="ObservableCollection{T}"/> of combo labels IN PLACE to match a new list,
    /// rewriting changed entries, appending new ones, trimming removed ones. Updating in place (rather
    /// than Clear+Add) keeps any bound <c>SelectedIndex</c> intact, so a live refresh of dropdown labels
    /// or ROM names doesn't disturb the user's current selection.
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
