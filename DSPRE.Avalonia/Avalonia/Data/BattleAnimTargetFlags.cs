using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public static class BattleAnimTargetFlags
    {
        public const int Attacker = 0x0002, AttackerPartner = 0x0004, Defender = 0x0008, DefenderPartner = 0x0010;
        public const int NotAttacker = 0x0020, AllBattlers = 0x0040;
        public const int BattlerSprites = 0x0100, PokemonSprites = 0x0200, Background = 0x0400, SpecificBattler = 0x0800;

        public const int PokemonSprite0 = 0x0002, PokemonSprite1 = 0x0004, PokemonSprite2 = 0x0008, PokemonSprite3 = 0x0010;

        // With SpecificBattler the other bits name fixed slots, not roles: the player's and the enemy's first battler.
        public const int PlayerSlot = 0, EnemySlot = 1;

        public static List<int> Targets(int flag, int attacker, int defender)
        {
            List<int> list = new List<int>(2);
            if ((flag & SpecificBattler) != 0)
            {
                if ((flag & Attacker) != 0) list.Add(PlayerSlot);
                if ((flag & Defender) != 0 && !list.Contains(EnemySlot)) list.Add(EnemySlot);
                return list;
            }
            if ((flag & AllBattlers) != 0) { list.Add(attacker); if (defender != attacker) list.Add(defender); return list; }
            if ((flag & NotAttacker) != 0) { if (defender != attacker) list.Add(defender); return list; }
            if ((flag & Attacker) != 0) list.Add(attacker);
            if ((flag & Defender) != 0 && !list.Contains(defender)) list.Add(defender);
            return list;
        }

        public static string Describe(int flag, bool brief = false)
        {
            List<string> parts = new List<string>();
            if ((flag & PokemonSprites) != 0)
            {
                if ((flag & PokemonSprite0) != 0) parts.Add("copy 0");
                if ((flag & PokemonSprite1) != 0) parts.Add("copy 1");
                if ((flag & PokemonSprite2) != 0) parts.Add("copy 2");
                if ((flag & PokemonSprite3) != 0) parts.Add("copy 3");
            }
            else if ((flag & SpecificBattler) != 0)
            {
                if ((flag & Attacker) != 0) parts.Add("the player's first Pokemon");
                if ((flag & AttackerPartner) != 0) parts.Add("the player's second Pokemon");
                if ((flag & Defender) != 0) parts.Add("the enemy's first Pokemon");
                if ((flag & DefenderPartner) != 0) parts.Add("the enemy's second Pokemon");
            }
            else
            {
                if ((flag & AllBattlers) != 0) parts.Add("everyone");
                else if ((flag & NotAttacker) != 0) parts.Add("everyone but the attacker");
                else
                {
                    if ((flag & Attacker) != 0) parts.Add("attacker");
                    if ((flag & AttackerPartner) != 0) parts.Add("attacker's ally");
                    if ((flag & Defender) != 0) parts.Add("defender");
                    if ((flag & DefenderPartner) != 0) parts.Add("defender's ally");
                }
            }
            if ((flag & Background) != 0) parts.Add("background");
            if (parts.Count == 0) parts.Add("nobody");

            string where = brief ? ""
                         : (flag & PokemonSprites) != 0 ? " (as dropped sprites)"
                         : (flag & BattlerSprites) != 0 ? " (as battle sprites)" : "";
            return string.Join(" and ", parts) + where;
        }
    }
}
