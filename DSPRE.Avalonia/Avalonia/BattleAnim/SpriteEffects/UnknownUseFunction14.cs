using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>Sprite function 14, BattleAnimSpriteFunc_Unused in the decomp: no retail move uses it. Three copies
    /// shrink onto the defender one after another, it shakes, and they grow back out.</summary>
    public static class UnknownUseFunction14
    {
        private const int Count = 3;

        // Afterimage_Init and Afterimage_Update in scale mode: the sprites show one by one, each running its own copy of
        // the scale, and hide when theirs ends.
        private sealed class Afterimage
        {
            private readonly CellActor[] _sprites;
            private readonly ScaleLerp[] _scales;
            private readonly int _delay;
            private int _counter, _active;

            public Afterimage(ScaleLerp scale, int delay, CellActor[] sprites)
            {
                _sprites = sprites; _delay = delay;
                _scales = new ScaleLerp[sprites.Length];
                for (int i = 0; i < sprites.Length; i++) { _scales[i] = scale.Clone(); sprites[i].Visible = false; }
            }

            public bool Update()
            {
                bool[] on = { true, true, true, true };
                if (_delay <= _counter && _active < _sprites.Length) { _sprites[_active++].Visible = true; _counter = 0; }
                if (_active < _sprites.Length) _counter++;
                for (int i = 0; i < _active; i++)
                {
                    on[i] = _scales[i].Update();
                    if (on[i]) { _sprites[i].ScaleX = _scales[i].AffineX; _sprites[i].ScaleY = _scales[i].AffineY; }
                    else _sprites[i].Visible = false;
                }
                for (int i = 0; i < _sprites.Length; i++) if (on[i]) return true;
                return false;
            }
        }

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int defender = sys.Defender;
            int dx = (short)sys.MonX(defender);
            // MON_SPRITE_SHADOW_HEIGHT, taken as the species' height above its shadow.
            int dy = (short)(sys.MonY(defender) - sys.MonYOffset(defender));
            CellActor[] sprites = new CellActor[Count];
            for (int i = 0; i < Count; i++)
            {
                CellActor s = sprites[i] = i == 0 ? sprite : sys.NewSprite(sprite);
                if (i == 0) s.SetSeq(1);
                else s.Xlu = true;
                s.SetPosition(dx, dy);
                s.Visible = false;
            }
            int state = 0;
            Afterimage images = null;
            Shake shake = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        images = new Afterimage(new ScaleLerp(25, 10, 10, 10), 9, new[] { sprites[2], sprites[1], sprites[0] });
                        state++;
                        break;
                    case 1:
                        if (!images.Update()) { sprites[0].Visible = true; state++; shake = new Shake(4, 0, 1, 6); }
                        break;
                    case 2:
                        if (shake.Update()) { sys.SetMonX(defender, dx + shake.X); sys.SetMonY(defender, dy + shake.Y); }
                        else
                        {
                            state++;
                            images = new Afterimage(new ScaleLerp(10, 10, 25, 6), 5, new[] { sprites[0], sprites[1], sprites[2] });
                            sprites[0].Visible = true;
                        }
                        break;
                    case 3:
                        if (!images.Update()) state++;
                        break;
                    default:
                        foreach (CellActor s in sprites) sys.DeleteSprite(s);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
