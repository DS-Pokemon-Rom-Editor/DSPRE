using System.Collections.Generic;
using System.Text;

namespace DSPRE
{
    /// <summary>
    /// What each toolbox patch does, where its changes are edited afterwards, and anything to know before applying
    /// it, shown from the toolbox's Notes button. Patches without their own notes show their summary and credit.
    /// </summary>
    public static class PatchNotes
    {
        private static readonly Dictionary<string, string> Detail = new()
        {
            ["trainerClassMetadata"] =
                "What it does\n" +
                "Each trainer class gets its own record in a/1/5/5 holding its gender, prize money, eye-contact music, " +
                "battle music and VS intro, in place of the game's shared tables. Classes can then be copied and the " +
                "last one removed.\n\n" +
                "Where to edit\n" +
                "Gender, prize, eye-contact and battle music: the Trainer editor's Classes tab, which also copies and " +
                "removes classes.\n" +
                "VS intro (style, name shown and art): the VS intro editor, which lists every class's own intro.\n\n" +
                "VS styles\n" +
                "0 Dynamic terrain/time: the Poké Ball intros. The game picks grass, water or cave, and early or late, " +
                "from the area and the trainer's level.\n" +
                "1 Gym Leader / Rival: the portrait slides in with a banner and the VS mark. The name is a fixed " +
                "trainer name or the rival's saved name.\n" +
                "2 Elite Four / Champion: the portrait in the League frame against the player, with a clash shake.\n" +
                "3 Rocket Admin: the portrait over a backdrop.\n" +
                "4 Kimono Girl: shoji doors slide shut and open.\n" +
                "5 Red: the old Poké Ball while black blocks fill the screen.\n" +
                "6 Team Rocket: Rocket R's fly in and shrink away.\n" +
                "7 to 12: one fixed Poké Ball intro each (normal, water and cave, early then late).\n" +
                "13 Frontier Brain: like style 1, with the Frontier VS mark.\n\n" +
                "Requirements\n" +
                "US HeartGold or SoulSilver, the ARM9 expansion and a ds-rom project. Not compatible with hg-engine.\n\n" +
                "Backups\n" +
                "Applying keeps .backup copies of the ARM9, overlays 1, 12, 80, 115, 117, 118, 119 and 120 and the " +
                "synthetic overlay, and of a/1/5/5 as unpacked/a155.narc.backup.",

            ["trainerShiny"] =
                "What it does\n" +
                "Party members ticked Shiny in the Trainer editor battle as shiny. The routine sits in the synthetic " +
                "overlay and the trainer party setup is pointed at it.\n\n" +
                "Where to edit\n" +
                "The Shiny box on each party member in the Trainer editor.\n\n" +
                "Requirements\n" +
                "US HeartGold or SoulSilver (or Italian HeartGold), the ARM9 expansion and a ds-rom project.",
        };

        /// <summary>The notes for a patch, with where an installed copy sits when that can be read back.</summary>
        public static string For(PatchToolboxLogic.PatchInfo patch)
        {
            if (patch == null) return "";
            StringBuilder text = new StringBuilder();
            // A patch with its own notes says what it does there; the rest show their summary.
            text.Append(Detail.TryGetValue(patch.Key, out string detail) ? detail : patch.Description).Append("\n\n");
            if (patch.Key == "trainerClassMetadata" && patch.State == PatchToolboxLogic.PatchState.Applied &&
                TrainerClassMetadataPatch.InstalledOffset() is uint offset)
                text.Append("Installed at\nSynthetic overlay offset 0x").Append(offset.ToString("X"))
                    .Append(" (runtime address 0x").Append((TrainerClassMetadataPatch.SyntheticBase + offset).ToString("X8")).Append(").\n\n");
            string credit = PatchToolboxLogic.CreditLine(patch.Key);
            if (credit != null) text.Append("Credits\n").Append(credit).Append('.');
            return text.ToString().TrimEnd();
        }
    }
}
