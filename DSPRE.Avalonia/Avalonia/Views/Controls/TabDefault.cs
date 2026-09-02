using Avalonia.Controls;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>Opening a tab strip on a tab that is actually there.</summary>
    public static class TabDefault
    {
        /// <summary>
        /// Selects the first tab that is visible for this game.
        ///
        /// A hidden TabItem still counts as tab zero, so a strip whose first tab belongs to another game
        /// opens showing that tab's empty panel while the headers read something else.
        /// </summary>
        public static void SelectFirstVisible(TabControl tabs)
        {
            if (tabs == null) return;
            if (tabs.SelectedItem is TabItem shown && shown.IsVisible) return;
            foreach (var item in tabs.Items)
                if (item is TabItem tab && tab.IsVisible) { tabs.SelectedItem = tab; return; }
        }
    }
}
