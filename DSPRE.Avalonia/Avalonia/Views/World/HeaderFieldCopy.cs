using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>
    /// A right-click menu on a header field's label that copies the field, or pastes a value copied from any header
    /// earlier in the session.
    /// </summary>
    public static class HeaderFieldCopy
    {
        public static readonly AttachedProperty<string> KeyProperty =
            AvaloniaProperty.RegisterAttached<Control, string>("Key", typeof(HeaderFieldCopy));

        public static string GetKey(Control label) => label.GetValue(KeyProperty);
        public static void SetKey(Control label, string key) => label.SetValue(KeyProperty, key);

        static HeaderFieldCopy()
        {
            KeyProperty.Changed.AddClassHandler<Control>((label, _) => Attach(label));
        }

        // A field's label, or the title of a group of settings copied together.
        private static string NameOf(Control label) =>
            label is TextBlock text ? text.Text : label is global::Avalonia.Controls.Primitives.HeaderedContentControl group ? group.Header?.ToString()?.ToLowerInvariant() : "";

        private static void Attach(Control label)
        {
            // A label is hit only on its glyphs unless it has a background.
            if (label is TextBlock text) text.Background ??= Brushes.Transparent;
            MenuItem copy = new MenuItem();
            MenuItem paste = new MenuItem();
            ContextMenu menu = new ContextMenu { Items = { copy, paste } };
            menu.Opening += (_, _) =>
            {
                string key = GetKey(label);
                HeaderEditorViewModel vm = label.DataContext as HeaderEditorViewModel;
                copy.Header = "Copy " + NameOf(label);
                string held = vm?.HeldField(key);
                paste.Header = held == null ? "Paste" : "Paste " + held;
                copy.IsEnabled = vm?.CanCopyField(key) == true;
                paste.IsEnabled = held != null && vm?.CanCopyField(key) == true;
            };
            copy.Click += (_, _) => (label.DataContext as HeaderEditorViewModel)?.CopyField(GetKey(label));
            paste.Click += (_, _) => (label.DataContext as HeaderEditorViewModel)?.PasteField(GetKey(label));
            label.ContextMenu = menu;
        }
    }
}
