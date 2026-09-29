using System.Collections.Generic;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    public static class BattleAnimSchema
    {
        private static readonly Dictionary<string, string> Opcodes = new()
        {
            ["Delay"] = "Wait",
            ["WaitForAnimTasks"] = "Wait for effects",
            ["BeginLoop"] = "Loop start",
            ["EndLoop"] = "Loop end",
            ["End"] = "End",
            ["PlaySoundEffect"] = "Play sound",
            ["Nop0"] = "Mon as background",
            ["Nop1"] = "Restore mon background",
            ["SetBG0BG1AlphaBlending"] = "Set blend",
            ["SetDefaultAlphaBlending"] = "Reset blend",
            ["Call"] = "Call subroutine",
            ["Return"] = "Return",
            ["SetVar"] = "Set variable",
            ["ResetVars"] = "Clear variable",
            ["JumpByTurn"] = "Branch on turn",
            ["JumpIfTurn"] = "Jump on turn",
            ["Jump"] = "Jump",
            ["SwitchBg"] = "Change background",
            ["SetBgSwitchVar"] = "Scroll background",
            ["RestoreBg"] = "Restore background",
            ["WaitForPartialBgSwitch"] = "Background half-wait",
            ["WaitForBgSwitch"] = "Wait for background",
            ["SetBg"] = "Set background",
            ["PlayPannedSoundEffect"] = "Play sound (pan)",
            ["PanSoundEffects"] = "Sound pan",
            ["PlayMovingSoundEffectAtkDef"] = "Sound pan sweep",
            ["PlayLoopedSoundEffect"] = "Play sound (repeat)",
            ["PlayDelayedSoundEffect"] = "Play sound (timed)",
            ["StopSoundEffect"] = "Stop sound",
            ["Nop3"] = "Sound task",
            ["Nop2"] = "Set blend control",
            ["JumpIfEqual"] = "Jump on variable",
            ["LoadPokemonSpriteIntoBg"] = "Drop mon background",
            ["RemovePokemonSpriteFromBg"] = "Reset mon background",
            ["Nop4"] = "Background priority",
            ["Nop5"] = "Background priority 2",
            ["Nop6"] = "Background priority 3",
            ["Nop7"] = "Hide mon",
            ["Nop8"] = "Show mon",
            ["Nop9"] = "Party-attack BG off",
            ["Nop10"] = "Party-attack BG end",
            ["CallFunc"] = "Run effect routine",
            ["Nop11"] = "Run cell routine",
            ["CreateEmitter"] = "Add particles",
            ["CreateEmitterEx"] = "Add particles (emitter)",
            ["CreateEmitterForMove"] = "Add particles (segmented)",
            ["CreateEmitterForFriendlyFire"] = "Add particles (party)",
            ["WaitForAllEmitters"] = "Wait for particles",
            ["LoadParticleSystem"] = "Load particle set",
            ["LoadDebugParticleSystem"] = "Load particle set (ext.)",
            ["UnloadParticleSystem"] = "Stop particles",
            ["SetExtraParams"] = "Operator settings",
            ["InitPokemonSpriteManager"] = "Mon-copy init",
            ["LoadPokemonSpriteDummyResources"] = "Mon-copy load",
            ["AddPokemonSprite"] = "Drop mon copy",
            ["FreePokemonSpriteManager"] = "Mon-copy free",
            ["RemovePokemonSprite"] = "Reset mon copy",
            ["CancelTrackingTask"] = "Stop mon-copy motion",
            ["SetCameraProjection"] = "Change camera",
            ["SetCameraFlip"] = "Flip camera",
            ["JumpIfBattlerSide"] = "Branch on side",
            ["PlayPokemonCry"] = "Play cry",
            ["WaitForPokemonCries"] = "Stop cry",
            ["StartTransform"] = "Transform on",
            ["StartTransformRecolour"] = "Transform on (RC)",
            ["JumpIfWeather"] = "Branch on weather",
            ["JumpIfContest"] = "Branch (contest)",
            ["JumpIfFriendlyFire"] = "Branch (party attack)",
            ["InitSpriteManager"] = "Cell-actor init",
            ["LoadCharResObj"] = "Load cell graphics",
            ["LoadPlttRes"] = "Load cell palette",
            ["LoadCellResObj"] = "Load cell layout",
            ["LoadAnimResObj"] = "Load cell animation",
            ["AddSpriteWithFunc"] = "Add cell actor",
            ["AddSprite"] = "Add cell actor (simple)",
            ["FreeSpriteManager"] = "Free cell resources",
            ["SetPokemonSpriteVisible"] = "Show/hide mon copy",
            ["CreatePokemonCopy"] = "Drop particle copy",
            ["RemovePokemonCopy"] = "Reset particle copy",
            ["WaitForLRX"] = "Wait for button",
            ["JumpIfContestCondition"] = "Branch (contest check)",
            ["SwitchBgEx"] = "Change background if different",
            ["SwitchBgAnimated"] = "Change background (extended)",
            ["JumpIfBatonPass"] = "Branch (Baton Pass)",
        };

        public static string OpcodeDoc(string opName, bool script = false)
        {
            if (script) return WazaSeqSchema.Doc(opName);
            return opName != null && Docs.TryGetValue(opName, out var d) ? d : "";
        }

        private static readonly Dictionary<string, string> Docs = new()
        {
            ["Delay"] = "Pause the script for the given number of frames before continuing.",
            ["WaitForAnimTasks"] = "Pause until every active particle effect finishes, then continue.",
            ["BeginLoop"] = "Mark the start of a loop and set how many times it repeats.",
            ["EndLoop"] = "Jump back to the matching loop start if repeats remain.",
            ["End"] = "End the script. Playback stops here.",
            ["PlaySoundEffect"] = "Play a sound effect.",
            ["Nop0"] = "Render the mon as a flat background layer, used for palette or blend tricks like Camouflage.",
            ["Nop1"] = "Restore the mon to its normal sprite layer.",
            ["SetBG0BG1AlphaBlending"] = "Set the hardware alpha-blend weights (source and destination) used by translucency effects.",
            ["SetDefaultAlphaBlending"] = "Restore the alpha-blend weights to their default.",
            ["Call"] = "Call another animation script and return here when it ends.",
            ["Return"] = "Return from a called script to where it was called from.",
            ["SetVar"] = "Set a script variable to a value.",
            ["ResetVars"] = "Clear a script variable back to 0.",
            ["JumpByTurn"] = "Pick one of two branches depending on whether this is the move's first or second use. Drives two-turn moves (Fly, Dig) and moves that alternate between two variants (Lunar Dance).",
            ["JumpIfTurn"] = "Jump to a target only on the given turn (first or second use of the move).",
            ["Jump"] = "Jump to another point in this script.",
            ["SwitchBg"] = "Swap in a new scrolling background and start it moving.",
            ["SetBgSwitchVar"] = "Change one setting of the current scrolling background (speed, position or blend) without swapping it out.",
            ["RestoreBg"] = "Restore the original battle backdrop, ending the background effect.",
            ["WaitForPartialBgSwitch"] = "Wait until the background change is half faded in.",
            ["WaitForBgSwitch"] = "Wait until the background change has fully settled.",
            ["SetBg"] = "Set the background immediately, with no fade transition.",
            ["PlayPannedSoundEffect"] = "Play a sound effect at a fixed stereo pan position.",
            ["PanSoundEffects"] = "Set the stereo pan of the sound currently playing.",
            ["PlayMovingSoundEffectAtkDef"] = "Sweep a sound's stereo pan from one side to the other over time.",
            ["PlayLoopedSoundEffect"] = "Play a sound effect on a loop.",
            ["PlayDelayedSoundEffect"] = "Play a sound effect after a delay.",
            ["StopSoundEffect"] = "Stop a sound effect that is currently playing.",
            ["Nop3"] = "Start or stop a background sound task, an ambient loop tied to the effect.",
            ["Nop2"] = "Set which screen layers take part in the hardware blend.",
            ["JumpIfEqual"] = "Compare a script variable to a value and jump if it matches.",
            ["LoadPokemonSpriteIntoBg"] = "Create a background copy of the mon that can be moved independently of the real sprite.",
            ["RemovePokemonSpriteFromBg"] = "Remove a mon background copy.",
            ["Nop4"] = "Adjust the battle background's draw priority relative to the mons.",
            ["Nop5"] = "Adjust the battle background's draw priority relative to the mons (variant 2).",
            ["Nop6"] = "Adjust the battle background's draw priority relative to the mons (variant 3).",
            ["Nop7"] = "Hide a mon from the scene.",
            ["Nop8"] = "Show a previously hidden mon again.",
            ["Nop9"] = "Hide the party-attack background overlay.",
            ["Nop10"] = "End the party-attack background overlay.",
            ["CallFunc"] = "Run a built-in effect routine by ID. Covers most non-particle move motion: shakes, slides, scale changes, colour flashes and similar.",
            ["Nop11"] = "Run a built-in cell-actor routine by ID, driving a cell actor's per-frame behaviour.",
            ["CreateEmitter"] = "Spawn a particle effect from a loaded particle set into a slot.",
            ["CreateEmitterEx"] = "Spawn a particle effect using one specific emitter from a loaded particle set.",
            ["CreateEmitterForMove"] = "Spawn a particle effect built from several separate emitter definitions at once.",
            ["CreateEmitterForFriendlyFire"] = "Spawn a particle effect for a party (double or multi) attack.",
            ["WaitForAllEmitters"] = "Wait until every particle in a slot has finished.",
            ["LoadParticleSystem"] = "Load a particle set into a slot, ready to spawn.",
            ["LoadDebugParticleSystem"] = "Load a particle set from a specific archive into a slot.",
            ["UnloadParticleSystem"] = "Stop a slot's emitters immediately. Existing particles finish naturally, but no new ones spawn.",
            ["SetExtraParams"] = "Configure the next particle spawn's operator settings: priority, anchor, position, direction, field and camera mode.",
            ["InitPokemonSpriteManager"] = "Prepare the mon-copy (dropped sprite) system for use.",
            ["LoadPokemonSpriteDummyResources"] = "Load the graphics needed for a mon copy.",
            ["AddPokemonSprite"] = "Create a movable copy of a mon's sprite, used for effects like Substitute, Disable and Dark Void.",
            ["FreePokemonSpriteManager"] = "Free the mon-copy resources.",
            ["RemovePokemonSprite"] = "Remove a mon copy.",
            ["CancelTrackingTask"] = "Stop a mon copy's automatic motion.",
            ["SetCameraProjection"] = "Switch the effect camera to a different mode (spin, custom path, follow a mon and similar).",
            ["SetCameraFlip"] = "Flip the camera to the mirrored side, so an effect still looks correct when the caster is on the other side.",
            ["JumpIfBattlerSide"] = "Branch depending on whether the given mon is on the player's or the enemy's side.",
            ["PlayPokemonCry"] = "Play the mon's cry.",
            ["WaitForPokemonCries"] = "Wait the given number of frames, then stop the mon's cry if it is still playing.",
            ["StartTransform"] = "Turn on the Transform (Ditto) sprite swap.",
            ["StartTransformRecolour"] = "Turn on Transform, applying a recolour instead of a full sprite swap.",
            ["JumpIfWeather"] = "Branch depending on the current weather (clear, sun, rain, snow, sandstorm).",
            ["JumpIfContest"] = "Branch depending on whether this is a contest rather than a battle.",
            ["JumpIfContestCondition"] = "Check a contest-specific condition and branch.",
            ["JumpIfFriendlyFire"] = "Branch depending on whether this is a party (double or multi) attack.",
            ["InitSpriteManager"] = "Prepare the cell-actor system for use.",
            ["LoadCharResObj"] = "Load a cell actor's tile graphics.",
            ["LoadPlttRes"] = "Load a cell actor's palette.",
            ["LoadCellResObj"] = "Load a cell actor's cell layout.",
            ["LoadAnimResObj"] = "Load a cell actor's animation sequences.",
            ["AddSpriteWithFunc"] = "Create a cell actor driven by a built-in callback routine, a scripted per-move animation.",
            ["AddSprite"] = "Create a simple cell actor that just plays its animation with no extra behaviour.",
            ["FreeSpriteManager"] = "Free a cell actor's loaded resources.",
            ["SetPokemonSpriteVisible"] = "Show or hide a mon copy.",
            ["CreatePokemonCopy"] = "Create a particle-linked copy used to attach an effect to a mon.",
            ["RemovePokemonCopy"] = "Remove a particle-linked copy.",
            ["WaitForLRX"] = "Wait for a button press before continuing.",
            ["SwitchBgAnimated"] = "Swap in a new scrolling background with an extended parameter set, including the initial scroll position.",
            ["SwitchBgEx"] = "Change the background only if it isn't already showing, avoiding an unwanted restart.",
            ["PlayMovingSoundEffectNoCorrection"] = "Sweep a sound's stereo pan the same way as Sound pan sweep, with a fixed step size.",
            ["PlayMovingSoundEffectAtkDef2"] = "Sweep a sound's stereo pan, continuing after the main effect ends.",
            ["WaitForSoundEffects"] = "Wait until the current sound effect finishes playing.",
            ["JumpIfBatonPass"] = "Jump only when the attacker is being replaced through Baton Pass.",
            ["FlashScreen"] = "Flash the screen white and fade back out over the given number of frames.",
        };

        public static string OpcodeDisplay(string opName, bool script = false)
        {
            if (opName == null) return "";
            if (script && WazaSeqSchema.Display(opName) is string ws) return ws;
            if (Opcodes.TryGetValue(opName, out var s)) return s;
            string t = opName;
            var parts = t.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            return string.Join(" ", parts);
        }

        private static readonly Dictionary<string, string[]> Names = new()
        {
            ["Delay"] = new[] { "Frames" },
            ["BeginLoop"] = new[] { "Repeat count" },
            ["PlaySoundEffect"] = new[] { "Sound" },
            ["Nop0"] = new[] { "Flag" },
            ["Nop1"] = new[] { "Flag" },
            ["SetBG0BG1AlphaBlending"] = new[] { "Source weight", "Dest weight" },
            ["Call"] = new[] { "Target" },
            ["SetVar"] = new[] { "Variable", "Value" },
            ["JumpByTurn"] = new[] { "Turn 1 target", "Turn 2 target" },
            ["JumpIfTurn"] = new[] { "Turn", "Target" },
            ["Jump"] = new[] { "Target" },
            ["SwitchBg"] = new[] { "Background", "Mode" },
            ["SetBgSwitchVar"] = new[] { "Parameter", "Value" },
            ["RestoreBg"] = new[] { "Background", "Mode" },
            ["SetBg"] = new[] { "Background" },
            ["PlayPannedSoundEffect"] = new[] { "Sound", "Pan" },
            ["PanSoundEffects"] = new[] { "Pan" },
            ["PlayMovingSoundEffectAtkDef"] = new[] { "Sound", "Start pan", "End pan", "Step", "Wait" },
            ["PlayLoopedSoundEffect"] = new[] { "Sound", "Pan", "Wait", "Repeat" },
            ["PlayDelayedSoundEffect"] = new[] { "Sound", "Pan", "Wait" },
            ["Nop2"] = new[] { "Value" },
            ["Nop3"] = new[] { "Target", "Count" },
            ["JumpIfEqual"] = new[] { "Variable", "Value", "Target" },
            ["LoadPokemonSpriteIntoBg"] = new[] { "Flag", "Auto-move" },
            ["RemovePokemonSpriteFromBg"] = new[] { "Flag" },
            ["Nop4"] = new[] { "Which" },
            ["Nop6"] = new[] { "Which" },
            ["Nop7"] = new[] { "Mon" },
            ["Nop8"] = new[] { "Mon" },
            ["Nop9"] = new[] { "Which" },
            ["Nop10"] = new[] { "Which" },
            ["StopSoundEffect"] = new[] { "Sound" },
            ["CallFunc"] = new[] { "Routine", "Param count" },
            ["Nop11"] = new[] { "Routine", "Header", "Priority", "Param count" },
            ["CreateEmitter"] = new[] { "Particle slot", "Particle data", "Behaviour" },
            ["CreateEmitterEx"] = new[] { "Particle slot", "Emitter", "Particle data", "Behaviour" },
            ["CreateEmitterForMove"] = new[] { "Particle slot", "Data 1", "Data 2", "Data 3", "Data 4", "Data 5", "Data 6", "Behaviour" },
            ["CreateEmitterForFriendlyFire"] = new[] { "Particle slot", "Data 1", "Data 2", "Data 3", "Data 4", "Behaviour" },
            ["LoadParticleSystem"] = new[] { "Particle slot", "Particle data" },
            ["LoadDebugParticleSystem"] = new[] { "Particle slot", "Archive", "Particle data" },
            ["UnloadParticleSystem"] = new[] { "Particle slot" },
            ["SetExtraParams"] = new[] { "Field count", "Priority", "Anchor", "Position", "Direction", "Field", "Camera", "Extra" },
            ["LoadPokemonSpriteDummyResources"] = new[] { "Resource" },
            ["AddPokemonSprite"] = new[] { "Mon", "Auto-move", "Copy ID", "Resource" },
            ["RemovePokemonSprite"] = new[] { "Copy ID" },
            ["CancelTrackingTask"] = new[] { "Copy ID" },
            ["SetCameraProjection"] = new[] { "Camera", "Mode" },
            ["SetCameraFlip"] = new[] { "Camera", "Flag" },
            ["JumpIfBattlerSide"] = new[] { "Which mon", "If player", "If enemy" },
            ["PlayPokemonCry"] = new[] { "Which mon", "Pan", "Volume" },
            ["WaitForPokemonCries"] = new[] { "Frames" },
            ["StartTransform"] = new[] { "Type" },
            ["StartTransformRecolour"] = new[] { "Type" },
            ["JumpIfWeather"] = new[] { "Clear", "Sun", "Rain", "Snow", "Sandstorm" },
            ["JumpIfContest"] = new[] { "Target" },
            ["JumpIfFriendlyFire"] = new[] { "Target" },
            ["InitSpriteManager"] = new[] { "Resource", "Object count", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6" },
            ["LoadCharResObj"] = new[] { "Resource", "Archive" },
            ["LoadPlttRes"] = new[] { "Resource", "Archive", "Palette count" },
            ["LoadCellResObj"] = new[] { "Resource", "Archive" },
            ["LoadAnimResObj"] = new[] { "Resource", "Archive" },
            ["AddSpriteWithFunc"] = new[] { "Resource", "Driver routine", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6", "Param count" },
            ["AddSprite"] = new[] { "Resource", "Slot ID", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6" },
            ["FreeSpriteManager"] = new[] { "Resource" },
            ["SetPokemonSpriteVisible"] = new[] { "Copy", "Show" },
            ["CreatePokemonCopy"] = new[] { "Type", "Mode", "Copy ID" },
            ["RemovePokemonCopy"] = new[] { "Mode" },
            ["JumpIfContestCondition"] = new[] { "Target" },
            ["SwitchBgEx"] = new[] { "Background", "Mode", "Check" },
            ["SwitchBgAnimated"] = new[] { "Background", "Mode", "Start offset" },
            ["JumpIfBatonPass"] = new[] { "Target" },
        };

        public static string ParamName(string opName, int index, bool script = false)
        {
            if (script && WazaSeqSchema.Params(opName) is string[] wp && index >= 0 && index < wp.Length) return wp[index];
            return opName != null && Names.TryGetValue(opName, out var a) && index >= 0 && index < a.Length ? a[index] : "Param " + (index + 1);
        }

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

        public static string CommandName(string opName, bool script = false) => Token(OpcodeDisplay(opName, script), pascalCase: true);

        public static string ArgToken(string opName, int index, bool script = false) => Token(ParamName(opName, index, script), pascalCase: false);

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

        public static EnumOption[] EnumFor(string opName, int index)
        {
            if (opName == "SetExtraParams")
                return index switch { 1 => OpPri, 2 => OpTarget, 3 => OpPos, 4 => OpAxis, 5 => OpFld, 6 => OpCamera, _ => null };
            return null;
        }
    }
}
