using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The Pokémon sprite program-animation opcodes, in their numeric order (the value = the command id stored in
    /// the bytecode). These drive the per-Pokémon entry/idle program animations referenced by the animation data
    /// table (the Pokémon animation NARC).
    /// </summary>
    public enum PokemonAnimOp
    {
        End = 0, WaitFrame, SetOriginalPosition, SetVarIf, SetVar, CopyVar,
        Add, Multiply, Subtract, Divide, Modulo,
        Loop, LoopEnd, SetSpriteAttribute, AddSpriteAttribute, UpdateSpriteAttribute, Sin, Cos,
        SetTranslation, AddTranslation, UpdateAttribute, ApplyTranslation, ApplyScaleAndRotation, SetOffset,
        WaitTransform, SetYNormalization, TransformCurve, TransformCurveEven,
        TransformLinear, TransformLinearEven, TransformLinearBounded,
        SetStartDelay, Fade, WaitFade,
    }

    /// <summary>One decoded Pokémon animation command: an opcode plus its fixed-length argument words (each a 32-bit int).</summary>
    public sealed class PokemonAnimCommand
    {
        public PokemonAnimOp Op;
        public int[] Args;
        public PokemonAnimCommand(PokemonAnimOp op, int[] args) { Op = op; Args = args ?? Array.Empty<int>(); }
        public override string ToString() => Args.Length == 0 ? Op.ToString() : $"{Op} {string.Join(", ", Args)}";
    }

    /// <summary>
    /// Reads/writes a single Pokémon animation script (one file in the pokeanime NARC). The bytecode is a stream
    /// of little-endian 32-bit words: an opcode word followed by that opcode's fixed argument words. Parsing
    /// stops after <see cref="PokemonAnimOp.End"/>.
    /// </summary>
    public static class PokeAnimScript
    {
        // Argument-word count per opcode (index = (int)PokemonAnimOp).
        private static readonly int[] ArgCount =
        {
            /*End*/0, /*WaitFrame*/0, /*SetOriginalPosition*/0, /*SetVarIf*/7, /*SetVar*/2, /*CopyVar*/2,
            /*Add*/4, /*Multiply*/4, /*Subtract*/5, /*Divide*/5, /*Modulo*/5,
            /*Loop*/1, /*LoopEnd*/0, /*SetSpriteAttribute*/2, /*AddSpriteAttribute*/2, /*UpdateSpriteAttribute*/4, /*Sin*/6, /*Cos*/6,
            /*SetTranslation*/2, /*AddTranslation*/2, /*UpdateAttribute*/4, /*ApplyTranslation*/0, /*ApplyScaleAndRotation*/0, /*SetOffset*/2,
            /*WaitTransform*/0, /*SetYNormalization*/1, /*TransformCurve*/8, /*TransformCurveEven*/8,
            /*TransformLinear*/6, /*TransformLinearEven*/5, /*TransformLinearBounded*/6,
            /*SetStartDelay*/1, /*Fade*/4, /*WaitFade*/0,
        };

        public static int ArgsFor(PokemonAnimOp op)
        {
            int i = (int)op;
            return (i >= 0 && i < ArgCount.Length) ? ArgCount[i] : 0;
        }

        // Friendly argument names per opcode, for the editor's hints.
        private static readonly System.Collections.Generic.Dictionary<PokemonAnimOp, string[]> ArgNamesTable = new()
        {
            [PokemonAnimOp.SetVarIf] = new[] { "use1", "v1", "v2", "comp", "use2", "dst", "v4" },
            [PokemonAnimOp.SetVar] = new[] { "idx", "val" },
            [PokemonAnimOp.CopyVar] = new[] { "dstIdx", "srcIdx" },
            [PokemonAnimOp.Add] = new[] { "dst", "calc", "v1", "v2" },
            [PokemonAnimOp.Multiply] = new[] { "dst", "calc", "v1", "v2" },
            [PokemonAnimOp.Subtract] = new[] { "dst", "calc1", "calc2", "v1", "v2" },
            [PokemonAnimOp.Divide] = new[] { "dst", "calc1", "calc2", "v1", "v2" },
            [PokemonAnimOp.Modulo] = new[] { "dst", "calc1", "calc2", "v1", "v2" },
            [PokemonAnimOp.Loop] = new[] { "count" },
            [PokemonAnimOp.SetSpriteAttribute] = new[] { "ssParam", "idx" },
            [PokemonAnimOp.AddSpriteAttribute] = new[] { "ssParam", "idx" },
            [PokemonAnimOp.UpdateSpriteAttribute] = new[] { "ssParam", "use", "v", "ssCalc" },
            [PokemonAnimOp.Sin] = new[] { "dst", "radIdx", "use1", "L", "use2", "ofs" },
            [PokemonAnimOp.Cos] = new[] { "dst", "radIdx", "use1", "L", "use2", "ofs" },
            [PokemonAnimOp.SetTranslation] = new[] { "idx", "trans" },
            [PokemonAnimOp.AddTranslation] = new[] { "idx", "trans" },
            [PokemonAnimOp.UpdateAttribute] = new[] { "param", "use", "v", "calc" },
            [PokemonAnimOp.SetOffset] = new[] { "idx", "trans" },
            [PokemonAnimOp.SetYNormalization] = new[] { "flag" },
            [PokemonAnimOp.TransformCurve] = new[] { "apply", "wait", "type", "target", "L", "rad", "ofs", "loop" },
            [PokemonAnimOp.TransformCurveEven] = new[] { "apply", "wait", "type", "target", "L", "rad", "ofs", "loop" },
            [PokemonAnimOp.TransformLinear] = new[] { "apply", "wait", "target", "vel", "accel", "loop" },
            [PokemonAnimOp.TransformLinearEven] = new[] { "apply", "wait", "target", "move", "loop" },
            [PokemonAnimOp.TransformLinearBounded] = new[] { "apply", "wait", "target", "vel", "accel", "dst" },
            [PokemonAnimOp.SetStartDelay] = new[] { "wait" },
            [PokemonAnimOp.Fade] = new[] { "startEvy", "endEvy", "wait", "rgb" },
        };

        /// <summary>Friendly argument names for an opcode (empty array if it takes no args / has no labels).</summary>
        public static string[] ArgNames(PokemonAnimOp op) => ArgNamesTable.TryGetValue(op, out var n) ? n : Array.Empty<string>();

        /// <summary>Parses a script blob into commands. Tolerant: stops at End, or when a word isn't a known
        /// opcode / the args would run past the end (returns what parsed so far).</summary>
        public static List<PokemonAnimCommand> Parse(byte[] data)
        {
            var cmds = new List<PokemonAnimCommand>();
            if (data == null) return cmds;
            int pos = 0;
            int Words() => data.Length / 4;
            int ReadWord(int w) => BitConverter.ToInt32(data, w * 4);
            while (pos < Words())
            {
                int opVal = ReadWord(pos);
                if (opVal < 0 || opVal >= ArgCount.Length) break;   // not a valid opcode → stop
                var op = (PokemonAnimOp)opVal;
                int n = ArgCount[opVal];
                if (pos + 1 + n > Words()) break;                   // args would overrun → stop
                var args = new int[n];
                for (int i = 0; i < n; i++) args[i] = ReadWord(pos + 1 + i);
                cmds.Add(new PokemonAnimCommand(op, args));
                pos += 1 + n;
                if (op == PokemonAnimOp.End) break;
            }
            return cmds;
        }

        /// <summary>Serializes commands back to a little-endian word blob (for the editor's save path).</summary>
        public static byte[] Serialize(IReadOnlyList<PokemonAnimCommand> cmds)
        {
            int words = 0;
            foreach (var c in cmds) words += 1 + c.Args.Length;
            var data = new byte[words * 4];
            int pos = 0;
            void Write(int v) { BitConverter.GetBytes(v).CopyTo(data, pos * 4); pos++; }
            foreach (var c in cmds)
            {
                Write((int)c.Op);
                foreach (var a in c.Args) Write(a);
            }
            return data;
        }
    }
}
