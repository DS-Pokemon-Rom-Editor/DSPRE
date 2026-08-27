using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for two trainer-class-keyed tables outside the 5 narc-owned
    /// domains: src/trainermoney.c's PrizeMoney[] (struct array keyed by `.class = TRAINERCLASS_X`) and
    /// src/pokemon.c's sTrainerGenders[] (flat `[TRAINERCLASS_X] = TRAINER_MALE,` array). Neither has a
    /// narc build target; both compile straight into the ARM9 overlay, so edits need a full "Compile
    /// ROM" to take effect in-game, same as any other src/*.c change.</summary>
    public static class HgEngineTrainerClassTables
    {
        private const string MoneyRelPath = "src/trainermoney.c";
        private const string GenderRelPath = "src/pokemon.c";
        private const string ClassHeaderRelPath = "include/constants/trainerclass.h";
        private const string Prefix = "TRAINERCLASS_";

        private static bool TryResolveClassDesignator(int trainerClassId, out string designator)
        {
            designator = null;
            var table = HgEngineSymbolTable.Load(ClassHeaderRelPath);
            return table != null && table.TryGetNameWithPrefix(trainerClassId, Prefix, out designator);
        }

        public static bool TryGetPrizeMultiplier(int trainerClassId, out int multiplier)
        {
            multiplier = 0;
            if (!HgEngineProject.IsActive || !TryResolveClassDesignator(trainerClassId, out string designator)) return false;

            string text = TryReadSource(MoneyRelPath, out _);
            if (text == null) return false;

            var m = Regex.Match(text, @"\.class\s*=\s*" + Regex.Escape(designator) + @"\s*,\s*\.multiplier\s*=\s*(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out multiplier);
        }

        public static bool TrySetPrizeMultiplier(int trainerClassId, int multiplier, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryResolveClassDesignator(trainerClassId, out string designator))
            { error = $"Could not resolve a trainer class designator for id {trainerClassId}."; return false; }

            string text = TryReadSource(MoneyRelPath, out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var m = Regex.Match(text, @"(\.class\s*=\s*" + Regex.Escape(designator) + @"\s*,\s*\.multiplier\s*=\s*)(\d+)");
            if (m.Success)
            {
                text = text.Remove(m.Groups[2].Index, m.Groups[2].Length).Insert(m.Groups[2].Index, multiplier.ToString());
            }
            else
            {
                int close = text.LastIndexOf("};");
                if (close < 0) { error = "Could not find the end of PrizeMoney[] to insert a new entry."; return false; }
                string newEntry = $"    {{ .class = {designator}, .multiplier = {multiplier} }},\n";
                text = text.Insert(close, newEntry);
            }
            File.WriteAllText(path, text);
            return true;
        }

        // TRAINER_MALE = 0, TRAINER_FEMALE = 1 (include/trainer_data.h's TrainerGender enum).
        public static bool TryGetGender(int trainerClassId, out int gender)
        {
            gender = 0;
            if (!HgEngineProject.IsActive || !TryResolveClassDesignator(trainerClassId, out string designator)) return false;

            string text = TryReadSource(GenderRelPath, out _);
            if (text == null) return false;

            var m = Regex.Match(text, @"\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*(TRAINER_MALE|TRAINER_FEMALE)\s*,");
            if (!m.Success) return false;
            gender = m.Groups[1].Value == "TRAINER_FEMALE" ? 1 : 0;
            return true;
        }

        public static bool TrySetGender(int trainerClassId, int gender, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryResolveClassDesignator(trainerClassId, out string designator))
            { error = $"Could not resolve a trainer class designator for id {trainerClassId}."; return false; }

            string text = TryReadSource(GenderRelPath, out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            string genderName = gender == 1 ? "TRAINER_FEMALE" : "TRAINER_MALE";
            var m = Regex.Match(text, @"(\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*)(TRAINER_MALE|TRAINER_FEMALE)(\s*,)");
            if (m.Success)
            {
                text = text.Substring(0, m.Index) + m.Groups[1].Value + genderName + m.Groups[3].Value + text.Substring(m.Index + m.Length);
            }
            else
            {
                int close = text.IndexOf("};", text.IndexOf("sTrainerGenders"));
                if (close < 0) { error = "Could not find the end of sTrainerGenders[] to insert a new entry."; return false; }
                string newEntry = $"    [{designator}] = {genderName},\n";
                text = text.Insert(close, newEntry);
            }
            File.WriteAllText(path, text);
            return true;
        }

        private static string TryReadSource(string relPath, out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
