using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>What to call a script outside a map's own file: the name a project gave it, if any.</summary>
    public static class CommonScriptLabels
    {
        /// <summary>The first script number the games look for outside a map's own file.</summary>
        public const int First = 2000;

        /// <summary>The word an unnamed entry gets in the label lists.</summary>
        public const string Unnamed = "Script";

        public static string Key => GameFamily switch
        {
            GameFamilies.DP => "common_scripts_dp", GameFamilies.Plat => "common_scripts_pt", _ => "common_scripts_hgss",
        };

        /// <summary>The script's name, or null when nobody has named it.</summary>
        public static string Of(int scriptNumber)
        {
            if (scriptNumber < First) return null;
            int index = scriptNumber - First;
            string label = LabelStore.GetLabel(Key, index);
            return label == $"{Unnamed} {index}" ? null : label;
        }

        /// <summary>Names the script in this project's labels.</summary>
        /// <returns>null when saved, else why not.</returns>
        public static string Rename(int scriptNumber, string name)
        {
            if (scriptNumber < First) return null;
            LabelStore.SetLabel(Key, scriptNumber - First, name, global: false);
            string error = LabelStore.Save(global: false);
            AppEvents.RaiseLabelsChanged();
            return error;
        }
    }
}
