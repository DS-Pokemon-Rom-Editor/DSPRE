using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Which Pokémon a move-effect routine acts on, and what to call the flag on screen.
    ///
    /// The bits are from we_def.h:137-159. Their names read like sides but they are relative to the move:
    /// WT_SSPointerGet (we_tool.c:1431) looks M1 up as the attacker and E1 as the defender, and M2 and E2
    /// as their allies. It only looks the allies up in a double battle, so in a single battle a flag that
    /// asks only for M2 or E2 finds nobody and the routine does nothing at all. HeartGold's own scripts do
    /// that at 37 call sites, 28 of them WT_SHAKE.
    ///
    /// A flag is a set, not a choice. STAGE means everybody and OTHER means everybody except the attacker.
    /// </summary>
    public static class WestTargetFlags
    {
        public const int M1 = 0x0002, M2 = 0x0004, E1 = 0x0008, E2 = 0x0010;
        public const int Other = 0x0020, Stage = 0x0040;
        public const int Ssp = 0x0100, Cap = 0x0200, Bg = 0x0400, NotDefender = 0x0800;

        /// <summary>Cap ids 0 to 3 share the low bits with M1/M2/E1/E2, read that way when CAP is set.</summary>
        public const int C0 = 0x0002, C1 = 0x0004, C2 = 0x0008, C3 = 0x0010;

        /// <summary>Everybody the flag picks out, in the order the games pick them, for a single battle.</summary>
        public static List<int> Targets(int flag, int attacker, int defender)
        {
            var list = new List<int>(2);
            if ((flag & Stage) != 0) { list.Add(attacker); if (defender != attacker) list.Add(defender); return list; }
            if ((flag & Other) != 0) { if (defender != attacker) list.Add(defender); return list; }
            if ((flag & M1) != 0) list.Add(attacker);
            if ((flag & E1) != 0 && !list.Contains(defender)) list.Add(defender);
            return list;
        }

        /// <summary>The flag written out with the names the games use, for showing in the editor.</summary>
        /// <summary>
        /// Who a target flag picks out. The columnar views pass brief, because the "(as battle sprites)"
        /// half pushed the line past the edge of the pane; the detail pane shows the whole thing.
        /// </summary>
        public static string Describe(int flag, bool brief = false)
        {
            var parts = new List<string>();
            if ((flag & Cap) != 0)
            {
                if ((flag & C0) != 0) parts.Add("cap 0");
                if ((flag & C1) != 0) parts.Add("cap 1");
                if ((flag & C2) != 0) parts.Add("cap 2");
                if ((flag & C3) != 0) parts.Add("cap 3");
            }
            else
            {
                if ((flag & Stage) != 0) parts.Add("everyone");
                else if ((flag & Other) != 0) parts.Add("everyone but the attacker");
                else
                {
                    if ((flag & M1) != 0) parts.Add("attacker");
                    if ((flag & M2) != 0) parts.Add("attacker's ally");
                    if ((flag & E1) != 0) parts.Add("defender");
                    if ((flag & E2) != 0) parts.Add("defender's ally");
                }
            }
            if ((flag & Bg) != 0) parts.Add("background");
            if (parts.Count == 0) parts.Add("nobody");

            string where = brief ? ""
                         : (flag & Cap) != 0 ? " (as dropped sprites)"
                         : (flag & Ssp) != 0 ? " (as battle sprites)" : "";
            string not = (flag & NotDefender) != 0 ? ", not the defender" : "";
            return string.Join(" and ", parts) + where + not;
        }
    }
}
