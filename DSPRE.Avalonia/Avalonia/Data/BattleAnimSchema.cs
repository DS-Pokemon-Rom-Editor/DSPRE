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
            ["Nop0"] = "Pokémon as background",
            ["Nop1"] = "Pokémon background off",
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
            ["WaitForPartialBgSwitch"] = "Wait for half the background",
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
            ["LoadPokemonSpriteIntoBg"] = "Pokémon into background",
            ["RemovePokemonSpriteFromBg"] = "Pokémon out of background",
            ["Nop4"] = "Background priority",
            ["Nop5"] = "Background priority 2",
            ["Nop6"] = "Background priority 3",
            ["Nop7"] = "Hide Pokémon",
            ["Nop8"] = "Show Pokémon",
            ["Nop9"] = "Party-attack background off",
            ["Nop10"] = "Party-attack background end",
            ["CallFunc"] = "Run effect routine",
            ["Nop11"] = "Run cell routine",
            ["CreateEmitter"] = "Add particles",
            ["CreateEmitterEx"] = "Add particles (emitter)",
            ["CreateEmitterForMove"] = "Add particles (segmented)",
            ["CreateEmitterForFriendlyFire"] = "Add particles (party)",
            ["WaitForAllEmitters"] = "Wait for particles",
            ["LoadParticleSystem"] = "Load particle set",
            ["LoadDebugParticleSystem"] = "Load particle set (debug)",
            ["UnloadParticleSystem"] = "Stop particles",
            ["SetExtraParams"] = "Operator settings",
            ["InitPokemonSpriteManager"] = "Start Pokémon copies",
            ["LoadPokemonSpriteDummyResources"] = "Load Pokémon copies",
            ["AddPokemonSprite"] = "Add Pokémon copy",
            ["FreePokemonSpriteManager"] = "Free Pokémon copies",
            ["RemovePokemonSprite"] = "Remove Pokémon copy",
            ["CancelTrackingTask"] = "Stop Pokémon copy motion",
            ["SetCameraProjection"] = "Change camera",
            ["SetCameraFlip"] = "Flip camera",
            ["JumpIfBattlerSide"] = "Branch on side",
            ["PlayPokemonCry"] = "Play cry",
            ["WaitForPokemonCries"] = "Stop cry",
            ["StartTransform"] = "Transform on",
            ["StartTransformRecolour"] = "Transform on (recolour)",
            ["JumpIfWeather"] = "Branch on weather",
            ["JumpIfContest"] = "Branch (contest)",
            ["JumpIfFriendlyFire"] = "Branch (party attack)",
            ["InitSpriteManager"] = "Start cell actors",
            ["LoadCharResObj"] = "Load cell graphics",
            ["LoadPlttRes"] = "Load cell palette",
            ["LoadCellResObj"] = "Load cell layout",
            ["LoadAnimResObj"] = "Load cell animation",
            ["AddSpriteWithFunc"] = "Add cell actor",
            ["AddSprite"] = "Add cell actor (simple)",
            ["FreeSpriteManager"] = "Free cell resources",
            ["SetPokemonSpriteVisible"] = "Show or hide Pokémon copy",
            ["CreatePokemonCopy"] = "Add particle copy",
            ["RemovePokemonCopy"] = "Remove particle copy",
            ["WaitForLRX"] = "Wait for button",
            ["JumpIfContestCondition"] = "Branch (contest check)",
            ["SwitchBgEx"] = "Change background if different",
            ["SwitchBgAnimated"] = "Change background (extended)",
            ["JumpIfBatonPass"] = "Branch (Baton Pass)",
        };

        public static string OpcodeDoc(string opName, bool script = false)
        {
            if (script) return WazaSeqSchema.Doc(opName);
            return opName != null && Docs.TryGetValue(opName, out string d) ? d : "";
        }

        private static readonly Dictionary<string, string> Docs = new()
        {
            ["Delay"] = "Waits the given number of frames.",
            ["WaitForAnimTasks"] = "Waits until every particle effect has finished.",
            ["BeginLoop"] = "Starts a loop and sets how many times it repeats.",
            ["EndLoop"] = "Goes back to the start of the loop while repeats are left.",
            ["End"] = "Ends the script.",
            ["PlaySoundEffect"] = "Plays a sound effect.",
            ["Nop0"] = "Draws the Pokémon as a flat background layer, for colour and blend effects like Camouflage.",
            ["Nop1"] = "Puts the Pokémon back on its normal sprite layer.",
            ["SetBG0BG1AlphaBlending"] = "Sets how strongly see-through effects blend.",
            ["SetDefaultAlphaBlending"] = "Puts the blend strength back to normal.",
            ["Call"] = "Runs another animation script, then comes back here.",
            ["Return"] = "Goes back to the script that called this one.",
            ["SetVar"] = "Sets a script variable.",
            ["ResetVars"] = "Sets a script variable back to 0.",
            ["JumpByTurn"] = "Picks one of two branches by whether this is the move's first or second use, for two-turn moves like Fly and Dig.",
            ["JumpIfTurn"] = "Jumps only on the move's first or second use.",
            ["Jump"] = "Jumps to another point in this script.",
            ["SwitchBg"] = "Brings in a new scrolling background and starts it moving.",
            ["SetBgSwitchVar"] = "Changes the scrolling background's speed, position or blend without swapping it.",
            ["RestoreBg"] = "Brings back the normal battle background.",
            ["WaitForPartialBgSwitch"] = "Waits until the new background is half faded in.",
            ["WaitForBgSwitch"] = "Waits until the new background has fully faded in.",
            ["SetBg"] = "Sets the background straight away, with no fade.",
            ["PlayPannedSoundEffect"] = "Plays a sound effect from one side of the speakers.",
            ["PanSoundEffects"] = "Moves the sound that's playing to one side of the speakers.",
            ["PlayMovingSoundEffectAtkDef"] = "Moves a sound from one side of the speakers to the other.",
            ["PlayLoopedSoundEffect"] = "Plays a sound effect on a loop.",
            ["PlayDelayedSoundEffect"] = "Plays a sound effect after a delay.",
            ["StopSoundEffect"] = "Stops a sound effect that's playing.",
            ["Nop3"] = "Starts or stops a background sound that loops with the effect.",
            ["Nop2"] = "Sets which screen layers blend.",
            ["JumpIfEqual"] = "Jumps when a script variable equals a value.",
            ["LoadPokemonSpriteIntoBg"] = "Makes a background copy of the Pokémon that moves on its own.",
            ["RemovePokemonSpriteFromBg"] = "Removes the Pokémon's background copy.",
            ["Nop4"] = "Changes whether the battle background draws in front of or behind the Pokémon.",
            ["Nop5"] = "Changes whether the battle background draws in front of or behind the Pokémon (second variant).",
            ["Nop6"] = "Changes whether the battle background draws in front of or behind the Pokémon (third variant).",
            ["Nop7"] = "Hides a Pokémon.",
            ["Nop8"] = "Shows a hidden Pokémon again.",
            ["Nop9"] = "Hides the background used for a party attack in a double or multi battle.",
            ["Nop10"] = "Ends the background used for a party attack in a double or multi battle.",
            ["CallFunc"] = "Runs one of the game's built-in effects by number: shakes, slides, size changes, colour flashes and the like.",
            ["Nop11"] = "Runs one of the game's built-in sprite movements by number.",
            ["CreateEmitter"] = "Starts a particle effect from a loaded particle set.",
            ["CreateEmitterEx"] = "Starts one emitter from a loaded particle set.",
            ["CreateEmitterForMove"] = "Starts a particle effect made of several emitters at once.",
            ["CreateEmitterForFriendlyFire"] = "Starts a particle effect for a party attack in a double or multi battle.",
            ["WaitForAllEmitters"] = "Waits until every particle in a slot has finished.",
            ["LoadParticleSystem"] = "Loads a particle set into a slot.",
            ["LoadDebugParticleSystem"] = "Loads a particle set from a given archive into a slot.",
            ["UnloadParticleSystem"] = "Stops a slot's emitters; particles already out finish on their own.",
            ["SetExtraParams"] = "Sets up the next particle effect: priority, anchor, position, direction and camera.",
            ["InitPokemonSpriteManager"] = "Gets ready to make copies of a Pokémon's sprite.",
            ["LoadPokemonSpriteDummyResources"] = "Loads the graphics for a sprite copy.",
            ["AddPokemonSprite"] = "Makes a movable copy of a Pokémon's sprite, for effects like Substitute and Dark Void.",
            ["FreePokemonSpriteManager"] = "Frees the sprite copies.",
            ["RemovePokemonSprite"] = "Removes a sprite copy.",
            ["CancelTrackingTask"] = "Stops a sprite copy from moving by itself.",
            ["SetCameraProjection"] = "Switches the effect camera: spinning, a set path, following a Pokémon and so on.",
            ["SetCameraFlip"] = "Mirrors the camera, so the effect looks right from either side.",
            ["JumpIfBattlerSide"] = "Branches by whether a Pokémon is on the player's side or the opponent's.",
            ["PlayPokemonCry"] = "Plays the Pokémon's cry.",
            ["WaitForPokemonCries"] = "Waits the given number of frames, then stops the cry if it's still playing.",
            ["StartTransform"] = "Starts Transform's sprite swap.",
            ["StartTransformRecolour"] = "Starts Transform with a colour change only, keeping the sprite.",
            ["JumpIfWeather"] = "Branches on the weather: clear, sun, rain, snow or sandstorm.",
            ["JumpIfContest"] = "Branches on whether this is a contest or a battle.",
            ["JumpIfContestCondition"] = "Branches on a contest condition.",
            ["JumpIfFriendlyFire"] = "Branches on whether this is a party attack in a double or multi battle.",
            ["InitSpriteManager"] = "Gets ready to show effect sprites.",
            ["LoadCharResObj"] = "Loads an effect sprite's drawing.",
            ["LoadPlttRes"] = "Loads an effect sprite's colours.",
            ["LoadCellResObj"] = "Loads an effect sprite's cells.",
            ["LoadAnimResObj"] = "Loads an effect sprite's animations.",
            ["AddSpriteWithFunc"] = "Shows an effect sprite moved by one of the game's built-in routines.",
            ["AddSprite"] = "Shows an effect sprite that just plays its animation.",
            ["FreeSpriteManager"] = "Frees the effect sprites.",
            ["SetPokemonSpriteVisible"] = "Shows or hides a sprite copy.",
            ["CreatePokemonCopy"] = "Makes a copy of a Pokémon for a particle effect to follow.",
            ["RemovePokemonCopy"] = "Removes that copy.",
            ["WaitForLRX"] = "Waits for a button press, left over from testing.",
            ["SwitchBgAnimated"] = "Brings in a new scrolling background, also setting where it starts.",
            ["SwitchBgEx"] = "Changes the background only if it isn't already showing.",
            ["PlayMovingSoundEffectNoCorrection"] = "Moves a sound across the speakers in fixed steps.",
            ["PlayMovingSoundEffectAtkDef2"] = "Moves a sound across the speakers, carrying on after the effect ends.",
            ["WaitForSoundEffects"] = "Waits until the sound effect has finished.",
            ["JumpIfBatonPass"] = "Jumps only when the attacker is being swapped out by Baton Pass.",
            ["FlashScreen"] = "Flashes the screen white and fades back over the given number of frames.",
        };

        public static string OpcodeDisplay(string opName, bool script = false)
        {
            if (opName == null) return "";
            if (script && WazaSeqSchema.Display(opName) is string ws) return ws;
            if (Opcodes.TryGetValue(opName, out string s)) return s;
            string t = opName;
            string[] parts = t.Split('_');
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
            return opName != null && Names.TryGetValue(opName, out string[] a) && index >= 0 && index < a.Length ? a[index] : "Param " + (index + 1);
        }

        public static string Token(string text, bool pascalCase)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
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
