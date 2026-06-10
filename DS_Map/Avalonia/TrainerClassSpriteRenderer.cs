using System;
using System.Linq;
using Ekona.Images;
using Images;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Reusable trainer-class sprite renderer, extracted from the WinForms
    /// <c>TrainerEditor.LoadTrainerClassPic</c>/<c>UpdateTrainerClassPic</c>. Loads a
    /// trainer class's NCLR/NCGR (+ NCER for Plat/HGSS) from the trainerGraphics NARC
    /// and renders an animation frame to an Avalonia bitmap.
    ///
    /// Shared by the Avalonia Trainer Editor and the Table Editor (whose VS-Trainer
    /// preview was previously omitted for lack of this renderer). DP trainer classes
    /// have no NCER (no animation) — <see cref="FrameCount"/> is 0 and
    /// <see cref="Render"/> returns null.
    /// </summary>
    public sealed class TrainerClassSpriteRenderer
    {
        private PaletteBase _pal;
        private ImageBase _tile;
        private SpriteBase _sprite;

        public int FrameCount { get; private set; }
        public bool HasSprite => _sprite != null;

        /// <summary>Loads the graphics for a trainer class. Returns the max frame index (FrameCount-1), or 0 on DP / failure.</summary>
        public int Load(int trClassID)
        {
            _pal = null; _tile = null; _sprite = null; FrameCount = 0;
            try
            {
                string dir = gameDirs[DirNames.trainerGraphics].unpackedDir;

                int paletteFileID = trClassID * 5 + 1;
                string paletteFilename = paletteFileID.ToString("D4");
                _pal = new NCLR(dir + "\\" + paletteFilename, paletteFileID, paletteFilename);

                int tilesFileID = trClassID * 5;
                string tilesFilename = tilesFileID.ToString("D4");
                _tile = new NCGR(dir + "\\" + tilesFilename, tilesFileID, tilesFilename);

                if (gameFamily == GameFamilies.DP)
                    return 0; // DP has no NCER animation for trainer classes.

                int spriteFileID = trClassID * 5 + 2;
                string spriteFilename = spriteFileID.ToString("D4");
                _sprite = new NCER(dir + "\\" + spriteFilename, spriteFileID, spriteFilename);
                FrameCount = _sprite.Banks.Length;
                return FrameCount - 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerClassSpriteRenderer.Load failed: " + ex.Message);
                _pal = null; _tile = null; _sprite = null; FrameCount = 0;
                return 0;
            }
        }

        /// <summary>Renders the given frame to an Avalonia bitmap, or null if there is no animated sprite.</summary>
        public AvaBitmap Render(int frame, int width, int height)
        {
            if (_sprite == null) return null;
            try
            {
                int bank0OAMcount = _sprite.Banks[0].oams.Length;
                int[] oamEnabled = Enumerable.Range(0, bank0OAMcount).ToArray();
                frame = Math.Max(0, Math.Min(_sprite.Banks.Length, frame));

                var gdi = _sprite.Get_Image(_tile, _pal, frame, width, height,
                    false, false, false, true, true, -1, oamEnabled);
                return ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerClassSpriteRenderer.Render failed: " + ex.Message);
                return null;
            }
        }
    }
}
