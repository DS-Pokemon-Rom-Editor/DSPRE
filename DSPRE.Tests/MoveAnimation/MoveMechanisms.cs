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

            int funcCall = WestOpcodes.Id(version, "WEST_FUNC_CALL");
            bool anyParticle = false, anyMotion = false;

            foreach (var c in cmds)
            {
                string op = WestOpcodes.Name(version, c.OpId);
                if (op == null) continue;

                // Every opcode counts as its own mechanism. This is what stops the list going stale.
                found.Add("opcode: " + op);

                if (op.StartsWith("WEST_ADD_PARTICLE", StringComparison.Ordinal)
                    || op is "WEST_LOAD_PARTICLE" or "WEST_LOAD_PARTICLE_EX" or "WEST_WAIT_PARTICLE"
                            or "WEST_EXIT_PARTICLE")
                { anyParticle = true; found.Add("draws with: particles"); }

                if (op.StartsWith("WEST_CATS", StringComparison.Ordinal)) found.Add("draws with: cell actors");
                if (op.StartsWith("WEST_HAIKEI", StringComparison.Ordinal)) found.Add("draws with: a background swap");
                if (op.StartsWith("WEST_POKEOAM", StringComparison.Ordinal)) found.Add("draws with: dropped sprite copies");
                if (op is "WEST_POKEBG_DROP" or "WEST_POKEBG_DROP_RESET") found.Add("draws with: a Pokemon background");
                if (op is "WEST_HENSIN_ON" or "WEST_HENSIN_ON_RC") found.Add("draws with: a replaced Pokemon graphic");
                if (op == "WEST_FLASH") found.Add("screen: a flash");

                if (op.StartsWith("WEST_SE", StringComparison.Ordinal) || op.StartsWith("WEST_VOICE", StringComparison.Ordinal))
                    found.Add("plays: sound");

                if (op is "WEST_LOOP" or "WEST_LOOP_LABEL") found.Add("structure: a loop");
                if (op is "WEST_SEQ_CALL" or "WEST_END_CALL") found.Add("structure: a subroutine call");
                if (op is "WEST_TURN_CHK" or "WEST_SIDE_JP" or "WEST_SEQ_JP" or "WEST_TENKI_JP"
                        or "WEST_CONTEST_JP" or "WEST_PTAT_JP")
                    found.Add("structure: a branch");

                // The operator settings, each value counted separately: a setting nothing uses is one the
                // preview never has to get right, and a setting one move uses is easy to miss.
                if (op == "WEST_EX_DATA")
                {
                    for (int i = 0; i < c.Args.Length; i++)
                    {
                        var options = WestParamSchema.EnumFor(op, i);
                        if (options == null) continue;
                        foreach (var o in options)
                            if (o.Value == c.Args[i] && o.Label != "None")
                                found.Add($"setting: {WestParamSchema.ParamName(op, i)} = {o.Label}");
                    }
                }

                if (c.OpId == funcCall && c.Args.Length > 0)
                {
                    var r = WestRoutines.Get(c.Args[0]);
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
