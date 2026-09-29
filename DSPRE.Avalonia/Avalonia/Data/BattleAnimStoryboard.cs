using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public static class BattleAnimStoryboard
    {
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
                string name = BattleAnimCommands.Name(version, c.OpId) ?? $"#{c.OpId}";
                int depth = loopDepth > 0 ? loopDepth : 0;
                int stamp = frame;
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
                case "Delay": { int n = a.Length > 0 ? a[0] : 0; var s = ("wait", $"wait {n} frame(s)"); frame += n < 0 ? 0 : n; return s; }
                case "WaitForAnimTasks": return ("wait", "wait for current action to finish");
                case "End": return ("stop", "end");

                case "LoadParticleSystem":
                case "LoadDebugParticleSystem": return ("particle", $"load particle set (slot {Arg(0)}, data {Arg(1)})");
                case "CreateEmitter":
                case "CreateEmitterEx":
                case "CreateEmitterForMove":
                case "CreateEmitterForFriendlyFire": return ("particle", $"spawn particle emitter (slot {Arg(0)}, emitter {Arg(1)})");
                case "WaitForAllEmitters": return ("particle", "wait for particles to finish");
                case "UnloadParticleSystem": return ("particle", $"release particle set (slot {Arg(0)})");

                case "CallFunc":
                case "Nop11": return ("gear", $"call effect routine (func {Hex(0)}, {Arg(1)} arg(s))");
                case "Nop3": return ("gear", $"sound task (func {Hex(0)}, {Arg(1)} arg(s))");

                case "PlaySoundEffect":
                case "PlayPannedSoundEffect": return ("speaker", $"play sound {Arg(0)}");
                case "PlayLoopedSoundEffect": return ("speaker", $"repeat sound {Arg(0)}");
                case "PlayDelayedSoundEffect": return ("speaker", $"play sound {Arg(0)} (waited)");
                case "StopSoundEffect": return ("speaker", "stop sound");
                case "PlayPokemonCry": return ("speaker", "play cry");

                case "BeginLoop": loopDepth++; return ("reload", $"loop {Arg(0)} time(s):");
                case "EndLoop": if (loopDepth > 0) loopDepth--; return ("reset", "end loop");

                case "SetBG0BG1AlphaBlending": return ("blend", $"set blend alpha ({Arg(0)},{Arg(1)})");
                case "SetDefaultAlphaBlending": return ("blend", "reset blend alpha");
                case "Nop2": return ("blend", "set blend control");

                case "SwitchBg":
                case "SwitchBgAnimated":
                case "SetBg": return ("picture", "change background");
                case "RestoreBg": return ("picture", "restore background");

                case "SetCameraProjection": return ("camera", $"camera change ({Arg(0)})");
                case "SetCameraFlip": return ("camera", "camera reverse");

                case "Nop7": return ("hidden", $"hide Pokémon (client {Arg(0)})");
                case "Nop8": return ("eye", $"show Pokémon (client {Arg(0)})");

                case "FlashScreen": return ("flash", "screen flash");

                case "Call": return ("right", $"call subroutine {Arg(0)}");
                case "Return": return ("left", "return from subroutine");

                default:
                    string args = a.Length == 0 ? "" : " " + string.Join(", ", a);
                    return (null, $"{BattleAnimSchema.OpcodeDisplay(name)}{args}");
            }
        }
    }
}
