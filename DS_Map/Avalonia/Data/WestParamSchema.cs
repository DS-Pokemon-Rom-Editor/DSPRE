using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Human-readable display names for the move-effect script editor: friendly opcode titles, parameter labels and
    /// enum value names. The internal opcode identifiers stay behind the scenes (the interpreter switches on them);
    /// this layer is purely what the UI shows, so the editor reads as plain English rather than raw engine symbols.
    /// </summary>
    public static class WestParamSchema
    {
        // Internal opcode identifier → friendly title shown in the dropdown / summary.
        private static readonly Dictionary<string, string> Opcodes = new()
        {
            ["WEST_WAIT"] = "Wait",
            ["WEST_WAIT_FLAG"] = "Wait for effects",
            ["WEST_LOOP_LABEL"] = "Loop start",
            ["WEST_LOOP"] = "Loop end",
            ["WEST_SEQEND"] = "End",
            ["WEST_SE"] = "Play sound",
            ["WEST_POKEBG"] = "Mon as background",
            ["WEST_POKEBG_RESET"] = "Restore mon background",
            ["WEST_BLDALPHA_SET"] = "Set blend",
            ["WEST_BLDALPHA_RESET"] = "Reset blend",
            ["WEST_SEQ_CALL"] = "Call subroutine",
            ["WEST_END_CALL"] = "Return",
            ["WEST_WORK_SET"] = "Set variable",
            ["WEST_WORK_CLEAR"] = "Clear variable",
            ["WEST_TURN_CHK"] = "Branch on turn",
            ["WEST_TURN_JP"] = "Jump on turn",
            ["WEST_SEQ_JP"] = "Jump",
            ["WEST_HAIKEI_CHG"] = "Change background",
            ["WEST_HAIKEI_PARA_CHG"] = "Scroll background",
            ["WEST_HAIKEI_RECOVER"] = "Restore background",
            ["WEST_HAIKEI_HALF_WAIT"] = "Background half-wait",
            ["WEST_HAIKEI_CHG_WAIT"] = "Wait for background",
            ["WEST_HAIKEI_SET"] = "Set background",
            ["WEST_SEPLAY_PAN"] = "Play sound (pan)",
            ["WEST_SEPAN"] = "Sound pan",
            ["WEST_SEPAN_FLOW"] = "Sound pan sweep",
            ["WEST_SE_REPEAT"] = "Play sound (repeat)",
            ["WEST_SE_WAITPLAY"] = "Play sound (timed)",
            ["WEST_SE_STOP"] = "Stop sound",
            ["WEST_SE_TASK"] = "Sound task",
            ["WEST_BLDCNT_SET"] = "Set blend control",
            ["WEST_WORKCHK_JP"] = "Jump on variable",
            ["WEST_POKEBG_DROP"] = "Drop mon background",
            ["WEST_POKEBG_DROP_RESET"] = "Reset mon background",
            ["WEST_BGPRI_GAPSET"] = "Background priority",
            ["WEST_BGPRI_GAPSET2"] = "Background priority 2",
            ["WEST_BGPRI_GAPSET3"] = "Background priority 3",
            ["WEST_POKE_BANISH_ON"] = "Hide mon",
            ["WEST_POKE_BANISH_OFF"] = "Show mon",
            ["WEST_PARTY_ATTACK_BGOFF"] = "Party-attack BG off",
            ["WEST_PARTY_ATTACK_BGEND"] = "Party-attack BG end",
            ["WEST_FUNC_CALL"] = "Run effect routine",
            ["WEST_OLDACT_FUNC_CALL"] = "Run cell routine",
            ["WEST_ADD_PARTICLE"] = "Add particles",
            ["WEST_ADD_PARTICLE_EMIT_SET"] = "Add particles (emitter)",
            ["WEST_ADD_PARTICLE_SEP"] = "Add particles (segmented)",
            ["WEST_ADD_PARTICLE_PTAT"] = "Add particles (party)",
            ["WEST_WAIT_PARTICLE"] = "Wait for particles",
            ["WEST_LOAD_PARTICLE"] = "Load particle set",
            ["WEST_LOAD_PARTICLE_EX"] = "Load particle set (ext.)",
            ["WEST_EXIT_PARTICLE"] = "Stop particles",
            ["WEST_EX_DATA"] = "Operator settings",
            ["WEST_POKEOAM_RES_INIT"] = "Mon-copy init",
            ["WEST_POKEOAM_RES_LOAD"] = "Mon-copy load",
            ["WEST_POKEOAM_DROP"] = "Drop mon copy",
            ["WEST_POKEOAM_RES_FREE"] = "Mon-copy free",
            ["WEST_POKEOAM_DROP_RESET"] = "Reset mon copy",
            ["WEST_POKEOAM_AUTO_STOP"] = "Stop mon-copy motion",
            ["WEST_CAMERA_CHG"] = "Change camera",
            ["WEST_CAMERA_REVERCE"] = "Flip camera",
            ["WEST_SIDE_JP"] = "Branch on side",
            ["WEST_VOICE_PLAY"] = "Play cry",
            ["WEST_VOICE_WAIT_STOP"] = "Stop cry",
            ["WEST_HENSIN_ON"] = "Transform on",
            ["WEST_HENSIN_ON_RC"] = "Transform on (RC)",
            ["WEST_TENKI_JP"] = "Branch on weather",
            ["WEST_CONTEST_JP"] = "Branch (contest)",
            ["WEST_PTAT_JP"] = "Branch (party attack)",
            ["WEST_CATS_RES_INIT"] = "Cell-actor init",
            ["WEST_CATS_CAHR_RES_LOAD"] = "Load cell graphics",
            ["WEST_CATS_PLTT_RES_LOAD"] = "Load cell palette",
            ["WEST_CATS_CELL_RES_LOAD"] = "Load cell layout",
            ["WEST_CATS_CELLANM_RES_LOAD"] = "Load cell animation",
            ["WEST_CATS_ACT_ADD"] = "Add cell actor",
            ["WEST_CATS_ACT_ADD_EZ"] = "Add cell actor (simple)",
            ["WEST_CATS_RES_FREE"] = "Free cell resources",
            ["WEST_POKE_OAM_ENABLE"] = "Show/hide mon copy",
            ["WEST_PT_DROP"] = "Drop particle copy",
            ["WEST_PT_DROP_RESET"] = "Reset particle copy",
            ["WEST_POKEOAM_CHECK"] = "Check mon copy",
            ["WEST_KEY_WAIT"] = "Wait for button",
        };

        /// <summary>Friendly title for an opcode; falls back to a Title-Cased form of the raw identifier.</summary>
        public static string OpcodeDisplay(string opName)
        {
            if (opName == null) return "";
            if (Opcodes.TryGetValue(opName, out var s)) return s;
            // Generic fallback: strip a known prefix and title-case the remaining words.
            string t = opName;
            foreach (var pre in new[] { "WEST_", "WS_", "BE_", "SUB_" }) if (t.StartsWith(pre)) { t = t.Substring(pre.Length); break; }
            var parts = t.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            return string.Join(" ", parts);
        }

        // Internal opcode identifier → friendly parameter labels (in argument order).
        private static readonly Dictionary<string, string[]> Names = new()
        {
            ["WEST_WAIT"] = new[] { "Frames" },
            ["WEST_LOOP_LABEL"] = new[] { "Repeat count" },
            ["WEST_SE"] = new[] { "Sound" },
            ["WEST_POKEBG"] = new[] { "Flag" },
            ["WEST_POKEBG_RESET"] = new[] { "Flag" },
            ["WEST_BLDALPHA_SET"] = new[] { "Source weight", "Dest weight" },
            ["WEST_SEQ_CALL"] = new[] { "Target" },
            ["WEST_WORK_SET"] = new[] { "Variable", "Value" },
            ["WEST_TURN_CHK"] = new[] { "Turn 1 target", "Turn 2 target" },
            ["WEST_TURN_JP"] = new[] { "Turn", "Target" },
            ["WEST_SEQ_JP"] = new[] { "Target" },
            ["WEST_HAIKEI_CHG"] = new[] { "Background", "Mode" },
            ["WEST_HAIKEI_PARA_CHG"] = new[] { "Parameter", "Value" },
            ["WEST_HAIKEI_RECOVER"] = new[] { "Background", "Mode" },
            ["WEST_HAIKEI_SET"] = new[] { "Background" },
            ["WEST_SEPLAY_PAN"] = new[] { "Sound", "Pan" },
            ["WEST_SEPAN"] = new[] { "Pan" },
            ["WEST_SEPAN_FLOW"] = new[] { "Sound", "Start pan", "End pan", "Step", "Wait" },
            ["WEST_SE_REPEAT"] = new[] { "Sound", "Pan", "Wait", "Repeat" },
            ["WEST_SE_WAITPLAY"] = new[] { "Sound", "Pan", "Wait" },
            ["WEST_BLDCNT_SET"] = new[] { "Value" },
            ["WEST_SE_TASK"] = new[] { "Target", "Count" },
            ["WEST_WORKCHK_JP"] = new[] { "Variable", "Value", "Target" },
            ["WEST_POKEBG_DROP"] = new[] { "Flag", "Auto-move" },
            ["WEST_POKEBG_DROP_RESET"] = new[] { "Flag" },
            ["WEST_BGPRI_GAPSET"] = new[] { "Which" },
            ["WEST_BGPRI_GAPSET3"] = new[] { "Which" },
            ["WEST_POKE_BANISH_ON"] = new[] { "Mon" },
            ["WEST_POKE_BANISH_OFF"] = new[] { "Mon" },
            ["WEST_PARTY_ATTACK_BGOFF"] = new[] { "Which" },
            ["WEST_PARTY_ATTACK_BGEND"] = new[] { "Which" },
            ["WEST_SE_STOP"] = new[] { "Sound" },
            ["WEST_FUNC_CALL"] = new[] { "Routine", "Param count" },
            ["WEST_OLDACT_FUNC_CALL"] = new[] { "Routine", "Header", "Priority", "Param count" },
            ["WEST_ADD_PARTICLE"] = new[] { "Particle slot", "Particle data", "Behaviour" },
            ["WEST_ADD_PARTICLE_EMIT_SET"] = new[] { "Particle slot", "Emitter", "Particle data", "Behaviour" },
            ["WEST_ADD_PARTICLE_SEP"] = new[] { "Particle slot", "Data 1", "Data 2", "Data 3", "Data 4", "Data 5", "Data 6", "Behaviour" },
            ["WEST_ADD_PARTICLE_PTAT"] = new[] { "Particle slot", "Data 1", "Data 2", "Data 3", "Data 4", "Behaviour" },
            ["WEST_LOAD_PARTICLE"] = new[] { "Particle slot", "Particle data" },
            ["WEST_LOAD_PARTICLE_EX"] = new[] { "Particle slot", "Archive", "Particle data" },
            ["WEST_EXIT_PARTICLE"] = new[] { "Particle slot" },
            ["WEST_EX_DATA"] = new[] { "Field count", "Priority", "Anchor", "Position", "Direction", "Field", "Camera", "Extra" },
            ["WEST_POKEOAM_RES_LOAD"] = new[] { "Resource" },
            ["WEST_POKEOAM_DROP"] = new[] { "Mon", "Auto-move", "Copy ID", "Resource" },
            ["WEST_POKEOAM_DROP_RESET"] = new[] { "Copy ID" },
            ["WEST_POKEOAM_AUTO_STOP"] = new[] { "Copy ID" },
            ["WEST_CAMERA_CHG"] = new[] { "Camera", "Mode" },
            ["WEST_CAMERA_REVERCE"] = new[] { "Camera", "Flag" },
            ["WEST_SIDE_JP"] = new[] { "Which mon", "If player", "If enemy" },
            ["WEST_VOICE_PLAY"] = new[] { "Which mon", "Pan", "Volume" },
            ["WEST_VOICE_WAIT_STOP"] = new[] { "Frames" },
            ["WEST_HENSIN_ON"] = new[] { "Type" },
            ["WEST_HENSIN_ON_RC"] = new[] { "Type" },
            ["WEST_TENKI_JP"] = new[] { "Clear", "Sun", "Rain", "Snow", "Sandstorm" },
            ["WEST_CONTEST_JP"] = new[] { "Target" },
            ["WEST_PTAT_JP"] = new[] { "Target" },
            ["WEST_CATS_RES_INIT"] = new[] { "Resource", "Object count", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6" },
            ["WEST_CATS_CAHR_RES_LOAD"] = new[] { "Resource", "Archive" },
            ["WEST_CATS_PLTT_RES_LOAD"] = new[] { "Resource", "Archive", "Palette count" },
            ["WEST_CATS_CELL_RES_LOAD"] = new[] { "Resource", "Archive" },
            ["WEST_CATS_CELLANM_RES_LOAD"] = new[] { "Resource", "Archive" },
            ["WEST_CATS_ACT_ADD"] = new[] { "Resource", "Driver routine", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6", "Param count" },
            ["WEST_CATS_ACT_ADD_EZ"] = new[] { "Resource", "Slot ID", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6" },
            ["WEST_CATS_RES_FREE"] = new[] { "Resource" },
            ["WEST_POKE_OAM_ENABLE"] = new[] { "Copy", "Show" },
            ["WEST_PT_DROP"] = new[] { "Type", "Mode", "Copy ID" },
            ["WEST_PT_DROP_RESET"] = new[] { "Mode" },
        };

        /// <summary>Friendly label for argument <paramref name="index"/> of <paramref name="opName"/>, or
        /// <c>Param N+1</c> when no name is known (unknown opcode or a variable-payload argument past the fixed set).</summary>
        public static string ParamName(string opName, int index)
            => opName != null && Names.TryGetValue(opName, out var a) && index >= 0 && index < a.Length ? a[index] : "Param " + (index + 1);

        // ── Operator enums (engine values kept; labels are friendly) — for a FIELD_OPERATOR's settings ──
        public readonly record struct EnumOption(string Label, int Value);

        private static readonly EnumOption[] OpTarget =
            { new("None", 0), new("Attacker", 1), new("Defender", 2), new("Attacker side", 3), new("Defender side", 4) };
        private static readonly EnumOption[] OpPri =
            { new("None", 0), new("In front", 1), new("Behind", 2), new("By depth", 3) };
        private static readonly EnumOption[] OpCamera =
            { new("None", 0), new("Spin", 1), new("Custom", 2), new("Move", 3), new("Move 145", 4), new("Contest 169", 5), new("Move 126", 6), new("Attacker", 7), new("Defender", 8) };
        private static readonly EnumOption[] OpFld =
            { new("None", 0), new("Gravity", 0x0002), new("Random spread", 0x0004), new("Random interval", 0x0008), new("Magnet (pull to point)", 0x0010),
              new("Magnet strength", 0x0020), new("Spin", 0x0040), new("Spin axis", 0x0080), new("Converge to point", 0x1000), new("Converge ratio", 0x2000) };
        private static readonly EnumOption[] OpPos =
            { new("None", 0), new("Start (attacker)", 1), new("End (target)", 2), new("Custom point", 3), new("Start + offset", 4), new("End + offset", 5),
              new("Laser start", 6), new("Laser end", 7), new("Ring start", 8), new("Ring end", 9), new("Laser-2 start", 10), new("Laser-2 end", 11),
              new("Attacker-side + offset", 12), new("Defender-side + offset", 13), new("Laser-3 start", 14), new("Laser-3 end", 15),
              new("Laser-095 start", 16), new("Laser-095 end", 17), new("Laser-161 start", 18), new("Laser-161 end", 19), new("Laser-308 start", 20), new("Laser-308 end", 21),
              new("Laser-304 start", 22), new("Laser-304 end", 23), new("Laser-320 start", 24), new("Laser-320 end", 25), new("Laser-406 start", 26), new("Laser-406 end", 27),
              new("Contest bubble", 28), new("Contest thread", 29), new("Baton pass", 30), new("Bubble", 31), new("Dragon breath", 32), new("Contest 389", 33), new("Move 194", 34),
              new("Start + full offset", 100), new("End + full offset", 101) };
        private static readonly EnumOption[] OpAxis =
            { new("None", 0), new("Toward target", 1), new("Toward target (alt)", 2), new("Custom", 3), new("Sideways (attacker)", 4), new("Sideways (defender)", 5),
              new("Toward target (legacy)", 6), new("Toward target (legacy 2)", 7), new("Arc 3", 8), new("Arc 3 (alt)", 9), new("Arc 095", 10), new("Arc 095 (alt)", 11),
              new("Arc 161", 12), new("Arc 161 (alt)", 13), new("Arc 308", 14), new("Arc 308 (alt)", 15), new("Arc 304", 16), new("Arc 304 (alt)", 17),
              new("Arc 320", 18), new("Arc 320 (alt)", 19), new("Arc 406", 20), new("Arc 406 (alt)", 21), new("Contest bubble", 22), new("Contest thread", 23),
              new("Bubble", 24), new("Contest 389", 25), new("Move 194", 26) };

        /// <summary>The enum options for argument <paramref name="index"/> of <paramref name="opName"/>, or null for a
        /// plain integer. Currently the operator-settings (EX_DATA) fields: priority/anchor/position/direction/field/camera.</summary>
        public static EnumOption[] EnumFor(string opName, int index)
        {
            if (opName == "WEST_EX_DATA")
                return index switch { 1 => OpPri, 2 => OpTarget, 3 => OpPos, 4 => OpAxis, 5 => OpFld, 6 => OpCamera, _ => null };
            return null;
        }
    }
}
