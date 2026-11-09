using Avalonia;
using Avalonia.Media;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The fonts App.axaml defines, for views built in code. XAML uses the resource by its key.</summary>
    public static class UiFonts
    {
        /// <summary>The fixed-width font for code, logs and hex, the <c>CodeFont</c> resource.</summary>
        public static FontFamily Code =>
            Application.Current != null && Application.Current.TryGetResource("CodeFont", null, out object found) && found is FontFamily font
                ? font
                : FontFamily.Default;
    }
}
