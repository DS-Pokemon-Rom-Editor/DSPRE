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
            RomInfo.isHGE ? "hg-engine builds item text from its source."
            : Bank <= 0 ? "Item descriptions aren't located for this game version."
            : !FieldFontCharacters.Ready || FieldFont.LoadSystemFont() == null ? "The game font couldn't be read."
            : null;

        public sealed class Result
        {
            public int Updated { get; set; }
            /// <summary>Machine labels whose new description would not fit the bag's text box.</summary>
            public List<string> Kept { get; } = new List<string>();
        }

        /// <summary>Rewrites the descriptions of machines whose move changed.</summary>
        public static Result Update(IReadOnlyList<(int Machine, int OldMove, int NewMove)> changes)
        {
            var result = new Result();
            if (changes.Count == 0 || WhyNot() != null) return result;

            var descriptions = new TextArchive(Bank);
            var moveDescriptions = new TextArchive(RomInfo.moveDescriptionsTextNumbers).messages;
            string[] moveNames = RomInfo.GetAttackNames();
            var layout = Layout(descriptions.messages);
            var font = FieldFont.LoadSystemFont();

            foreach (var (machine, oldMove, newMove) in changes)
            {
                int item = TMEditor.MachineItemId(machine);
                string label = TMEditor.MachineLabelFromIndex(machine);
                if (item < 0 || item >= descriptions.messages.Count || oldMove == newMove) continue;
                string now = Flat(descriptions.messages[item]);
                string next = null;

                if (oldMove < moveNames.Length && newMove < moveNames.Length
                    && now == Flat(string.Format(PatchTemplate, moveNames[oldMove])))
                    next = string.Format(PatchTemplate, moveNames[newMove]);
                else if (newMove < moveDescriptions.Count)
                    next = Wrap(moveDescriptions[newMove], layout.Width, layout.Lines, font);

                if (next == null) { result.Kept.Add(label); continue; }
                descriptions.messages[item] = next;
                result.Updated++;
            }

            if (result.Updated > 0) descriptions.SaveToExpandedDir(Bank, false);
            return result;
        }

        /// <summary>The widest line and most lines any vanilla TM description uses, so rewrapped text fits the same box.</summary>
        private static (int Width, int Lines) Layout(List<string> descriptions)
        {
            var font = FieldFont.LoadSystemFont();
            int width = 0, lines = 0;
            for (int i = 0; i < TMEditor.VanillaMachineCount; i++)
            {
                int item = TMEditor.MachineItemId(i);
                if (item < 0 || item >= descriptions.Count) continue;
                string[] parts = Lines(descriptions[item]);
                lines = Math.Max(lines, parts.Length);
                foreach (string p in parts) width = Math.Max(width, font.Measure(p, FieldFontCharacters.GlyphFor));
            }
            return (width, lines);
        }

        /// <summary>Greedy word wrap by the font's letter widths; null when it needs more lines than the box has.</summary>
        public static string Wrap(string text, int width, int maxLines, FieldFont font)
        {
            var lines = new List<string>();
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
