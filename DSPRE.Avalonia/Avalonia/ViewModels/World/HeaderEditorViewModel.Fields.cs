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

        private string _internalName = "";
        public string InternalName
        {
            get => _internalName;
            set { if (Set(ref _internalName, value)) { UpdateInternalNameFeedback(); SetDirty(); } }
        }

        private IBrush _internalNameColor = StatusBrushes.Good;
        public IBrush InternalNameColor { get => _internalNameColor; set => Set(ref _internalNameColor, value); }
        private string _internalNameLen = "[ 0 ]";
        public string InternalNameLen { get => _internalNameLen; set => Set(ref _internalNameLen, value); }

        private decimal _matrixId; public decimal MatrixId { get => _matrixId; set { if (Set(ref _matrixId, value)) Apply(h => h.MatrixID = (ushort)value); } }
        private decimal _areaDataId; public decimal AreaDataId { get => _areaDataId; set { if (Set(ref _areaDataId, value)) { Apply(h => h.AreaDataID = (byte)value); UpdateWeatherRoom(); } } }
        private decimal _scriptFileId; public decimal ScriptFileId { get => _scriptFileId; set { if (Set(ref _scriptFileId, value)) Apply(h => h.ScriptFileID = (ushort)value); } }
        private decimal _levelScriptId; public decimal LevelScriptId { get => _levelScriptId; set { if (Set(ref _levelScriptId, value)) Apply(h => h.LevelScriptID = (ushort)value); } }
        private decimal _eventFileId; public decimal EventFileId { get => _eventFileId; set { if (Set(ref _eventFileId, value)) Apply(h => h.EventFileID = (ushort)value); } }
        private decimal _textArchiveId; public decimal TextArchiveId { get => _textArchiveId; set { if (Set(ref _textArchiveId, value)) Apply(h => h.TextArchiveID = (ushort)value); } }
        private decimal _wildPokemon; public decimal WildPokemon { get => _wildPokemon; set { if (Set(ref _wildPokemon, value)) { Apply(h => h.WildPokemon = (ushort)value); OnPropertyChanged(nameof(CanOpenEncounters)); } } }
        private decimal _battleBackground; public decimal BattleBackground { get => _battleBackground; set { if (Set(ref _battleBackground, value)) Apply(h => h.BattleBackground = (byte)value); } }

        // The no-encounter value (0xffff DPPt, 0xff HGSS) would open file 0, so Open is disabled.
        private int NullEncounterId => IsHgss ? MapHeader.HgssNullEncounterFileId : MapHeader.DpptNullEncounterFileId;
        public bool CanOpenEncounters => _header != null && (int)_wildPokemon != NullEncounterId;
        public int NoEncountersId => NullEncounterId;

        private decimal _cameraValue;
        public decimal CameraValue
        {
            get => _cameraValue;
            set { if (Set(ref _cameraValue, value)) { Apply(h => h.CameraAngleID = (byte)value); SyncCameraCombo(); } }
        }
        private int _cameraComboIndex = -1;
        public int CameraComboIndex
        {
            get => _cameraComboIndex;
            set { if (Set(ref _cameraComboIndex, value) && !_suppress && value >= 0) CameraValue = Camera.KeyAt(value); }
        }

        private decimal _weatherValue;
        public decimal WeatherValue
        {
            get => _weatherValue;
            set { if (Set(ref _weatherValue, value)) { Apply(h => h.WeatherID = (byte)value); SyncWeatherCombo(); UpdateWeatherRoom(); } }
        }
        private int _weatherComboIndex = -1;
        public int WeatherComboIndex
        {
            get => _weatherComboIndex;
            set { if (Set(ref _weatherComboIndex, value) && !_suppress && value >= 0) WeatherValue = Weather.KeyAt(value); }
        }

        public bool WeatherRoomChecked => RomInfo.GameFamily == GameFamilies.Plat;

        private string _weatherRoomWarning;
        /// <summary>Set when the weather's background may not fit next to the area data.</summary>
        public string WeatherRoomWarning { get => _weatherRoomWarning; private set { if (Set(ref _weatherRoomWarning, value)) OnPropertyChanged(nameof(HasWeatherRoomWarning)); } }
        public bool HasWeatherRoomWarning => _weatherRoomWarning != null;

        private string _weatherRoomDetail;
        public string WeatherRoomDetail { get => _weatherRoomDetail; private set => Set(ref _weatherRoomDetail, value); }

        private void UpdateWeatherRoom()
        {
            OnPropertyChanged(nameof(WeatherRoomChecked));
            FieldWeatherRoom.Result room = FieldWeatherRoom.Check((int)_weatherValue, (int)_areaDataId);
            WeatherRoomDetail = room == null ? null
                : $"Background needs {room.Needed:N0} bytes. Area {(int)_areaDataId} leaves about {Math.Max(0, room.Free):N0}.";
            WeatherRoomWarning = room == null || room.Fits ? null : "May black-screen on door, Fly or save loads";
        }

        private decimal _musicDayValue;
        public decimal MusicDayValue
        {
            get => _musicDayValue;
            set { if (Set(ref _musicDayValue, value)) { MusicPreview.StopIfStartedBy((this, false)); Apply(h => h.MusicDayID = (ushort)value); SyncMusicCombo(MusicDay, (int)value, i => _musicDayComboIndex = i, nameof(MusicDayComboIndex)); OnPropertyChanged(nameof(DayPlayIcon)); } }
        }
        private int _musicDayComboIndex = -1;
        public int MusicDayComboIndex
        {
            get => _musicDayComboIndex;
            set { if (Set(ref _musicDayComboIndex, value) && !_suppress && value >= 0) MusicDayValue = MusicDay.KeyAt(value); }
        }

        private decimal _musicNightValue;
        public decimal MusicNightValue
        {
            get => _musicNightValue;
            set { if (Set(ref _musicNightValue, value)) { MusicPreview.StopIfStartedBy((this, true)); Apply(h => h.MusicNightID = (ushort)value); SyncMusicCombo(MusicNight, (int)value, i => _musicNightComboIndex = i, nameof(MusicNightComboIndex)); OnPropertyChanged(nameof(NightPlayIcon)); } }
        }
        private int _musicNightComboIndex = -1;
        public int MusicNightComboIndex
        {
            get => _musicNightComboIndex;
            set { if (Set(ref _musicNightComboIndex, value) && !_suppress && value >= 0) MusicNightValue = MusicNight.KeyAt(value); }
        }

        private int _locationNameIndex = -1;
        public int LocationNameIndex
        {
            get => _locationNameIndex;
            set { if (Set(ref _locationNameIndex, value)) { OnPropertyChanged(nameof(SelectedHeaderTitle)); if (!_suppress && value >= 0) ApplyLocationName(value); } }
        }

        private int _areaSettingsIndex = -1;
        public int AreaSettingsIndex
        {
            get => _areaSettingsIndex;
            set { if (Set(ref _areaSettingsIndex, value) && !_suppress && value >= 0) ApplyAreaSettings(value); }
        }

        private int _areaIconIndex = -1;
        public int AreaIconIndex
        {
            get => _areaIconIndex;
            set { if (Set(ref _areaIconIndex, value) && !_suppress && value >= 0) ApplyAreaIcon(value); }
        }
        private Bitmap _areaIconImage; public Bitmap AreaIconImage { get => _areaIconImage; set => Set(ref _areaIconImage, value); }

        private bool _f0, _f1, _f2, _f3, _f4, _f5, _f6;
        public bool Flag0 { get => _f0; set { if (Set(ref _f0, value)) ApplyFlags(); } }
        public bool Flag1 { get => _f1; set { if (Set(ref _f1, value)) ApplyFlags(); } }
        public bool Flag2 { get => _f2; set { if (Set(ref _f2, value)) ApplyFlags(); } }
        public bool Flag3 { get => _f3; set { if (Set(ref _f3, value)) ApplyFlags(); } }
        public bool Flag4 { get => _f4; set { if (Set(ref _f4, value)) ApplyFlags(); } }
        public bool Flag5 { get => _f5; set { if (Set(ref _f5, value)) ApplyFlags(); } }
        public bool Flag6 { get => _f6; set { if (Set(ref _f6, value)) ApplyFlags(); } }

        private decimal _worldmapX; public decimal WorldmapX { get => _worldmapX; set { if (Set(ref _worldmapX, value)) ApplyHgss(h => h.WorldmapX = (byte)value); } }
        private decimal _worldmapY; public decimal WorldmapY { get => _worldmapY; set { if (Set(ref _worldmapY, value)) ApplyHgss(h => h.WorldmapY = (byte)value); } }
        private decimal _momCallIntroParam; public decimal MomCallIntroParam { get => _momCallIntroParam; set { if (Set(ref _momCallIntroParam, value)) ApplyHgss(h => h.MomCallIntroParam = (byte)value); } }
        private int _followModeIndex = -1;
        public int FollowModeIndex { get => _followModeIndex; set { if (Set(ref _followModeIndex, value)) ApplyHgss(h => h.FollowMode = (byte)Math.Max(0, value)); } }
        private bool _kantoFlag;
        public bool KantoFlag { get => _kantoFlag; set { if (Set(ref _kantoFlag, value)) { ApplyHgss(h => h.KantoFlag = value); OnPropertyChanged(nameof(JohtoFlag)); } } }
        public bool JohtoFlag => !_kantoFlag;
    }
}
