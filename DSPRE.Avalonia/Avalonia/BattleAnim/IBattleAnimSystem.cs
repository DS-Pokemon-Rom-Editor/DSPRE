using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim
{
    /// <summary>The battle animation system as the effect tasks see it. Battlers are 0 for the player's side and 1
    /// for the enemy's.</summary>
    public interface IBattleAnimSystem
    {
        int Attacker { get; }
        int Defender { get; }

        /// <summary>BattleAnimSystem_IsContest.</summary>
        bool Contest { get; }

        /// <summary>BattleAnimUtil_GetTransformDirectionX and Y: 1, or -1 for the enemy's side.</summary>
        int TransformDirectionX(int battler);
        int TransformDirectionY(int battler);

        /// <summary>BattleAnimUtil_GetBattlerPos: the battle's fixed point for the battler.</summary>
        (int X, int Y) BattlerPos(int battler);

        /// <summary>The Pokemon sprite's attributes: MON_SPRITE_X_CENTER, MON_SPRITE_Y_CENTER, MON_SPRITE_SCALE_X and
        /// Y (0x100 is full size), MON_SPRITE_HIDE.</summary>
        int MonX(int battler);
        int MonY(int battler);
        void SetMonX(int battler, int x);
        void SetMonY(int battler, int y);
        int MonScaleX(int battler);
        int MonScaleY(int battler);
        void SetMonScale(int battler, int x, int y);
        void SetMonHidden(int battler, bool hidden);

        /// <summary>BattleAnimSystem_GetBattlerSpriteOffset: the species' height above its shadow.</summary>
        int MonYOffset(int battler);

        /// <summary>PokemonSprite_StartFade: moves the sprite's palette toward a colour by one sixteenth a step, with
        /// <paramref name="delay"/> passes between steps.</summary>
        void StartMonFade(int battler, int from, int to, int delay, int rgb15);
        bool MonFadeActive(int battler);

        /// <summary>PaletteData_StartFade on the background palettes, and whether that fade still runs.</summary>
        void StartBgFade(int wait, int from, int to, int rgb15);
        bool BgFadeActive { get; }

        /// <summary>BrightnessController_StartTransition on the main screen's background and Pokemon, from
        /// <paramref name="start"/> to <paramref name="target"/> (-16 black, 16 white) in <paramref name="steps"/> passes.</summary>
        void StartBrightness(int steps, int target, int start);
        bool BrightnessDone { get; }

        /// <summary>G2_ChangeBlendAlpha: the weights of translucent sprites and what is behind them.</summary>
        void SetBlendAlpha(int eva, int evb);

        /// <summary>AlphaFadeContext_Init: a task that steps the blend weights; done once it has ended.</summary>
        AlphaFade StartAlphaFade(int ev1From, int ev1To, int ev2From, int ev2To, int steps);

        /// <summary>SpriteSystem_DrawSprites: draws a manager's sprites this pass if its draw flag is set, then steps
        /// their animation if its animate flag is set.</summary>
        void DrawSprites(int manager);

        /// <summary>SpriteSystem_NewSprite with BattleAnimSystem_GetLastSpriteTemplate: another sprite like the one
        /// AddSpriteWithFunc made, at the defender's point.</summary>
        CellActor NewSprite(CellActor like);

        void DeleteSprite(CellActor sprite);

        /// <summary>BattleAnimSystem_StartAnimTaskEx: another task, which first runs next pass. Tasks run by priority,
        /// lowest first, then in the order they started; effect tasks use 1100.</summary>
        void StartTask(Func<bool> task, int priority);

        /// <summary>SysTask_Start: a task the script's WaitForAnimTasks doesn't wait for.</summary>
        void StartSysTask(Func<bool> task, int priority);

        /// <summary>LCRNG_Next, the game's general random numbers.</summary>
        int Random();
    }

    public sealed class AlphaFade
    {
        public bool Done;
    }
}
