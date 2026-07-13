using System.Collections.Generic;
using System.Text;

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
            ["WEST_CONTEST_CHK_JP"] = "Branch (contest check)",
            ["WEST_HAIKEI_CHKCHG"] = "Change background if different",
            ["WEST_HAIKEI_CHG_EX"] = "Change background (extended)",
            ["WEST_BATONTATTI_JP"] = "Branch (Baton Pass)",
        };

        /// <summary>Friendly title for an opcode; falls back to a Title-Cased form of the raw identifier.</summary>
        /// <summary>One-line description of what the opcode does, or "" if none is known.</summary>
        public static string OpcodeDoc(string opName)
        {
            if (WazaSeqSchema.Handles(opName)) return WazaSeqSchema.Doc(opName);
            return opName != null && Docs.TryGetValue(opName, out var d) ? d : "";
        }

        // Internal opcode identifier → one-line description, for the move-script command guide.
        private static readonly Dictionary<string, string> Docs = new()
        {
            ["WEST_WAIT"] = "Pause the script for the given number of frames before continuing.",
            ["WEST_WAIT_FLAG"] = "Pause until every active particle effect finishes, then continue.",
            ["WEST_LOOP_LABEL"] = "Mark the start of a loop and set how many times it repeats.",
            ["WEST_LOOP"] = "Jump back to the matching loop start if repeats remain.",
            ["WEST_SEQEND"] = "End the script. Playback stops here.",
            ["WEST_SE"] = "Play a sound effect.",
            ["WEST_POKEBG"] = "Render the mon as a flat background layer, used for palette or blend tricks like Camouflage.",
            ["WEST_POKEBG_RESET"] = "Restore the mon to its normal sprite layer.",
            ["WEST_BLDALPHA_SET"] = "Set the hardware alpha-blend weights (source and destination) used by translucency effects.",
            ["WEST_BLDALPHA_RESET"] = "Restore the alpha-blend weights to their default.",
            ["WEST_SEQ_CALL"] = "Call another WEST script and return here when it ends.",
            ["WEST_END_CALL"] = "Return from a called script to where it was called from.",
            ["WEST_WORK_SET"] = "Set a script variable to a value.",
            ["WEST_WORK_CLEAR"] = "Clear a script variable back to 0.",
            ["WEST_TURN_CHK"] = "Pick one of two branches depending on whether this is the move's first or second use. Drives two-turn moves (Fly, Dig) and moves that alternate between two variants (Lunar Dance).",
            ["WEST_TURN_JP"] = "Jump to a target only on the given turn (first or second use of the move).",
            ["WEST_SEQ_JP"] = "Jump to another point in this script.",
            ["WEST_HAIKEI_CHG"] = "Swap in a new scrolling background and start it moving.",
            ["WEST_HAIKEI_PARA_CHG"] = "Change one setting of the current scrolling background (speed, position or blend) without swapping it out.",
            ["WEST_HAIKEI_RECOVER"] = "Restore the original battle backdrop, ending the background effect.",
            ["WEST_HAIKEI_HALF_WAIT"] = "Wait until the background change is half faded in.",
            ["WEST_HAIKEI_CHG_WAIT"] = "Wait until the background change has fully settled.",
            ["WEST_HAIKEI_SET"] = "Set the background immediately, with no fade transition.",
            ["WEST_SEPLAY_PAN"] = "Play a sound effect at a fixed stereo pan position.",
            ["WEST_SEPAN"] = "Set the stereo pan of the sound currently playing.",
            ["WEST_SEPAN_FLOW"] = "Sweep a sound's stereo pan from one side to the other over time.",
            ["WEST_SE_REPEAT"] = "Play a sound effect on a loop.",
            ["WEST_SE_WAITPLAY"] = "Play a sound effect after a delay.",
            ["WEST_SE_STOP"] = "Stop a sound effect that is currently playing.",
            ["WEST_SE_TASK"] = "Start or stop a background sound task, an ambient loop tied to the effect.",
            ["WEST_BLDCNT_SET"] = "Set which screen layers take part in the hardware blend.",
            ["WEST_WORKCHK_JP"] = "Compare a script variable to a value and jump if it matches.",
            ["WEST_POKEBG_DROP"] = "Create a background copy of the mon that can be moved independently of the real sprite.",
            ["WEST_POKEBG_DROP_RESET"] = "Remove a mon background copy.",
            ["WEST_BGPRI_GAPSET"] = "Adjust the battle background's draw priority relative to the mons.",
            ["WEST_BGPRI_GAPSET2"] = "Adjust the battle background's draw priority relative to the mons (variant 2).",
            ["WEST_BGPRI_GAPSET3"] = "Adjust the battle background's draw priority relative to the mons (variant 3).",
            ["WEST_POKE_BANISH_ON"] = "Hide a mon from the scene.",
            ["WEST_POKE_BANISH_OFF"] = "Show a previously hidden mon again.",
            ["WEST_PARTY_ATTACK_BGOFF"] = "Hide the party-attack background overlay.",
            ["WEST_PARTY_ATTACK_BGEND"] = "End the party-attack background overlay.",
            ["WEST_FUNC_CALL"] = "Run a built-in effect routine by ID. Covers most non-particle move motion: shakes, slides, scale changes, colour flashes and similar.",
            ["WEST_OLDACT_FUNC_CALL"] = "Run a built-in cell-actor routine by ID, driving a CATS actor's per-frame behaviour.",
            ["WEST_ADD_PARTICLE"] = "Spawn a particle effect from a loaded particle set into a slot.",
            ["WEST_ADD_PARTICLE_EMIT_SET"] = "Spawn a particle effect using one specific emitter from a loaded particle set.",
            ["WEST_ADD_PARTICLE_SEP"] = "Spawn a particle effect built from several separate emitter definitions at once.",
            ["WEST_ADD_PARTICLE_PTAT"] = "Spawn a particle effect for a party (double or multi) attack.",
            ["WEST_WAIT_PARTICLE"] = "Wait until every particle in a slot has finished.",
            ["WEST_LOAD_PARTICLE"] = "Load a particle set into a slot, ready to spawn.",
            ["WEST_LOAD_PARTICLE_EX"] = "Load a particle set from a specific archive into a slot.",
            ["WEST_EXIT_PARTICLE"] = "Stop a slot's emitters immediately. Existing particles finish naturally, but no new ones spawn.",
            ["WEST_EX_DATA"] = "Configure the next particle spawn's operator settings: priority, anchor, position, direction, field and camera mode.",
            ["WEST_POKEOAM_RES_INIT"] = "Prepare the mon-copy (dropped sprite) system for use.",
            ["WEST_POKEOAM_RES_LOAD"] = "Load the graphics needed for a mon copy.",
            ["WEST_POKEOAM_DROP"] = "Create a movable copy of a mon's sprite, used for effects like Substitute, Disable and Dark Void.",
            ["WEST_POKEOAM_RES_FREE"] = "Free the mon-copy resources.",
            ["WEST_POKEOAM_DROP_RESET"] = "Remove a mon copy.",
            ["WEST_POKEOAM_AUTO_STOP"] = "Stop a mon copy's automatic motion.",
            ["WEST_CAMERA_CHG"] = "Switch the effect camera to a different mode (spin, custom path, follow a mon and similar).",
            ["WEST_CAMERA_REVERCE"] = "Flip the camera to the mirrored side, so an effect still looks correct when the caster is on the other side.",
            ["WEST_SIDE_JP"] = "Branch depending on whether the given mon is on the player's or the enemy's side.",
            ["WEST_VOICE_PLAY"] = "Play the mon's cry.",
            ["WEST_VOICE_WAIT_STOP"] = "Wait the given number of frames, then stop the mon's cry if it is still playing.",
            ["WEST_HENSIN_ON"] = "Turn on the Transform (Ditto) sprite swap.",
            ["WEST_HENSIN_ON_RC"] = "Turn on Transform, applying a recolour instead of a full sprite swap.",
            ["WEST_TENKI_JP"] = "Branch depending on the current weather (clear, sun, rain, snow, sandstorm).",
            ["WEST_CONTEST_JP"] = "Branch depending on whether this is a contest rather than a battle.",
            ["WEST_CONTEST_CHK_JP"] = "Check a contest-specific condition and branch.",
            ["WEST_PTAT_JP"] = "Branch depending on whether this is a party (double or multi) attack.",
            ["WEST_CATS_RES_INIT"] = "Prepare the cell-actor system for use.",
            ["WEST_CATS_CAHR_RES_LOAD"] = "Load a cell actor's tile graphics.",
            ["WEST_CATS_PLTT_RES_LOAD"] = "Load a cell actor's palette.",
            ["WEST_CATS_CELL_RES_LOAD"] = "Load a cell actor's cell layout.",
            ["WEST_CATS_CELLANM_RES_LOAD"] = "Load a cell actor's animation sequences.",
            ["WEST_CATS_ACT_ADD"] = "Create a cell actor driven by a built-in callback routine, a scripted per-move animation.",
            ["WEST_CATS_ACT_ADD_EZ"] = "Create a simple cell actor that just plays its animation with no extra behaviour.",
            ["WEST_CATS_RES_FREE"] = "Free a cell actor's loaded resources.",
            ["WEST_POKE_OAM_ENABLE"] = "Show or hide a mon copy.",
            ["WEST_PT_DROP"] = "Create a particle-linked copy used to attach an effect to a mon.",
            ["WEST_PT_DROP_RESET"] = "Remove a particle-linked copy.",
            ["WEST_POKEOAM_CHECK"] = "Check whether a mon copy exists and is active.",
            ["WEST_KEY_WAIT"] = "Wait for a button press before continuing.",
            ["WEST_HAIKEI_CHG_EX"] = "Swap in a new scrolling background with an extended parameter set, including the initial scroll position.",
            ["WEST_HAIKEI_CHKCHG"] = "Change the background only if it isn't already showing, avoiding an unwanted restart.",
            ["WEST_SEPAN_FLOWFIX"] = "Sweep a sound's stereo pan the same way as Sound pan sweep, with a fixed step size.",
            ["WEST_SEPAN_FLOW_AF"] = "Sweep a sound's stereo pan, continuing after the main effect ends.",
            ["WEST_SEWAIT_FLAG"] = "Wait until the current sound effect finishes playing.",
            ["WEST_BATONTATTI_JP"] = "Jump only when the attacker is being replaced through Baton Pass.",
            ["WEST_FLASH"] = "Flash the screen white and fade back out over the given number of frames.",
        };

        public static string OpcodeDisplay(string opName)
        {
            if (opName == null) return "";
            if (WazaSeqSchema.Handles(opName) && WazaSeqSchema.Display(opName) is string ws) return ws;
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
            ["WEST_CONTEST_CHK_JP"] = new[] { "Target" },
            ["WEST_HAIKEI_CHKCHG"] = new[] { "Background", "Mode", "Check" },
            ["WEST_HAIKEI_CHG_EX"] = new[] { "Background", "Mode", "Start offset" },
            ["WEST_BATONTATTI_JP"] = new[] { "Target" },
        };

        /// <summary>Friendly label for argument <paramref name="index"/> of <paramref name="opName"/>, or
        /// <c>Param N+1</c> when no name is known (unknown opcode or a variable-payload argument past the fixed set).</summary>
        public static string ParamName(string opName, int index)
        {
            if (WazaSeqSchema.Handles(opName) && WazaSeqSchema.Params(opName) is string[] wp && index >= 0 && index < wp.Length) return wp[index];
            return opName != null && Names.TryGetValue(opName, out var a) && index >= 0 && index < a.Length ? a[index] : "Param " + (index + 1);
        }

        // ── Single-word tokens for the plain-text script editor ─────────────────────────
        // The card view shows spaced titles ("Add particles"); the text editor instead needs a single typeable
        // word per command/argument/enum-value, so a line reads like a command line ("AddParticles slot=0 data=482").
        // These are derived mechanically from the existing friendly text (every letter/digit run becomes one
        // camel/Pascal-case word) rather than a second hand-curated table, so the two views can never drift apart.
        // Any distinguishing text already in the friendly name (e.g. the "(emitter)"/"(segmented)" suffixes on the
        // several "Add particles" variants) carries over and keeps the tokens unique.
        public static string Token(string text, bool pascalCase)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            bool capNext = pascalCase, sawFirst = false;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(!sawFirst ? (pascalCase ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))
                                        : (capNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)));
                    sawFirst = true;
                    capNext = false;
                }
                else capNext = true;
            }
            return sb.ToString();
        }

        /// <summary>Single-word command name for the text editor (PascalCase), e.g. <c>AddParticlesEmitter</c>.</summary>
        public static string CommandName(string opName) => Token(OpcodeDisplay(opName), pascalCase: true);

        /// <summary>Single-word argument label for the text editor (camelCase), e.g. <c>particleSlot</c>.</summary>
        public static string ArgToken(string opName, int index) => Token(ParamName(opName, index), pascalCase: false);

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
