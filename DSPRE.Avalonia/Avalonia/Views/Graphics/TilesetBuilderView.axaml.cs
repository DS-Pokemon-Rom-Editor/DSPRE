using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class TilesetBuilderView : Window
    {
        private TilesetBuilderViewModel VM => DataContext as TilesetBuilderViewModel;

        private static readonly IReadOnlyList<FilePickerFileType> PngOnly =
            new[] { new FilePickerFileType("PNG pictures") { Patterns = new[] { "*.png" } } };

        // The three files are written together under one name, so the picker asks for the first of them.
        private static readonly IReadOnlyList<FilePickerFileType> ColourList =
            new[] { new FilePickerFileType("Background files") { Patterns = new[] { "*.NCLR" } } };

        public TilesetBuilderView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new TilesetBuilderViewModel();
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            string path = await DialogHelper.OpenFile(this, "Pick a PNG to make a background from", PngOnly);
            if (path == null) return;
            string why = VM?.Open(path);
            if (why != null) await DialogHelper.ShowError(why, "That picture could not be opened", this);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !VM.CanSave) return;
            try
            {
                string suggested = System.IO.Path.GetFileNameWithoutExtension(VM.PictureName) + ".NCLR";
                string path = await DialogHelper.SaveFile(this, "Name the three files", ColourList, suggested);
                if (path == null) return;
                // The picker only asks about the .NCLR; the two files written beside it get the same question.
                string stem = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? "", System.IO.Path.GetFileNameWithoutExtension(path));
                List<string> there = new List<string>();
                foreach (string ext in new[] { ".NCGR", ".NSCR" }) if (System.IO.File.Exists(stem + ext)) there.Add(System.IO.Path.GetFileName(stem + ext));
                if (there.Count > 0 && !await DialogHelper.AskYesNo(string.Join(" and ", there) + (there.Count == 1 ? " already exists beside it. Replace it?" : " already exist beside it. Replace them?"), "Replace files", this)) return;
                string why = VM.Save(path);
                if (why != null) await DialogHelper.ShowError(why, "Those files could not be written", this);
                else await DialogHelper.ShowInfo(VM.SavedAs, "Written");
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("TilesetBuilder save failed: " + ex);
                await DialogHelper.ShowError("Those files could not be written: " + ex.Message,
                                             "Those files could not be written", this);
            }
        }
    }
}
