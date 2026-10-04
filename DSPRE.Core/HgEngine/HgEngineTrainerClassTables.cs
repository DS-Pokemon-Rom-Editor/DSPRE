using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for two trainer-class-keyed tables outside the 5 narc-owned
    /// domains: src/trainermoney.c's PrizeMoney[] (struct array keyed by `.class = TRAINERCLASS_X`) and
    /// src/pokemon.c's sTrainerGenders[] (flat `[TRAINERCLASS_X] = TRAINER_MALE,` array). Neither has a
    /// narc build target; both compile straight into the ARM9 overlay, so edits need a full "Compile
    /// ROM" to take effect in-game, same as any other src/*.c change. Entries match by the class id their
    /// name stands for, so an alias isn't taken for a missing entry.</summary>
    public static class HgEngineTrainerClassTables
    {
        private const string MoneyRelPath = "src/trainermoney.c";
        private const string GenderRelPath = "src/pokemon.c";
        private const string ClassHeaderRelPath = "include/constants/trainerclass.h";
        private const string Prefix = "TRAINERCLASS_";
        private const string MoneyTable = "PrizeMoney";
        private const string GenderTable = "sTrainerGenders";

        private static HgEngineSymbolTable Classes => HgEngineSymbolTable.Load(ClassHeaderRelPath);

        private static bool TryResolveClassDesignator(int trainerClassId, out string designator)
        {
            designator = null;
            return Classes?.TryGetNameWithPrefix(trainerClassId, Prefix, out designator) == true;
        }

        private static int? ClassOf(string token) =>
            Classes != null && HgEngineSourceExpression.TryEvaluate(token, n => Classes.TryGetValue(n, out int v) ? v : null, out int id) ? id : null;

        private static CInitItem MoneyEntry(string text, int classId, out CDeclaration table)
        {
            table = CSourceFile.For(text).Find(MoneyTable);
            return table?.Init.Items.FirstOrDefault(i => i.List?.Field("class") is CInitItem c && ClassOf(c.ValueText(text)) == classId);
        }

        private static CInitItem GenderEntry(string text, int classId, out CDeclaration table)
        {
            table = CSourceFile.For(text).Find(GenderTable);
            return table?.Init.Items.FirstOrDefault(i => i.IndexText != null && ClassOf(i.IndexText) == classId);
        }

        public static bool TryGetPrizeMultiplier(int trainerClassId, out int multiplier)
        {
            multiplier = 0;
            if (!HgEngineProject.IsActive) return false;
            string text = TryReadSource(MoneyRelPath, out _);
            if (text == null) return false;
            var entry = MoneyEntry(text, trainerClassId, out var table);
            if (table != null && CompiledOut(table)) return false;
            var field = entry?.List.Field("multiplier");
            return field != null && HgEngineSourceExpression.TryEvaluate(field.ValueText(text), _ => null, out multiplier);
        }

        public static bool TrySetPrizeMultiplier(int trainerClassId, int multiplier, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryResolveClassDesignator(trainerClassId, out string designator))
            { error = $"Could not resolve a trainer class designator for id {trainerClassId}."; return false; }

            string text = TryReadSource(MoneyRelPath, out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var entry = MoneyEntry(text, trainerClassId, out var table);
            if (table == null) { error = $"{MoneyRelPath} has no {MoneyTable}."; return false; }
            if (CompiledOut(table)) { error = $"{MoneyTable} is only built with EXPAND_TRAINER_PRIZE_MONEY; turn it on in hg-engine Settings to change prize money."; return false; }
            if (entry != null)
            {
                var field = entry.List.Field("multiplier");
                if (field == null) { error = $"{MoneyRelPath}: {designator}'s entry has no .multiplier."; return false; }
                text = text.Substring(0, field.ValueStart) + multiplier + text.Substring(field.ValueEnd);
            }
            else
            {
                int at = HgEngineSwarms.LineStart(text, table.Init.Close);
                text = text.Insert(at, $"    {{ .class = {designator}, .multiplier = {multiplier} }},\n");
            }
            return HgEngineVerifiedWrite.TryWrite(path, MoneyRelPath, text, written =>
            {
                var back = MoneyEntry(written, trainerClassId, out _)?.List.Field("multiplier");
                return back != null && HgEngineSourceExpression.TryEvaluate(back.ValueText(written), _ => null, out int m) && m == multiplier ? null : "the multiplier differs";
            }, out error);
        }

        // TRAINER_MALE = 0, TRAINER_FEMALE = 1 (include/trainer_data.h's TrainerGender enum).
        public static bool TryGetGender(int trainerClassId, out int gender)
        {
            gender = 0;
            if (!HgEngineProject.IsActive) return false;
            string text = TryReadSource(GenderRelPath, out _);
            if (text == null) return false;
            var entry = GenderEntry(text, trainerClassId, out var table);
            if (entry == null || CompiledOut(table)) return false;
            string value = entry.ValueText(text).Trim();
            if (value != "TRAINER_MALE" && value != "TRAINER_FEMALE") return false;
            gender = value == "TRAINER_FEMALE" ? 1 : 0;
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
            var entry = GenderEntry(text, trainerClassId, out var table);
            if (table == null) { error = $"{GenderRelPath} has no {GenderTable}."; return false; }
            if (CompiledOut(table)) { error = $"{GenderTable} is only built with EXPAND_TRAINER_GENDER_TABLE; turn it on in hg-engine Settings to change genders."; return false; }
            if (entry != null) text = text.Substring(0, entry.ValueStart) + genderName + text.Substring(entry.ValueEnd);
            else text = text.Insert(HgEngineSwarms.LineStart(text, table.Init.Close), $"    [{designator}] = {genderName},\n");

            return HgEngineVerifiedWrite.TryWrite(path, GenderRelPath, text, written =>
                TryGenderIn(written, trainerClassId) == genderName ? null : "the gender differs", out error);
        }

        private static string TryGenderIn(string text, int classId) => GenderEntry(text, classId, out _)?.ValueText(text).Trim();

        // The table sits under an #ifdef the checkout's config.h leaves off.
        private static bool CompiledOut(CDeclaration table) => HgEngineConfigState.Compiles(table.Conditions) == false;

        private static string TryReadSource(string relPath, out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path).Replace("\r\n", "\n") : null;
        }
    }
}
