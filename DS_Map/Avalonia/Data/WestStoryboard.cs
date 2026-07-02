using System.Collections.Generic;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Renders a WEST (move visual-effect) command list as a readable, frame-stamped "storyboard" — a plain-text
    /// timeline of what the effect does (load/spawn particles, sounds, screen shakes & fades via FUNC_CALL, waits,
    /// loops, end). This is the data-level "view the animation" for Phase 1: it covers every move without a
    /// particle renderer. Frames advance on WAIT; everything else is listed at the current frame.
    /// </summary>
    public static class WestStoryboard
    {
        public static string Build(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            var sb = new StringBuilder();
            int frame = 0;
            int loopDepth = 0;
            foreach (var c in cmds)
            {
                string name = WestOpcodes.Name(version, c.OpId) ?? $"#{c.OpId}";
                int indentDepth = loopDepth > 0 ? loopDepth : 0;
                int stamp = frame;   // the frame this command runs at (WAIT advances it for the NEXT command)
                string desc = Describe(name, c.Args, ref frame, ref loopDepth);
                if (desc == null) continue;
                string indent = new string(' ', 2 * indentDepth);
                sb.Append('f').Append(stamp.ToString("D3")).Append("  ").Append(indent).AppendLine(desc);
            }
            if (sb.Length == 0) sb.AppendLine("(empty script)");
            return sb.ToString();
        }

        private static string Describe(string name, int[] a, ref int frame, ref int loopDepth)
        {
            string Arg(int i) => i < a.Length ? a[i].ToString() : "?";
            string Hex(int i) => i < a.Length ? "0x" + a[i].ToString("X") : "?";

            switch (name)
            {
                case "WEST_WAIT": { int n = a.Length > 0 ? a[0] : 0; string s = $"⏱ wait {n} frame(s)"; frame += n < 0 ? 0 : n; return s; }
                case "WEST_WAIT_FLAG": return "⏱ wait for current action to finish";
                case "WEST_SEQEND": return "■ end";

                case "WEST_LOAD_PARTICLE":
                case "WEST_LOAD_PARTICLE_EX": return $"✦ load particle set (slot {Arg(0)}, data {Arg(1)})";
                case "WEST_ADD_PARTICLE":
                case "WEST_ADD_PARTICLE_EMIT_SET":
                case "WEST_ADD_PARTICLE_SEP":
                case "WEST_ADD_PARTICLE_PTAT": return $"✦ spawn particle emitter (slot {Arg(0)}, emitter {Arg(1)})";
                case "WEST_WAIT_PARTICLE": return "✦ wait for particles to finish";
                case "WEST_EXIT_PARTICLE": return $"✦ release particle set (slot {Arg(0)})";

                case "WEST_FUNC_CALL":
                case "WEST_OLDACT_FUNC_CALL": return $"⚙ call effect routine (func {Hex(0)}, {Arg(1)} arg(s))";
                case "WEST_SE_TASK": return $"⚙ sound task (func {Hex(0)}, {Arg(1)} arg(s))";

                case "WEST_SE":
                case "WEST_SE_L": case "WEST_SE_R": case "WEST_SE_C":
                case "WEST_SEPLAY_PAN": return $"🔊 play sound {Arg(0)}";
                case "WEST_SE_REPEAT": return $"🔊 repeat sound {Arg(0)}";
                case "WEST_SE_WAITPLAY": return $"🔊 play sound {Arg(0)} (waited)";
                case "WEST_SE_STOP": return "🔊 stop sound";
                case "WEST_VOICE_PLAY": return "🔊 play cry";

                case "WEST_LOOP_LABEL": loopDepth++; return $"↻ loop {Arg(0)} time(s):";
                case "WEST_LOOP": if (loopDepth > 0) loopDepth--; return "↺ end loop";

                case "WEST_BLDALPHA_SET": return $"◑ set blend alpha ({Arg(0)},{Arg(1)})";
                case "WEST_BLDALPHA_RESET": return "◑ reset blend alpha";
                case "WEST_BLDCNT_SET": return "◑ set blend control";

                case "WEST_HAIKEI_CHG":
                case "WEST_HAIKEI_CHG_EX":
                case "WEST_HAIKEI_SET": return "▣ change background";
                case "WEST_HAIKEI_RECOVER": return "▣ restore background";

                case "WEST_CAMERA_CHG": return $"🎥 camera change ({Arg(0)})";
                case "WEST_CAMERA_REVERCE": return "🎥 camera reverse";

                case "WEST_POKE_BANISH_ON": return $"👻 hide Pokémon (client {Arg(0)})";
                case "WEST_POKE_BANISH_OFF": return $"👁 show Pokémon (client {Arg(0)})";

                case "WEST_FLASH": return "⚡ screen flash";

                case "WEST_SEQ_CALL": return $"→ call subroutine {Arg(0)}";
                case "WEST_END_CALL": return "← return from subroutine";

                default:
                    // Generic fallback: friendly opcode title + args (never the raw engine identifier).
                    string args = a.Length == 0 ? "" : " " + string.Join(", ", a);
                    return $"· {WestParamSchema.OpcodeDisplay(name)}{args}";
            }
        }
    }
}
