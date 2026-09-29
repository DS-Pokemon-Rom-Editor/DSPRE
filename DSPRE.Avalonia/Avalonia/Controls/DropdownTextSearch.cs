using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Type-to-jump in dropdowns ignoring accents and case, also matching after a leading [id] tag.</summary>
    public static class DropdownTextSearch
    {
        private sealed class Typed { public string Term = ""; public DateTime Last; }
        private static readonly ConditionalWeakTable<ComboBox, Typed> Terms = new();

        // Avalonia's search forgets the typed letters after the same pause.
        private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

        public static void Install() =>
            InputElement.TextInputEvent.AddClassHandler<ComboBox>(OnTextInput, RoutingStrategies.Tunnel);

        private static void OnTextInput(ComboBox box, TextInputEventArgs e)
        {
            if (e.Handled || !box.IsTextSearchEnabled || string.IsNullOrEmpty(e.Text)) return;
            var typed = Terms.GetOrCreateValue(box);
            var now = DateTime.UtcNow;
            if (now - typed.Last > Pause) typed.Term = "";
            typed.Last = now;
            typed.Term += e.Text;

            int found = Find(box, typed.Term, afterTag: false);
            if (found < 0) found = Find(box, typed.Term, afterTag: true);
            if (found >= 0) box.SelectedIndex = found;
            e.Handled = true;
        }

        private static int Find(ComboBox box, string term, bool afterTag)
        {
            for (int i = 0; i < box.ItemCount; i++)
            {
                string text = TextOf(box.Items[i]);
                if (afterTag)
                {
                    if (!text.StartsWith('[')) continue;
                    int close = text.IndexOf(']');
                    if (close < 0) continue;
                    text = text.Substring(close + 1).TrimStart();
                }
                if (SearchMatch.StartsWith(text, term)) return i;
            }
            return -1;
        }

        private static string TextOf(object item) => item switch
        {
            null => "",
            ContentControl c => c.Content?.ToString() ?? "",
            _ => item.ToString() ?? "",
        };
    }
}
