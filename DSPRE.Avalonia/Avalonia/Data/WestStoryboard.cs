using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Renders a WEST (move visual-effect) command list as a readable, frame-stamped "storyboard": a timeline
    /// of what the effect does (load/spawn particles, sounds, screen shakes & fades via FUNC_CALL, waits,
    /// loops, end). This is the data-level "view the animation" that covers every move without a particle
    /// renderer. Frames advance on WAIT; everything else is listed at the current frame.
    /// </summary>
    public static class WestStoryboard
    {
        /// <summary>One storyboard line: the frame it runs at, its loop depth, an icon key and what it does.</summary>
        public sealed record Line(string Frame, int Depth, string Icon, string Text)
        {
            public global::Avalonia.Thickness Indent => new global::Avalonia.Thickness(16 * Depth, 0, 0, 0);
        }

        public static List<Line> Build(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            var lines = new List<Line>();
            int frame = 0;
            int loopDepth = 0;
            foreach (var c in cmds)
            {
                string name = WestOpcodes.Name(version, c.OpId) ?? $"#{c.OpId}";
                int depth = loopDepth > 0 ? loopDepth : 0;
                int stamp = frame;   // the frame this command runs at (WAIT advances it for the NEXT command)
                var desc = Describe(name, c.Args, ref frame, ref loopDepth);
                if (desc == null) continue;
                lines.Add(new Line("f" + stamp.ToString("D3"), depth, desc.Value.Icon, desc.Value.Text));
            }
            return lines;
        }

        private static (string Icon, string Text)? Describe(string name, int[] a, ref int frame, ref int loopDepth)
        {
            string Arg(int i) => i < a.Length ? a[i].ToString() : "?";
            string Hex(int i) => i < a.Length ? "0x" + a[i].ToString("X") : "?";

            switch (name)
            {
                case "WEST_WAIT": { int n = a.Length > 0 ? a[0] : 0; var s = ("wait", $"wait {n} frame(s)"); frame += n < 0 ? 0 : n; return s; }
                case "WEST_WAIT_FLAG": return ("wait", "wait for current action to finish");
                case "WEST_SEQEND": return ("stop", "end");

                case "WEST_LOAD_PARTICLE":
                case "WEST_LOAD_PARTICLE_EX": return ("particle", $"load particle set (slot {Arg(0)}, data {Arg(1)})");
                case "WEST_ADD_PARTICLE":
                case "WEST_ADD_PARTICLE_EMIT_SET":
                case "WEST_ADD_PARTICLE_SEP":
                case "WEST_ADD_PARTICLE_PTAT": return ("particle", $"spawn particle emitter (slot {Arg(0)}, emitter {Arg(1)})");
                case "WEST_WAIT_PARTICLE": return ("particle", "wait for particles to finish");
                case "WEST_EXIT_PARTICLE": return ("particle", $"release particle set (slot {Arg(0)})");

                case "WEST_FUNC_CALL":
                case "WEST_OLDACT_FUNC_CALL": return ("gear", $"call effect routine (func {Hex(0)}, {Arg(1)} arg(s))");
                case "WEST_SE_TASK": return ("gear", $"sound task (func {Hex(0)}, {Arg(1)} arg(s))");

                case "WEST_SE":
                case "WEST_SE_L": case "WEST_SE_R": case "WEST_SE_C":
                case "WEST_SEPLAY_PAN": return ("speaker", $"play sound {Arg(0)}");
                case "WEST_SE_REPEAT": return ("speaker", $"repeat sound {Arg(0)}");
                case "WEST_SE_WAITPLAY": return ("speaker", $"play sound {Arg(0)} (waited)");
                case "WEST_SE_STOP": return ("speaker", "stop sound");
                case "WEST_VOICE_PLAY": return ("speaker", "play cry");

                case "WEST_LOOP_LABEL": loopDepth++; return ("reload", $"loop {Arg(0)} time(s):");
                case "WEST_LOOP": if (loopDepth > 0) loopDepth--; return ("reset", "end loop");

                case "WEST_BLDALPHA_SET": return ("blend", $"set blend alpha ({Arg(0)},{Arg(1)})");
                case "WEST_BLDALPHA_RESET": return ("blend", "reset blend alpha");
                case "WEST_BLDCNT_SET": return ("blend", "set blend control");

                case "WEST_HAIKEI_CHG":
                case "WEST_HAIKEI_CHG_EX":
                case "WEST_HAIKEI_SET": return ("picture", "change background");
                case "WEST_HAIKEI_RECOVER": return ("picture", "restore background");

                case "WEST_CAMERA_CHG": return ("camera", $"camera change ({Arg(0)})");
                case "WEST_CAMERA_REVERCE": return ("camera", "camera reverse");

                case "WEST_POKE_BANISH_ON": return ("hidden", $"hide Pokémon (client {Arg(0)})");
                case "WEST_POKE_BANISH_OFF": return ("eye", $"show Pokémon (client {Arg(0)})");

                case "WEST_FLASH": return ("flash", "screen flash");

                case "WEST_SEQ_CALL": return ("right", $"call subroutine {Arg(0)}");
                case "WEST_END_CALL": return ("left", "return from subroutine");

                default:
                    // Generic fallback: friendly opcode title + args (never the raw engine identifier).
                    string args = a.Length == 0 ? "" : " " + string.Join(", ", a);
                    return (null, $"{WestParamSchema.OpcodeDisplay(name)}{args}");
            }
        }
    }
}
