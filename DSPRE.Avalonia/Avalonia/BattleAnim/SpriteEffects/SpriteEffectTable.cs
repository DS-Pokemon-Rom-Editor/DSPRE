using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>Sprite functions AddSpriteWithFunc calls, by index in sBattleAnimSpriteFuncs (script_func_tables.c).
    /// Each returns its task (false once ended), or null if it started its own tasks.</summary>
    public static class SpriteEffectTable
    {
        public static Func<IBattleAnimSystem, CellActor, Func<bool>> Get(int id) => id switch
        {
            0 => SpriteExample.Start,
            1 => StringShot.Start,
            2 => Kinesis.Start,
            3 => Trick.Start,
            4 => Metronome.Start,
            5 => Constrict.Start,
            6 => Bonemerang.Start,
            7 => ScaryFace.Start,
            8 => Foresight.Start,
            9 => LockOn.Start,
            10 => Swagger.Start,
            11 => MeanLook.Start,
            12 => Torment.Start,
            13 => BatonPass.Start,
            14 => UnknownUseFunction14.Start,
            15 => Grudge.Start,
            16 => GrassWhistle.Start,
            17 => IcicleSpear.Start,
            18 => FakeOut.Start,
            19 => Taunt.Start,
            20 => HelpingHand.Start,
            21 => Assist.Start,
            22 => MetalClaw.Start,
            23 => Ingrain.Start,
            24 => FrenzyPlant.Start,
            25 => OffsetAndAnimate.Start,
            26 => FollowMe.Start,
            27 => Fissure.Start,
            28 => EscapeItem.Start,
            29 => Sleep.Start,
            30 => Burn.Start,
            31 => Freeze.Start,
            32 => ConfusionStatus.Start,
            _ => null,
        };
    }
}
