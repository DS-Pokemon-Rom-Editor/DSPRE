using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_ScaryFace, also used by Glare: the attacker stretches up and back while a face grows
    /// beside it, rises and fades out.</summary>
    public static class ScaryFace
    {
        private const int AttackerStart = 10, AttackerRef = 10, AttackerEnd = 15, AttackerFrames = 12;
        private const int FaceStart = 5, FaceRef = 10, FaceEnd = 12, FaceScaleFrames = 32, FaceOffsetX = 32, FaceMoveFrames = 32;
        private const int FaceMoveYEnemy = -8, FaceMoveYPlayer = -24;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int dirX = sys.TransformDirectionX(attacker), dirY = sys.TransformDirectionY(attacker);
            int attackerX = (short)sys.MonX(attacker), attackerY = (short)sys.MonY(attacker);
            ScaleLerp attackerScale = new ScaleLerp(AttackerStart, AttackerRef, AttackerEnd, AttackerFrames);
            sprite.Animate = true;
            sprite.Xlu = true;
            int baseX = attackerX + FaceOffsetX * dirX, baseY = attackerY;
            int moveY = dirY < 0 ? FaceMoveYEnemy : FaceMoveYPlayer;
            PosLerp facePos = new PosLerp(0, 0, 0, moveY * dirY, FaceMoveFrames);
            ScaleLerp faceScale = new ScaleLerp(FaceStart, FaceRef, FaceEnd, FaceScaleFrames);
            AlphaFade faceAlpha = null;
            sys.SetBlendAlpha(31, 26);
            int state = 0, attackerState = 0, faceState = 0;

            void UpdateAttacker()
            {
                if (attackerState == 0)
                {
                    if (attackerScale.Update()) sys.SetMonScale(attacker, sys.MonScaleX(attacker), attackerScale.Y);
                    else { attackerState++; attackerScale = new ScaleLerp(AttackerEnd, AttackerRef, AttackerStart, AttackerFrames); }
                }
                else if (attackerState == 1)
                {
                    if (attackerScale.Update()) sys.SetMonScale(attacker, sys.MonScaleX(attacker), attackerScale.Y);
                    else { sys.SetMonScale(attacker, sys.MonScaleX(attacker), BattleAnimMath.MonScaleOne); attackerState++; }
                }
            }

            bool UpdateFace()
            {
                switch (faceState)
                {
                    case 0:
                        if (faceScale.Update()) { sprite.ScaleX = faceScale.AffineX; sprite.ScaleY = faceScale.AffineY; }
                        if (facePos.Update()) sprite.SetPosition(baseX + facePos.X, baseY + facePos.Y);
                        else { faceState++; faceAlpha = sys.StartAlphaFade(16, 0, 14, 16, 8); }
                        return false;
                    case 1:
                        if (faceAlpha.Done) { sprite.Visible = false; faceState++; }
                        return false;
                    default:
                        return true;
                }
            }

            return () =>
            {
                if (state != 0)
                {
                    sys.DeleteSprite(sprite);
                    return false;
                }
                UpdateAttacker();
                if (UpdateFace()) state++;
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
