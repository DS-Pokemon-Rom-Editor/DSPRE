using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public readonly struct BattleAnimSpriteResources
    {
        public readonly int Char, Pltt, Cell, CellAnm;
        public BattleAnimSpriteResources(int c, int p, int ce, int ca) { Char = c; Pltt = p; Cell = ce; CellAnm = ca; }
        public bool HasCellAnimation => Char >= 0 && Pltt >= 0 && Cell >= 0 && CellAnm >= 0;
    }

    public static class BattleAnimSprites
    {
        public static BattleAnimSpriteResources Extract(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            int ch = -1, pl = -1, ce = -1, ca = -1;
            if (cmds != null)
            {
                foreach (var c in cmds)
                {
                    string name = BattleAnimCommands.Name(version, c.OpId);
                    if (name == null || c.Args.Length < 2) continue;
                    int arc = c.Args[1];
                    switch (name)
                    {
                        case "LoadCharResObj": ch = arc; break;
                        case "LoadPlttRes": pl = arc; break;
                        case "LoadCellResObj": ce = arc; break;
                        case "LoadAnimResObj": ca = arc; break;
                    }
                }
            }
            return new BattleAnimSpriteResources(ch, pl, ce, ca);
        }
    }
}
