using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;

namespace DSPRE.Editors
{
    /// <summary>
    /// Rewrites a TM's item description for the move it now teaches. PlatPatches' extra TMs keep their
    /// "It teaches the move X." form; any other gets the new move's own description, rewrapped to the bag's text box.
    /// </summary>
    public static class TmItemDescriptions
    {
        public const string PatchTemplate = "It teaches the move {0}.\\nIt can be used on a Pokemon.";

        public static int Bank => RomInfo.itemDescriptionsTextNumber;

        public static string WhyNot() =>
            RomInfo.isHGE && !HgEngine.HgEngineProject.IsActive ? "Open the hg-engine folder to change its item text."
            : !RomInfo.isHGE && Bank <= 0 ? "Item descriptions aren't located for this game version."
            : !FieldFontCharacters.Ready || FieldFont.LoadSystemFont() == null ? "The game font couldn't be read."
            : null;

        public sealed class Result
        {
            public int Updated { get; set; }
            /// <summary>Machine labels whose new description would not fit the bag's text box.</summary>
            public List<string> Kept { get; } = new List<string>();
        }

        /// <summary>
        /// The text archive and line holding an item's bag description. hg-engine splits them by generation, so
        /// each item is looked up in its checkout.
        /// </summary>
        public static bool TryLocate(int item, out int bank, out int line)
        {
            if (RomInfo.isHGE) return HgEngine.HgEngineItemText.TryLocate(item, "DESCRIPTION", out bank, out line);
            bank = Bank;
            line = item;
            return bank > 0 && item >= 0;
        }

        /// <summary>Rewrites the descriptions of machines whose move changed.</summary>
        public static Result Update(IReadOnlyList<(int Machine, int OldMove, int NewMove)> changes)
        {
            Result result = new Result();
            if (changes.Count == 0 || WhyNot() != null) return result;

            Dictionary<int, EditableTextBank> banks = new Dictionary<int, EditableTextBank>();
            EditableTextBank BankOf(int id) => banks.TryGetValue(id, out EditableTextBank b) ? b : banks[id] = new EditableTextBank(id);
            List<string> moveDescriptions = new TextArchive(RomInfo.moveDescriptionsTextNumbers).messages;
            string[] moveNames = RomInfo.GetAttackNames();
            (int Width, int Lines) layout = Layout(BankOf);
            FieldFont font = FieldFont.LoadSystemFont();
            HashSet<int> touched = new HashSet<int>();

            foreach ((int machine, int oldMove, int newMove) in changes)
            {
                int item = TMEditor.MachineItemId(machine);
                string label = TMEditor.MachineLabelFromIndex(machine);
                if (oldMove == newMove || !TryLocate(item, out int bankId, out int line)) continue;
                EditableTextBank descriptions = BankOf(bankId);
                if (descriptions.ReadOnlyReason != null || line >= descriptions.Messages.Count) { result.Kept.Add(label); continue; }
                string now = Flat(descriptions.Messages[line]);
                string next = null;

                if (oldMove < moveNames.Length && newMove < moveNames.Length
                    && now == Flat(string.Format(PatchTemplate, moveNames[oldMove])))
                    next = string.Format(PatchTemplate, moveNames[newMove]);
                else if (newMove < moveDescriptions.Count)
                    next = Wrap(moveDescriptions[newMove], layout.Width, layout.Lines, font);

                if (next == null) { result.Kept.Add(label); continue; }
                descriptions.Messages[line] = next;
                touched.Add(bankId);
                result.Updated++;
            }

            foreach (int id in touched)
                if (banks[id].Save() is string error) AppLogger.Error($"TM descriptions: text {id} was not saved: {error}");
            return result;
        }

        /// <summary>The widest line and most lines any vanilla TM description uses, so rewrapped text fits the same box.</summary>
        private static (int Width, int Lines) Layout(Func<int, EditableTextBank> bankOf)
        {
            FieldFont font = FieldFont.LoadSystemFont();
            int width = 0, lines = 0;
            for (int i = 0; i < TMEditor.VanillaMachineCount; i++)
            {
                if (!TryLocate(TMEditor.MachineItemId(i), out int bank, out int line)) continue;
                List<string> descriptions = bankOf(bank).Messages;
                if (line >= descriptions.Count) continue;
                string[] parts = Lines(descriptions[line]);
                lines = Math.Max(lines, parts.Length);
                foreach (string p in parts) width = Math.Max(width, font.Measure(p, FieldFontCharacters.GlyphFor));
            }
            return (width, lines);
        }

        /// <summary>Greedy word wrap by the font's letter widths; null when it needs more lines than the box has.</summary>
        public static string Wrap(string text, int width, int maxLines, FieldFont font)
        {
            List<string> lines = new List<string>();
            string line = "";
            foreach (string word in Flat(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.Measure(candidate, FieldFontCharacters.GlyphFor) > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = candidate;
            }
            if (line.Length > 0) lines.Add(line);
            return lines.Count == 0 || lines.Count > maxLines ? null : string.Join("\\n", lines);
        }

        private static string[] Lines(string message) =>
            Regex.Split(message ?? "", @"\\[nrf]").Where(l => l.Length > 0).ToArray();

        private static string Flat(string message) =>
            Regex.Replace(string.Join(" ", Lines(message)), @"\s+", " ").Trim();
    }
}
