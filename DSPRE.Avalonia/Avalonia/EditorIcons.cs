using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// The editor icons the WinForms shell used, exposed as bindable properties so XAML can reach them with
    /// <c>{x:Static}</c>.
    /// </summary>
    public static class EditorIcons
    {
        public static Bitmap Header       { get; } = ResourceImages.GetBitmap("map_header");
        public static Bitmap Map          { get; } = ResourceImages.GetBitmap("map_editor");
        public static Bitmap Events       { get; } = ResourceImages.GetBitmap("event_editor");
        public static Bitmap Matrix       { get; } = ResourceImages.GetBitmap("matrix_editor");
        public static Bitmap AreaData     { get; } = ResourceImages.GetBitmap("tileset_editor");
        public static Bitmap Encounters   { get; } = ResourceImages.GetBitmap("wild_editor");
        public static Bitmap Scripts      { get; } = ResourceImages.GetBitmap("script_editor");
        public static Bitmap LevelScripts { get; } = ResourceImages.GetBitmap("destroyLevelScript");
        public static Bitmap Text         { get; } = ResourceImages.GetBitmap("text_editor");
    }
}
