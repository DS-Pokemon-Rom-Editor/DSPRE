using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class EventEditorViewModel
    {
        private Event _current;          // base for shared position props
        private Spawnable _spawn;
        private Overworld _ow;
        private Warp _warp;
        private Trigger _trig;

        private int _selSpawn = -1, _selOw = -1, _selWarp = -1, _selTrig = -1;
        public int SelectedSpawnableIndex { get => _selSpawn; set { if (Set(ref _selSpawn, value)) LoadSpawnable(value); } }
        public int SelectedOverworldIndex { get => _selOw; set { if (Set(ref _selOw, value)) LoadOverworld(value); } }
        public int SelectedWarpIndex { get => _selWarp; set { if (Set(ref _selWarp, value)) LoadWarp(value); } }
        public int SelectedTriggerIndex { get => _selTrig; set { if (Set(ref _selTrig, value)) LoadTrigger(value); } }

        private const int TabOverworlds = 0, TabWarps = 1, TabTriggers = 2, TabSpawnables = 3;
        private int _tabIndex;
        public int SelectedTabIndex
        {
            get => _tabIndex;
            set
            {
                if (!Set(ref _tabIndex, value)) return;
                // The position boxes are shared by every tab, so they follow the tab's own selection.
                Event e = value switch { TabOverworlds => _ow, TabWarps => _warp, TabTriggers => _trig, TabSpawnables => _spawn, _ => null };
                bool was = _suppress;
                _suppress = true;
                LoadPosition(e);
                _suppress = was;
                OnPropertyChanged(nameof(HasSelectedEvent));
                RefreshMarkers();
            }
        }

        public bool HasSpawn => _spawn != null;
        public bool HasOw => _ow != null;
        public bool HasWarp => _warp != null;
        public bool HasTrig => _trig != null;

        // Shared by all four kinds, so the box keeps its value with nothing selected.
        private decimal _xMap, _yMap, _zPos, _xMat, _yMat;
        public decimal XMap
        {
            get => _xMap;
            set
            {
                Set(ref _xMap, value);
                if (_suppress || _current == null) return;
                short v = (short)value;
                if (_current.XMapPosition == v) return;
                _current.XMapPosition = v; Dirty(); RefreshMarkers();
            }
        }
        public decimal YMap
        {
            get => _yMap;
            set
            {
                Set(ref _yMap, value);
                if (_suppress || _current == null) return;
                short v = (short)value;
                if (_current.YMapPosition == v) return;
                _current.YMapPosition = v; Dirty(); RefreshMarkers();
            }
        }
        // Only overworlds store z as 16.16 fixed point. Scaling the others would truncate them to zero.
        private static bool ZIsFixedPointFor(Event e) => e is Overworld;
        private bool ZIsFixedPoint => ZIsFixedPointFor(_current);
        private decimal ToDisplayZ(int raw) => ZIsFixedPoint ? raw / 65536m : raw;
        // Clamped to what the field holds: overworlds a signed 16.16 word, triggers an unsigned halfword.
        private int FromDisplayZ(decimal v) => _current switch
        {
            Overworld => (int)Math.Clamp(v * 65536m, int.MinValue, int.MaxValue),
            Trigger => (int)Math.Clamp(v, ushort.MinValue, ushort.MaxValue),
            _ => (int)Math.Clamp(v, int.MinValue, int.MaxValue),
        };
        private static float HeightHint(Event e) => ZIsFixedPointFor(e) ? e.ZPosition / 262144f : e.ZPosition * 0.25f;
        /// <summary>The height in tiles if it were read the way an overworld's is.</summary>
        public string ZPosNote
        {
            get
            {
                if (_current == null || ZIsFixedPoint || _current.ZPosition == 0) return "";
                double tiles = _current.ZPosition / 65536.0;
                return $"= {tiles:0.###} tiles";
            }
        }

        public string ZPosNoteTip =>
            "Nothing in the games reads this height: arriving through a warp uses only x and z, and "
            + "talking to a sign matches only its square. It's shown in tiles as if it were an overworld's height.";

        public decimal ZPos
        {
            get => _zPos;
            set
            {
                Set(ref _zPos, value);
                if (_suppress || _current == null) return;
                int v = FromDisplayZ(value);
                if (_current.ZPosition == v) return;
                _current.ZPosition = v; Dirty(); RefreshMarkers(); OnPropertyChanged(nameof(ZPosNote));
            }
        }
        public decimal XMatrix
        {
            get => _xMat;
            set
            {
                Set(ref _xMat, value);
                if (_suppress || _current == null) return;
                ushort v = (ushort)value;
                if (_current.XMatrixPosition == v) return;
                _current.XMatrixPosition = v; Dirty(); DisplayMap();
            }
        }
        public decimal YMatrix
        {
            get => _yMat;
            set
            {
                Set(ref _yMat, value);
                if (_suppress || _current == null) return;
                ushort v = (ushort)value;
                if (_current.YMatrixPosition == v) return;
                _current.YMatrixPosition = v; Dirty(); DisplayMap();
            }
        }

        private decimal _spScript, _spType, _spDir;
        public decimal SpScript
        {
            get => _spScript;
            set
            {
                if (Set(ref _spScript, value) && !_suppress && _spawn != null)
                {
                    _spawn.ScriptNumber = (ushort)value; Dirty();
                    OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange)); OnPropertyChanged(nameof(SpScriptCommonInfo)); OnPropertyChanged(nameof(SpScriptHasCommonInfo)); OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptGenericWarningVisible)); RaiseSpHidden();
                }
            }
        }
        public decimal SpType { get => _spType; set { if (Set(ref _spType, value) && !_suppress && _spawn != null) { _spawn.Type = (ushort)value; Dirty(); RaiseSpHidden(); } } }
        public decimal SpDir { get => _spDir; set { if (Set(ref _spDir, value) && !_suppress && _spawn != null) { _spawn.Dir = (ushort)value; Dirty(); } } }
        public IReadOnlyList<string> SpawnableTypeNames => DSPRE.Resources.PokeDatabase.EventEditor.Spawnables.TypesArray;
        public IReadOnlyList<string> SpawnableDirectionNames => DSPRE.Resources.PokeDatabase.EventEditor.Spawnables.OrientationsArray;

        public int SpScriptIndex
        {
            get => IndexOfAvailableScript(_spScript);
            set { if (value >= 0 && value < _availableScriptIds.Count) SpScript = _availableScriptIds[value]; }
        }
        public bool SpScriptIndexOutOfRange => _spawn != null && SpScriptIndex < 0;

        // A hidden-item spawnable runs 8000 plus an entry's script, so it picks the entry.
        private readonly List<int> _hiddenScripts = new List<int>(), _hiddenItemIds = new List<int>();
        public ObservableCollection<string> HiddenItemEntries { get; } = new ObservableCollection<string>();

        private void PopulateHiddenItemEntries()
        {
            HiddenItemEntries.Clear();
            _hiddenScripts.Clear();
            _hiddenItemIds.Clear();
            if (IsHGE || !RomInfo.IsHiddenItemsEditorAvailable()) return;
            try
            {
                int count = HiddenItemTable.Count();
                if (count < 0) return;
                string[] names = RomInfo.GetItemNames();
                foreach (HiddenItemTable.Entry e in HiddenItemTable.Read(count).OrderBy(e => e.Script))
                {
                    _hiddenScripts.Add(e.Script);
                    _hiddenItemIds.Add(e.Item);
                    HiddenItemEntries.Add($"Script {e.Script}: {(e.Item < names.Length ? names[e.Item] : $"Item {e.Item}")} x{e.Quantity}");
                }
            }
            catch (Exception ex) { AppLogger.Error("PopulateHiddenItemEntries: " + ex.Message); }
        }

        public bool SpIsHiddenItem => _spawn != null && (int)_spType == Spawnable.TypeHiddenitem && _hiddenScripts.Count > 0;

        public int SpHiddenIndex
        {
            get => _hiddenScripts.IndexOf((int)_spScript - HiddenItemTable.ScriptBase);
            set { if (value >= 0 && value < _hiddenScripts.Count) SpScript = HiddenItemTable.ScriptBase + _hiddenScripts[value]; }
        }

        public bool SpHiddenPickerVisible => SpIsHiddenItem && SpHiddenIndex >= 0;
        public int SpHiddenItemId => SpHiddenIndex >= 0 ? _hiddenItemIds[SpHiddenIndex] : 0;
        public bool SpScriptFieldsVisible => !SpHiddenPickerVisible;

        private void RaiseSpHidden()
        {
            OnPropertyChanged(nameof(SpIsHiddenItem)); OnPropertyChanged(nameof(SpHiddenIndex));
            OnPropertyChanged(nameof(SpHiddenPickerVisible)); OnPropertyChanged(nameof(SpScriptFieldsVisible));
            OnPropertyChanged(nameof(SpHiddenItemId));
        }

        public void OpenHiddenItemTable()
        {
            if (SpHiddenIndex >= 0) AvaloniaEditorLauncher.OpenItemTableEditorAtHiddenItem(_hiddenScripts[SpHiddenIndex]);
        }

        private decimal _owId, _owSprite, _owMove, _owType, _owFlag, _owScript, _owOrient, _owSight, _owXr, _owYr;
        public decimal OwId { get => _owId; set { if (Set(ref _owId, value) && !_suppress && _ow != null) { _ow.OwID = (ushort)value; Dirty(); } } }
        public decimal OwSprite
        {
            get => _owSprite;
            set { if (Set(ref _owSprite, value) && !_suppress && _ow != null) { _ow.OverlayTableEntry = (ushort)value; Dirty(); RefreshMarkers(); UpdateOwSpritePreview();
                  // The sprite decides whether this is an apricorn tree, so the panel follows it.
                  OnPropertyChanged(nameof(OwIsApricornTree)); } }
        }
        public decimal OwMovement { get => _owMove; set { if (Set(ref _owMove, value) && !_suppress && _ow != null) { _ow.Movement = (ushort)value; Dirty(); } } }
        public decimal OwType { get => _owType; set { if (Set(ref _owType, value) && !_suppress && _ow != null) { _ow.Type = (ushort)value; Dirty(); } } }
        public decimal OwFlag { get => _owFlag; set { if (Set(ref _owFlag, value) && !_suppress && _ow != null) { _ow.Flag = (ushort)value; Dirty(); } } }
        public decimal OwScript
        {
            get => _owScript;
            set
            {
                if (Set(ref _owScript, value) && !_suppress && _ow != null)
                {
                    _ow.ScriptNumber = (ushort)value; Dirty();
                    OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
                    OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible));
                }
            }
        }
        public int OwScriptIndex
        {
            get => IndexOfAvailableScript(_owScript);
            set { if (value >= 0 && value < _availableScriptIds.Count) OwScript = _availableScriptIds[value]; }
        }
        public bool OwScriptIndexOutOfRange => _ow != null && OwScriptIndex < 0;

        public string OwScriptCommonInfo
        {
            get
            {
                if (_ow == null || _owKind != OwKind.Normal || OwTalkRunsNothing || !OwScriptIndexOutOfRange) return null;

                return CommonScriptInfo((int)_owScript);
            }
        }

        // A named script's card already titles it, so only its file and number are added.
        private static string CommonScriptInfo(int scriptNumber)
        {
            CommonScriptId.Result result = CommonScriptId.Resolve(RomInfo.GameFamily, scriptNumber);
            if (result.Kind != CommonScriptId.Kind.Resolved) return null;
            return CommonScriptLabels.Of(scriptNumber) != null
                ? result.Where
                : $"Script file {result.ScriptArchiveId}, script {result.ManualUserId}";
        }

        public string TrScriptCommonInfo => _trig != null && TrScriptIndexOutOfRange ? CommonScriptInfo((int)_trScript) : null;
        public bool TrScriptHasCommonInfo => !string.IsNullOrEmpty(TrScriptCommonInfo);
        public bool TrScriptGenericWarningVisible => TrScriptIndexOutOfRange && !TrScriptHasCommonInfo;

        public string SpScriptCommonInfo => _spawn != null && SpScriptIndexOutOfRange ? CommonScriptInfo((int)_spScript) : null;
        public bool SpScriptHasCommonInfo => !string.IsNullOrEmpty(SpScriptCommonInfo);
        public bool SpScriptGenericWarningVisible => SpScriptIndexOutOfRange && !SpScriptHasCommonInfo;

        /// <summary>Opens the Script editor on the script, or on a common script in its file.</summary>
        public void GoToScript(int scriptNumber)
        {
            if (scriptNumber == EventFile.NoScript) { StatusText = "This event has no script."; return; }
            StatusText = AvaloniaEditorLauncher.GoToScript(scriptNumber, () => _pairedScriptFileId);
        }

        public void GoToOverworld(int eventFile, int owIndex)
        {
            SelectedEventIndex = eventFile;
            // With unsaved edits the switch waits on a prompt, so the overworld is picked once loaded.
            if (_selectedIndex == eventFile && owIndex >= 0 && owIndex < (_file?.Overworlds.Count ?? 0)) SelectedOverworldIndex = owIndex;
        }

        public void GoToSpawnable(int eventFile, int spawnIndex)
        {
            SelectedEventIndex = eventFile;
            if (_selectedIndex == eventFile && spawnIndex >= 0 && spawnIndex < (_file?.Spawnables.Count ?? 0)) SelectEvent(TabSpawnables, spawnIndex);
        }

        public void GoToOverworldScript() { if (_ow != null) GoToScript(_ow.ScriptNumber); }
        public void GoToTriggerScript()   { if (_trig != null) GoToScript(_trig.ScriptNumber); }
        public void GoToSpawnableScript() { if (_spawn != null) GoToScript(_spawn.ScriptNumber); }

        public bool OwScriptHasCommonInfo => !string.IsNullOrEmpty(OwScriptCommonInfo);

        public string OwScriptCommonName => OwScriptHasCommonInfo ? CommonScriptName((int)_owScript) : null;
        public string TrScriptCommonName => TrScriptHasCommonInfo ? CommonScriptName((int)_trScript) : null;
        public string SpScriptCommonName => SpScriptHasCommonInfo ? CommonScriptName((int)_spScript) : null;

        private static string CommonScriptName(int scriptNumber) =>
            CommonScriptLabels.Of(scriptNumber) ?? CommonScriptId.Resolve(RomInfo.GameFamily, scriptNumber).Name;

        /// <summary>The script number on show: overworld (0), trigger (1) or spawnable (2).</summary>
        public int ShownScript(int kind) => kind switch { 0 => (int)_owScript, 1 => (int)_trScript, _ => (int)_spScript };

        public void RenameCommonScript(int kind, string name)
        {
            string error = CommonScriptLabels.Rename(ShownScript(kind), name);
            if (error != null) _ = DialogHelper.ShowErrorAsync("The name wasn't saved:\n" + error, "Event editor");
            OnPropertyChanged(nameof(OwScriptCommonName)); OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptCommonInfo));
            OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptCommonInfo));
        }
        // A trainer's script is in the trainer script file, so it's expected to be missing here.
        public bool OwScriptGenericWarningVisible => !OwTalkRunsNothing && OwScriptIndexOutOfRange && !OwScriptHasCommonInfo
            && !(_owKind == OwKind.Trainer && !OwTrainerIndexOutOfRange);
        public decimal OwOrientation
        {
            get => _owOrient;
            set { if (Set(ref _owOrient, value) && !_suppress && _ow != null) { _ow.Orientation = (short)value; Dirty(); RefreshMarkers(); UpdateOwSpritePreview(); } }
        }
        public decimal OwSight { get => _owSight; set { if (Set(ref _owSight, value) && !_suppress && _ow != null) { _ow.SightRange = (ushort)value; Dirty(); } } }
        public decimal OwXRange { get => _owXr; set { if (Set(ref _owXr, value) && !_suppress && _ow != null) { _ow.XRange = (short)value; Dirty(); } } }
        public decimal OwYRange { get => _owYr; set { if (Set(ref _owYr, value) && !_suppress && _ow != null) { _ow.YRange = (short)value; Dirty(); } } }

        // The sprite list skips unused table keys, so its index is looked up.
        public int OwSpriteIndex
        {
            get => OverworldTableKeys == null ? -1 : Array.IndexOf(OverworldTableKeys, (uint)_owSprite);
            set
            {
                if (OverworldTableKeys == null || value < 0 || value >= OverworldTableKeys.Length) return;
                OwSprite = OverworldTableKeys[value];
                OnPropertyChanged();
            }
        }
        // A value the game doesn't define shows no selection, so it isn't changed to another movement.
        public int OwMovementIndex
        {
            get
            {
                for (int i = 0; i < _owMoveTable.Count; i++) if (_owMoveTable[i].Value == _owMove) return i;
                return -1;
            }
            set
            {
                if (value < 0 || value >= _owMoveTable.Count) return;
                OwMovement = _owMoveTable[value].Value;
                OnPropertyChanged(nameof(OwMovementUnknown)); OnPropertyChanged(nameof(OwMovementUnknownNote));
            }
        }
        public int OwOrientationIndex
        {
            get => _owOrient == -1 ? OwNoFacingIndex : (_owOrient >= 0 && _owOrient < OwNoFacingIndex ? (int)_owOrient : -1);
            set
            {
                if (value < 0 || value > OwNoFacingIndex) return;
                OwOrientation = value == OwNoFacingIndex ? -1 : value;
            }
        }

        private void UpdateOwSpritePreview()
        {
            global::Avalonia.Media.Imaging.Bitmap bmp = null;
            NsbmdRenderModel model = null;
            if (_ow != null && Overworld3DObjects.IsModel(_ow.OverlayTableEntry))
            {
                try { if (Overworld3DObjects.Load(_ow.OverlayTableEntry) is NSBMD nsbmd) model = NsbmdGeometry.BuildModel(nsbmd.Models[0]); }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is IndexOutOfRangeException)
                {
                    AppLogger.Error("UpdateOwSpritePreview model: " + ex.Message);
                }
            }
            if (!ReferenceEquals(model, OwModelPreview))
            {
                OwModelPreview = model;
                OnPropertyChanged(nameof(HasOwModelPreview));
                OwModelPreviewChanged?.Invoke(this, EventArgs.Empty);
            }
            if (_ow != null && model == null)
            {
                try
                {
                    OverworldSprites.SpritePixels pix = OverworldSprites.Get(_ow.OverlayTableEntry, (ushort)Math.Max((short)0, _ow.Orientation));
                    if (pix != null && pix.Width > 0 && pix.Height > 0)
                    {
                        byte[] bgra = new byte[pix.Rgba.Length];
                        for (int i = 0; i < bgra.Length; i += 4)
                        {
                            bgra[i] = pix.Rgba[i + 2]; bgra[i + 1] = pix.Rgba[i + 1];
                            bgra[i + 2] = pix.Rgba[i]; bgra[i + 3] = pix.Rgba[i + 3];
                        }
                        bmp = ImageConverter.ToAvaloniaBitmap(new DSPRE.RawImage(pix.Width, pix.Height, bgra));
                    }
                }
                catch (Exception ex) { AppLogger.Error("UpdateOwSpritePreview: " + ex.Message); }
            }
            OwSpritePreview = bmp;
            OnPropertyChanged(nameof(HasOwSpritePreview));
            OnPropertyChanged(nameof(IsDynamicSprite));
            OnPropertyChanged(nameof(DynamicSpriteTip));
        }

        private decimal _warpHeader, _warpAnchor, _warpHeight;
        public decimal WarpHeader { get => _warpHeader; set { if (Set(ref _warpHeader, value)) { OnPropertyChanged(nameof(WarpHeaderName)); if (!_suppress && _warp != null) { _warp.Header = (ushort)value; Dirty(); } } } }
        private IReadOnlyList<string> _headerNames;
        public string WarpHeaderName
        {
            get
            {
                try { _headerNames ??= HeaderLabels.Friendly(); } catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.WarpHeaderName", ex); return ""; }
                if (_headerNames == null) return "";
                int i = (int)_warpHeader;
                return i >= 0 && i < _headerNames.Count ? _headerNames[i].Trim() : "No such header";
            }
        }
        public decimal WarpAnchor { get => _warpAnchor; set { if (Set(ref _warpAnchor, value) && !_suppress && _warp != null) { _warp.Anchor = (ushort)value; Dirty(); } } }
        public decimal WarpHeight { get => _warpHeight; set { if (Set(ref _warpHeight, value) && !_suppress && _warp != null) { _warp.Height = (uint)value; Dirty(); } } }

        private decimal _trScript, _trW, _trH, _trVarVal, _trVar;
        public decimal TrScript
        {
            get => _trScript;
            set
            {
                if (Set(ref _trScript, value) && !_suppress && _trig != null)
                {
                    _trig.ScriptNumber = (ushort)value; Dirty();
                    OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange)); OnPropertyChanged(nameof(TrScriptCommonInfo)); OnPropertyChanged(nameof(TrScriptHasCommonInfo)); OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptGenericWarningVisible));
                }
            }
        }
        public int TrScriptIndex
        {
            get => IndexOfAvailableScript(_trScript);
            set { if (value >= 0 && value < _availableScriptIds.Count) TrScript = _availableScriptIds[value]; }
        }
        public bool TrScriptIndexOutOfRange => _trig != null && TrScriptIndex < 0;
        public decimal TrWidth { get => _trW; set { if (Set(ref _trW, value)) { OnPropertyChanged(nameof(TrNeverFires)); if (!_suppress && _trig != null) { _trig.WidthX = (ushort)value; Dirty(); RefreshMarkers(); } } } }
        public decimal TrHeight { get => _trH; set { if (Set(ref _trH, value)) { OnPropertyChanged(nameof(TrNeverFires)); if (!_suppress && _trig != null) { _trig.HeightY = (ushort)value; Dirty(); RefreshMarkers(); } } } }
        public bool TrNeverFires => _trW == 0 || _trH == 0;
        public decimal TrVarValue
        {
            get => _trVarVal;
            set { if (Set(ref _trVarVal, value) && !_suppress && _trig != null) { _trig.ExpectedVarValue = (ushort)value; Dirty(); } OnPropertyChanged(nameof(TrVarValueHex)); }
        }
        public decimal TrVar
        {
            get => _trVar;
            set { if (Set(ref _trVar, value) && !_suppress && _trig != null) { _trig.VariableWatched = (ushort)value; Dirty(); } OnPropertyChanged(nameof(TrVarHex)); }
        }

        // Script variables are normally written in hex, so both variable fields take either form.
        private bool _trHex;
        public bool TrHex { get => _trHex; set { if (Set(ref _trHex, value)) OnPropertyChanged(nameof(TrDecimal)); } }
        public bool TrDecimal => !_trHex;
        public string TrVarHex
        {
            get => ((ushort)_trVar).ToString("X4");
            set { if (TryParseHex(value, out ushort v)) TrVar = v; else OnPropertyChanged(nameof(TrVarHex)); }
        }
        public string TrVarValueHex
        {
            get => ((ushort)_trVarVal).ToString("X4");
            set { if (TryParseHex(value, out ushort v)) TrVarValue = v; else OnPropertyChanged(nameof(TrVarValueHex)); }
        }
        private static bool TryParseHex(string s, out ushort value) => HexText.TryParseUShort(s, out value);
    }
}
