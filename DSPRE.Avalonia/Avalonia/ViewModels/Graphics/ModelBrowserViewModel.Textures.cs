using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class ModelBrowserViewModel
    {
        /// <summary>The texture sets this model can use.</summary>
        public ObservableCollection<string> TextureChoices { get; } = new();
        public bool HasTextureChoice => TextureChoices.Count > 1;
        private readonly List<int> _textureSetEntries = new();

        private int _textureChoice;
        public int TextureChoice
        {
            get => _textureChoice;
            set
            {
                if (!Set(ref _textureChoice, value) || _selected == null) return;
                Draw();
                RaiseAll();
                if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BuildTextureChoices(ModelAssets.Archive a)
        {
            TextureChoices.Clear();
            _textureSetEntries.Clear();
            bool embedded = ModelAssets.EmbeddedTextures(a, ShowingIndex) != null
                         || ModelAssets.NeighbouringTextures(a, ShowingIndex) != null;
            TextureChoices.Add(embedded ? "Its own pictures" : "No embedded pictures");
            _textureSetEntries.Add(-1);
            int sets = ModelAssets.TextureSetCount(a);
            List<BuildingModelTextureSet> uses = _buildingTextureSets
                .Where(x => x.Indoor == a.Indoor && x.ModelIds.Contains(ShowingIndex))
                .OrderBy(x => x.TextureSetId).ToList();
            HashSet<int> authoritative = new HashSet<int>(uses.Select(x => x.TextureSetId));
            NSBMD model = ModelAssets.LoadModel(a, ShowingIndex);

            foreach (BuildingModelTextureSet use in uses)
            {
                if (use.TextureSetId < 0 || use.TextureSetId >= sets) continue;
                ModelAssets.TextureCoverage coverage = ModelAssets.Coverage(model, ModelAssets.TextureSet(a, use.TextureSetId));
                string areas = string.Join(", ", use.AreaIds.Take(4));
                if (use.AreaIds.Count > 4) areas += ", …";
                AddTextureChoice(use.TextureSetId,
                    $"ROM uses set {use.TextureSetId} · {CoverageText(coverage)} · area{(use.AreaIds.Count == 1 ? "" : "s")} {areas}");
            }

            List<(int id, ModelAssets.TextureCoverage coverage)> compatible = new List<(int id, ModelAssets.TextureCoverage coverage)>();
            for (int i = 0; uses.Count == 0 && i < sets; i++)
            {
                if (authoritative.Contains(i)) continue;
                ModelAssets.TextureCoverage coverage = ModelAssets.Coverage(model, ModelAssets.TextureSet(a, i));
                if (coverage.HasMatches) compatible.Add((i, coverage));
            }
            foreach ((int id, ModelAssets.TextureCoverage coverage) candidate in compatible
                .OrderByDescending(x => x.coverage.Complete)
                .ThenByDescending(x => x.coverage.MatchedTextures)
                .ThenBy(x => x.id))
                AddTextureChoice(candidate.id,
                    $"Likely match: set {candidate.id} · {CoverageText(candidate.coverage)}");

            // Clear then set: the refilled box has nothing selected, and the same number raises no change.
            _textureChoice = -1;
            OnPropertyChanged(nameof(TextureChoice));
            _textureChoice = uses.Count > 0 && TextureChoices.Count > 1 ? 1 : 0;
            OnPropertyChanged(nameof(TextureChoice));
            OnPropertyChanged(nameof(HasTextureChoice));
        }

        private void AddTextureChoice(int setId, string label)
        {
            _textureSetEntries.Add(setId);
            TextureChoices.Add(label);
        }

        private static string CoverageText(ModelAssets.TextureCoverage coverage)
        {
            if (coverage.RequiredTextures == 0) return "the model uses no textures";
            string textures = coverage.MatchedTextures == coverage.RequiredTextures
                ? $"all {Plural.Of(coverage.RequiredTextures, "texture")} found"
                : $"{coverage.MatchedTextures} of {coverage.RequiredTextures} textures found";
            if (coverage.RequiredPalettes == 0) return textures;
            string palettes = coverage.MatchedPalettes == coverage.RequiredPalettes
                ? $"all {coverage.RequiredPalettes} palette{(coverage.RequiredPalettes == 1 ? "" : "s")} found"
                : $"{coverage.MatchedPalettes} of {coverage.RequiredPalettes} palettes found";
            return textures + "; " + palettes;
        }

        public ObservableCollection<string> PreviewTextureNames { get; } = new();
        public ObservableCollection<string> PreviewPaletteNames { get; } = new();
        public bool HasPreviewTextureChoice => PreviewTextureNames.Count > 1;
        public bool HasPreviewPaletteChoice => PreviewPaletteNames.Count > 1;
        private List<NSBMDTexture> _previewTextures = new();
        private List<NSBMDPalette> _previewPalettes = new();
        private int _previewTextureIndex = -1, _previewPaletteIndex = -1;
        private bool _fillingTexturePreview;

        public int PreviewTextureIndex
        {
            get => _previewTextureIndex;
            set
            {
                int picked = PreviewTextureNames.Count == 0 ? -1 : Math.Max(0, value);
                if (!Set(ref _previewTextureIndex, picked) || _fillingTexturePreview) return;
                _fillingTexturePreview = true;
                _previewPaletteIndex = BestPaletteFor(picked);
                OnPropertyChanged(nameof(PreviewPaletteIndex));
                _fillingTexturePreview = false;
                RenderTexturePreview();
            }
        }

        public int PreviewPaletteIndex
        {
            get => _previewPaletteIndex;
            set
            {
                int picked = PreviewPaletteNames.Count == 0 ? -1 : Math.Max(0, value);
                if (Set(ref _previewPaletteIndex, picked) && !_fillingTexturePreview) RenderTexturePreview();
            }
        }

        private void LoadTexturePreview(ModelAssets.Archive archive, int index)
        {
            if (!ModelAssets.LoadTextureBundle(archive, index, out _previewTextures, out _previewPalettes))
            {
                Whynot = "This texture set couldn't be opened.";
                return;
            }

            _fillingTexturePreview = true;
            PreviewTextureNames.Clear();
            PreviewPaletteNames.Clear();
            foreach (NSBMDTexture texture in _previewTextures)
                PreviewTextureNames.Add(string.IsNullOrWhiteSpace(texture.Texname)
                    ? $"Texture {PreviewTextureNames.Count}" : texture.Texname);
            foreach (NSBMDPalette palette in _previewPalettes)
                PreviewPaletteNames.Add(string.IsNullOrWhiteSpace(palette.Palname)
                    ? $"Palette {PreviewPaletteNames.Count}" : palette.Palname);
            _previewTextureIndex = _previewTextures.Count > 0 ? 0 : -1;
            _previewPaletteIndex = BestPaletteFor(_previewTextureIndex);
            _fillingTexturePreview = false;
            OnPropertyChanged(nameof(PreviewTextureIndex));
            OnPropertyChanged(nameof(PreviewPaletteIndex));
            OnPropertyChanged(nameof(HasPreviewTextureChoice));
            OnPropertyChanged(nameof(HasPreviewPaletteChoice));
            RenderTexturePreview();
        }

        private int BestPaletteFor(int textureIndex)
        {
            string texture = textureIndex >= 0 && textureIndex < _previewTextures.Count
                ? _previewTextures[textureIndex].Texname : "";
            return ModelTexturePairing.BestPaletteIndex(_previewPalettes, texture);
        }

        private void RenderTexturePreview()
        {
            TexturePreview = null;
            if (_previewTextureIndex < 0 || _previewTextureIndex >= _previewTextures.Count)
            {
                Whynot = "This texture set contains no picture to show.";
                OnPropertyChanged(nameof(HasNoModel));
                return;
            }
            NSBMDTexture texture = _previewTextures[_previewTextureIndex];
            RGBA[] palette = _previewPaletteIndex >= 0 && _previewPaletteIndex < _previewPalettes.Count
                ? _previewPalettes[_previewPaletteIndex].Paldata : null;
            NsbmdTextureData decoded = NsbmdTextureDecoder.Decode(new NSBMDMaterial
            {
                Format = texture.Format,
                Width = texture.Width,
                Height = texture.Height,
                Texdata = texture.Texdata,
                Spdata = texture.Spdata,
                Color0 = texture.Color0,
                Paldata = palette,
            });
            if (decoded == null)
            {
                Whynot = texture.Format == 7 || palette != null
                    ? "This texture is malformed or uses data the preview can't decode."
                    : "This texture needs a palette, but this set doesn't contain one.";
                ShowFacts();
                OnPropertyChanged(nameof(HasNoModel));
                return;
            }
            Whynot = "";
            // No bitmap can be made without a running UI.
            try { TexturePreview = ImageConverter.FromRgba(decoded.Rgba, decoded.Width, decoded.Height); }
            catch (InvalidOperationException) { TexturePreview = null; }
            ShowFacts(new DetailFact("Texture", PreviewTextureNames[_previewTextureIndex]),
                      new DetailFact("Size", $"{decoded.Width} × {decoded.Height}"),
                      new DetailFact("Textures", _previewTextures.Count.ToString()),
                      new DetailFact("Palettes", _previewPalettes.Count.ToString()));
        }

        private void ClearTexturePreview()
        {
            TexturePreview = null;
            _previewTextures.Clear();
            _previewPalettes.Clear();
            PreviewTextureNames.Clear();
            PreviewPaletteNames.Clear();
            _previewTextureIndex = _previewPaletteIndex = -1;
            OnPropertyChanged(nameof(PreviewTextureIndex));
            OnPropertyChanged(nameof(PreviewPaletteIndex));
            OnPropertyChanged(nameof(HasTexturePreview));
            OnPropertyChanged(nameof(HasPreviewTextureChoice));
            OnPropertyChanged(nameof(HasPreviewPaletteChoice));
        }
    }
}
