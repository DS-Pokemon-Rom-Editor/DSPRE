using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.ViewModels
{
    public class ExportPickItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Label { get; set; }
        public string FileName { get; set; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public int Width { get; set; }
        public int Height { get; set; }
        public Func<byte[]> GetIndices { get; set; }
        public Func<uint[]> GetPalette { get; set; }
    }

    /// <summary>
    /// Pick any mix of individual sprites and gender sprite sheets, export them all as one zip.
    /// File names inside the zip match what the individual Export buttons already produce.
    /// </summary>
    public class SpriteExportWizardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly PokemonSpriteEditorViewModel _sprite;
        private readonly Window _owner;

        public ObservableCollection<ExportPickItem> IndividualSprites { get; } = new ObservableCollection<ExportPickItem>();
        public ObservableCollection<ExportPickItem> SpriteSheets { get; } = new ObservableCollection<ExportPickItem>();

        private string _statusText;
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }

        public SpriteExportWizardViewModel(PokemonSpriteEditorViewModel sprite, Window owner)
        {
            _sprite = sprite;
            _owner = owner;

            for (int slot = 0; slot < 4; slot++)
            {
                if (!sprite.HasSpriteSlot(slot)) continue;
                string pose = sprite.SpritePoseLabel(slot);
                if (sprite.HasPalette(false))
                    IndividualSprites.Add(MakeSpriteItem(pose, slot, false));
                if (sprite.HasPalette(true))
                    IndividualSprites.Add(MakeSpriteItem(pose, slot, true));
            }

            foreach (bool female in new[] { true, false })
            {
                string genderLabel = female ? "Female" : "Male";
                if (sprite.HasPalette(false))
                    SpriteSheets.Add(MakeSheetItem(genderLabel, female, false));
                if (sprite.HasPalette(true))
                    SpriteSheets.Add(MakeSheetItem(genderLabel, female, true));
            }

            if (sprite.CanUseFullSheet)
            {
                if (sprite.HasPalette(false)) SpriteSheets.Add(MakeFullSheetItem(false));
                if (sprite.HasPalette(true)) SpriteSheets.Add(MakeFullSheetItem(true));
            }
        }

        private ExportPickItem MakeSpriteItem(string pose, int slot, bool shiny) => new ExportPickItem
        {
            Label = pose + (shiny ? " (Shiny)" : ""),
            FileName = $"mon{_sprite.CurrentId:D3}_{pose.Replace(" ", "")}{(shiny ? "Shiny" : "")}.png",
            Width = _sprite.SpritePixelWidth,
            Height = _sprite.SpritePixelHeight,
            GetIndices = () => _sprite.GetRawSpriteIndices(slot),
            GetPalette = () => _sprite.GetPalette(shiny)
        };

        private ExportPickItem MakeFullSheetItem(bool shiny) => new ExportPickItem
        {
            Label = "Both Genders Sheet" + (shiny ? " (Shiny)" : ""),
            FileName = $"mon{_sprite.CurrentId:D3}_Both_{(shiny ? "Shiny" : "Normal")}_sheet.png",
            Width = _sprite.SpritePixelWidth * 4,
            Height = _sprite.SpritePixelHeight,
            GetIndices = () => _sprite.GetRawFullSheetIndices(),
            GetPalette = () => _sprite.GetPalette(shiny)
        };

        private ExportPickItem MakeSheetItem(string genderLabel, bool female, bool shiny) => new ExportPickItem
        {
            Label = $"{genderLabel} Sheet" + (shiny ? " (Shiny)" : ""),
            FileName = $"mon{_sprite.CurrentId:D3}_{genderLabel}_{(shiny ? "Shiny" : "Normal")}_sheet.png",
            Width = _sprite.SpritePixelWidth * 2,
            Height = _sprite.SpritePixelHeight,
            GetIndices = () => _sprite.GetRawSheetIndices(female),
            GetPalette = () => _sprite.GetPalette(shiny)
        };

        public void SelectAll(bool selected)
        {
            foreach (var i in IndividualSprites) i.IsSelected = selected;
            foreach (var i in SpriteSheets) i.IsSelected = selected;
        }

        public async Task RunAsync()
        {
            var selected = IndividualSprites.Concat(SpriteSheets).Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) { StatusText = "Nothing selected."; return; }

            string path = await DialogHelper.SaveFile(_owner, "Export Selected as ZIP",
                new[] { DialogHelper.ZipFilter, DialogHelper.AllFilter },
                $"mon{_sprite.CurrentId:D3}_sprites.zip");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                using (var zipStream = File.Create(path))
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    foreach (var item in selected)
                    {
                        byte[] indices = item.GetIndices();
                        uint[] palette = item.GetPalette();
                        if (indices == null || palette == null) continue;
                        byte[] png = IndexedPng.Write(indices, palette, item.Width, item.Height);
                        var entry = zip.CreateEntry(item.FileName);
                        using (var entryStream = entry.Open())
                            entryStream.Write(png, 0, png.Length);
                    }
                }
                StatusText = $"Exported {selected.Count} file(s) to {Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Export failed: {ex.Message}";
            }
        }
    }
}
