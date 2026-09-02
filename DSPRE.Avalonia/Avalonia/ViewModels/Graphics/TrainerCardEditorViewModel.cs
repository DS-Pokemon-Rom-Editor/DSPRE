using DSPRE.Avalonia.Data;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public class TrainerCardEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly TrainerCardGraphics _graphics = new();
        public bool GraphicsAvailable => _graphics.Available;
        public string[] RankNames => TrainerCardGraphics.RankNames;

        private int _selectedRankIndex;
        public int SelectedRankIndex
        {
            get => _selectedRankIndex;
            set { if (Set(ref _selectedRankIndex, value)) RefreshCardPreviews(); }
        }

        private AvaBitmap _cardFrontPreview, _cardBackPreview, _trainerMalePreview, _trainerFemalePreview;
        public AvaBitmap CardFrontPreview { get => _cardFrontPreview; private set => Set(ref _cardFrontPreview, value); }
        public AvaBitmap CardBackPreview { get => _cardBackPreview; private set => Set(ref _cardBackPreview, value); }
        public AvaBitmap TrainerMalePreview { get => _trainerMalePreview; private set => Set(ref _trainerMalePreview, value); }
        public AvaBitmap TrainerFemalePreview { get => _trainerFemalePreview; private set => Set(ref _trainerFemalePreview, value); }

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        public bool HasChanges => _graphics.HasChanges;

        public TrainerCardEditorViewModel()
        {
            RefreshCardPreviews();
            RefreshTrainerPreviews();
        }

        private void RefreshCardPreviews()
        {
            if (!GraphicsAvailable)
            {
                CardFrontPreview = CardBackPreview = null;
                StatusText = "Trainer card graphics are not available for this ROM.";
                OnPropertyChanged(nameof(HasChanges));
                return;
            }
            var front = _graphics.ComposeCardFront(SelectedRankIndex);
            var back = _graphics.ComposeCardBack(SelectedRankIndex);
            CardFrontPreview = ImageConverter.ToAvaloniaBitmap(front);
            CardBackPreview = ImageConverter.ToAvaloniaBitmap(back);
            StatusText = (front == null || back == null) ? "Could not decode the current card design." : string.Empty;
            OnPropertyChanged(nameof(HasChanges));
        }

        private void RefreshTrainerPreviews()
        {
            if (!GraphicsAvailable) { TrainerMalePreview = TrainerFemalePreview = null; return; }
            TrainerMalePreview = ImageConverter.ToAvaloniaBitmap(_graphics.ComposeTrainer(male: true));
            TrainerFemalePreview = ImageConverter.ToAvaloniaBitmap(_graphics.ComposeTrainer(male: false));
        }

        private void RefreshAll()
        {
            RefreshCardPreviews();
            RefreshTrainerPreviews();
        }

        public string ImportCardFront(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportCardFront(raw);
            if (error == null) RefreshAll(); // rebuilds all 7 rank palettes too
            return error;
        }

        public string ImportCardBack(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportCardBack(raw);
            if (error == null) RefreshAll();
            return error;
        }

        public string ImportTrainerMale(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportTrainerMale(raw);
            if (error == null) RefreshAll(); // may also recolor the Normal rank's card
            return error;
        }

        public string ImportTrainerFemale(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportTrainerFemale(raw);
            if (error == null) RefreshAll();
            return error;
        }

        public string ExportCardFront(string pngPath) => SavePng(_graphics.ComposeCardFront(SelectedRankIndex), pngPath);
        public string ExportCardBack(string pngPath) => SavePng(_graphics.ComposeCardBack(SelectedRankIndex), pngPath);
        public string ExportTrainerMale(string pngPath) => SavePng(_graphics.ComposeTrainer(male: true), pngPath);
        public string ExportTrainerFemale(string pngPath) => SavePng(_graphics.ComposeTrainer(male: false), pngPath);

        public string ImportRankPalette(string nclrPath)
        {
            byte[] bytes;
            try { bytes = System.IO.File.ReadAllBytes(nclrPath); }
            catch (Exception ex) { return ex.Message; }
            string error = _graphics.ImportRankPaletteRaw(SelectedRankIndex, bytes);
            if (error == null) RefreshAll();
            return error;
        }

        public string ExportRankPalette(string nclrPath)
        {
            byte[] bytes = _graphics.ExportRankPaletteRaw(SelectedRankIndex);
            if (bytes == null) return "Could not read the current palette.";
            try { System.IO.File.WriteAllBytes(nclrPath, bytes); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        public void RevertChanges()
        {
            _graphics.RevertAll();
            RefreshAll();
        }

        private static DSPRE.RawImage DecodePng(string path, out string error)
        {
            error = null;
            try
            {
                using var stream = System.IO.File.OpenRead(path);
                var raw = ImageConverter.DecodeRawImage(stream);
                if (raw == null) error = "Could not read this PNG.";
                return raw;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        private static string SavePng(DSPRE.RawImage raw, string path)
        {
            if (raw == null) return "Could not decode this image.";
            try { ImageConverter.ToAvaloniaBitmap(raw).Save(path); return null; }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
