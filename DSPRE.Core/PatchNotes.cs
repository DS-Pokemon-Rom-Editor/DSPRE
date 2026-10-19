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
            ["trainerCustomPokeballs"] =
                "What it does\n" +
                "Repurposes each trainer party member's ball capsule field as a Poké Ball item ID. " +
                "Zero keeps the default Poké Ball. Enemy trainers lose capsule seal effects. Existing nonzero " +
                "capsule values become item IDs, so review existing parties after applying.\n\n" +
                "Where to edit\n" +
                "The Trainer editor switches Ball Capsule to a Poké Ball picker, hides Edit and previews the item icon. " +
                "The picker offers the 16 original balls and the eight HGSS balls, including Apricorn balls and Sport Ball. " +
                "Park Ball is not supported by the game's setter. Unknown values are preserved until replaced.\n\n" +
                "Requirements\n" +
                "HeartGold or SoulSilver, any language. No ARM9 expansion or synthetic overlay required. " +
                "Not compatible with hg-engine.",
            ["classIntros"] =
                "What it does\n" +
                "Each trainer class gets its own VS intro and battle music. Without it only gym leaders, the Elite Four, Team Galactic " +
                "and the Frontier Brains (the Tower Tycoon in Diamond and Pearl) have one. Gym leader and Elite Four intros take their " +
                "own portrait, name and colours in Platinum, or a face cut from any class's battle sprite in Diamond and Pearl.\n" +
                "Nothing changes in game until you edit a class.\n\n" +
                "Where to edit\n" +
                "The VS intro editor's Class intros tab.\n\n" +
                "Good to know\n" +
                "The Battle Frontier (the Battle Tower in Diamond and Pearl), link and double battles, and in Platinum the Fight Area " +
                "tag battle with Volkner still pick their intro by the kind of battle.\n\n" +
                "Requirements\n" +
                "US or European English Diamond, Pearl or Platinum, with the ARM9 expansion. Not compatible with hg-engine.",
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
                "0 Dynamic terrain/time: the Poké Ball intros. The game picks normal, water or cave from the tile the " +
                "player stands on, and early or late from the time of day (late from 20:00 to 03:59; cave battle " +
                "backgrounds always count as late).\n" +
                "1 Gym Leader / Rival: the portrait slides in with a banner and the VS mark. The name is a fixed " +
                "trainer name or the rival's saved name.\n" +
                "2 Elite Four / Champion: the portrait in the League frame against the player, with a clash shake.\n" +
                "3 Rocket Admin: the portrait over a backdrop.\n" +
                "4 Kimono Girl: shoji doors slide shut and open.\n" +
                "5 Red: a field of blocks sweeps across the screen.\n" +
                "6 Team Rocket: Rocket R's fly in and shrink away.\n" +
                "7 to 12: one fixed Poké Ball intro each (normal, water and cave, early then late).\n" +
                "13 Frontier Brain: like style 1, with the Frontier VS mark.\n\n" +
                "Requirements\n" +
                "US HeartGold or SoulSilver, the ARM9 expansion and a ds-rom project. Not compatible with hg-engine.",

            ["trainerShiny"] =
                "What it does\n" +
                "Party members ticked Shiny in the Trainer editor battle as shiny. The routine sits in the synthetic " +
                "overlay and the trainer party setup is pointed at it.\n\n" +
                "Where to edit\n" +
                "The Shiny box on each party member in the Trainer editor.\n\n" +
                "Requirements\n" +
                "US HeartGold or SoulSilver (or Italian HeartGold), the ARM9 expansion and a ds-rom project.",
            ["punchingMovesExpanded"] =
                "What it does\n" +
                "The battle code keeps the moves Iron Fist boosts as a list of 15 in its overlay. This moves the list into " +
                "the expanded ARM9 area with room for 64 and points the code at it; the Move Data editor changes the " +
                "list only once it is here.\n\n" +
                "Where to edit\n" +
                "The Move Data editor's Ability based group: tick Punching on a move.\n\n" +
                "Trainer AI\n" +
                "Nothing more to do: the AI scores a move's damage with Iron Fist counted, so it sees the new list.\n\n" +
                "Guide\n" +
                "https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/editing_moves/#punching-moves\n\n" +
                "Requirements\n" +
                "US HeartGold, Platinum (Rev 1) or Diamond, the ARM9 expansion and a ds-rom project. Not compatible with hg-engine.\n\n" +
                "Research\n" +
                "The list, its count check and its pointer were documented by MrHam88 and the DS Pokémon Hacking wiki's move editing guide.",
            ["evolutionSlots"] =
                "What it does\n" +
                "The game reads a Pokémon's evolutions into a buffer of 7 slots and checks those 7 when a Pokémon levels up, " +
                "is traded or has an item used on it. This raises all four numbers to 42, the most the code's one-byte " +
                "buffer size allows, and pads every evolution file to fill the new buffer, since a shorter file would leave " +
                "leftover memory in the slots the game checks.\n\n" +
                "Where to edit\n" +
                "Pokémon > Evolutions editor, which then lists 42 slots.\n\n" +
                "Limits\n" +
                "42 evolutions per Pokémon. The old WinForms editor still shows only the first 7.\n\n" +
                "Requirements\n" +
                "US Diamond, Platinum (Rev 1) or HeartGold. Not for hg-engine, which sets its slots in its own source.",
            ["regionalDexCount"] =
                "What it does\n" +
                "The game checks the regional Pokédex against numbers built into its code: how many regional species " +
                "complete it, the professor's last rating step before he calls it complete, and in Diamond, Pearl and " +
                "Platinum the highest regional number it looks up. This sets them from the regional order saved in " +
                "Pokédex Lists. Completion leaves out the mythicals the game ignores: Manaphy in Diamond and Pearl, Mew " +
                "and Celebi in HeartGold and SoulSilver.\n\n" +
                "Where to edit\n" +
                "Pokémon > Pokédex lists, Regional dex tab. Saving a different size there offers this patch.\n\n" +
                "Limits\n" +
                "The numbers are single bytes, so the regional Pokédex can hold at most 255 counted species. The " +
                "professor's earlier rating steps keep their numbers.\n\n" +
                "Requirements\n" +
                "US Diamond, Platinum (Rev 1) or HeartGold. Not for hg-engine.",
            ["soundMovesExpanded"] =
                "What it does\n" +
                "The battle code keeps the moves Soundproof blocks as a list of 12 in its overlay. This moves the list into " +
                "the expanded ARM9 area with room for 64 and points the code at it; the Move Data editor changes the " +
                "list only once it is here.\n\n" +
                "Where to edit\n" +
                "The Move Data editor's Ability based group: tick Sound on a move.\n\n" +
                "Trainer AI\n" +
                "The AI keeps its own list of sound moves in its overlay, which this patch does not change, so trainers " +
                "with the Basic flag may still use a new sound move into Soundproof. The DS Pokémon Hacking wiki's move " +
                "editing guide shows how to edit that table.\n\n" +
                "Guide\n" +
                "https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/editing_moves/#sound-based-moves\n\n" +
                "Requirements\n" +
                "US HeartGold, Platinum (Rev 1) or Diamond, the ARM9 expansion and a ds-rom project. Not compatible with hg-engine.\n\n" +
                "Research\n" +
                "The list, its count check and its pointer were documented by MrHam88 and the DS Pokémon Hacking wiki's move editing guide.",
            ["pickupSlots"] =
                "What it does\n" +
                "In the game, each Pickup level range reads its nine items and two rare items from one shared sliding list. This gives every range its own items. Pickup gives the same items as before until you change them.\n\n" +
                "Where to edit\n" +
                "Items > Item tables (pickup, hidden, Rock Smash).\n\n" +
                "Requirements\n" +
                "Diamond, Pearl, Platinum, HeartGold or SoulSilver, with the ARM9 expansion. The toolbox lists the versions it is mapped for. Not compatible with hg-engine.",
            ["martsExpanded"] =
                "What it does\n" +
                "Moves every mart into the expanded ARM9 area with room for 64 marts of 64 items. Every mart keeps its items.\n\n" +
                "Where to edit\n" +
                "Items > Mart editor, which can then add items and custom marts.\n\n" +
                "Requirements\n" +
                "Diamond, Pearl, Platinum, HeartGold or SoulSilver, with the ARM9 expansion. English dumps and the other measured versions are supported. Not compatible with hg-engine.",
            ["bpShopExpanded"] =
                "What it does\n" +
                "Moves the Battle Point exchange counters and their prices into the expanded ARM9 area with room for 255 items on each counter.\n\n" +
                "Where to edit\n" +
                "Items > Battle Point shop.\n\n" +
                "Requirements\n" +
                "Platinum, with the ARM9 expansion. Not compatible with hg-engine.",
            ["swarmTableExpanded"] =
                "What it does\n" +
                "Moves the table of places a swarm can happen into the expanded ARM9 area with room for 255 rows.\n\n" +
                "Where to edit\n" +
                "Pokémon > Special encounters editor, Swarms tab.\n\n" +
                "Requirements\n" +
                "US HeartGold, SoulSilver, Platinum (Rev 1) or Diamond and the other measured versions, with the ARM9 expansion. Not compatible with hg-engine.",
            ["typeChartExpanded"] =
                "What it does\n" +
                "Moves the type chart into the expanded ARM9 area with room for 253 matchups. The existing matchups are kept.\n\n" +
                "Where to edit\n" +
                "Pokémon > Type chart.\n\n" +
                "Requirements\n" +
                "Diamond, Pearl, Platinum, HeartGold or SoulSilver on the measured versions, with the ARM9 expansion. Not compatible with hg-engine.",
            ["vsIntroTimings"] =
                "What it does\n" +
                "Lets each trainer class set its own VS intro timings: flashes, slides and holds. A timing left at 0 keeps the game's value.\n\n" +
                "Where to edit\n" +
                "Trainers > VS intro editor, Timings for the selected class.\n\n" +
                "Requirements\n" +
                "US HeartGold or SoulSilver, with the ARM9 expansion and the trainer class metadata patch. Not compatible with hg-engine.",
            ["trainerClassTablesExpanded"] =
                "What it does\n" +
                "Moves the trainer class gender and prize money tables into the synthetic overlay with room for 256 classes, so new classes can be added.\n\n" +
                "Where to edit\n" +
                "Trainers > Trainer editor, Classes tab.\n\n" +
                "Requirements\n" +
                "Platinum, with the ARM9 expansion. Not compatible with hg-engine.",
            ["trainerEncounterBgmRepointed"] =
                "What it does\n" +
                "Moves the eye-contact music table into the synthetic overlay with room for an entry per class, so more classes can have their own encounter music.\n\n" +
                "Where to edit\n" +
                "Trainers > Trainer editor, Classes tab.\n\n" +
                "Requirements\n" +
                "Platinum, with the ARM9 expansion. Not compatible with hg-engine.",
            ["vsClassTableExpanded"] =
                "What it does\n" +
                "HeartGold and SoulSilver pick a trainer class's VS intro and battle music from a table with a fixed number of rows. " +
                "This moves the table into the expanded ARM9 area with room for 255 classes. Every class keeps the intro it has now.\n\n" +
                "Where to edit\n" +
                "Trainers > VS intro editor.\n\n" +
                "Requirements\n" +
                "HeartGold or SoulSilver, with the ARM9 expansion. Not compatible with hg-engine.",
            ["hpBarText"] =
                "What it does\n" +
                "Removes the white box behind the name, level and HP in battle, so your HP bar art shows behind them.\n\n" +
                "Where to edit\n" +
                "Graphics > HP bar layout and Graphics > Battle screen. Their previews drop the box once the patch is in.\n\n" +
                "Requirements\n" +
                "US HeartGold, SoulSilver, Platinum (Rev 1) or Diamond, with the ARM9 expansion. Not compatible with hg-engine.",
        };

        // What every code patch changes, the room it takes and what it backs up. None can be removed by DSPRE.
        private static readonly Dictionary<string, (string Changes, string Storage, string Backups)> CodeChanges = new()
        {
            ["arm9"] = ("Two short code changes in the ARM9, a branch and a start-up routine, make the game load the synthetic overlay.",
                "Creates the synthetic overlay: 88 KB that the other code patches and DSPRE's moved tables live in.",
                "the ARM9 (the new synthetic overlay is not backed up)"),
            ["bdhcam"] = ("A branch in the ARM9 and two code changes in overlay 5 (Platinum) or overlay 1 (HeartGold and SoulSilver) call the camera routine.",
                "The camera routine in the synthetic overlay at offset 0x115B0.",
                "the ARM9 and that overlay"),
            ["buildingRotation"] = ("Four bytes in overlay 5 (Diamond, Pearl and Platinum) or overlay 1 (HeartGold and SoulSilver) branch to the rotation routine.",
                "The rotation routine in the synthetic overlay, at the offset chosen when applying.",
                "that overlay"),
            ["trainerShiny"] = ("The ARM9's trainer party setup is pointed at the routine, and flag 0x40 is left out of the party member's ability.",
                "156 bytes in the synthetic overlay, at the offset chosen when applying.",
                "the ARM9"),
            ["trainerCustomPokeballs"] = ("Replaces the 84-byte ARM9 trainer capsule routine in place (0x72A44 in most versions).",
                "No additional space or synthetic overlay.", "the ARM9"),
            ["trainerClassMetadata"] = ("Hooks in the ARM9 and overlays 1, 12, 80, 115, 117, 118, 119 and 120 read each class's record, and a/1/5/5 holds a record per class.",
                "About 8 KB in the synthetic overlay, plus a/1/5/5.",
                "the ARM9, overlays 1, 12, 80, 115, 117, 118, 119 and 120 and the synthetic overlay, and of a/1/5/5 as unpacked/a155.narc"),
            ["classIntros"] = ("The ARM9's class intro switch jumps to the patch's routine, and overlay 5's pointers to one gym leader's intro record (Volkner's in Platinum, Roark's in Diamond and Pearl) and Aaron's point at the class's record instead.",
                "About 8.3 KB in the synthetic overlay: the routine and a 32-byte row for each of 256 classes.",
                "the ARM9, overlay 5 and the synthetic overlay"),
            ["vsIntroTimings"] = ("22 places in overlays 115, 117, 118 and 119 call a helper that looks up the battle's class.",
                "About 2.4 KB in the synthetic overlay: the helper and eight timings for each of 256 classes.",
                "overlays 115, 117, 118 and 119 and the synthetic overlay"),
            ["trainerClassTablesExpanded"] = ("The ARM9's gender table pointer and overlay 16's prize money table pointer are moved to the new tables.",
                "Two blocks of 288 bytes in the synthetic overlay, room for 256 classes each.",
                "the ARM9, overlay 16 and the synthetic overlay"),
            ["trainerEncounterBgmRepointed"] = ("The ARM9's two pointers to the eye-contact music table are moved to the new table; giving a class music raises the entry count byte.",
                "About 1 KB in the synthetic overlay, room for an entry per class.",
                "the ARM9 and the synthetic overlay"),
            ["dynamicHeaders"] = ("The ARM9 stops reading its built-in header table: its references are rewritten so headers are loaded one at a time from a NARC (a/0/5/0 in HeartGold and SoulSilver, debug/cb_edit/d_test.narc in Platinum).",
                "No synthetic overlay space; the headers move into that NARC.",
                "the ARM9"),
            ["matrix"] = ("A few ARM9 instructions are changed so matrix 0 can be up to twice its size.",
                "None.",
                "the ARM9"),
            ["scrcmdRepoint"] = ("The ARM9's pointers to the script command table and its count are moved to the copy. No command is added.",
                "About 3.4 KB in the synthetic overlay, at the offset chosen when applying.",
                "the ARM9"),
            ["trainerNames"] = ("One ARM9 byte, the trainer name length, is raised.",
                "None.",
                "the ARM9"),
            ["sameHeldItemOdds"] = ("Two ARM9 bytes turn the same-item branch into one that always goes on to the odds.",
                "None.",
                "the ARM9"),
            ["punchingMovesExpanded"] = ("The battle overlay's pointer to the list and its count check (overlay 12 in HeartGold, 16 in Platinum, 11 in Diamond) are moved to the new list.",
                "160 bytes in the synthetic overlay.",
                "the battle overlay and the synthetic overlay"),
            ["soundMovesExpanded"] = ("The battle overlay's pointer to the list and its count check (overlay 12 in HeartGold, 16 in Platinum, 11 in Diamond) are moved to the new list.",
                "160 bytes in the synthetic overlay.",
                "the battle overlay and the synthetic overlay"),
            ["regionalDexCount"] = ("Two or three single-byte numbers in the ARM9's Pokédex code.",
                "None.",
                "the ARM9"),
            ["evolutionSlots"] = ("Four single-byte numbers in the ARM9's evolution check: its buffer size and the three slot loops. Every evolution file is padded with empty slots.",
                "None. Each evolution file grows from 44 to 252 bytes.",
                "the ARM9; the evolution files are copied to backups/evolutions in the project"),
            ["typeChartExpanded"] = ("The battle overlay's pointers to the chart and Conversion 2's count are moved to the new chart; in Diamond, Pearl and Platinum the Pokétch's copy is rewritten.",
                "About 800 bytes in the synthetic overlay, room for 253 matchups.",
                "the battle overlay and the synthetic overlay"),
            ["swarmTableExpanded"] = ("The swarm code's table pointers and row count are moved to the new table; saving with more or fewer rows changes the count byte.",
                "About 1 KB in the synthetic overlay, room for 255 rows.",
                "the code file holding the swarm code and the synthetic overlay"),
            ["bpShopExpanded"] = ("The ARM9's two counter list pointers and overlay 7's price lookup are moved to the new lists; saving changes the price count byte.",
                "About 2 KB in the synthetic overlay, room for 255 items on each counter.",
                "the ARM9, overlay 7 and the synthetic overlay"),
            ["pickupSlots"] = ("The Pickup code's range clamp becomes a multiply, and its two item list pointers are moved to the new tables.",
                "About 400 bytes in the synthetic overlay: a common and a rare item table, nine items for each of the ten level ranges.",
                "the Pickup overlay and the synthetic overlay"),
            ["vsClassTableExpanded"] = ("The ARM9's pointer to the class table and the count its search stops at are changed to the new table.",
                "About 0.5 KB in the synthetic overlay.",
                "the ARM9 and the synthetic overlay"),
            ["hpBarText"] = ("Four calls and nine copies in the battle overlay (overlay 12 in HeartGold and SoulSilver, 16 in Platinum, 11 in Diamond) go through the patch's routine.",
                "140 bytes in the synthetic overlay.",
                "the battle overlay and the synthetic overlay"),
            ["martsExpanded"] = ("The ARM9's common mart pointer and specialty mart pointer table are moved to the new layout; saving changes the common mart's count byte.",
                "About 8.7 KB in the synthetic overlay, room for 64 marts of 64 items.",
                "the ARM9 and the synthetic overlay"),
        };

        /// <summary>The notes for a patch, with where an installed copy sits when that can be read back.</summary>
        public static string For(PatchToolboxLogic.PatchInfo patch)
        {
            if (patch == null) return "";
            StringBuilder text = new StringBuilder();
            // A patch with its own notes says what it does there; the rest show their summary.
            text.Append(Detail.TryGetValue(patch.Key, out string detail) ? detail : patch.Description).Append("\n\n");
            if (CodeChanges.TryGetValue(patch.Key, out (string Changes, string Storage, string Backups) code))
                text.Append("Changes\n").Append(code.Changes).Append("\n\n")
                    .Append("Storage\n").Append(code.Storage).Append("\n\n")
                    .Append("Backups\n").Append("Applying keeps .backup copies of ").Append(code.Backups).Append(".\n\n")
                    .Append("Removal\n").Append("DSPRE can't remove it. Restore the backups to undo it, before anything else changes those files.\n\n");
            if (patch.Key == "trainerClassMetadata" && patch.State == PatchToolboxLogic.PatchState.Applied &&
                TrainerClassMetadataPatch.InstalledOffset() is uint offset)
                text.Append("Installed at\nSynthetic overlay offset 0x").Append(offset.ToString("X"))
                    .Append(" (runtime address 0x").Append((RomInfo.synthOverlayLoadAddress + offset).ToString("X8")).Append(").\n\n");
            string credit = PatchToolboxLogic.CreditLine(patch.Key);
            if (credit != null) text.Append("Credits\n").Append(credit).Append('.');
            return text.ToString().TrimEnd();
        }
    }
}
