using System.Collections.Generic;
using DSPRE.ROMFiles;

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
            List<Line> lines = new List<Line>();
            int frame = 0;
            int loopDepth = 0;
            foreach (WazaSeqCommand c in cmds)
            {
                string name = BattleAnimCommands.Name(version, c.OpId) ?? $"#{c.OpId}";
                int depth = loopDepth > 0 ? loopDepth : 0;
                int stamp = frame;
                (string Icon, string Text)? desc = Describe(name, c.Args, ref frame, ref loopDepth);
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
                case "Delay": { int n = a.Length > 0 ? a[0] : 0; (string, string) s = ("wait", $"Wait {Plural.Of(n, "frame")}"); frame += n < 0 ? 0 : n; return s; }
                case "WaitForAnimTasks": return ("wait", "Wait for effects");
                case "End": return ("stop", "End");

                case "LoadParticleSystem":
                case "LoadDebugParticleSystem": return ("particle", $"Load particle set {Arg(1)} into slot {Arg(0)}");
                case "CreateEmitter":
                case "CreateEmitterEx":
                case "CreateEmitterForMove":
                case "CreateEmitterForFriendlyFire": return ("particle", $"Emitter {Arg(1)} from slot {Arg(0)}");
                case "WaitForAllEmitters": return ("particle", "Wait for the particles to finish");
                case "UnloadParticleSystem": return ("particle", $"Stop the particles in slot {Arg(0)}");

                case "CallFunc": return ("gear", BattleAnimScriptDisplay.RoutineName(a.Length > 0 ? a[0] : -1));
                case "Nop11": return ("gear", $"Cell routine {Hex(0)}");
                case "Nop3": return ("gear", $"Background sound {Hex(0)}");

                case "PlaySoundEffect":
                case "PlayPannedSoundEffect": return ("speaker", $"Play sound {Arg(0)}");
                case "PlayLoopedSoundEffect": return ("speaker", $"Loop sound {Arg(0)}");
                case "PlayDelayedSoundEffect": return ("speaker", $"Play sound {Arg(0)} after a delay");
                case "StopSoundEffect": return ("speaker", "Stop the sound");
                case "PlayPokemonCry": return ("speaker", "Play the cry");

                case "BeginLoop": loopDepth++; return ("reload", a.Length > 0 ? $"Repeat {Plural.Of(a[0], "time")}:" : "Repeat:");
                case "EndLoop": if (loopDepth > 0) loopDepth--; return ("reset", "End of repeat");

                case "SetBG0BG1AlphaBlending": return ("blend", $"Set blending to {Arg(0)} and {Arg(1)}");
                case "SetDefaultAlphaBlending": return ("blend", "Reset blending");
                case "Nop2": return ("blend", "Blend layers");

                case "SwitchBg":
                case "SwitchBgAnimated":
                case "SetBg": return ("picture", "Change the background");
                case "RestoreBg": return ("picture", "Bring back the background");

                case "SetCameraProjection": return ("camera", $"Switch to camera {Arg(0)}");
                case "SetCameraFlip": return ("camera", "Mirror the camera");

                case "Nop7": return ("hidden", $"Hide Pokémon {Arg(0)}");
                case "Nop8": return ("eye", $"Show Pokémon {Arg(0)}");

                case "FlashScreen": return ("flash", "Flash the screen");

                case "Call": return ("right", $"Run subroutine {Arg(0)}");
                case "Return": return ("left", "Return");

                default:
                    string args = a.Length == 0 ? "" : " " + string.Join(", ", a);
                    return (null, $"{BattleAnimSchema.OpcodeDisplay(name)}{args}");
            }
        }
    }
}
