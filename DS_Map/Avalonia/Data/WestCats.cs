using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Pulls the cell-animation (CATS) resource file indices out of a WEST move-effect script. A move that uses the
    /// cell-actor layer issues <c>CATS_*_RES_LOAD res, arc</c> commands; the <c>arc</c> word is the file index into
    /// the matching wazaeffect/effectclact NARC (char/pltt/cell/cellanm). Only a minority of moves use this; the
    /// rest are particle-only (<see cref="HasCellAnimation"/> is false).
    /// </summary>
    public readonly struct WestCatsResources
    {
        public readonly int Char, Pltt, Cell, CellAnm;
        public WestCatsResources(int c, int p, int ce, int ca) { Char = c; Pltt = p; Cell = ce; CellAnm = ca; }
        public bool HasCellAnimation => Char >= 0 && Pltt >= 0 && Cell >= 0 && CellAnm >= 0;
    }

    public static class WestCats
    {
        // arc_no is the 2nd arg word of each CATS_*_RES_LOAD opcode (res_no, arc_no, …).
        public static WestCatsResources Extract(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            int ch = -1, pl = -1, ce = -1, ca = -1;
            if (cmds != null)
            {
                foreach (var c in cmds)
                {
                    string name = WestOpcodes.Name(version, c.OpId);
                    if (name == null || c.Args.Length < 2) continue;
                    int arc = c.Args[1];
                    switch (name)
                    {
                        case "WEST_CATS_CAHR_RES_LOAD": ch = arc; break;   // (source spells it "CAHR")
                        case "WEST_CATS_PLTT_RES_LOAD": pl = arc; break;
                        case "WEST_CATS_CELL_RES_LOAD": ce = arc; break;
                        case "WEST_CATS_CELLANM_RES_LOAD": ca = arc; break;
                    }
                }
            }
            return new WestCatsResources(ch, pl, ce, ca);
        }
    }
}
