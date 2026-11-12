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
        private void Apply(Action<MapHeader> set)
        {
            if (_header == null) return;
            set(_header);
            SetDirty();
        }
        private void ApplyHgss(Action<HeaderHGSS> set)
        {
            if (_header is HeaderHGSS h) { set(h); SetDirty(); }
        }

        private void ApplyLocationName(int index)
        {
            switch (GameFamily)
            {
                case GameFamilies.DP: Apply(h => ((HeaderDP)h).LocationName = (ushort)index); break;
                case GameFamilies.Plat: Apply(h => ((HeaderPt)h).LocationName = (byte)index); break;
                default: Apply(h => ((HeaderHGSS)h).LocationName = (byte)index); break;
            }
            if (_header != null) UpdateHeaderLocationInTree(_header.ID, index);
        }

        private void ApplyAreaSettings(int index)
        {
            if (_header == null) return;
            if (GameFamily == GameFamilies.HGSS)
            {
                ((HeaderHGSS)_header).LocationType = (byte)index;
                SetDirty();
            }
            else
            {
                string s = index >= 0 && index < AreaSettingsItems.Count ? AreaSettingsItems[index] : null;
                if (s != null && s.Length >= 4 && byte.TryParse(s.Substring(1, 3), out byte spec))
                {
                    _header.LocationSpecifier = spec;
                    SetDirty();
                }
            }
        }

        private void ApplyAreaIcon(int index)
        {
            switch (GameFamily)
            {
                case GameFamilies.DP: break;
                case GameFamilies.Plat: Apply(h => ((HeaderPt)h).AreaIcon = (byte)index); break;
                default: Apply(h => ((HeaderHGSS)h).AreaIcon = (byte)index); break;
            }
            UpdateAreaIconImage();
        }

        private void ApplyFlags()
        {
            if (_header == null) return;
            byte v = 0;
            if (_f0) v |= 1 << 0;
            if (_f1) v |= 1 << 1;
            if (_f2) v |= 1 << 2;
            if (_f3) v |= 1 << 3;
            if (IsHgss)
            {
                if (_f4) v |= 1 << 4;
                if (_f5) v |= 1 << 5;
                if (_f6) v |= 1 << 6;
            }
            _header.Flags = v;
            SetDirty();
        }

        private void LoadFlags()
        {
            byte v = _header.Flags;
            Flag0 = (v & (1 << 0)) != 0;
            Flag1 = (v & (1 << 1)) != 0;
            Flag2 = (v & (1 << 2)) != 0;
            Flag3 = (v & (1 << 3)) != 0;
            Flag4 = (v & (1 << 4)) != 0;
            Flag5 = (v & (1 << 5)) != 0;
            Flag6 = (v & (1 << 6)) != 0;
        }

        // Closed combos are poked a frame later, or a relabelled selected item shows stale text.
        private void OnLabelsChanged(object sender, EventArgs e)
        {
            Camera.LoadLabels(LabelStore.CameraKey);
            Weather.LoadLabels(LabelStore.WeatherKey);
            _cameraComboIndex = -1; OnPropertyChanged(nameof(CameraComboIndex));
            _weatherComboIndex = -1; OnPropertyChanged(nameof(WeatherComboIndex));
            Dispatcher.UIThread.Post(() => { SyncCameraCombo(); SyncWeatherCombo(); }, DispatcherPriority.Background);
        }

        private void SyncCameraCombo() { _cameraComboIndex = Camera.IndexOf((int)_cameraValue); OnPropertyChanged(nameof(CameraComboIndex)); }
        private void SyncWeatherCombo() { _weatherComboIndex = Weather.IndexOf((int)_weatherValue); OnPropertyChanged(nameof(WeatherComboIndex)); }
        private void SyncMusicCombo(MappedCombo combo, int value, Action<int> setBacking, string propName)
        { setBacking(combo.IndexOf(value)); OnPropertyChanged(propName); }

        private void UpdateAreaIconImage()
        {
            string name = null;
            switch (GameFamily)
            {
                case GameFamilies.DP: name = "dpareaicon"; break;
                case GameFamilies.Plat: if (_areaIconIndex >= 0) name = "areaicon0" + _areaIconIndex; break;
                default:
                    if (_areaIconIndex >= 0 && PokeDatabase.System.AreaPics.HgssAreaPicDict.TryGetValue(_areaIconIndex, out string n)) name = n;
                    break;
            }
            AreaIconImage = name != null ? ResImage(name) : null;
        }

        private static Bitmap ResImage(string name) => ResourceImages.GetBitmap(name);

        private void UpdateInternalNameFeedback()
        {
            int len = _internalName?.Length ?? 0;
            InternalNameColor = len > 13 ? StatusBrushes.Bad : len > 7 ? StatusBrushes.Warn : StatusBrushes.Good;
            InternalNameLen = $"[ {len} ]";
        }
    }
}
