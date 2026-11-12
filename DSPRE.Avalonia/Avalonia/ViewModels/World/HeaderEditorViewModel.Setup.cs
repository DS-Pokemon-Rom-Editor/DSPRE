using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class HeaderEditorViewModel
    {
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                // No ROM yet: the Maps workspace initializes with the main window.
                StatusText = "No ROM loaded.";
                return;
            }
            StatusText = "Loading headers…";
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay, DirNames.TextArchives, DirNames.DynamicHeaders });

                _dynamicHeaders = MapHeader.UsesDynamicHeaders;
                CanAddRemove = _dynamicHeaders;
                OnPropertyChanged(nameof(CanAddRemove));
                OnPropertyChanged(nameof(OffersHeadersPatch));
                OnPropertyChanged(nameof(AddTip));
                OnPropertyChanged(nameof(RemoveTip));

                _headerListNames = HeaderLists.GetHeaderListBoxNames();
                _internalNames = HeaderLists.GetInternalNames();

                BuildFamilyCombos();
                LoadLocationNames();

                LoadLocationIndices();
                SelectedHeaderId = FindInitialHeaderId();   // so only this header's folder opens on load
                RebuildTree();

                StatusText = $"{_headerListNames.Count} headers.";
                SelectHeader(SelectedHeaderId);
            }
            catch (FileNotFoundException)
            {
                await DialogHelper.ShowErrorAsync(InternalNamesPath + " doesn't exist.", "Couldn't read internal names");
            }
            catch (Exception ex) when (AppLogger.Noted("HeaderEditorViewModel.SetupAsync", ex))
            {
                AppLogger.Error("HeaderEditorViewModel.SetupAsync: " + ex);
                await DialogHelper.ShowErrorAsync($"Couldn't load headers:\n{ex.Message}", "Header editor");
            }
        }

        private void BuildFamilyCombos()
        {
            AreaSettingsItems.Clear();
            AreaIconItems.Clear();
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    Camera.LoadLabels(LabelStore.CameraKey);
                    MusicDay.Load(PokeDatabase.MusicDB.DPMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.DPMusicDict);
                    Weather.LoadLabels(LabelStore.WeatherKey);
                    foreach (string s in PokeDatabase.MapType.DPPtValues) AreaSettingsItems.Add(s);
                    ShowAreaIcon = false;
                    WildPokeMax = 65535;
                    break;
                case GameFamilies.Plat:
                    Camera.LoadLabels(LabelStore.CameraKey);
                    MusicDay.Load(PokeDatabase.MusicDB.PtMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.PtMusicDict);
                    Weather.LoadLabels(LabelStore.WeatherKey);
                    foreach (string s in PokeDatabase.MapType.DPPtValues) AreaSettingsItems.Add(s);
                    foreach (string s in PokeDatabase.Area.PtAreaIconValues) AreaIconItems.Add(s);
                    ShowAreaIcon = true;
                    WildPokeMax = 65535;
                    break;
                default:
                    Camera.LoadLabels(LabelStore.CameraKey);
                    MusicDay.Load(PokeDatabase.MusicDB.HGSSMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.HGSSMusicDict);
                    Weather.LoadLabels(LabelStore.WeatherKey);
                    foreach (string s in PokeDatabase.Area.HGSSAreaProperties) AreaSettingsItems.Add(s);
                    foreach (string s in PokeDatabase.Area.HGSSAreaIconsDict.Values) AreaIconItems.Add(s);
                    ShowAreaIcon = true;
                    WildPokeMax = 255;
                    break;
            }
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.LabelsChanged += OnLabelsChanged;
            OnPropertyChanged(nameof(ShowAreaIcon));
            OnPropertyChanged(nameof(WildPokeMax));
            OnPropertyChanged(nameof(NoEncountersId));
            OnPropertyChanged(nameof(BattleBackgroundMax)); OnPropertyChanged(nameof(BattleBackgroundNames));
            OnPropertyChanged(nameof(WeatherMax));
            OnPropertyChanged(nameof(CameraMax));
        }

        private void LoadLocationNames()
        {
            LocationNames.Clear();
            foreach (string m in ReadLocationNames()) LocationNames.Add(m);
        }

        private List<string> ReadLocationNames()
        {
            try { return new TextArchive(LocationNamesTextNumber).Messages.ToList(); }
            catch (Exception ex) { AppLogger.WarnOnce("HeaderEditorViewModel.ReadLocationNames", ex); return new List<string>(); }
        }
    }
}
