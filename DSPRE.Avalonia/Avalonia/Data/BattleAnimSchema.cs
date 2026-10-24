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
            ["Nop0"] = "Pokémon onto background",
            ["Nop1"] = "Pokémon off background",
            ["SetBG0BG1AlphaBlending"] = "Set blend",
            ["SetDefaultAlphaBlending"] = "Reset blend",
            ["Call"] = "Call subroutine",
            ["Return"] = "Return",
            ["SetVar"] = "Set variable",
            ["ResetVars"] = "Clear variable",
            ["JumpByTurn"] = "Branch (second animation)",
            ["JumpIfTurn"] = "Jump (second animation)",
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
            ["Nop2"] = "Blend layers",
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
            ["SetExtraParams"] = "Emitter settings",
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
            ["JumpIfFriendlyFire"] = "Branch (ally hit)",
            ["InitSpriteManager"] = "Start effect sprites",
            ["LoadCharResObj"] = "Load sprite graphics",
            ["LoadPlttRes"] = "Load sprite colours",
            ["LoadCellResObj"] = "Load sprite cells",
            ["LoadAnimResObj"] = "Load sprite animation",
            ["AddSpriteWithFunc"] = "Add moving sprite",
            ["AddSprite"] = "Add sprite",
            ["FreeSpriteManager"] = "Free effect sprites",
            ["SetPokemonSpriteVisible"] = "Show or hide Pokémon copy",
            ["CreatePokemonCopy"] = "Add particle copy",
            ["RemovePokemonCopy"] = "Remove particle copy",
            ["WaitForLRX"] = "Wait for button",
            ["JumpIfContestCondition"] = "Branch (contest check)",
            ["SwitchBgEx"] = "Change background if different",
            ["SwitchBgAnimated"] = "Change background (extended)",
            ["JumpIfBatonPass"] = "Branch (Baton Pass)",
            ["PlayMovingSoundEffectNoCorrection"] = "Sound pan sweep (fixed steps)",
            ["PlayMovingSoundEffectAtkDef2"] = "Sound pan sweep (keeps going)",
            ["WaitForSoundEffects"] = "Wait for sound",
            ["FlashScreen"] = "Flash screen",
        };

        public static string OpcodeDoc(string opName, bool script = false)
        {
            if (script) return WazaSeqSchema.Doc(opName);
            return opName != null && Docs.TryGetValue(opName, out string d) ? d : "";
        }

        private static readonly Dictionary<string, string> Docs = new()
        {
            ["Delay"] = "Waits a number of frames.",
            ["WaitForAnimTasks"] = "Waits for running effects to finish.",
            ["BeginLoop"] = "Starts a loop.",
            ["EndLoop"] = "Repeats from the loop start.",
            ["End"] = "Ends the animation.",
            ["PlaySoundEffect"] = "Plays a sound.",
            ["Nop0"] = "Draws the Pokémon on a background layer.",
            ["Nop1"] = "Puts the Pokémon back on its sprite layer.",
            ["SetBG0BG1AlphaBlending"] = "Sets how strongly effects blend.",
            ["SetDefaultAlphaBlending"] = "Resets the blend.",
            ["Call"] = "Runs a subroutine, then returns.",
            ["Return"] = "Returns to the calling script.",
            ["SetVar"] = "Sets a variable for the next command.",
            ["ResetVars"] = "Clears the variables.",
            ["JumpByTurn"] = "Picks a branch by the second animation setting, which the move's battle script sets.",
            ["JumpIfTurn"] = "Jumps when the second animation is set.",
            ["Jump"] = "Jumps to another point.",
            ["SwitchBg"] = "Fades in a new background.",
            ["SetBgSwitchVar"] = "Changes how the background scrolls.",
            ["RestoreBg"] = "Fades back to the battle background.",
            ["WaitForPartialBgSwitch"] = "Waits until the background is half faded in.",
            ["WaitForBgSwitch"] = "Waits for the background fade.",
            ["SetBg"] = "Sets the background at once.",
            ["PlayPannedSoundEffect"] = "Plays a sound to one side.",
            ["PanSoundEffects"] = "Pans the playing sound.",
            ["PlayMovingSoundEffectAtkDef"] = "Pans a sound from one side to the other.",
            ["PlayLoopedSoundEffect"] = "Plays a sound several times.",
            ["PlayDelayedSoundEffect"] = "Plays a sound after a delay.",
            ["StopSoundEffect"] = "Stops a sound.",
            ["Nop3"] = "Starts or stops a looping sound.",
            ["Nop2"] = "Sets which layers blend.",
            ["JumpIfEqual"] = "Jumps when a variable equals a value.",
            ["LoadPokemonSpriteIntoBg"] = "Copies the Pokémon onto a background layer.",
            ["RemovePokemonSpriteFromBg"] = "Removes the Pokémon copy from the background layer.",
            ["Nop4"] = "Puts the background in front of or behind the Pokémon.",
            ["Nop5"] = "Puts the background in front of or behind the Pokémon.",
            ["Nop6"] = "Puts the background in front of or behind the Pokémon.",
            ["Nop7"] = "Hides a Pokémon.",
            ["Nop8"] = "Shows a Pokémon.",
            ["Nop9"] = "Hides the background shown when the move hits an ally.",
            ["Nop10"] = "Ends the background shown when the move hits an ally.",
            ["CallFunc"] = "Runs a built-in effect.",
            ["Nop11"] = "Runs a built-in sprite movement.",
            ["CreateEmitter"] = "Starts an emitter from a loaded particle set.",
            ["CreateEmitterEx"] = "Starts an emitter with its own settings.",
            ["CreateEmitterForMove"] = "Starts several emitters.",
            ["CreateEmitterForFriendlyFire"] = "Starts an emitter when the move hits an ally.",
            ["WaitForAllEmitters"] = "Waits for the particles to finish.",
            ["LoadParticleSystem"] = "Loads a particle set into a slot.",
            ["LoadDebugParticleSystem"] = "Loads a particle set from another archive.",
            ["UnloadParticleSystem"] = "Frees a particle slot.",
            ["SetExtraParams"] = "Sets up the next emitter.",
            ["InitPokemonSpriteManager"] = "Starts Pokémon copies.",
            ["LoadPokemonSpriteDummyResources"] = "Loads graphics for a Pokémon copy.",
            ["AddPokemonSprite"] = "Adds a Pokémon copy.",
            ["FreePokemonSpriteManager"] = "Frees the Pokémon copies.",
            ["RemovePokemonSprite"] = "Removes a Pokémon copy.",
            ["CancelTrackingTask"] = "Stops a copy moving on its own.",
            ["SetCameraProjection"] = "Changes the particle camera.",
            ["SetCameraFlip"] = "Turns the particle camera upside down.",
            ["JumpIfBattlerSide"] = "Branches on the Pokémon's side.",
            ["PlayPokemonCry"] = "Plays a cry.",
            ["WaitForPokemonCries"] = "Stops the cry after a number of frames.",
            ["StartTransform"] = "Starts Transform.",
            ["StartTransformRecolour"] = "Starts Transform's recolour.",
            ["JumpIfWeather"] = "Branches on the weather.",
            ["JumpIfContest"] = "Jumps in a contest.",
            ["JumpIfContestCondition"] = "Branches on a contest condition.",
            ["JumpIfFriendlyFire"] = "Branches when the move hits an ally.",
            ["InitSpriteManager"] = "Starts effect sprites.",
            ["LoadCharResObj"] = "Loads a sprite's graphics.",
            ["LoadPlttRes"] = "Loads a sprite's colours.",
            ["LoadCellResObj"] = "Loads a sprite's cells.",
            ["LoadAnimResObj"] = "Loads a sprite's animation.",
            ["AddSpriteWithFunc"] = "Adds a sprite moved by a built-in routine.",
            ["AddSprite"] = "Adds a sprite that plays its animation.",
            ["FreeSpriteManager"] = "Frees the effect sprites.",
            ["SetPokemonSpriteVisible"] = "Shows or hides a Pokémon copy.",
            ["CreatePokemonCopy"] = "Makes a Pokémon copy for particles to follow.",
            ["RemovePokemonCopy"] = "Removes the Pokémon copy from the background layer.",
            ["WaitForLRX"] = "Waits for a button press. Left over from testing.",
            ["SwitchBgAnimated"] = "Fades in a new background at a set position.",
            ["SwitchBgEx"] = "Changes the background unless it's already showing.",
            ["PlayMovingSoundEffectNoCorrection"] = "Pans a sound across in fixed steps.",
            ["PlayMovingSoundEffectAtkDef2"] = "Pans a sound across, past the end of the effect.",
            ["WaitForSoundEffects"] = "Waits for the sound to finish.",
            ["JumpIfBatonPass"] = "Jumps when Baton Pass is switching the user out.",
            ["FlashScreen"] = "Flashes the screen white.",
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
            ["SetBG0BG1AlphaBlending"] = new[] { "Effect", "Background" },
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
            ["Nop7"] = new[] { "Pokémon" },
            ["Nop8"] = new[] { "Pokémon" },
            ["Nop9"] = new[] { "Which" },
            ["Nop10"] = new[] { "Which" },
            ["StopSoundEffect"] = new[] { "Sound" },
            ["CallFunc"] = new[] { "Routine", "Values" },
            ["Nop11"] = new[] { "Routine", "Header", "Priority", "Values" },
            ["CreateEmitter"] = new[] { "Particle slot", "Particle data", "Behaviour" },
            ["CreateEmitterEx"] = new[] { "Particle slot", "Emitter", "Particle data", "Behaviour" },
            ["CreateEmitterForMove"] = new[] { "Particle slot", "Data 1", "Data 2", "Data 3", "Data 4", "Data 5", "Data 6", "Behaviour" },
            ["CreateEmitterForFriendlyFire"] = new[] { "Particle slot", "Data 1", "Data 2", "Data 3", "Data 4", "Behaviour" },
            ["LoadParticleSystem"] = new[] { "Particle slot", "Particle data" },
            ["LoadDebugParticleSystem"] = new[] { "Particle slot", "Archive", "Particle data" },
            ["UnloadParticleSystem"] = new[] { "Particle slot" },
            ["SetExtraParams"] = new[] { "Values", "Priority", "Anchor", "Position", "Direction", "Field", "Camera", "Extra" },
            ["LoadPokemonSpriteDummyResources"] = new[] { "Resource" },
            ["AddPokemonSprite"] = new[] { "Pokémon", "Auto-move", "Copy ID", "Resource" },
            ["RemovePokemonSprite"] = new[] { "Copy ID" },
            ["CancelTrackingTask"] = new[] { "Copy ID" },
            ["SetCameraProjection"] = new[] { "Camera", "Mode" },
            ["SetCameraFlip"] = new[] { "Camera", "Flag" },
            ["JumpIfBattlerSide"] = new[] { "Pokémon", "If player", "If enemy" },
            ["PlayPokemonCry"] = new[] { "Pokémon", "Pan", "Volume" },
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
            ["AddSpriteWithFunc"] = new[] { "Resource", "Routine", "ID 1", "ID 2", "ID 3", "ID 4", "ID 5", "ID 6", "Values" },
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

        /// <summary>The label a card shows for a value. Script text keeps <see cref="ParamName"/>.</summary>
        public static string ParamLabel(string opName, int index, IReadOnlyList<int> args, bool script = false)
        {
            if (!script && opName == "CallFunc" && index >= 2 && args != null && args.Count > 0)
                return BattleAnimFuncs.Label(args[0], index - 2) ?? "Value " + (index - 1);
            string name = ParamName(opName, index, script);
            return name.StartsWith("Param ", System.StringComparison.Ordinal) ? "Value " + (index + 1) : name;
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
