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
        // Kept for the session.
        private static readonly Dictionary<string, object[]> HeldFields = new Dictionary<string, object[]>();

        private (System.Func<object[]> Get, System.Action<object[]> Set)? FieldAccess(string key) => key switch
        {
            "InternalName" => (() => new object[] { InternalName }, v => InternalName = (string)v[0]),
            "LocationName" => (() => new object[] { LocationNameIndex }, v => LocationNameIndex = (int)v[0]),
            "MapType" => (() => new object[] { AreaSettingsIndex }, v => AreaSettingsIndex = (int)v[0]),
            "AreaIcon" => (() => new object[] { AreaIconIndex }, v => AreaIconIndex = (int)v[0]),
            "MusicDay" => (() => new object[] { MusicDayValue }, v => MusicDayValue = (decimal)v[0]),
            "MusicNight" => (() => new object[] { MusicNightValue }, v => MusicNightValue = (decimal)v[0]),
            "Weather" => (() => new object[] { WeatherValue }, v => WeatherValue = (decimal)v[0]),
            "Camera" => (() => new object[] { CameraValue }, v => CameraValue = (decimal)v[0]),
            "BattleBackground" => (() => new object[] { BattleBackground }, v => BattleBackground = (decimal)v[0]),
            "Matrix" => (() => new object[] { MatrixId }, v => MatrixId = (decimal)v[0]),
            "AreaData" => (() => new object[] { AreaDataId }, v => AreaDataId = (decimal)v[0]),
            "ScriptFile" => (() => new object[] { ScriptFileId }, v => ScriptFileId = (decimal)v[0]),
            "LevelScript" => (() => new object[] { LevelScriptId }, v => LevelScriptId = (decimal)v[0]),
            "EventFile" => (() => new object[] { EventFileId }, v => EventFileId = (decimal)v[0]),
            "TextArchive" => (() => new object[] { TextArchiveId }, v => TextArchiveId = (decimal)v[0]),
            "Encounters" => (() => new object[] { WildPokemon }, v => WildPokemon = (decimal)v[0]),
            "MapSettings" => (() => new object[] { Flag0, Flag1, Flag2, Flag3, Flag4, Flag5, Flag6 },
                              v => { Flag0 = (bool)v[0]; Flag1 = (bool)v[1]; Flag2 = (bool)v[2]; Flag3 = (bool)v[3];
                                     Flag4 = (bool)v[4]; Flag5 = (bool)v[5]; Flag6 = (bool)v[6]; }),
            "TownMap" => (() => new object[] { WorldmapX, WorldmapY }, v => { WorldmapX = (decimal)v[0]; WorldmapY = (decimal)v[1]; }),
            "FollowMode" => (() => new object[] { FollowModeIndex }, v => FollowModeIndex = (int)v[0]),
            "MomCall" => (() => new object[] { MomCallIntroParam }, v => MomCallIntroParam = (decimal)v[0]),
            "Region" => (() => new object[] { KantoFlag }, v => KantoFlag = (bool)v[0]),
            _ => null,
        };

        public bool CanCopyField(string key) => _header != null && FieldAccess(key) != null;

        public void CopyField(string key)
        {
            if (!CanCopyField(key)) return;
            HeldFields[key] = FieldAccess(key).Value.Get();
            StatusText = "Copied " + (HeldField(key) ?? key) + ".";
        }

        public void PasteField(string key)
        {
            if (!CanCopyField(key) || !HeldFields.TryGetValue(key, out object[] held)) return;
            FieldAccess(key).Value.Set(held);
            StatusText = "Pasted " + HeldField(key) + ".";
        }

        /// <summary>Copied value as the menu shows it, null if empty.</summary>
        public string HeldField(string key)
        {
            if (!HeldFields.TryGetValue(key, out object[] held)) return null;
            return key switch
            {
                "MapSettings" => "map settings",
                "TownMap" => $"{held[0]}, {held[1]}",
                "Region" => (bool)held[0] ? "Kanto" : "Johto",
                "InternalName" => (string)held[0],
                _ => System.Convert.ToString(held[0], System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        // Kept across ROM loads, so it carries the family whose layout its bytes are in.
        private static (byte[] Bytes, GameFamilies Family) _clipboard;

        public void Copy()
        {
            if (_header == null) return;
            _clipboard = (_header.ToByteArray(), GameFamily);
            StatusText = $"Copied header {_header.ID}.";
        }

        public void Paste()
        {
            if (_header == null || _clipboard.Bytes == null) { StatusText = "Nothing to paste."; return; }
            if (_clipboard.Family != GameFamily) { StatusText = $"The copied header is from {(_clipboard.Family == GameFamilies.HGSS ? "HeartGold or SoulSilver" : _clipboard.Family == GameFamilies.Plat ? "Platinum" : "Diamond or Pearl")} and can't be pasted here."; return; }
            MapHeader h = MapHeader.LoadFromByteArray(_clipboard.Bytes, (ushort)_header.ID, GameFamily);
            if (h == null) { StatusText = "Clipboard header is incompatible."; return; }
            _header = h;
            PopulateFromHeader();
            SetDirty();
            StatusText = "Pasted the header.";
        }

        public void Reset()
        {
            LoadHeader(SelectedHeaderId);
            StatusText = "Reverted to saved header.";
        }

        public async Task ImportAsync()
        {
            if (_header == null) return;
            FilePickerFileType filter = new global::Avalonia.Platform.Storage.FilePickerFileType("DSPRE header")
            { Patterns = new[] { "*.dsh", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import header", new[] { filter });
            if (path == null) return;
            try
            {
                if (PlainFiles.Length(path) > 48) throw new InvalidDataException();
                MapHeader h = MapHeader.LoadFromFile(path, (ushort)_header.ID, 0);
                if (h == null) throw new InvalidDataException();
                _header = h;
                PopulateFromHeader();
                SetDirty();
                SaveNotice.Imported($"header {_header.ID}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("HeaderEditorViewModel.ImportAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import: this isn't a header file, or it's damaged.\n{ex.Message}", "Header editor"); }
        }

        public async Task ExportAsync()
        {
            if (_header == null) return;
            FilePickerFileType filter = new global::Avalonia.Platform.Storage.FilePickerFileType("DSPRE header") { Patterns = new[] { "*.dsh" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export header", new[] { filter }, $"header_{_header.ID:D4}.dsh");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _header.ToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("HeaderEditorViewModel.ExportAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Header editor"); }
        }

        public void GoToHeader(int id)
        {
            if (id < 0 || id >= _headerListNames.Count) return;
            if (MustAskBeforeLeaving((ushort)id))
            {
                if (!_switchPending) _ = SwitchHeaderAsync((ushort)id);
                return;
            }
            if (!string.IsNullOrWhiteSpace(TreeFilterText)) TreeFilterText = "";
            SelectHeader((ushort)id);
        }

        /// <summary>Shows a linked file in its Maps tab; false means open the editor window.</summary>
        public Func<string, bool> OpenInTab
        {
            get => _openInTab;
            set { _openInTab = value; OnPropertyChanged(nameof(OpenTips)); }
        }
        private Func<string, bool> _openInTab;

        // One name per linked file, for the Open buttons' tooltips.
        private static readonly (string Key, string Name, string Editor)[] LinkedFiles =
        {
            ("Matrix", "matrix", "Matrix editor"), ("AreaData", "area data", "Area data editor"),
            ("Script", "script file", "Script editor"), ("LevelScript", "level script file", "Level script editor"),
            ("Event", "event file", "Event editor"), ("Text", "text archive", "Text editor"),
            ("Encounters", "encounters", "Wild Pokémon editor"),
        };

        /// <summary>The Open buttons' tooltips: where the linked file opens.</summary>
        public Dictionary<string, string> OpenTips
        {
            get
            {
                Dictionary<string, string> tips = new();
                foreach ((string key, string name, string editor) in LinkedFiles)
                    tips[key] = _openInTab != null ? $"Show the linked {name} in its tab" : $"Open the linked {name} in the {editor}";
                return tips;
            }
        }

        public void OpenMatrix() { if (_header != null && _openInTab?.Invoke("Matrix") != true) AvaloniaEditorLauncher.OpenMatrixEditor(_header.MatrixID, _header.ID); }
        public void OpenAreaData() { if (_header != null && _openInTab?.Invoke("Area Data") != true) AvaloniaEditorLauncher.OpenAreaDataEditor(_header.AreaDataID); }
        public void OpenEvents() { if (_header != null && _openInTab?.Invoke("Events") != true) AvaloniaEditorLauncher.OpenEventEditor(_header.EventFileID); }
        public void OpenScripts() { if (_header != null && _openInTab?.Invoke("Scripts") != true) AvaloniaEditorLauncher.OpenScriptEditor(_header.ScriptFileID); }
        public void OpenLevelScripts() { if (_header != null && _openInTab?.Invoke("Level Scripts") != true) AvaloniaEditorLauncher.OpenLevelScriptEditor(_header.LevelScriptID); }
        public void OpenTexts() { if (_header != null && _openInTab?.Invoke("Text") != true) AvaloniaEditorLauncher.OpenTextEditor(_header.TextArchiveID); }
        public void OpenEncounters() { if (CanOpenEncounters && _openInTab?.Invoke("Encounters") != true) AvaloniaEditorLauncher.OpenWildEditor(_header.WildPokemon); }

        public void OpenBattleScenery() => AvaloniaEditorLauncher.OpenBattleSceneBrowser();

        private void UpdateCurrentInternalName()
        {
            ushort id = _header.ID;
            MapHeader.WriteInternalName(id, _internalName);

            if (id < _internalNames.Count) _internalNames[id] = _internalName;
            if (id < _headerListNames.Count) _headerListNames[id] = id.ToString("D3") + MapHeader.NameSeparator + _internalName;
            RebuildTree();   // refresh the leaf's label (and any active search)
        }
    }
}
