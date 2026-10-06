using System.Collections.Generic;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.Data
{
    public readonly struct BattleAnimCommand
    {
        public readonly string Name;
        public readonly int ArgCount;
        public readonly bool IsVariable;
        public readonly int CountIndex;
        public BattleAnimCommand(string name, int argCount, bool isVariable, int countIndex)
        { Name = name; ArgCount = argCount; IsVariable = isVariable; CountIndex = countIndex; }
    }

    public static class BattleAnimCommands
    {
        private static readonly BattleAnimCommand[] Plat =
        {
            new("Delay", 1, false, -1),
            new("WaitForAnimTasks", 0, false, -1),
            new("BeginLoop", 1, false, 0),
            new("EndLoop", 0, false, -1),
            new("End", 0, false, -1),
            new("PlaySoundEffect", 1, false, -1),
            new("Nop0", 1, false, -1),
            new("Nop1", 1, false, -1),
            new("SetBG0BG1AlphaBlending", 2, false, -1),
            new("SetDefaultAlphaBlending", 0, false, -1),
            new("Call", 1, false, -1),
            new("Return", 0, false, -1),
            new("SetVar", 2, false, -1),
            new("JumpByTurn", 2, false, -1),
            new("JumpIfTurn", 2, false, -1),
            new("Jump", 1, false, -1),
            new("SwitchBg", 2, false, -1),
            new("SetBgSwitchVar", 2, false, -1),
            new("RestoreBg", 2, false, -1),
            new("WaitForPartialBgSwitch", 0, false, -1),
            new("WaitForBgSwitch", 0, false, -1),
            new("SetBg", 1, false, -1),
            new("PlayPannedSoundEffect", 2, false, -1),
            new("PanSoundEffects", 1, false, -1),
            new("PlayMovingSoundEffectAtkDef", 5, false, -1),
            new("PlayLoopedSoundEffect", 4, false, -1),
            new("PlayDelayedSoundEffect", 3, false, -1),
            new("Nop2", 1, false, -1),
            new("Nop3", 2, true, 1),
            new("WaitForSoundEffects", 0, false, -1),
            new("JumpIfEqual", 3, false, 1),
            new("LoadPokemonSpriteIntoBg", 2, false, -1),
            new("RemovePokemonSpriteFromBg", 1, false, -1),
            new("JumpIfContestCondition", 1, false, -1),
            new("SwitchBgEx", 3, false, -1),
            new("PlayMovingSoundEffectNoCorrection", 5, false, -1),
            new("PlayMovingSoundEffectAtkDef2", 5, false, -1),
            new("Nop4", 1, false, -1),
            new("Nop5", 0, false, -1),
            new("Nop6", 1, false, -1),
            new("Nop7", 1, false, -1),
            new("Nop8", 1, false, -1),
            new("Nop9", 1, false, -1),
            new("Nop10", 1, false, -1),
            new("StopSoundEffect", 1, false, -1),
            new("CallFunc", 2, true, 1),
            new("CreateEmitter", 3, false, -1),
            new("CreateEmitterEx", 4, false, -1),
            new("CreateEmitterForMove", 8, false, -1),
            new("CreateEmitterForFriendlyFire", 6, false, -1),
            new("WaitForAllEmitters", 0, false, -1),
            new("LoadParticleSystem", 2, false, -1),
            new("LoadDebugParticleSystem", 3, false, -1),
            new("UnloadParticleSystem", 1, false, -1),
            new("Nop11", 2, true, 1),
            new("SetExtraParams", 1, true, 0),
            new("InitPokemonSpriteManager", 0, false, -1),
            new("LoadPokemonSpriteDummyResources", 1, false, -1),
            new("AddPokemonSprite", 4, false, -1),
            new("FreePokemonSpriteManager", 0, false, -1),
            new("RemovePokemonSprite", 1, false, -1),
            new("CancelTrackingTask", 1, false, -1),
            new("SetCameraProjection", 2, false, -1),
            new("SetCameraFlip", 2, false, -1),
            new("JumpIfBattlerSide", 3, false, -1),
            new("PlayPokemonCry", 3, false, -1),
            new("WaitForPokemonCries", 1, false, -1),
            new("ResetVars", 0, false, -1),
            new("StartTransform", 1, false, -1),
            new("StartTransformRecolour", 1, false, -1),
            new("JumpIfWeather", 5, false, -1),
            new("JumpIfContest", 1, false, -1),
            new("JumpIfFriendlyFire", 1, false, -1),
            new("InitSpriteManager", 8, false, -1),
            new("LoadCharResObj", 2, false, -1),
            new("LoadPlttRes", 3, false, -1),
            new("LoadCellResObj", 2, false, -1),
            new("LoadAnimResObj", 2, false, -1),
            new("AddSpriteWithFunc", 9, true, 8),
            new("AddSprite", 8, false, -1),
            new("FreeSpriteManager", 1, false, -1),
            new("SetPokemonSpriteVisible", 2, false, -1),
            new("CreatePokemonCopy", 3, false, -1),
            new("RemovePokemonCopy", 1, false, -1),
            new("WaitForLRX", 0, false, -1),
        };

        private static readonly BattleAnimCommand[] Hgss =
        {
            new("Delay", 1, false, -1),
            new("WaitForAnimTasks", 0, false, -1),
            new("BeginLoop", 1, false, 0),
            new("EndLoop", 0, false, -1),
            new("End", 0, false, -1),
            new("PlaySoundEffect", 1, false, -1),
            new("Nop0", 1, false, -1),
            new("Nop1", 1, false, -1),
            new("SetBG0BG1AlphaBlending", 2, false, -1),
            new("SetDefaultAlphaBlending", 0, false, -1),
            new("Call", 1, false, -1),
            new("Return", 0, false, -1),
            new("SetVar", 2, false, -1),
            new("JumpByTurn", 2, false, -1),
            new("JumpIfTurn", 2, false, -1),
            new("Jump", 1, false, -1),
            new("SwitchBg", 2, false, -1),
            new("SetBgSwitchVar", 2, false, -1),
            new("RestoreBg", 2, false, -1),
            new("WaitForPartialBgSwitch", 0, false, -1),
            new("WaitForBgSwitch", 0, false, -1),
            new("SetBg", 1, false, -1),
            new("PlayPannedSoundEffect", 2, false, -1),
            new("PanSoundEffects", 1, false, -1),
            new("PlayMovingSoundEffectAtkDef", 5, false, -1),
            new("PlayLoopedSoundEffect", 4, false, -1),
            new("PlayDelayedSoundEffect", 3, false, -1),
            new("Nop2", 1, false, -1),
            new("Nop3", 2, true, 1),
            new("WaitForSoundEffects", 0, false, -1),
            new("JumpIfEqual", 3, false, 1),
            new("LoadPokemonSpriteIntoBg", 2, false, -1),
            new("RemovePokemonSpriteFromBg", 1, false, -1),
            new("JumpIfContestCondition", 1, false, -1),
            new("SwitchBgEx", 3, false, -1),
            new("PlayMovingSoundEffectNoCorrection", 5, false, -1),
            new("PlayMovingSoundEffectAtkDef2", 5, false, -1),
            new("Nop4", 1, false, -1),
            new("Nop5", 0, false, -1),
            new("Nop6", 1, false, -1),
            new("Nop7", 1, false, -1),
            new("Nop8", 1, false, -1),
            new("Nop9", 1, false, -1),
            new("Nop10", 1, false, -1),
            new("StopSoundEffect", 1, false, -1),
            new("CallFunc", 2, true, 1),
            new("CreateEmitter", 3, false, -1),
            new("CreateEmitterEx", 4, false, -1),
            new("CreateEmitterForMove", 8, false, -1),
            new("CreateEmitterForFriendlyFire", 6, false, -1),
            new("WaitForAllEmitters", 0, false, -1),
            new("LoadParticleSystem", 2, false, -1),
            new("LoadDebugParticleSystem", 3, false, -1),
            new("UnloadParticleSystem", 1, false, -1),
            new("Nop11", 2, true, 1),
            new("SetExtraParams", 1, true, 0),
            new("InitPokemonSpriteManager", 0, false, -1),
            new("LoadPokemonSpriteDummyResources", 1, false, -1),
            new("AddPokemonSprite", 4, false, -1),
            new("FreePokemonSpriteManager", 0, false, -1),
            new("RemovePokemonSprite", 1, false, -1),
            new("CancelTrackingTask", 1, false, -1),
            new("SetCameraProjection", 2, false, -1),
            new("SetCameraFlip", 2, false, -1),
            new("JumpIfBattlerSide", 3, false, -1),
            new("PlayPokemonCry", 3, false, -1),
            new("WaitForPokemonCries", 1, false, -1),
            new("ResetVars", 0, false, -1),
            new("StartTransform", 1, false, -1),
            new("StartTransformRecolour", 1, false, -1),
            new("JumpIfWeather", 5, false, -1),
            new("JumpIfContest", 1, false, -1),
            new("JumpIfFriendlyFire", 1, false, -1),
            new("InitSpriteManager", 8, false, -1),
            new("LoadCharResObj", 2, false, -1),
            new("LoadPlttRes", 3, false, -1),
            new("LoadCellResObj", 2, false, -1),
            new("LoadAnimResObj", 2, false, -1),
            new("AddSpriteWithFunc", 9, true, 8),
            new("AddSprite", 8, false, -1),
            new("FreeSpriteManager", 1, false, -1),
            new("SetPokemonSpriteVisible", 2, false, -1),
            new("CreatePokemonCopy", 3, false, -1),
            new("RemovePokemonCopy", 1, false, -1),
            new("WaitForLRX", 0, false, -1),
            new("FlashScreen", 1, false, -1),
            new("SwitchBgAnimated", 3, false, -1),
            new("JumpIfBatonPass", 1, false, -1),
        };

        public static BattleAnimCommand[] Table(WazaSeqVersion v) => v == WazaSeqVersion.HGSS ? WithHgEngine(Hgss) : Plat;
        public static int Count(WazaSeqVersion v) => Table(v).Length;
        public static bool TryGet(WazaSeqVersion v, int id, out BattleAnimCommand op)
        {
            BattleAnimCommand[] t = Table(v);
            if (id >= 0 && id < t.Length && t[id].ArgCount >= 0) { op = t[id]; return true; }
            op = default; return false;
        }

        private static System.Collections.Generic.IReadOnlyDictionary<int, HgEngine.HgEngineScriptCommands.Command> _extendedFrom;
        private static BattleAnimCommand[] _extended;

        // hg-engine adds commands after the game's own (changepermanentbg is 0x58); a number it skips stays unknown.
        private static BattleAnimCommand[] WithHgEngine(BattleAnimCommand[] retail)
        {
            IReadOnlyDictionary<int, HgEngineScriptCommands.Command> added = HgEngine.HgEngineScriptCommands.Animation();
            if (added.Count == 0) return retail;
            if (ReferenceEquals(added, _extendedFrom)) return _extended;
            int last = System.Math.Max(retail.Length - 1, System.Linq.Enumerable.Max(added.Keys));
            BattleAnimCommand[] table = new BattleAnimCommand[last + 1];
            for (int i = 0; i <= last; i++)
                table[i] = i < retail.Length ? retail[i]
                    : added.TryGetValue(i, out HgEngineScriptCommands.Command c) ? new BattleAnimCommand(c.Name, c.ArgCount, false, -1)
                    : new BattleAnimCommand("op" + i, -1, false, -1);
            _extendedFrom = added;
            return _extended = table;
        }
        public static string Name(WazaSeqVersion v, int id) => TryGet(v, id, out BattleAnimCommand op) ? op.Name : null;
        public static int Id(WazaSeqVersion v, string name)
        {
            BattleAnimCommand[] t = Table(v);
            for (int i = 0; i < t.Length; i++) if (t[i].Name == name) return i;
            return -1;
        }
    }
}
