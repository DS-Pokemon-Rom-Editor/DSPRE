using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// A small "Enable in toolbox" button beside an option that needs a code patch. The option itself stays in view,
    /// greyed out, and the tooltip names the patch and what it allows.
    /// </summary>
    public sealed class EnableInToolbox : Button
    {
        public static readonly StyledProperty<string> PatchProperty = AvaloniaProperty.Register<EnableInToolbox, string>(nameof(Patch));
        public static readonly StyledProperty<string> NeedProperty = AvaloniaProperty.Register<EnableInToolbox, string>(nameof(Need));

        /// <summary>The patch's toolbox key.</summary>
        public string Patch { get => GetValue(PatchProperty); set => SetValue(PatchProperty, value); }

        /// <summary>What the patch allows, as the start of a sentence: "Adding fonts".</summary>
        public string Need { get => GetValue(NeedProperty); set => SetValue(NeedProperty, value); }

        protected override Type StyleKeyOverride => typeof(Button);

        public EnableInToolbox()
        {
            Content = "Enable in toolbox";
            Padding = new Thickness(6, 1);
            FontSize = 12;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PatchProperty || change.Property == NeedProperty) ToolTip.SetTip(this, TipFor(Patch, Need));
        }

        public static string TipFor(string patch, string need) => string.IsNullOrEmpty(patch) ? null : PatchHandover.Needs(patch, need);

        protected override void OnClick()
        {
            base.OnClick();
            if (!string.IsNullOrEmpty(Patch)) AvaloniaEditorLauncher.OpenPatchToolboxAt(Patch);
        }
    }
}
