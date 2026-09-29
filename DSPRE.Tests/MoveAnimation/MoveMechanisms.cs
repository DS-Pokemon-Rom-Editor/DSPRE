using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Avalonia.Data;

namespace DSPRE.Tests
{
    /// <summary>What a move animation is made of, named one mechanism at a time.</summary>
    internal static class MoveMechanisms
    {
        /// <summary>Every mechanism one script uses.</summary>
        public static SortedSet<string> Of(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            if (cmds == null || cmds.Count == 0) return found;

            int funcCall = BattleAnimCommands.Id(version, "CallFunc");
            bool anyParticle = false, anyMotion = false;

            foreach (var c in cmds)
            {
                string op = BattleAnimCommands.Name(version, c.OpId);
                if (op == null) continue;

                // Every opcode counts as its own mechanism. This is what stops the list going stale.
                found.Add("opcode: " + op);

                if (op.StartsWith("CreateEmitter", StringComparison.Ordinal)
                    || op is "LoadParticleSystem" or "LoadDebugParticleSystem" or "WaitForAllEmitters"
                            or "UnloadParticleSystem")
                { anyParticle = true; found.Add("draws with: particles"); }

                if (op is "InitSpriteManager" or "LoadCharResObj" or "LoadPlttRes" or "LoadCellResObj" or "LoadAnimResObj"
                        or "AddSpriteWithFunc" or "AddSprite" or "FreeSpriteManager")
                    found.Add("draws with: cell actors");
                if (op is "SwitchBg" or "SwitchBgEx" or "SwitchBgAnimated" or "SetBgSwitchVar" or "SetBg" or "RestoreBg"
                        or "WaitForBgSwitch" or "WaitForPartialBgSwitch")
                    found.Add("draws with: a background swap");
                if (op is "InitPokemonSpriteManager" or "LoadPokemonSpriteDummyResources" or "AddPokemonSprite"
                        or "RemovePokemonSprite" or "FreePokemonSpriteManager" or "CancelTrackingTask")
                    found.Add("draws with: dropped sprite copies");
                if (op is "LoadPokemonSpriteIntoBg" or "RemovePokemonSpriteFromBg") found.Add("draws with: a Pokemon background");
                if (op is "StartTransform" or "StartTransformRecolour") found.Add("draws with: a replaced Pokemon graphic");
                if (op == "FlashScreen") found.Add("screen: a flash");

                if (op.Contains("SoundEffect", StringComparison.Ordinal) || op is "PlayPokemonCry" or "WaitForPokemonCries")
                    found.Add("plays: sound");

                if (op is "EndLoop" or "BeginLoop") found.Add("structure: a loop");
                if (op is "Call" or "Return") found.Add("structure: a subroutine call");
                if (op is "JumpByTurn" or "JumpIfBattlerSide" or "Jump" or "JumpIfWeather"
                        or "JumpIfContest" or "JumpIfFriendlyFire")
                    found.Add("structure: a branch");

                // The operator settings, each value counted separately: a setting nothing uses is one the
                // preview never has to get right, and a setting one move uses is easy to miss.
                if (op == "SetExtraParams")
                {
                    for (int i = 0; i < c.Args.Length; i++)
                    {
                        var options = BattleAnimSchema.EnumFor(op, i);
                        if (options == null) continue;
                        foreach (var o in options)
                            if (o.Value == c.Args[i] && o.Label != "None")
                                found.Add($"setting: {BattleAnimSchema.ParamName(op, i)} = {o.Label}");
                    }
                }

                if (c.OpId == funcCall && c.Args.Length > 0)
                {
                    var r = BattleAnimFuncs.Get(c.Args[0]);
                    found.Add("routine: " + (r?.Name ?? c.Args[0].ToString()));
                    anyMotion = true;
                    if (c.Args[0] is 82 or 83) found.Add("draws with: a status overlay");
                }
            }

            // A move that never spawns a particle has to carry itself on Pokemon motion alone, which is a
            // different drawing path and worth covering on purpose.
            if (!anyParticle && anyMotion) found.Add("draws with: motion only");

            return found;
        }
    }
}
