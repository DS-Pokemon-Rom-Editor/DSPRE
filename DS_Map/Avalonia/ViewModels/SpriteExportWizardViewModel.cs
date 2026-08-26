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
    public class ExportPickItem
    {
        public string Label { get; set; }
        public string FileName { get; set; }
        public bool IsSelected { get; set; }
        public Func<RawImage> Compose { get; set; }
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
        }

        private ExportPickItem MakeSpriteItem(string pose, int slot, bool shiny) => new ExportPickItem
        {
            Label = pose + (shiny ? " (Shiny)" : ""),
            FileName = $"mon{_sprite.CurrentId:D3}_{pose.Replace(" ", "")}{(shiny ? "Shiny" : "")}.png",
            Compose = () => _sprite.ComposeSpriteRaw(slot, shiny)
        };

        private ExportPickItem MakeSheetItem(string genderLabel, bool female, bool shiny) => new ExportPickItem
        {
            Label = $"{genderLabel} Sheet" + (shiny ? " (Shiny)" : ""),
            FileName = $"mon{_sprite.CurrentId:D3}_{genderLabel}_{(shiny ? "Shiny" : "Normal")}_sheet.png",
            Compose = () => _sprite.ComposeSheetRaw(female, shiny)
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
                        var raw = item.Compose();
                        if (raw == null) continue;
                        var entry = zip.CreateEntry(item.FileName);
                        using (var entryStream = entry.Open())
                            ImageConverter.ToAvaloniaBitmap(raw).Save(entryStream, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
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
