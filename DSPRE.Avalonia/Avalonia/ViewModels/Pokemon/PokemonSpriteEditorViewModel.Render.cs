using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Editors.Utils;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;
using FormSpriteData = DSPRE.Graphics.AlternateFormSprites.Form;
using Avalonia.Media;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PokemonSpriteEditorViewModel
    {
        private static void RefreshSwatches(ObservableCollection<PaletteSwatch> target, uint[] palette, bool[] used, bool shiny)
        {
            target.Clear();
            if (palette == null) return;
            for (int i = 0; i < palette.Length; i++)
            {
                uint c = palette[i];
                Color color = global::Avalonia.Media.Color.FromArgb((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
                target.Add(new PaletteSwatch
                {
                    Brush = new global::Avalonia.Media.SolidColorBrush(color),
                    IsPlaceholder = used != null && i < used.Length && !used[i],
                    Index = i,
                    Shiny = shiny
                });
            }
        }

        private void ApplyPalettesAndPublish()
        {
            if (_normalPal == null) return;

            RefreshSwatches(NormalSwatches, _normalPal, _normalPalUsed, false);
            RefreshSwatches(ShinySwatches, _shinyPal, _shinyPalUsed, true);

            UpdateFrameAvailability();
            RenderCurrentFrameForAllCells();

            // Each gender gets a list of 80×80 frames; pokeanm picks the one shown. The count sets the limit.
            int frontW = SlotWidth(3) != 0 ? SlotWidth(3) : SlotWidth(2);
            BattleFrameCount = frontW != 0 ? Math.Max(1, frontW / 80) : 2;
            BattleFrontM = RenderFrames(3, _normalPal, BattleFrameCount);
            BattleFrontF = RenderFrames(2, _normalPal, BattleFrameCount);
            BattleBackM  = RenderFrames(1, _normalPal, BattleFrameCount);
            BattleBackF  = RenderFrames(0, _normalPal, BattleFrameCount);
            uint[] shiny = _shinyPal ?? _normalPal;
            BattleFrontMShiny = RenderFrames(3, shiny, BattleFrameCount);
            BattleFrontFShiny = RenderFrames(2, shiny, BattleFrameCount);
            BattleBackMShiny  = RenderFrames(1, shiny, BattleFrameCount);
            BattleBackFShiny  = RenderFrames(0, shiny, BattleFrameCount);
            OnPropertyChanged(nameof(CanUseFullSheet));
            OnPropertyChanged(nameof(IsHgEngineSourced));
            OnPropertyChanged(nameof(ShowShinyFullSheetImport));
            OnPropertyChanged(nameof(ShowFemaleFormImport));
            OnPropertyChanged(nameof(ShowFemaleShinyFormImport));
        }

        // Some ROM sprites have one drawn frame (Deoxys's back, several Unown). The rest is zero padding.
        private void UpdateFrameAvailability()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                byte[] indices = _rawSprites[slot];
                bool hasFrame1 = indices != null && !IsFrameBlank(indices, 0);
                bool hasFrame2 = indices != null && !IsFrameBlank(indices, 1);
                if (!hasFrame1 && !hasFrame2) hasFrame1 = true; // nothing loaded at all: don't hide both toggle buttons
                FrameCellForSlot(slot, false).SetFrameAvailability(hasFrame1, hasFrame2);
                FrameCellForSlot(slot, true).SetFrameAvailability(hasFrame1, hasFrame2);
            }
        }

        private FrameCellState FrameCellForSlot(int slot, bool shiny) => slot switch
        {
            0 => shiny ? FemaleBackShinyFrame  : FemaleBackNormalFrame,
            1 => shiny ? MaleBackShinyFrame    : MaleBackNormalFrame,
            2 => shiny ? FemaleFrontShinyFrame : FemaleFrontNormalFrame,
            3 => shiny ? MaleFrontShinyFrame   : MaleFrontNormalFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

        private static bool IsFrameBlank(byte[] indices, int frame)
        {
            const int fw = 80, h = 80, srcW = 160;
            int x0 = frame * fw;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < fw; x++)
                    if (indices[y * srcW + x0 + x] != 0) return false;
            return true;
        }

        // Writes into the array every import path changes, so Save stores it like an imported palette.
        public void SetPaletteColor(bool shiny, int index, uint argb, object group = null)
        {
            uint[] pal = shiny ? _shinyPal : _normalPal;
            if (pal == null || index < 0 || index >= pal.Length) return;
            pal[index] = argb;
            ApplyPalettesAndPublish();
            Edited(group);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private bool HasSlot(int slot) => _rawSprites[slot] != null;

        public int CurrentId => _currentId;
        public bool HasSpriteSlot(int slot) => HasSlot(slot);
        public bool HasPalette(bool shiny) => (shiny ? _shinyPal : _normalPal) != null;
        public string SpritePoseLabel(int slot) => SpriteImportWizardViewModel.AllPoses[slot];
        public byte[] GetRawSpriteIndices(int slot) => _rawSprites[slot];
        public byte[] GetRawSheetIndices(bool female) => BuildGenderSheetIndices(female);
        public byte[] GetRawFullSheetIndices() => BuildFullSheetIndices();
        public uint[] GetPalette(bool shiny) => shiny ? _shinyPal : _normalPal;
        public int SpritePixelWidth => SpriteWidth;
        public int SpritePixelHeight => SpriteHeight;
        private int SlotWidth(int slot) => _rawSprites[slot] != null ? SpriteWidth : 0;

        private System.Collections.Generic.IReadOnlyList<AvaBitmap> RenderFrames(int slot, uint[] palette, int count)
        {
            if (!HasSlot(slot)) return null;
            AvaBitmap[] frames = new AvaBitmap[count];
            for (int i = 0; i < count; i++) frames[i] = RenderBattleSprite(slot, palette, i);
            return frames;
        }

        private void RenderCurrentFrameForAllCells()
        {
            foreach (FrameCellState cell in AllFrameCells) cell.OnChanged(cell.Frame);
        }

        // The 80×80 cell at x = frame*80, palette index 0 transparent. Null when out of range.
        private AvaBitmap RenderBattleSprite(int slot, uint[] palette, int frame)
        {
            try
            {
                RawImage raw = ComposeSprite(slot, palette, transparentIndex0: true, frame: frame);
                return raw != null ? ImageConverter.ToAvaloniaBitmap(raw) : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("PokemonSpriteEditorViewModel.RenderBattleSprite", ex); return null; }
        }

        // frame >= 0 crops the 80-wide cell at x = frame*80. transparentIndex0 clears palette index 0.
        private RawImage ComposeSprite(int slot, uint[] palette, bool transparentIndex0, int frame)
        {
            byte[] indices = _rawSprites[slot];
            if (indices == null || palette == null) return null;

            int srcW = SpriteWidth, srcH = SpriteHeight;
            int x0 = 0, w = srcW;
            if (frame >= 0)
            {
                const int fw = 80;
                x0 = frame * fw; w = fw;
                if (x0 + fw > srcW) return null;
            }

            RawImage outImg = new RawImage(w, srcH);
            for (int y = 0; y < srcH; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4;
                    byte pi = indices[y * SpriteWidth + x0 + x];
                    if (transparentIndex0 && pi == 0) continue;   // stays 0,0,0,0
                    uint c = palette[pi & 0xF];
                    outImg.Bgra[o] = (byte)c;
                    outImg.Bgra[o + 1] = (byte)(c >> 8);
                    outImg.Bgra[o + 2] = (byte)(c >> 16);
                    outImg.Bgra[o + 3] = (byte)(c >> 24);
                }
            }
            return outImg;
        }

        private void ClearBitmaps()
        {
            FemaleBackNormal = MaleBackNormal = FemaleFrontNormal = MaleFrontNormal = null;
            FemaleBackShiny  = MaleBackShiny  = FemaleFrontShiny  = MaleFrontShiny  = null;
            BattleFrontM = BattleFrontF = BattleBackM = BattleBackF = null;
            _rawSprites = new byte[4][];
            _normalPal = null; _shinyPal = null;
            _normalPalUsed = AllUsed(); _shinyPalUsed = AllUsed();
            NormalSwatches.Clear();
            ShinySwatches.Clear();
            foreach (FrameCellState cell in AllFrameCells) cell.Frame = 0;
            CanAddOppositeGenderSprites = false;
            AddOppositeGenderLabel = "";
            _loadedFromHgEngine = false;
            OnPropertyChanged(nameof(IsHgEngineSourced));
            OnPropertyChanged(nameof(ShowShinyFullSheetImport));
        }
    }
}
