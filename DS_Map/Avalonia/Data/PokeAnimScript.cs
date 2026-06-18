using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The "PAST" (Poke Anime Sequence Tool) program-animation opcodes, in their numeric order (the value =
    /// the command id stored in the bytecode). Decoded from the leaked source (src/pokeanime/past.h). These
    /// drive the per-mon entry/idle program animations referenced by POKE_ANM_DATA.prg_anm_* (pokeanime NARC).
    /// </summary>
    public enum PastOp
    {
        End = 0, SetRequest, SetDefault, SetIfWorkVal, SetWorkVal, CopyWorkVal,
        AddWorkVal, MulWorkVal, SubWorkVal, DivWorkVal, ModWorkVal,
        StartLoop, EndLoop, SetVal, AddVal, SetAddVal, SetWorkValSin, SetWorkValCos,
        SetTrans, AddTrans, SetAddParam, ApplyTrans, ApplyAffine, SetD,
        HoldCmd, SetDyCorrect, CallMfCurve, CallMfCurveDivTime,
        CallMfLine, CallMfLineDivTime, CallMfLineDst,
        SetWait, PaletteFade, WaitPaletteFade,
    }

    /// <summary>One decoded PAST command: an opcode plus its fixed-length argument words (each a 32-bit int).</summary>
    public sealed class PastCommand
    {
        public PastOp Op;
        public int[] Args;
        public PastCommand(PastOp op, int[] args) { Op = op; Args = args ?? Array.Empty<int>(); }
        public override string ToString() => Args.Length == 0 ? Op.ToString() : $"{Op} {string.Join(", ", Args)}";
    }

    /// <summary>
    /// Reads/writes a single PAST animation script (one file in the pokeanime NARC). The bytecode is a stream
    /// of little-endian 32-bit words: an opcode word followed by that opcode's fixed argument words. Parsing
    /// stops after <see cref="PastOp.End"/>.
    /// </summary>
    public static class PokeAnimScript
    {
        // Argument-word count per opcode (index = (int)PastOp), from the macros in past.h.
        private static readonly int[] ArgCount =
        {
            /*End*/0, /*SetRequest*/0, /*SetDefault*/0, /*SetIfWorkVal*/7, /*SetWorkVal*/2, /*CopyWorkVal*/2,
            /*AddWorkVal*/4, /*MulWorkVal*/4, /*SubWorkVal*/5, /*DivWorkVal*/5, /*ModWorkVal*/5,
            /*StartLoop*/1, /*EndLoop*/0, /*SetVal*/2, /*AddVal*/2, /*SetAddVal*/4, /*SetWorkValSin*/6, /*SetWorkValCos*/6,
            /*SetTrans*/2, /*AddTrans*/2, /*SetAddParam*/4, /*ApplyTrans*/0, /*ApplyAffine*/0, /*SetD*/2,
            /*HoldCmd*/0, /*SetDyCorrect*/1, /*CallMfCurve*/8, /*CallMfCurveDivTime*/8,
            /*CallMfLine*/6, /*CallMfLineDivTime*/5, /*CallMfLineDst*/6,
            /*SetWait*/1, /*PaletteFade*/4, /*WaitPaletteFade*/0,
        };

        public static int ArgsFor(PastOp op)
        {
            int i = (int)op;
            return (i >= 0 && i < ArgCount.Length) ? ArgCount[i] : 0;
        }

        /// <summary>Parses a script blob into commands. Tolerant: stops at End, or when a word isn't a known
        /// opcode / the args would run past the end (returns what parsed so far).</summary>
        public static List<PastCommand> Parse(byte[] data)
        {
            var cmds = new List<PastCommand>();
            if (data == null) return cmds;
            int pos = 0;
            int Words() => data.Length / 4;
            int ReadWord(int w) => BitConverter.ToInt32(data, w * 4);
            while (pos < Words())
            {
                int opVal = ReadWord(pos);
                if (opVal < 0 || opVal >= ArgCount.Length) break;   // not a valid opcode → stop
                var op = (PastOp)opVal;
                int n = ArgCount[opVal];
                if (pos + 1 + n > Words()) break;                   // args would overrun → stop
                var args = new int[n];
                for (int i = 0; i < n; i++) args[i] = ReadWord(pos + 1 + i);
                cmds.Add(new PastCommand(op, args));
                pos += 1 + n;
                if (op == PastOp.End) break;
            }
            return cmds;
        }

        /// <summary>Serializes commands back to a little-endian word blob (for the editor's save path).</summary>
        public static byte[] Serialize(IReadOnlyList<PastCommand> cmds)
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
