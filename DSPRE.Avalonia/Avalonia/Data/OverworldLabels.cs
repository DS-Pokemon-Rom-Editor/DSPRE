namespace DSPRE.Avalonia.Data
{
    /// <summary>What to call an overworld sprite entry, which is a number until somebody names it.</summary>
    public static class OverworldLabels
    {
        public const string Key = "overworld_sprites";

        /// <summary>The entry's name if the project or the global list gives it one, otherwise its number.</summary>
        public static string Of(uint id) => LabelStore.GetLabel(Key, (int)id);
    }
}
