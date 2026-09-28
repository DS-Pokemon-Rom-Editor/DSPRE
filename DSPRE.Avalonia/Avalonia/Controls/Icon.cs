using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// 16px icons as images: <c>controls:Icon.Key="save"</c> on a button puts the icon before its text.
    /// Symbol glyphs and emoji are missing from most Linux fonts, so icons are never characters.
    /// </summary>
    public static class Icon
    {
        public static readonly AttachedProperty<string> KeyProperty =
            AvaloniaProperty.RegisterAttached<ContentControl, string>("Key", typeof(Icon));

        public static string GetKey(ContentControl c) => c.GetValue(KeyProperty);
        public static void SetKey(ContentControl c, string value) => c.SetValue(KeyProperty, value);

        /// <summary>The label beside the icon, for labels that come from a binding (binding Content would be replaced).</summary>
        public static readonly AttachedProperty<string> TextProperty =
            AvaloniaProperty.RegisterAttached<ContentControl, string>("Text", typeof(Icon));

        public static string GetText(ContentControl c) => c.GetValue(TextProperty);
        public static void SetText(ContentControl c, string value) => c.SetValue(TextProperty, value);

        /// <summary>Puts the icon after the text, for arrows that point on (Next, a menu's chevron).</summary>
        public static readonly AttachedProperty<bool> AfterProperty =
            AvaloniaProperty.RegisterAttached<ContentControl, bool>("After", typeof(Icon));

        public static bool GetAfter(ContentControl c) => c.GetValue(AfterProperty);
        public static void SetAfter(ContentControl c, bool value) => c.SetValue(AfterProperty, value);

        private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

        static Icon()
        {
            KeyProperty.Changed.AddClassHandler<ContentControl>((c, _) => Apply(c));
            TextProperty.Changed.AddClassHandler<ContentControl>((c, _) => Apply(c));
            AfterProperty.Changed.AddClassHandler<ContentControl>((c, _) => Apply(c));
            ToolTip.TipProperty.Changed.AddClassHandler<ContentControl>((c, _) => { if (GetKey(c) != null) Name(c); });
            ContentControl.ContentProperty.Changed.AddClassHandler<ContentControl>((c, e) =>
            {
                if (e.NewValue is string && GetKey(c) != null) Apply(c);
            });
        }

        /// <summary>The icon's bitmap: a DSPRE icon from Assets/Icons, else one of the shell's own icons by name.</summary>
        public static Bitmap Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            Bitmap bmp = null;
            try
            {
                var uri = new Uri($"avares://DSPRE.Avalonia/Avalonia/Assets/Icons/{key}.png");
                if (AssetLoader.Exists(uri)) bmp = new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception ex) { AppLogger.Warn($"Icon '{key}' failed to load: {ex.Message}"); }
            bmp ??= ResourceImages.GetBitmap(key);
            if (bmp == null) AppLogger.Warn($"No icon named '{key}'.");
            Cache[key] = bmp;
            return bmp;
        }

        /// <summary>A 16px image of the icon, for places that build controls in code.</summary>
        public static Image Image(string key)
        {
            var bmp = Get(key);
            var img = new Image { Source = bmp, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            img.Classes.Add("icon");
            // The drawn icons are exact 16px pixel art; the shell's larger ones need smoothing to shrink.
            RenderOptions.SetBitmapInterpolationMode(img,
                bmp != null && bmp.PixelSize.Width == 16 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
            return img;
        }

        /// <summary>Icon followed by text, as button content built in code.</summary>
        public static Control Content(string key, string text, bool after = false)
        {
            if (string.IsNullOrEmpty(text)) return Image(key);
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(after ? label : Image(key));
            panel.Children.Add(after ? Image(key) : label);
            return panel;
        }

        private static readonly AttachedProperty<string> StaticTextProperty =
            AvaloniaProperty.RegisterAttached<ContentControl, string>("StaticText", typeof(Icon));

        private static void Apply(ContentControl c)
        {
            if (c.Content is string s) c.SetValue(StaticTextProperty, s);
            string text = GetText(c) ?? c.GetValue(StaticTextProperty) ?? "";
            string key = GetKey(c);
            c.Content = key == null ? text : Content(key, text, GetAfter(c));
            Name(c);
        }

        // Screen readers and UI automation read a button's name from text content, which an icon panel hides.
        private static void Name(ContentControl c)
        {
            string text = GetText(c) ?? c.GetValue(StaticTextProperty);
            string name = !string.IsNullOrEmpty(text) ? text : ToolTip.GetTip(c) as string;
            if (!string.IsNullOrEmpty(name)) AutomationProperties.SetName(c, name);
        }
    }

    /// <summary>A standalone 16px icon: <c>&lt;controls:IconImage Key="pin"/&gt;</c>.</summary>
    public class IconImage : Image
    {
        public static readonly StyledProperty<string> KeyProperty = AvaloniaProperty.Register<IconImage, string>(nameof(Key));

        public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

        protected override Type StyleKeyOverride => typeof(Image);

        public IconImage()
        {
            Width = 16; Height = 16;
            VerticalAlignment = VerticalAlignment.Center;
            Classes.Add("icon");
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != KeyProperty) return;
            var bmp = Icon.Get(Key);
            Source = bmp;
            RenderOptions.SetBitmapInterpolationMode(this,
                bmp != null && bmp.PixelSize.Width == 16 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
        }
    }
}
