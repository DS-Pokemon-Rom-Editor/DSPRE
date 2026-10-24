namespace DSPRE.Avalonia.BattleAnim
{
    /// <summary>Who a move's animation plays on in a single battle.</summary>
    public static class BattleAnimTarget
    {
        // Move data keeps the range as one bit per RANGE_ value after RANGE_SINGLE_TARGET.
        private const ushort SingleTargetSpecial = 1 << 0, User = 1 << 4, UserSide = 1 << 5, Field = 1 << 6, Ally = 1 << 8,
                             UserOrAlly = 1 << 9;
        private const ushort EffectCounter = 89, EffectMirrorCoat = 144, EffectMetalBurst = 227;

        /// <summary>BattleSystem_CalcMoveTarget makes the user the defender for these ranges, including ally moves in a
        /// single battle. Counter, Mirror Coat and Metal Burst then retarget the last attacker.</summary>
        public static bool DefenderIsUser(ushort range, ushort battleEffect)
        {
            if ((range & (User | UserSide | Field | Ally | UserOrAlly)) != 0) return true;
            if ((range & SingleTargetSpecial) == 0) return false;
            return battleEffect != EffectCounter && battleEffect != EffectMirrorCoat && battleEffect != EffectMetalBurst;
        }
    }
}
