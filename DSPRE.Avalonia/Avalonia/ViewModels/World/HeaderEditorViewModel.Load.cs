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
        /// <summary>Plays the day or night music, or stops it when it is already playing.</summary>
        public void PlayMusic(bool night) => MusicPreview.Toggle((int)(night ? MusicNightValue : MusicDayValue), (this, night));

        private void LoadHeader(ushort headerId)
        {
            MusicPreview.Stop();   // the music belonged to the header being left
            if (headerId >= _headerListNames.Count) return;

            _header = MapHeader.GetMapHeader(headerId);
            if (_header == null) return;

            PopulateFromHeader();
            SetClean();
            _history.Reset(UndoState());
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
            StatusText = "";
            OnPropertyChanged(nameof(UnsavedChangesDescription));
            OnPropertyChanged(nameof(CurrentHeaderId));
            UpdateHeaderLocationInTree(headerId, CurrentHeaderLocationIndex());   // correct folder after reset/paste/import
        }

        private void PopulateFromHeader()
        {
            if (_header == null) return;
            _suppress = true;
            try
            {
                InternalName = _header.ID < _internalNames.Count ? _internalNames[_header.ID] : "";
                MatrixId = _header.MatrixID;
                AreaDataId = _header.AreaDataID;
                ScriptFileId = _header.ScriptFileID;
                LevelScriptId = _header.LevelScriptID;
                EventFileId = _header.EventFileID;
                TextArchiveId = _header.TextArchiveID;
                WildPokemon = _header.WildPokemon;
                BattleBackground = _header.BattleBackground;
                CameraValue = _header.CameraAngleID;
                WeatherValue = _header.WeatherID;
                UpdateWeatherRoom();
                MusicDayValue = _header.MusicDayID;
                MusicNightValue = _header.MusicNightID;

                switch (GameFamily)
                {
                    case GameFamilies.DP:
                        LocationNameIndex = ((HeaderDP)_header).LocationName;
                        AreaSettingsIndex = FindAreaSettingsBySpecifier(_header.LocationSpecifier);
                        break;
                    case GameFamilies.Plat:
                        LocationNameIndex = ((HeaderPt)_header).LocationName;
                        AreaIconIndex = ((HeaderPt)_header).AreaIcon;
                        AreaSettingsIndex = FindAreaSettingsBySpecifier(_header.LocationSpecifier);
                        break;
                    default:
                        HeaderHGSS h = (HeaderHGSS)_header;
                        LocationNameIndex = h.LocationName;
                        AreaIconIndex = h.AreaIcon;
                        AreaSettingsIndex = h.LocationType;
                        WorldmapX = h.WorldmapX;
                        WorldmapY = h.WorldmapY;
                        MomCallIntroParam = h.MomCallIntroParam;
                        FollowModeIndex = h.FollowMode;
                        KantoFlag = h.KantoFlag;
                        break;
                }

                // IsHgss and the rest read a static, so they are raised here or the HGSS-only fields never show.
                OnPropertyChanged(nameof(IsHgss));
                OnPropertyChanged(nameof(IsDp));
                OnPropertyChanged(nameof(ShowHgssOnly));

                LoadFlags();
                SyncCameraCombo();
                SyncWeatherCombo();
                // Refilled combos drop their selection, and an unchanged value skips the setter's sync.
                SyncMusicCombo(MusicDay, (int)_musicDayValue, i => _musicDayComboIndex = i, nameof(MusicDayComboIndex));
                SyncMusicCombo(MusicNight, (int)_musicNightValue, i => _musicNightComboIndex = i, nameof(MusicNightComboIndex));
                UpdateAreaIconImage();
            }
            finally { _suppress = false; }
            OnPropertyChanged(nameof(CanOpenEncounters));
            OnPropertyChanged(nameof(SelectedHeaderTitle));
            OnPropertyChanged(nameof(SelectedHeaderSubtitle));
        }

        private int FindAreaSettingsBySpecifier(int specifier)
        {
            for (int i = 0; i < AreaSettingsItems.Count; i++)
            {
                string s = AreaSettingsItems[i];
                if (s.Length >= 4 && s[0] == '[' && int.TryParse(s.Substring(1, 3), out int n) && n == specifier)
                    return i;
            }
            return -1;
        }
    }
}
