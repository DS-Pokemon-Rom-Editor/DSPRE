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
    /// <summary>
    /// Avalonia port of the WinForms <c>EventEditor</c>, core scope. Edits an event
    /// file's four event lists (Spawnables, Overworlds, Warps, Triggers): select an
    /// event to edit its position (map + matrix) and its type-specific fields, add/remove
    /// events, and save / import / export. The 3D map overlay with event markers and
    /// click-to-place are deferred (the renderer foundation is in place for later).
    /// </summary>
    public class EventEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        /// <summary>
        /// Whether the parts of this editor that are still being tried out are shown: walking the map,
        /// the animated preview and the drag gizmos. They are off with the beta editors, because they
        /// are the same kind of unfinished, but the editor itself is not gated.
        /// </summary>
        public bool ShowBetaFeatures => BetaEditors.Enabled;


        /// <summary>
        /// The level script file the selected header points at, so the preview can run what the map runs by
        /// itself.
        /// </summary>
        public int LevelScriptId { get; set; } = -1;

        private bool _isCatchAllHeader;

        /// <summary>Whether the chosen header is the one every unwalked square belongs to. </summary>
        public bool IsCatchAllHeader
        {
            get => _isCatchAllHeader;
            private set { if (Set(ref _isCatchAllHeader, value)) OnPropertyChanged(nameof(CatchAllNote)); }
        }

        public string CatchAllNote => FieldCatchAllHeader.Explanation;

        /// <summary>The text archive the selected header points at. -1 when nothing set it.</summary>
        public int TextArchiveId { get; set; } = -1;

        /// <summary>
        /// The gaps the map's messages leave for words the game fills in, so the preview can show real
        /// words instead of the tag.
        /// </summary>
        public System.Collections.Generic.List<FieldStringVar> GatherStringVars()
        {
            List<FieldStringVar> none = new System.Collections.Generic.List<FieldStringVar>();
            if (TextArchiveId < 0) return none;

            System.Collections.Generic.List<string> lines;
            try { lines = new TextArchive(TextArchiveId).Messages; }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.GatherStringVars", ex); return none; }
            if (lines == null) return none;

            // Which scripts show which message. A script that could not be read is skipped rather than
            // stopping the rest.
            Dictionary<int, List<int>> showsMessage = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
            try
            {
                MapHeader header = _headerId < 0 ? null : MapHeader.GetMapHeader((ushort)_headerId);
                if (header != null)
                {
                    ScriptFile sf = new ScriptFile(header.ScriptFileID);
                    for (int i = 0; i < (sf.AllScripts?.Count ?? 0); i++)
                    {
                        int scriptNumber = i + 1;          // scripts are counted from one
                        foreach (ScriptCommand cmd in sf.AllScripts[i].Commands ?? new System.Collections.Generic.List<ScriptCommand>())
                        {
                            // A command's name carries its parameters too ("Message 0x6"), so compare the
                            // first word. Several commands put something in the box, not just Message.
                            string full = cmd?.Name ?? "";
                            int space = full.IndexOf(' ');
                            string name = space > 0 ? full.Substring(0, space) : full;
                            if (name.IndexOf("Message", System.StringComparison.Ordinal) < 0) continue;
                            if (name == "CloseMessage" || name == "OpenMessage"
                             || name == "GetCommonMessageArchive" || name == "FreezeMessage") continue;
                            if (cmd.CmdParams == null || cmd.CmdParams.Count == 0) continue;
                            // A parameter arrives as its raw bytes, smallest first.
                            byte[] raw = cmd.CmdParams[0];
                            if (raw == null || raw.Length == 0) continue;
                            int id = raw.Length >= 2 ? raw[0] | (raw[1] << 8) : raw[0];
                            if (id < 0 || id >= lines.Count) continue;
                            if (!showsMessage.TryGetValue(id, out List<int> who)) showsMessage[id] = who = new System.Collections.Generic.List<int>();
                            if (!who.Contains(scriptNumber)) who.Add(scriptNumber);
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("Event editor: overworld sprite table: " + ex.Message); }

            List<(int, string)> numbered = new System.Collections.Generic.List<(int, string)>(lines.Count);
            for (int i = 0; i < lines.Count; i++) numbered.Add((i, lines[i]));

            return FieldStringVars.Gather(numbered,
                id => showsMessage.TryGetValue(id, out List<int> who) ? who : (System.Collections.Generic.IEnumerable<int>)new int[0]);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private EventFile _file;

        // ── 3D map view ────────────────────────────────────────────────────────────────
        private Dictionary<int, (ushort matrixId, byte areaId, ushort scriptFileId, ushort headerId)> _eventToHeader; // event file -> its header's matrix + area + paired script file + the header itself
        private GameMatrix _matrix;
        private byte _areaDataId;
        private readonly Dictionary<long, byte[,]> _collisionCache = new Dictionary<long, byte[,]>();

        /// <summary>Raised after the 3D map is (re)loaded so the view can push the new model.</summary>
        public event EventHandler MapLoaded;
        /// <summary>Raised when the event markers change so the view can push the new marker mesh.</summary>
        public event EventHandler MarkersChanged;

        public NsbmdRenderModel Model3D { get; private set; }

        /// <summary>Set when the scene was rebuilt for an edit, so the view keeps its camera where it is.</summary>
        public bool KeepViewOnLoad { get; private set; }

        // ── Overworlds the game draws as a 3D model ────────────────────────────────────────────────────
        // They are built into the scene the way buildings are. Moving or changing one shows its picture until
        // the scene is rebuilt, a moment after the edits stop.
        private string _builtModelObjects = "";
        private global::Avalonia.Threading.DispatcherTimer _modelRebuild;

        private string ModelObjectsNow()
        {
            if (_file == null || !_showOw) return "";
            StringBuilder key = new StringBuilder();
            foreach (Overworld ow in _file.Overworlds)
                if (Overworld3DObjects.IsModel(ow.OverlayTableEntry))
                    key.Append(ow.OverlayTableEntry).Append(',').Append(ow.XMatrixPosition).Append(',').Append(ow.YMatrixPosition).Append(',')
                       .Append(ow.XMapPosition).Append(',').Append(ow.YMapPosition).Append(',').Append(ow.ZPosition).Append(';');
            return key.ToString();
        }

        private bool ModelObjectsAsBuilt => _builtModelObjects == ModelObjectsNow();

        private void RebuildForModelObjectsSoon()
        {
            if (_modelRebuild == null)
            {
                _modelRebuild = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                _modelRebuild.Tick += (_, _) =>
                {
                    _modelRebuild.Stop();
                    if (ModelObjectsAsBuilt || _matrix == null) return;
                    KeepViewOnLoad = true;
                    try { DisplayMap(); }
                    finally { KeepViewOnLoad = false; }
                };
            }
            _modelRebuild.Stop();
            _modelRebuild.Start();
        }

        /// <summary>The model overworlds standing on one map, placed in its local space the way buildings are.</summary>
        private IEnumerable<PlacedBuilding> ModelObjectsOn(int cellX, int cellY, MapFile map, float altitudeY)
        {
            List<PlacedBuilding> placed = new List<PlacedBuilding>();
            if (_file == null || !_showOw) return placed;
            BdhcFile terrain = null;
            if (map?.Bdhc != null && BdhcFile.TryParse(map.Bdhc, out BdhcFile parsed)) terrain = parsed;
            foreach (Overworld ow in _file.Overworlds)
            {
                if (ow.XMatrixPosition != cellX || ow.YMatrixPosition != cellY || !Overworld3DObjects.IsModel(ow.OverlayTableEntry)) continue;
                NSBMD nsbmd = Overworld3DObjects.Load(ow.OverlayTableEntry);
                if (nsbmd == null) continue;
                NSBMDModel model = nsbmd.Models[0];

                float unit = NsbmdGeometry.TileSize / 16f;   // one field unit
                float localX = (ow.XMapPosition + 0.5f) * NsbmdGeometry.TileSize;
                float localZ = (ow.YMapPosition + 0.5f) * NsbmdGeometry.TileSize + Overworld3DObjects.OffsetZ(ow.OverlayTableEntry) * unit;
                // The same ground the markers stand on: the terrain nearest the event's own height.
                float y = 0f;
                if (terrain != null && terrain.TryGetHeight(localX, localZ, HeightHint(ow) - altitudeY, out float ground)) y = ground;
                float x = localX - NsbmdGeometry.MapStride / 2f;
                float z = localZ - NsbmdGeometry.MapStride / 2f;

                float scale = (model.ModelScale == 0 ? 1f : model.ModelScale) / 64f;
                placed.Add(new PlacedBuilding
                {
                    Model = model,
                    Transform = Mat4.Multiply(Mat4.Scale(scale, scale, scale), Mat4.Translate(x / scale, y / scale, z / scale)),
                    ModelId = -1 - (int)ow.OverlayTableEntry,
                    TileX = cellX * MapFile.MapSize + ow.XMapPosition,
                    TileZ = cellY * MapFile.MapSize + ow.YMapPosition,
                });
            }
            return placed;
        }
        public float[] MarkerMesh { get; private set; }
        public int MarkerVertexCount { get; private set; }

        public List<NsbmdGlControl.SpriteInstance> Sprites { get; private set; }
        /// <summary>Raised when overworld sprites change so the view can push them to the GL control.</summary>
        public event EventHandler SpritesChanged;

        private string _mapInfo = "";
        public string MapInfo { get => _mapInfo; set => Set(ref _mapInfo, value); }

        public ObservableCollection<string> EventNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Spawnables { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Overworlds { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Warps { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Triggers { get; } = new ObservableCollection<string>();

        // ── Overworld sprite/movement/orientation dropdowns (mirrors WinForms EventEditor) ──
        public ObservableCollection<string> OwSpriteEntries { get; } = new ObservableCollection<string>();
        // Only the movement types the loaded game defines; the list differs per game family.
        public ObservableCollection<string> OwMovementNames { get; } = new ObservableCollection<string>();
        private IReadOnlyList<OverworldMovement> _owMoveTable = new List<OverworldMovement>();

        /// <summary>The event's movement type isn't one this game defines, so leave it alone.</summary>
        public bool OwMovementUnknown => _ow != null && OwMovementIndex < 0;
        public string OwMovementUnknownNote => OwMovementUnknown
            ? $"Movement {(int)_owMove} isn't one this game defines. It is kept as-is unless you pick another."
            : null;
        // The record allows -1 for no facing; it sits last so 0-3 stay index == value.
        public ObservableCollection<string> OwOrientationNames { get; } = new ObservableCollection<string> { "Up", "Down", "Left", "Right", "None" };
        private const int OwNoFacingIndex = 4;

        private global::Avalonia.Media.Imaging.Bitmap _owSpritePreview;
        public global::Avalonia.Media.Imaging.Bitmap OwSpritePreview { get => _owSpritePreview; private set => Set(ref _owSpritePreview, value); }
        public bool HasOwSpritePreview => _owSpritePreview != null;

        /// <summary>Whether a script picks this overworld's sprite at run time, so the preview is only a stand-in.</summary>
        public bool IsDynamicSprite => _ow != null && IsVariableOverworld(_ow.OverlayTableEntry);
        public string DynamicSpriteTip => IsDynamicSprite
            ? $"Dynamic: a script picks this sprite while playing, from variable 0x{0x4020 + _ow.OverlayTableEntry - FirstVariableOverworld:X4}. Shown as a stand-in."
            : null;

        /// <summary>The model shown in the sprite box for an entry the game draws as a model, or null.</summary>
        public NsbmdRenderModel OwModelPreview { get; private set; }
        public bool HasOwModelPreview => OwModelPreview != null;
        public event EventHandler OwModelPreviewChanged;

        // ── Overworld "kind" radio group (Standard / Trainer / Item), mirrors WinForms'
        // normalRadioButton/isTrainerRadioButton/isItemRadioButton: selecting a kind sets ow.type and
        // locks ow.scriptNumber to a value computed from the Trainer/Item dropdown (Script is only
        // free-form for Standard).
        private enum OwKind { Normal, Trainer, Item }
        private OwKind _owKind;

        // ── Event type ──────────────────────────────────────────────────────────────── The full set the
        // game defines, not just Standard/Trainer/Item.
        private ushort _owRawType;
        public ObservableCollection<string> OwEventTypes { get; } = new ObservableCollection<string>();
        private IReadOnlyList<OverworldEventType> _owTypeTable = new List<OverworldEventType>();

        public int OwEventTypeIndex
        {
            get
            {
                for (int i = 0; i < _owTypeTable.Count; i++) if (_owTypeTable[i].Value == _owRawType) return i;
                return -1;
            }
            set
            {
                if (value < 0 || value >= _owTypeTable.Count) return;
                ushort v = _owTypeTable[value].Value;
                if (v == _owRawType) return;
                _owRawType = v;
                SetOwKind(KindOfType(v));
                if (!_suppress && _ow != null) { _ow.Type = v; Dirty(); }
                RaiseOwTypeInfoChanged();
            }
        }

        /// <summary>True when the loaded event's type isn't one this game family defines at all.</summary>
        public bool OwTypeUnknown => _ow != null && OwEventTypeIndex < 0;
        public string OwTypeUnknownNote => OwTypeUnknown
            ? $"This event's type is {_owRawType}, which this game doesn't define. It is left untouched unless you pick a type above."
            : null;

        private OverworldEventType CurrentOwType =>
            _owTypeTable.FirstOrDefault(t => t.Value == _owRawType);
        public string OwTypeNote => CurrentOwType?.Note;
        public bool OwHasTypeNote => !string.IsNullOrEmpty(OwTypeNote);

        /// <summary>param1's meaning depends on the type; hidden when the engine never reads it.</summary>
        public string OwParam1Label => CurrentOwType?.Param1Label;
        public bool OwParam1Visible => !string.IsNullOrEmpty(OwParam1Label);

        /// <summary>param0 for a non-trainer type that reads it; trainers show it as sight range.</summary>
        public string OwParam0Label => CurrentOwType?.IsTrainer == true ? null : CurrentOwType?.Param0Label;
        public bool OwParam0Visible => !string.IsNullOrEmpty(OwParam0Label);

        /// <summary>Type 9 runs an empty script when talked to, so its own script number is never reached by talking.</summary>
        public bool OwTalkRunsNothing => _owRawType == 9;

        private static OwKind KindOfType(ushort t)
            => t == (ushort)Overworld.OwType.ITEM ? OwKind.Item
             : (OverworldEventTypes.Find(RomInfo.GameFamily, t)?.IsTrainer == true ? OwKind.Trainer : OwKind.Normal);

        private void RaiseOwTypeInfoChanged()
        {
            OnPropertyChanged(nameof(OwEventTypeIndex)); OnPropertyChanged(nameof(OwTypeUnknown));
            OnPropertyChanged(nameof(OwTypeUnknownNote)); OnPropertyChanged(nameof(OwTypeNote));
            OnPropertyChanged(nameof(OwHasTypeNote)); OnPropertyChanged(nameof(OwParam1Label));
            OnPropertyChanged(nameof(OwParam1Visible)); OnPropertyChanged(nameof(OwTalkRunsNothing));
            OnPropertyChanged(nameof(OwParam0Label)); OnPropertyChanged(nameof(OwParam0Visible));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible)); OnPropertyChanged(nameof(OwScriptCommonInfo)); OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
        }

        public decimal OwParam1
        {
            get => _ow?.Param1 ?? 0;
            set { if (_ow == null || _suppress) return; if (_ow.Param1 == (ushort)value) return; _ow.Param1 = (ushort)value; Dirty(); OnPropertyChanged(nameof(OwParam1)); }
        }
        public decimal OwParam2
        {
            get => _ow?.Param2 ?? 0;
            set { if (_ow == null || _suppress) return; if (_ow.Param2 == (ushort)value) return; _ow.Param2 = (ushort)value; Dirty(); OnPropertyChanged(nameof(OwParam2)); }
        }

        // Script is normally locked when Trainer/Item drives it, but if the current script number
        // doesn't resolve to any entry in that list (out-of-range/hand-edited data), unlock it so the
        // user isn't stuck looking at a value they can't change from either the dropdown or the field.
        public bool OwScriptEnabled => _owKind == OwKind.Normal || OwTrainerIndexOutOfRange || OwItemIndexOutOfRange;

        // Item mode already shows the resolved item/quantity in its own dropdown, so the raw Script
        // fields would just be redundant clutter there; hide them entirely instead of graying them out.
        // Still shown if the item index doesn't resolve, so there's a way to see/edit the raw number.
        public bool OwScriptSectionVisible => _owKind != OwKind.Item || OwItemIndexOutOfRange;

        public bool OwTrainerFieldsVisible => _owKind == OwKind.Trainer;

        // ── apricorn trees ──────────────────────────────────────────────────────────
        private const int FirstApricornSprite = 0x0106, LastApricornSprite = 0x0114;

        public bool OwIsApricornTree
        {
            get
            {
                if (_ow == null) return false;
                int sprite = _ow.OverlayTableEntry;
                return sprite >= FirstApricornSprite && sprite <= LastApricornSprite;
            }
        }

        public string OwApricornNote =>
            "This is an apricorn tree. What colour it grows is not stored here: the game looks up the "
            + "apricorn bed this number picks and asks the save what is in it, so changing the number "
            + "moves the tree to another bed and it comes out a different apricorn. The sprite chosen "
            + "above is only what it looks like before the game replaces it.";

        /// <summary>HG Engine (community HeartGold decompilation, <see cref="RomInfo.IsHGE"/>) uses a
        /// single generic item script (always exactly 7000) whose behavior reads the item ID straight
        /// out of the event's own param0/sightRange field, instead of vanilla's one-script-per-item
        /// scheme (script = 7000 + dropdown index). So the Item panel shows a different control
        /// depending on which engine the loaded ROM actually runs.</summary>
        public bool IsHGE => RomInfo.IsHGE;
        public bool OwItemFieldsVisibleVanilla => _owKind == OwKind.Item && !IsHGE;
        public bool OwItemFieldsVisibleHGE => _owKind == OwKind.Item && IsHGE;

        public ObservableCollection<string> OwTrainerEntries { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> OwItemEntries { get; } = new ObservableCollection<string>();

        private int _owTrainerIndex = -1;
        public int OwTrainerIndex
        {
            get => _owTrainerIndex;
            set { if (Set(ref _owTrainerIndex, value) && !_suppress && _ow != null && _owKind == OwKind.Trainer) RecomputeTrainerScript(); }
        }

        // The Script field's raw value can point past the end of the current trainer roster (a script
        // number that doesn't correspond to any known trainer, e.g. hand-edited data or a ROM-specific
        // reserved value); show that clearly instead of a silently-blank dropdown.
        public bool OwTrainerIndexOutOfRange => _owKind == OwKind.Trainer && (_owTrainerIndex < 0 || _owTrainerIndex >= OwTrainerEntries.Count);

        /// <summary>Trainer 0 is the empty slot the games never use; picking it gives a battle with nobody.</summary>
        public bool OwTrainerIsDummy => _owKind == OwKind.Trainer && _owTrainerIndex == 0;

        private bool _owPartnerTrainer;
        public bool OwPartnerTrainer
        {
            get => _owPartnerTrainer;
            set { if (Set(ref _owPartnerTrainer, value) && !_suppress && _ow != null && _owKind == OwKind.Trainer) RecomputeTrainerScript(); }
        }

        private int _owItemIndex = -1;
        public int OwItemIndex
        {
            get => _owItemIndex;
            set { if (Set(ref _owItemIndex, value) && !_suppress && _ow != null && _owKind == OwKind.Item && value >= 0) ForceOwScript((ushort)(7000 + value)); }
        }

        public bool OwItemIndexOutOfRange => _owKind == OwKind.Item && (_owItemIndex < 0 || _owItemIndex >= OwItemEntries.Count);

        /// <summary>HG Engine only: the item ID for this event, read/written straight to the same
        /// underlying field as <see cref="OwSight"/> (param0), see <see cref="IsHGE"/>. Also keeps
        /// OwSight's own cached backing field in sync so it doesn't show a stale value if the user
        /// later switches this event back to Standard/Trainer kind.</summary>
        public decimal OwItemIdHGE
        {
            get => _ow?.SightRange ?? 0;
            set
            {
                if (_ow == null || _suppress || _owKind != OwKind.Item) return;
                _ow.SightRange = (ushort)value;
                _owSight = value;
                ForceOwScript(7000); // always exactly 7000 under HG Engine, never 7000+index
                OnPropertyChanged(nameof(OwItemIdHGE));
                OnPropertyChanged(nameof(OwSight));
            }
        }

        private void RaiseOwKindChanged()
        {
            OnPropertyChanged(nameof(OwTrainerFieldsVisible));
            OnPropertyChanged(nameof(OwIsApricornTree)); OnPropertyChanged(nameof(OwApricornNote));
            OnPropertyChanged(nameof(OwItemFieldsVisibleVanilla)); OnPropertyChanged(nameof(OwItemFieldsVisibleHGE));
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwTrainerIsDummy)); OnPropertyChanged(nameof(OwItemIndexOutOfRange));
            OnPropertyChanged(nameof(OwItemIdHGE));
            OnPropertyChanged(nameof(OwScriptEnabled));
            OnPropertyChanged(nameof(OwScriptSectionVisible));
            OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible));
        }

        /// <summary>Sets OwScript's model value + observable field directly, bypassing the property's equality guard
        /// (WinForms always forces the script number when the type/trainer/item selection changes).</summary>
        private void ForceOwScript(ushort value)
        {
            if (_ow != null) _ow.ScriptNumber = value;
            _owScript = value;
            OnPropertyChanged(nameof(OwScript));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
            Dirty();
        }

        private void RecomputeTrainerScript()
        {
            if (_owTrainerIndex < 0) return;
            int idx = _owTrainerIndex;
            ushort scriptNum = (ushort)(idx + (_owPartnerTrainer ? 4999 : 2999));
            ForceOwScript(scriptNum);
        }

        /// <summary>Keeps the script field consistent with the kind of event this now is. The type value
        /// itself belongs to the type picker; this only deals with the script number the kind implies.</summary>
        // The script a Standard event had before it was switched to Trainer or Item, so switching back restores it.
        private ushort? _owNormalScript;

        // Script numbers the trainer and item dropdowns write; any other number was typed or picked by the user.
        private static bool ScriptOwnedBy(OwKind kind, ushort script) => kind switch
        {
            OwKind.Trainer => script >= 3000 && script < GroundItemScriptsLogic.ItemScrMin,
            OwKind.Item => script >= GroundItemScriptsLogic.ItemScrMin && script < GroundItemScriptsLogic.ItemScrMax,
            _ => false,
        };

        private void SetOwKind(OwKind kind)
        {
            OwKind previous = _owKind;
            bool changed = previous != kind;
            _owKind = kind;
            RaiseOwKindChanged();
            if (!changed || _suppress || _ow == null) return;
            if (previous == OwKind.Normal) _owNormalScript = _ow.ScriptNumber;

            switch (kind)
            {
                case OwKind.Normal:
                    // A message id or a hand-written script number belongs to the user and must survive a type change.
                    if (ScriptOwnedBy(previous, _ow.ScriptNumber)) ForceOwScript(_owNormalScript ?? 0);
                    break;
                case OwKind.Item:
                    if (IsHGE)
                    {
                        // Item ID lives in sightRange/param0, already whatever it was, just lock the script.
                        ForceOwScript(7000);
                    }
                    else if (_owItemIndex < 0 && OwItemEntries.Count > 0) OwItemIndex = 0;
                    else ForceOwScript((ushort)(7000 + Math.Max(_owItemIndex, 0)));
                    break;
                case OwKind.Trainer:
                    // Don't pick a trainer for them: entry 0 is the dummy slot, not a real trainer, and no
                    // retail event uses it. Leave it unselected so the warning prompts a real choice.
                    if (_owTrainerIndex >= 0) RecomputeTrainerScript();
                    break;
            }
        }

        private void PopulateOwEventTypes()
        {
            _owTypeTable = OverworldEventTypes.For(RomInfo.GameFamily);
            OwEventTypes.Clear();
            foreach (OverworldEventType t in _owTypeTable) OwEventTypes.Add(t.ToString());

            _owMoveTable = OverworldMovements.For(RomInfo.GameFamily);
            OwMovementNames.Clear();
            foreach (OverworldMovement m in _owMoveTable) OwMovementNames.Add(m.ToString());
        }

        private void PopulateOwTrainerAndItemEntries()
        {
            PopulateOwTrainerEntries();
            PopulateOwItemEntries();
        }

        private void PopulateOwTrainerEntries()
        {
            try
            {
                DSPRE.Avalonia.Data.ListSync.Apply(OwTrainerEntries,
                    new List<string>(TrainerNames.GetAll()));
            }
            catch (Exception ex)
            {
                OwTrainerEntries.Clear();
                AppLogger.Error("PopulateOwTrainerEntries: " + ex.Message);
            }
        }

        private void OnNamesChanged(object sender, EventArgs e)
        {
            PopulateOwTrainerEntries();
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptEnabled));
        }

        public void Attach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            AppEvents.NamesChanged += OnNamesChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        public void Detach() { AppEvents.NamesChanged -= OnNamesChanged; AppEvents.RomPatchStateChanged -= OnPatchStateChanged; }

        // The item standardisation patch changes what each ground item entry is called; the script number stays.
        // The item standardisation patch renumbers every ground item in the event files on disk and tells the user
        // unsaved changes here are dropped, so the open file is read again with the new names. Other patches leave it.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (IsHGE) return;
            bool standardized;
            try { standardized = PatchToolboxLogic.CheckScriptsStandardizedItemNumbers(); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return; }
            if (standardized == _itemsStandardized) return;

            PopulateOwItemEntries();
            if (_selectedIndex < 0) return;
            int keepOw = _selOw;
            LoadFile(_selectedIndex);
            // Refilling the item list drops the box's pick, so the overworld is picked again once it has settled.
            if (keepOw >= 0)
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedOverworldIndex = keepOw,
                                                                     global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>For a standalone window closing; the Maps workspace's instance keeps listening while its tab is hidden.</summary>
        void IDetachable.Detach() { Detach(); DetachSaves(); }

        public void DetachSaves()
        {
            AppEvents.EventFileSaved -= OnSavedElsewhere;
            AppEvents.MapSaved -= OnMapSavedElsewhere;
            _spritesSaved?.Dispose();
        }

        private bool _itemsStandardized;

        private void PopulateOwItemEntries()
        {
            OwItemEntries.Clear();
            if (IsHGE) return; // HG Engine uses a direct Item ID input (OwItemIdHGE) instead of this dropdown.
            try
            {
                string[] itemNames = RomInfo.GetItemNames();
                _itemsStandardized = PatchToolboxLogic.CheckScriptsStandardizedItemNumbers();
                if (_itemsStandardized)
                {
                    foreach (string name in itemNames) OwItemEntries.Add(name);
                }
                else
                {
                    ScriptFile itemScript = new ScriptFile(RomInfo.ItemScriptFileNumber);
                    foreach ((int scriptIndex, int itemId, int quantity) entry in DSUtils.GetGroundItemScriptEntries(itemScript))
                    {
                        string name = entry.itemId < itemNames.Length ? itemNames[entry.itemId] : ("Item " + entry.itemId);
                        OwItemEntries.Add(entry.quantity + "x " + name);
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("PopulateOwItemEntries: " + ex.Message); }
        }

        /// <summary>Re-reads the ground-item script entries after they've been added/removed via the
        /// Manage Ground Items dialog, and re-syncs the current selection to the (possibly shifted)
        /// index for the event's own scriptNumber.</summary>
        public void RefreshOwItemEntries()
        {
            PopulateOwItemEntries();
            if (_ow == null || _owKind != OwKind.Item || IsHGE) return;

            int itemIdx = _ow.ScriptNumber - 7000;
            _owItemIndex = (itemIdx >= 0 && itemIdx < OwItemEntries.Count) ? itemIdx : -1;
            OnPropertyChanged(nameof(OwItemIndex));
            OnPropertyChanged(nameof(OwItemIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptSectionVisible));
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Current selection ───────────────────────────────────────────────────────
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

        // Tab order in the view: Overworlds, Warps, Triggers, Spawnables.
        private int _tabIndex;
        public int SelectedTabIndex
        {
            get => _tabIndex;
            set
            {
                if (!Set(ref _tabIndex, value)) return;
                // The position boxes are shared by every tab, so they follow the tab's own selection.
                Event e = value switch { 0 => _ow, 1 => _warp, 2 => _trig, 3 => _spawn, _ => null };
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

        // ── Shared position (map + matrix) ──────────────────────────────────────────
        private decimal _xMap, _yMap, _zPos, _xMat, _yMat;
        // These five are shared by all four event kinds, so the box keeps a backing field that outlives any
        // one selection.
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
        // Only overworlds store z as 16.16 fixed point (1 tile = 65536 units). Triggers and spawnables
        // store a plain value, so scaling those would truncate anything the user types to zero.
        /// <summary>
        /// Whether this kind of event keeps its height as a fixed-point world value rather than a plain
        /// number.
        /// </summary>
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
        /// <summary>The event's height in the same units the 3D scene uses, whichever way its z is stored.</summary>
        private static float HeightHint(Event e) => ZIsFixedPointFor(e) ? e.ZPosition / 262144f : e.ZPosition * 0.25f;
        /// <summary>What the height would be in tiles if it were read the way an overworld's is. </summary>
        public string ZPosNote
        {
            get
            {
                if (_current == null || ZIsFixedPoint || _current.ZPosition == 0) return "";
                double tiles = _current.ZPosition / 65536.0;
                return $"= {tiles:0.###} tiles";
            }
        }

        /// <summary>The long version of the note, for hovering.</summary>
        public string ZPosNoteTip =>
            "Nothing in the games reads this height: arriving through a warp uses only x and z, and "
            + "talking to a sign matches only its square. This is what the number would be in tiles if "
            + "it were read the way an overworld's height is.";

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

        // ── Spawnable fields ────────────────────────────────────────────────────────
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

        // A hidden-item spawnable runs 8000 plus an entry's script, so it picks the entry instead of a raw number.
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
        // The raw script stays reachable when the number matches no entry.
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

        // ── Overworld fields ────────────────────────────────────────────────────────
        private decimal _owId, _owSprite, _owMove, _owType, _owFlag, _owScript, _owOrient, _owSight, _owXr, _owYr;
        public decimal OwId { get => _owId; set { if (Set(ref _owId, value) && !_suppress && _ow != null) { _ow.OwID = (ushort)value; Dirty(); } } }
        public decimal OwSprite
        {
            get => _owSprite;
            set { if (Set(ref _owSprite, value) && !_suppress && _ow != null) { _ow.OverlayTableEntry = (ushort)value; Dirty(); RefreshMarkers(); UpdateOwSpritePreview();
                  // Whether this is an apricorn tree is decided by the sprite, so the panel has to
                  // follow when the sprite changes.
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

        /// <summary>When the raw Script number for a Standard-type event isn't found in the header's own
        /// paired script file, it may still be a valid "common"/global script (see <see cref="CommonScriptId"/>)
        /// rather than a real error. Non-null only in that case, so the UI can show something useful
        /// instead of a plain "doesn't match" warning.</summary>
        public string OwScriptCommonInfo
        {
            get
            {
                if (_ow == null || _owKind != OwKind.Normal || OwTalkRunsNothing || !OwScriptIndexOutOfRange) return null;

                return CommonScriptInfo((int)_owScript);
            }
        }

        /// <summary>Shared by every event type: a script number that isn't in this header's own script
        /// file may still be a valid common/global script rather than a mistake.</summary>
        private static string CommonScriptInfo(int scriptNumber)
        {
            CommonScriptId.Result result = CommonScriptId.Resolve(RomInfo.GameFamily, scriptNumber);
            if (result.Kind != CommonScriptId.Kind.Resolved) return null;
            // Unnamed, the range is the card's title, so this line only says where the script is.
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

        /// <summary>Open the Script Editor on the given script, following a common script to its real
        /// archive the way the old editor's per-event "go to script" buttons did, and landing on the
        /// script entry itself rather than just its file.</summary>
        public void GoToScript(int scriptNumber)
        {
            if (scriptNumber == EventFile.NoScript) { StatusText = "This event has no script."; return; }
            StatusText = AvaloniaEditorLauncher.GoToScript(scriptNumber, () => _pairedScriptFileId);
        }

        /// <summary>Shows one overworld of one event file, asked for from another editor.</summary>
        public void GoToOverworld(int eventFile, int owIndex)
        {
            SelectedEventIndex = eventFile;
            // The switch waits on a prompt when this file has unsaved edits; only pick the overworld once it's loaded.
            if (_selectedIndex == eventFile && owIndex >= 0 && owIndex < (_file?.Overworlds.Count ?? 0)) SelectedOverworldIndex = owIndex;
        }

        /// <summary>Shows one spawnable of one event file, asked for from another editor.</summary>
        public void GoToSpawnable(int eventFile, int spawnIndex)
        {
            SelectedEventIndex = eventFile;
            if (_selectedIndex == eventFile && spawnIndex >= 0 && spawnIndex < (_file?.Spawnables.Count ?? 0)) SelectEvent(3, spawnIndex);
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

        /// <summary>The script number of the overworld (0), trigger (1) or spawnable (2) on show.</summary>
        public int ShownScript(int kind) => kind switch { 0 => (int)_owScript, 1 => (int)_trScript, _ => (int)_spScript };

        /// <summary>Names a shared script in this project's labels.</summary>
        public void RenameCommonScript(int kind, string name)
        {
            CommonScriptLabels.Rename(ShownScript(kind), name);
            OnPropertyChanged(nameof(OwScriptCommonName)); OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptCommonInfo));
            OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptCommonInfo));
        }
        public bool OwScriptGenericWarningVisible => !OwTalkRunsNothing && OwScriptIndexOutOfRange && !OwScriptHasCommonInfo;
        public decimal OwOrientation
        {
            get => _owOrient;
            set { if (Set(ref _owOrient, value) && !_suppress && _ow != null) { _ow.Orientation = (short)value; Dirty(); RefreshMarkers(); UpdateOwSpritePreview(); } }
        }
        public decimal OwSight { get => _owSight; set { if (Set(ref _owSight, value) && !_suppress && _ow != null) { _ow.SightRange = (ushort)value; Dirty(); } } }
        public decimal OwXRange { get => _owXr; set { if (Set(ref _owXr, value) && !_suppress && _ow != null) { _ow.XRange = (short)value; Dirty(); } } }
        public decimal OwYRange { get => _owYr; set { if (Set(ref _owYr, value) && !_suppress && _ow != null) { _ow.YRange = (short)value; Dirty(); } } }

        // Dropdown-friendly wrappers: the sprite entry list is the sparse set of valid overlay-table
        // keys (RomInfo.overworldTableKeys), so its ComboBox index isn't the same as the raw value;
        // movement/orientation are plain 0..N index=value lists so they pass straight through.
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
        // A value the game doesn't define shows nothing selected rather than snapping to another movement.
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

        // ── Warp fields ───────────────────────────────────────────────────────────────
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

        // ── Trigger fields ──────────────────────────────────────────────────────────
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
        public decimal TrWidth { get => _trW; set { if (Set(ref _trW, value) && !_suppress && _trig != null) { _trig.WidthX = (ushort)value; Dirty(); } } }
        public decimal TrHeight { get => _trH; set { if (Set(ref _trH, value) && !_suppress && _trig != null) { _trig.HeightY = (ushort)value; Dirty(); } } }
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

        // Script variables are normally written in hex, so the two variable fields can be typed either way.
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

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _archive.IsDirty;
        public string UnsavedChangesDescription => DSPRE.Avalonia.Data.StagedArchive.Join($"Event file {_selectedIndex}", _dirty, _archive.Describe("event file", "event files"));
        public void SaveChanges() => Save();
        public void DiscardChanges()
        {
            // Added and removed files go with the open file's edits.
            _dirty = false;
            _archive.Discard();
            ListEventFiles();
            if (_selectedIndex >= EventNames.Count) { _selectedIndex = EventNames.Count - 1; OnPropertyChanged(nameof(SelectedEventIndex)); }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (_selectedIndex >= 0) LoadFile(_selectedIndex);
        }

        // Added, removed and saved event files wait here until Save.
        private readonly StagedArchive _archive = new(DirNames.EventFiles);

        private void ListEventFiles()
        {
            int count = _archive.Count;
            EventNames.Clear();
            for (int i = 0; i < count; i++) EventNames.Add("Event File " + i);
        }
        private void Dirty() { _undo?.Record(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo ────────────────────────────────────────────────────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() { _undo?.Undo(); SyncDirtyWithUndo(); }
        public void Redo() { _undo?.Redo(); SyncDirtyWithUndo(); }
        private void SyncDirtyWithUndo()
        {
            if (_undo == null || _undo.IsDirty == _dirty) return;
            _dirty = _undo.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void StartUndo()
        {
            _undo = _file == null ? null : new ByteStateUndo(() => _file.ToByteArray(), RestoreState,
                () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        // Swaps in the earlier file and selects the same events again, the one being edited last.
        private void RestoreState(byte[] state)
        {
            int spawn = _selSpawn, ow = _selOw, warp = _selWarp, trig = _selTrig;
            Event active = _current;
            int id = _file.ID;
            using (MemoryStream ms = new MemoryStream(state)) _file = new EventFile(ms) { ID = id };
            _current = null; _spawn = null; _ow = null; _warp = null; _trig = null;
            _selSpawn = _selOw = _selWarp = _selTrig = -1;
            RefreshLists();
            int Keep(int i, int count) => i < count ? i : count - 1;
            List<Action> order = new List<Action>
            {
                () => SelectedSpawnableIndex = Keep(spawn, _file.Spawnables.Count),
                () => SelectedOverworldIndex = Keep(ow, _file.Overworlds.Count),
                () => SelectedWarpIndex = Keep(warp, _file.Warps.Count),
                () => SelectedTriggerIndex = Keep(trig, _file.Triggers.Count),
            };
            int last = active is Overworld ? 1 : active is Warp ? 2 : active is Trigger ? 3 : 0;
            for (int i = 0; i < order.Count; i++) if (i != last) order[i]();
            order[last]();
            foreach (string n in new[] { nameof(HasSelectedEvent), nameof(HasSpawn), nameof(HasOw), nameof(HasWarp), nameof(HasTrig) })
                OnPropertyChanged(n);
            Dirty();
            RefreshMarkers();
        }
        private void SetClean() { _undo?.MarkSaved(); if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _selectedIndex = -1;
        public int SelectedEventIndex
        {
            get => _selectedIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (_dirty && !_suppress && value >= 0 && _selectedIndex >= 0 && value != _selectedIndex)
                {
                    // Ask before dropping edits. Snap the box back to the current file until they answer.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedIndex, v => _selectedIndex = v, () => OnPropertyChanged(nameof(SelectedEventIndex)));
                    _ = ConfirmEventFileSwitchAsync(requested);
                    return;
                }
                if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadFile(value);
            }
        }

        private async Task ConfirmEventFileSwitchAsync(int requested)
        {
            // Shared with every other editor that guards a record switch, so this offers Save as well
            // as Discard. Discard-only forced the habit of saving first and switching second, which is
            // the habit the prompt exists to make unnecessary.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "event file")) return;
            SetClean();
            // Discarding can drop the file being switched to when it was added and not saved.
            if (requested >= EventNames.Count) requested = EventNames.Count - 1;
            if (Set(ref _selectedIndex, requested, nameof(SelectedEventIndex))) LoadFile(requested);
        }

        public int InitialIndex { get; set; }

        /// <summary>When set (>=0), selected right after the event file finishes loading; lets callers
        /// (e.g. Research Helper's Overworld/Trainer Watcher) jump straight to a specific overworld
        /// entry instead of just the containing event file.</summary>
        public int InitialOverworldIndex { get; set; } = -1;
        public int InitialSpawnableIndex { get; set; } = -1;

        // ── 3D marker visibility toggles (per event type) ────────────────────────────────
        private bool _showOw = true, _showWarp = true, _showTrig = true, _showSpawn = true, _showGrid;

        // Flat 2D view.
        private bool _flat2D = SettingsManager.Settings?.EventEditorFlat2D ?? false;
        public bool Flat2D
        {
            get => _flat2D;
            set
            {
                if (!Set(ref _flat2D, value)) return;
                if (SettingsManager.Settings != null)
                {
                    SettingsManager.Settings.EventEditorFlat2D = value;
                    SettingsManager.Save();
                }
                ViewModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when the 2D/3D choice changes, so the view can reconfigure the GL control.</summary>
        public event EventHandler ViewModeChanged;
        public bool ShowOverworlds { get => _showOw; set { if (Set(ref _showOw, value)) RefreshMarkers(); } }
        public bool ShowWarps { get => _showWarp; set { if (Set(ref _showWarp, value)) RefreshMarkers(); } }
        public bool ShowTriggers { get => _showTrig; set { if (Set(ref _showTrig, value)) RefreshMarkers(); } }
        public bool ShowSpawnables { get => _showSpawn; set { if (Set(ref _showSpawn, value)) RefreshMarkers(); } }
        public bool ShowGrid { get => _showGrid; set { if (Set(ref _showGrid, value)) RefreshMarkers(); } }
        /// <summary>The tile-boundary grid overlay is only built for small matrices (≤4 cells): it's an
        /// expensive per-tile collision scan that would otherwise repeat on every event add/move for a
        /// large header, so bigger matrices skip it rather than pay that cost every refresh. The
        /// checkbox is disabled instead of silently doing nothing so the limit is visible.</summary>
        public bool GridToggleEnabled => _matrix != null && _matrix.Width * _matrix.Height <= 4;

        private bool _stitchGrid = true;
        public bool StitchGrid { get => _stitchGrid; set { if (Set(ref _stitchGrid, value)) DisplayMap(); } }
        private NsbmdGeometry.MatrixStitchMode StitchMode => _stitchGrid ? NsbmdGeometry.MatrixStitchMode.Grid : NsbmdGeometry.MatrixStitchMode.Continuous;

        public EventEditorViewModel() { if (Design.IsDesignMode) EventNames.Add("Event 0"); }
        public EventEditorViewModel(bool _)
        {
            AppEvents.EventFileSaved += OnSavedElsewhere;
            AppEvents.MapSaved += OnMapSavedElsewhere;
            _spritesSaved = AppEvents.WhenSaved(() => { UpdateOwSpritePreview(); RefreshMarkers(); }, DirNames.OWSprites);
        }

        private readonly IDisposable _spritesSaved;

        // A saved map can move buildings and terrain under the events, so the scene is drawn again.
        private void OnMapSavedElsewhere(object sender, int mapIndex)
        {
            if (_matrix?.Maps == null || Model3D == null || !_matrix.Maps.Cast<ushort>().Contains((ushort)mapIndex)) return;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DisplayMap();
                StatusText = $"Map {mapIndex} was saved in another window and redrawn.";
            });
        }

        // Another open copy of this event file saved: show it, unless this copy holds its own edits.
        private void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || id != _selectedIndex) return;
            if (_dirty) { _ = OfferReloadAsync(id); return; }
            LoadFile(id);
            StatusText = $"Event file {id} was saved in another window and reloaded.";
        }

        // Keeping these edits means saving them over what the other window wrote, so ask.
        private async Task OfferReloadAsync(int id)
        {
            bool reload = await DialogHelper.AskYesNoAsync(
                $"Event file {id} was saved in another window.\n\nLoad it and drop the changes made here?", "Event file changed", _owner);
            if (id != _selectedIndex) return;
            if (!reload) { StatusText = $"Event file {id} was saved in another window. Saving here will overwrite it."; return; }
            LoadFile(id);
            StatusText = $"Event file {id} was saved in another window and reloaded.";
        }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            _headerNames = null;   // per ROM
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.EventFiles, DirNames.Maps, DirNames.ExteriorBuildingModels,
                    DirNames.BuildingTextures, DirNames.MapTextures, DirNames.Matrices,
                    DirNames.AreaData, DirNames.DynamicHeaders, DirNames.OWSprites,
                    // Needed by PopulateOwTrainerAndItemEntries below (Trainer/Item dropdowns).
                    DirNames.TrainerProperties, DirNames.Scripts, DirNames.TextArchives });
                if (GameFamily == GameFamilies.HGSS)
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.InteriorBuildingModels });
                _eventToHeader = BuildEventHeaderLookup();
                // Overworld sprite lookups are populated during ROM/event setup in WinForms;
                // ensure they exist for the 3D sprite billboards here (SetOWtable must run first).
                try { if (Ow3DSpriteDict == null) Set3DOverworldsDict(); } catch (Exception ex) { AppLogger.Error("Set3DOverworldsDict: " + ex.Message); }
                try { if (OverworldTable == null) { SetOWtable(); ReadOWTable(); } } catch (Exception ex) { AppLogger.Error("ReadOWTable: " + ex.Message); }
                OwSpriteEntries.Clear();
                if (OverworldTableKeys != null)
                    foreach (uint key in OverworldTableKeys) OwSpriteEntries.Add(OverworldLabels.Of(key));
                PopulateOwEventTypes();
                PopulateOwTrainerAndItemEntries();
                ListEventFiles();
                int count = EventNames.Count;
                StatusText = $"{count} event files.";
                if (count > 0) SelectedEventIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
                if (InitialOverworldIndex >= 0 && InitialOverworldIndex < _file?.Overworlds.Count)
                    SelectedOverworldIndex = InitialOverworldIndex;
                if (InitialSpawnableIndex >= 0 && InitialSpawnableIndex < _file?.Spawnables.Count)
                    SelectEvent(3, InitialSpawnableIndex);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Failed to set up Event Editor:\n{ex.Message}", "Event editor error");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                // Read on every file load, so edits saved in the item tables show up here.
                PopulateHiddenItemEntries();
                using (MemoryStream ms = new MemoryStream(_archive.Read(index) ?? throw new FileNotFoundException($"Event file {index} is missing.")))
                    _file = new EventFile(ms) { ID = index };
                ClearSelection();
                RefreshLists();
                ResolveMatrixForFile(index);
                SetClean();
                StartUndo();
                StatusText = $"Loaded event file {index}.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Failed to load event file {index}:\n{ex.Message}", "Event editor error"); }
        }

        private void RefreshLists()
        {
            _suppress = true;
            Spawnables.Clear(); Overworlds.Clear(); Warps.Clear(); Triggers.Clear();
            if (_file != null)
            {
                for (int i = 0; i < _file.Spawnables.Count; i++) Spawnables.Add($"Spawnable {i:D2}");
                for (int i = 0; i < _file.Overworlds.Count; i++) Overworlds.Add($"Overworld {i:D2}");
                for (int i = 0; i < _file.Warps.Count; i++) Warps.Add($"Warp {i:D2}");
                for (int i = 0; i < _file.Triggers.Count; i++) Triggers.Add($"Trigger {i:D2}");
            }
            _suppress = false;
        }

        private void LoadPosition(Event e)
        {
            _current = e;
            if (e == null) return;
            XMap = e.XMapPosition; YMap = e.YMapPosition; ZPos = ToDisplayZ(e.ZPosition);
            OnPropertyChanged(nameof(ZPosNote));
            XMatrix = e.XMatrixPosition; YMatrix = e.YMatrixPosition;
        }

        private void LoadSpawnable(int i)
        {
            _spawn = (_file != null && i >= 0 && i < _file.Spawnables.Count) ? _file.Spawnables[i] : null;
            OnPropertyChanged(nameof(HasSpawn));
            if (_spawn == null) { if (_current is Spawnable) _current = null; return; }
            _suppress = true;
            LoadPosition(_spawn);
            SpScript = _spawn.ScriptNumber; SpType = _spawn.Type; SpDir = _spawn.Dir;
            _suppress = false;
            OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange)); OnPropertyChanged(nameof(SpScriptCommonInfo)); OnPropertyChanged(nameof(SpScriptHasCommonInfo)); OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptGenericWarningVisible)); RaiseSpHidden();
            RefreshMarkers();
        }

        private void LoadOverworld(int i)
        {
            _ow = (_file != null && i >= 0 && i < _file.Overworlds.Count) ? _file.Overworlds[i] : null;
            OnPropertyChanged(nameof(HasOw));
            if (_ow == null) { if (_current is Overworld) _current = null; UpdateOwSpritePreview(); return; }
            _suppress = true;
            LoadPosition(_ow);
            OwId = _ow.OwID; OwSprite = _ow.OverlayTableEntry; OwMovement = _ow.Movement; OwType = _ow.Type;
            OwFlag = _ow.Flag; OwScript = _ow.ScriptNumber; OwOrientation = _ow.Orientation; OwSight = _ow.SightRange;
            OwXRange = _ow.XRange; OwYRange = _ow.YRange;
            OnPropertyChanged(nameof(OwSpriteIndex)); OnPropertyChanged(nameof(OwMovementIndex));
            OnPropertyChanged(nameof(OwMovementUnknown)); OnPropertyChanged(nameof(OwMovementUnknownNote)); OnPropertyChanged(nameof(OwOrientationIndex));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));

            // Derive the Standard/Trainer/Item radio selection and locked-script dropdown index from
            // the raw type/scriptNumber. Expanded rosters keep the direct one-based trainer mapping.
            _owRawType = _ow.Type;
            _owNormalScript = null;
            if (KindOfType(_ow.Type) == OwKind.Trainer)
            {
                _owKind = OwKind.Trainer;
                bool partner = _ow.ScriptNumber >= 4999;
                int idx = partner ? _ow.ScriptNumber - 4999 : _ow.ScriptNumber - 2999;
                // Out of range (past the end of the current trainer roster) → leave unselected (-1) rather
                // than clamping into a wrong trainer; OwTrainerIndexOutOfRange surfaces this in the UI.
                _owTrainerIndex = (idx >= 0 && idx < OwTrainerEntries.Count) ? idx : -1;
                _owPartnerTrainer = partner;
            }
            else if (_ow.Type == (ushort)Overworld.OwType.ITEM
                     || (_ow.ScriptNumber >= GroundItemScriptsLogic.ItemScrMin && _ow.ScriptNumber < GroundItemScriptsLogic.ItemScrMax))
            {
                _owKind = OwKind.Item;
                int itemIdx = _ow.ScriptNumber - 7000;
                _owItemIndex = (itemIdx >= 0 && itemIdx < OwItemEntries.Count) ? itemIdx : -1;
            }
            else
            {
                _owKind = OwKind.Normal;
            }
            RaiseOwKindChanged();
            OnPropertyChanged(nameof(OwTrainerIndex)); OnPropertyChanged(nameof(OwPartnerTrainer)); OnPropertyChanged(nameof(OwItemIndex));
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwTrainerIsDummy)); OnPropertyChanged(nameof(OwItemIndexOutOfRange)); OnPropertyChanged(nameof(OwScriptEnabled));
            RaiseOwTypeInfoChanged(); OnPropertyChanged(nameof(OwParam1)); OnPropertyChanged(nameof(OwParam2));

            _suppress = false;
            RefreshMarkers();
            UpdateOwSpritePreview();
        }

        private void LoadWarp(int i)
        {
            _warp = (_file != null && i >= 0 && i < _file.Warps.Count) ? _file.Warps[i] : null;
            OnPropertyChanged(nameof(HasWarp));
            if (_warp == null) { if (_current is Warp) _current = null; return; }
            _suppress = true;
            LoadPosition(_warp);
            WarpHeader = _warp.Header; WarpAnchor = _warp.Anchor; WarpHeight = _warp.Height;
            _suppress = false;
            RefreshMarkers();
        }

        private void LoadTrigger(int i)
        {
            _trig = (_file != null && i >= 0 && i < _file.Triggers.Count) ? _file.Triggers[i] : null;
            OnPropertyChanged(nameof(HasTrig));
            if (_trig == null) { if (_current is Trigger) _current = null; return; }
            _suppress = true;
            LoadPosition(_trig);
            TrScript = _trig.ScriptNumber; TrWidth = _trig.WidthX; TrHeight = _trig.HeightY;
            TrVarValue = _trig.ExpectedVarValue; TrVar = _trig.VariableWatched;
            _suppress = false;
            OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange)); OnPropertyChanged(nameof(TrScriptCommonInfo)); OnPropertyChanged(nameof(TrScriptHasCommonInfo)); OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptGenericWarningVisible));
            RefreshMarkers();
        }

        // ── Add / remove ────────────────────────────────────────────────────────────
        // New events go on the cell the view is showing, not (0,0) which is usually another header's map.
        private (int x, int y) NewEventCell()
        {
            if (_current != null) return (_current.XMatrixPosition, _current.YMatrixPosition);
            HashSet<(int x, int y)> cells = HeaderCells();
            if (cells != null)
                foreach ((int x, int y) c in cells) return c;
            return (0, 0);
        }

        public void AddSpawnable() { if (_file == null) return; (int cx, int cy) = NewEventCell(); _file.Spawnables.Add(new Spawnable(cx, cy)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.Spawnables.Count - 1; }
        public void RemoveSpawnable() { if (_file == null || _selSpawn < 0 || _selSpawn >= _file.Spawnables.Count) return; _file.Spawnables.RemoveAt(_selSpawn); RefreshLists(); Dirty(); SelectedSpawnableIndex = -1; RefreshMarkers(); }
        public void AddOverworld()
        {
            if (_file == null) return;
            (int cx, int cy) = NewEventCell();
            _file.Overworlds.Add(new Overworld(FreeOverworldId(), cx, cy));
            RefreshLists(); Dirty(); SelectedOverworldIndex = _file.Overworlds.Count - 1;
        }
        public void RemoveOverworld() { if (_file == null || _selOw < 0 || _selOw >= _file.Overworlds.Count) return; _file.Overworlds.RemoveAt(_selOw); RefreshLists(); Dirty(); SelectedOverworldIndex = -1; RefreshMarkers(); }
        public void AddWarp() { if (_file == null) return; (int cx, int cy) = NewEventCell(); _file.Warps.Add(new Warp(cx, cy)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.Warps.Count - 1; }
        public void RemoveWarp() { if (_file == null || _selWarp < 0 || _selWarp >= _file.Warps.Count) return; _file.Warps.RemoveAt(_selWarp); RefreshLists(); Dirty(); SelectedWarpIndex = -1; RefreshMarkers(); }
        public void AddTrigger() { if (_file == null) return; (int cx, int cy) = NewEventCell(); _file.Triggers.Add(new Trigger(cx, cy)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.Triggers.Count - 1; }
        public void RemoveTrigger() { if (_file == null || _selTrig < 0 || _selTrig >= _file.Triggers.Count) return; _file.Triggers.RemoveAt(_selTrig); RefreshLists(); Dirty(); SelectedTriggerIndex = -1; RefreshMarkers(); }

        // ── Duplicate selected (copy ctors) ──────────────────────────────────────────────
        public void DuplicateSpawnable() { if (_file == null || _spawn == null) return; _file.Spawnables.Add(new Spawnable(_spawn)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.Spawnables.Count - 1; }
        // Scripts and hide flags find an overworld by its id, so a copy needs its own.
        public void DuplicateOverworld() { if (_file == null || _ow == null) return; _file.Overworlds.Add(new Overworld(_ow) { OwID = (ushort)FreeOverworldId() }); RefreshLists(); Dirty(); SelectedOverworldIndex = _file.Overworlds.Count - 1; }

        private int FreeOverworldId()
        {
            int id = 0;
            while (_file.Overworlds.Any(o => o.OwID == id)) id++;
            return id;
        }
        public void DuplicateWarp() { if (_file == null || _warp == null) return; _file.Warps.Add(new Warp(_warp)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.Warps.Count - 1; }
        public void DuplicateTrigger() { if (_file == null || _trig == null) return; _file.Triggers.Add(new Trigger(_trig)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.Triggers.Count - 1; }

        /// <summary>Follow the selected warp to its destination: switch to that header's event file.</summary>
        public void TestWarp()
        {
            if (_warp == null) return;
            try
            {
                MapHeader h = MapHeader.GetMapHeader(_warp.Header);
                if (h == null) { StatusText = $"Destination header {_warp.Header} not found."; return; }
                int dest = h.EventFileID;
                if (dest < 0 || dest >= EventNames.Count) { StatusText = $"Header {_warp.Header} → event file {dest} (out of range)."; return; }
                SelectedEventIndex = dest;
                StatusText = $"Warp leads to header {_warp.Header} → event file {dest}.";
            }
            catch (Exception ex) { StatusText = "Test warp failed: " + ex.Message; }
        }

        // ── Sort overworlds by OW id ─────────────────────────────────────────────────────
        public void SortOverworldsAsc() { if (_file == null) return; _file.Overworlds.Sort((a, b) => a.OwID.CompareTo(b.OwID)); RefreshLists(); Dirty(); }
        public void SortOverworldsDesc() { if (_file == null) return; _file.Overworlds.Sort((a, b) => b.OwID.CompareTo(a.OwID)); RefreshLists(); Dirty(); }

        // ── Event file add / remove ──────────────────────────────────────────────────────
        public void AddEventFile()
        {
            try
            {
                int newId = _archive.Add(new EventFile().ToByteArray());
                EventNames.Add("Event File " + newId);
                SelectedEventIndex = newId;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Added event file {newId}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Couldn't add event file:\n{ex.Message}", "Event editor error"); }
        }

        public async Task RemoveLastEventFileAsync()
        {
            int count = EventNames.Count;
            if (count == 0) return;
            int last = count - 1;
            // A header still pointing at the file would load garbage, so it stays while one does.
            List<(ushort Header, string Name)> uses = HeaderLinks.HeadersLinking(HeaderLinks.Kind.Event, last);
            if (uses.Count > 0)
            {
                await DialogHelper.ShowInfoAsync($"Event file {last} stays: these headers still use it." + Environment.NewLine + HeaderLinks.Describe(uses)
                    + Environment.NewLine + "Point them at another event file first.", "Event editor");
                return;
            }
            if (!await DialogHelper.AskYesNoAsync($"Delete the last event file ({last})?", "Delete event file?")) return;
            try
            {
                // The edits belong to the file being deleted.
                if (_selectedIndex == last) { SetClean(); SelectedEventIndex = last - 1; }
                _archive.RemoveLast();
                EventNames.RemoveAt(last);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Removed event file {last}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Couldn't remove event file:\n{ex.Message}", "Event editor error"); }
        }

        // ── 3D map view + event markers ─────────────────────────────────────────────────

        /// <summary>
        /// Builds the reverse map: event-file index → (matrix, area data) via the header that
        /// references it (<see cref="MapHeader.EventFileID"/>). This is the real ROM linkage
        /// the WinForms editor uses to pick the correct map + texture packs for an event file.
        /// </summary>
        private static Dictionary<int, (ushort, byte, ushort, ushort)> BuildEventHeaderLookup()
        {
            Dictionary<int, (ushort, byte, ushort, ushort)> lookup = new Dictionary<int, (ushort, byte, ushort, ushort)>();
            try
            {
                int headerCount = GetHeaderCount();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        MapHeader header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        if (!lookup.ContainsKey(header.EventFileID))
                            lookup[header.EventFileID] = (header.MatrixID, header.AreaDataID, header.ScriptFileID, h);
                    }
                    catch { /* skip bad header */ }
                }
            }
            catch (Exception ex) { AppLogger.Error("Event→header lookup failed: " + ex.Message); }
            return lookup;
        }

        /// <summary>Resolves the matrix + area for the loaded event file, then renders the whole matrix.</summary>
        private void ResolveMatrixForFile(int eventIndex)
        {
            _matrix = null; _areaDataId = 0; _matrixId = -1; _headerId = -1;
            _collisionCache.Clear();
            int pairedScriptFileId = -1;
            try
            {
                if (_eventToHeader != null && _eventToHeader.TryGetValue(eventIndex, out (ushort matrixId, byte areaId, ushort scriptFileId, ushort headerId) hdr))
                {
                    _matrixId = hdr.Item1;
                    _matrix = new GameMatrix(hdr.Item1);
                    _areaDataId = hdr.Item2;
                    pairedScriptFileId = hdr.Item3;
                    _headerId = hdr.Item4;
                }
            }
            catch (Exception ex) { AppLogger.Error("Matrix resolve failed: " + ex.Message); }
            // A file no header uses comes back on the matrix and textures picked for it last time, both together.
            (int Matrix, int Area) chosen = (-1, 0);
            bool hasChoice = _headerId < 0 && _viewChoices.TryGetValue(eventIndex, out chosen);
            if (_headerId < 0) FillViewMatrixNames();
            _settingView = true;
            ViewMatrixIndex = hasChoice ? chosen.Matrix : -1;
            ViewArea = hasChoice ? chosen.Area : 0;
            _settingView = false;
            OnPropertyChanged(nameof(NeedsViewChoice));
            if (hasChoice) { ShowOnChosen(); PopulateAvailableScripts(pairedScriptFileId); return; }
            OnPropertyChanged(nameof(GridToggleEnabled));
            PopulateAvailableScripts(pairedScriptFileId);
            DisplayMap();
        }

        // ── A file no header uses is drawn on whichever matrix and textures the user picks. The header-driven
        // parts (scope, camera, scripts) stay as they are for a file with no header. ──────────────────────────
        public bool NeedsViewChoice => _headerId < 0;
        private readonly Dictionary<int, (int Matrix, int Area)> _viewChoices = new Dictionary<int, (int Matrix, int Area)>();
        private bool _settingView;
        // A list, not a number box: "nothing picked yet" has to be a state the box keeps.
        public ObservableCollection<string> ViewMatrixNames { get; } = new ObservableCollection<string>();
        private int _viewMatrixIndex = -1;
        private decimal _viewArea;
        public int ViewMatrixIndex { get => _viewMatrixIndex; set { if (Set(ref _viewMatrixIndex, value) && !_settingView) ShowOnChosen(); } }
        public decimal ViewArea { get => _viewArea; set { if (Set(ref _viewArea, value) && !_settingView) ShowOnChosen(); } }

        private void FillViewMatrixNames()
        {
            int count = Filesystem.GetMatrixCount();
            if (ViewMatrixNames.Count == count) return;
            ViewMatrixNames.Clear();
            for (int i = 0; i < count; i++)
            {
                string name;
                try { name = new GameMatrix(i).ToString(); }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is EndOfStreamException) { name = "Matrix " + i; }
                ViewMatrixNames.Add(name);
            }
        }
        // Bound before any ROM is open, when there are no area data files to count.
        public decimal MaxViewArea => RomInfo.GameDirs?.ContainsKey(RomInfo.DirNames.AreaData) == true
            ? Math.Clamp(Filesystem.GetAreaDataCount() - 1, 0, byte.MaxValue) : 0;

        private void ShowOnChosen()
        {
            if (_headerId >= 0 || _viewMatrixIndex < 0) return;
            int matrixId = _viewMatrixIndex;
            try { _matrix = new GameMatrix(matrixId); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is EndOfStreamException)
            {
                StatusText = $"Matrix {matrixId} could not be read: {ex.Message}";
                return;
            }
            _matrixId = matrixId;
            _areaDataId = (byte)_viewArea;
            if (_selectedIndex >= 0) _viewChoices[_selectedIndex] = (matrixId, _areaDataId);
            _collisionCache.Clear();
            OnPropertyChanged(nameof(GridToggleEnabled));
            DisplayMap();
        }

        // ── Scripts available to this event file (the header's paired script file) ────────
        /// <summary>The scripts defined in the header's paired script file (via <see cref="MapHeader.ScriptFileID"/>),
        /// so Overworld/Trigger/Spawnable "Script" fields can be picked from a dropdown of what's actually
        /// callable here instead of a free-form number. Values are each script's <c>manualUserID</c>, the
        /// number these events' <c>scriptNumber</c> fields reference, after "No script" for 0.</summary>
        public ObservableCollection<string> AvailableScripts { get; } = new ObservableCollection<string>();
        private readonly List<uint> _availableScriptIds = new List<uint>();

        private int _pairedScriptFileId = -1;
        private void PopulateAvailableScripts(int scriptFileId)
        {
            _pairedScriptFileId = scriptFileId;
            AvailableScripts.Clear();
            _availableScriptIds.Clear();
            _availableScriptIds.Add(EventFile.NoScript);
            AvailableScripts.Add("No script");
            if (scriptFileId >= 0)
            {
                try
                {
                    ScriptFile scriptFile = new ScriptFile(scriptFileId);
                    foreach (ScriptCommandContainer container in scriptFile.AllScripts)
                    {
                        _availableScriptIds.Add(container.ManualUserID);
                        AvailableScripts.Add($"Script {container.ManualUserID} ({container.Commands?.Count ?? 0} cmds)");
                    }
                }
                catch (Exception ex) { AppLogger.Error("PopulateAvailableScripts: " + ex.Message); }
            }
            OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange)); OnPropertyChanged(nameof(TrScriptCommonInfo)); OnPropertyChanged(nameof(TrScriptHasCommonInfo)); OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptGenericWarningVisible));
            OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange)); OnPropertyChanged(nameof(SpScriptCommonInfo)); OnPropertyChanged(nameof(SpScriptHasCommonInfo)); OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptGenericWarningVisible)); RaiseSpHidden();
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible));
        }

        private int IndexOfAvailableScript(decimal rawValue) => _availableScriptIds.IndexOf((uint)rawValue);

        private int _matrixId = -1;
        private int _headerId = -1;

        /// <summary>
        /// Renders only the maps this event file's events actually sit on (the cells they
        /// occupy in the header's matrix), stitched together. This keeps the view focused on
        /// the maps the file belongs to rather than loading an entire (possibly world-sized)
        /// matrix. Each cell's tileset is resolved through its header section / the file's area.
        /// </summary>
        private void DisplayMap()
        {
            Model3D = null;
            try
            {
                if (_matrix == null)
                {
                    MapInfo = "No header uses this event file, so the game never loads it. Pick a matrix and area data to preview it.";
                    MapLoaded?.Invoke(this, EventArgs.Empty); RefreshMarkers(); return;
                }

                // Prefer the cells this event file's own header owns, the same rule the map editor's "This
                // header" view uses.
                ISet<(int x, int y)> include = HeaderCells();
                string scopeName = "this header";

                // The catch-all header owns every square nobody walks on, hundreds of them, and has
                // nothing in it to edit. Stitching all that is a long wait for an empty answer.
                if (include != null && FieldCatchAllHeader.IsCatchAll(include.Count))
                {
                    Model3D = null;
                    IsCatchAllHeader = true;
                    StatusText = FieldCatchAllHeader.Explanation;
                    MapLoaded?.Invoke(this, EventArgs.Empty);
                    return;
                }
                IsCatchAllHeader = false;

                if (include == null)
                {
                    // No headers section to go on: small matrices render whole, big ones fall back to
                    // the events' own bounding box so a world-sized matrix isn't loaded entirely.
                    int total = _matrix.Width * _matrix.Height;
                    include = total <= 256 ? null : EventCells();
                    scopeName = "events";
                }

                _builtModelObjects = ModelObjectsNow();
                Model3D = MatrixSceneBuilder.Build(_matrix, _areaDataId, GameFamily, areaForMap: null, includeCells: include, mode: StitchMode,
                                                   extrasForCell: ModelObjectsOn);
                string scope = include == null ? "full" : $"{include.Count} maps, {scopeName}";
                MapInfo = Model3D != null
                    ? $"Matrix {_matrixId}  ·  {_matrix.Width}×{_matrix.Height} ({scope})  ·  area {_areaDataId}"
                    : $"Matrix {_matrixId}: no renderable maps.";
            }
            catch (Exception ex)
            {
                MapInfo = "Map render failed: " + ex.Message;
                AppLogger.Error("Event map render failed: " + ex.Message);
            }
            MapLoaded?.Invoke(this, EventArgs.Empty);
            RefreshMarkers();
        }

        /// <summary>
        /// The matrix cells belonging to this event file's header, or null when the matrix has no
        /// headers section to identify them by.
        /// </summary>
        private HashSet<(int x, int y)> HeaderCells() => _matrix?.CellsOfHeader(_headerId);

        /// <summary>The box around every cell with an event, so the maps between them stitch without holes; capped against a stray far-off event.</summary>
        private HashSet<(int x, int y)> EventCells()
        {
            HashSet<(int x, int y)> set = new HashSet<(int x, int y)>();
            if (_file == null || _matrix == null) return set;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            void Note(Event e)
            {
                int x = e.XMatrixPosition, y = e.YMatrixPosition;
                if (x < 0 || y < 0 || x >= _matrix.Width || y >= _matrix.Height) return;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            foreach (Overworld e in _file.Overworlds) Note(e);
            foreach (Warp e in _file.Warps) Note(e);
            foreach (Trigger e in _file.Triggers) Note(e);
            foreach (Spawnable e in _file.Spawnables) Note(e);
            if (maxX < minX) return set;   // no events

            const int MaxSpan = 12;        // cap the bounding box per axis (keeps loads sane)
            if (maxX - minX > MaxSpan) maxX = minX + MaxSpan;
            if (maxY - minY > MaxSpan) maxY = minY + MaxSpan;

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    set.Add((x, y));
            return set;
        }

        // Per-type marker colours (RGB 0..1).
        private static (float r, float g, float b) MarkerColor(int type) => type switch
        {
            0 => (0.25f, 0.95f, 0.35f),   // overworld  → green
            1 => (1.00f, 0.62f, 0.10f),   // warp       → orange
            2 => (0.95f, 0.25f, 0.90f),   // trigger    → magenta
            _ => (0.20f, 0.85f, 0.95f),   // spawnable  → cyan
        };

        private const int MapTiles = 32;

        /// <summary>
        /// Rebuilds the event overlay: warps/triggers/spawnables as flat colour quads and
        /// overworlds as upright textured billboards (their real sprite). Everything is placed
        /// by matrix cell + tile + z so it sits where it will be in-game.
        /// </summary>
        public void RefreshMarkers()
        {
            MarkerMesh = null; MarkerVertexCount = 0;
            List<NsbmdGlControl.SpriteInstance> sprites = new List<NsbmdGlControl.SpriteInstance>();
            NsbmdRenderModel m = Model3D;
            if (_file != null && m != null && m.CellStrideX != 0)
            {
                float tileX = m.CellStrideX / MapTiles;
                float tileZ = m.CellStrideZ / MapTiles;

                (float x, float z) Cell(Event e) => EventCellRaw(m, e);
                // The map is a 3D model whose top plane is the walkable ground. Place each event on
                // the sampled surface at its tile (so it sits ON the plane, not at world-Y 0 which is
                // below it). For overworld/spawnable Y, the field is a height-lookup hint, not an additive lift.
                float EventY(float rawX, float rawZ, Event e) => EventSurfaceY(m, rawX, rawZ, e);

                (float x, float y, float z) Foot(Event e)
                {
                    (float rawX, float rawZ) = Cell(e);
                    return m.ToNormalized(rawX, EventY(rawX, rawZ, e), rawZ);
                }

                List<float> v = new List<float>(256);
                void Quad(Event e, (float r, float g, float b) col)
                {
                    bool sel = ReferenceEquals(e, _current);
                    (float, float, float) c = sel ? (1f, 1f, 1f) : col;
                    float half = (sel ? 0.46f : 0.40f);
                    (float rawX, float rawZ) = Cell(e);
                    AddMarker(v, m, rawX, EventY(rawX, rawZ, e), rawZ, half * tileX, half * tileZ, c);
                }

                if (_showGrid && _matrix != null && _matrix.Width * _matrix.Height <= 4)
                {
                    int n = MapFile.MapSize;
                    float inset = tileX * 0.07f;
                    for (int gcy = 0; gcy < _matrix.Height; gcy++)
                        for (int gcx = 0; gcx < _matrix.Width; gcx++)
                        {
                            if (!m.TryCellPlacement(gcx, gcy, out NsbmdRenderModel.CellPlacement gp)) continue;
                            long key = ((long)gcy << 32) | (uint)gcx;
                            if (!_collisionCache.TryGetValue(key, out byte[,] col))
                            {
                                col = null;
                                try
                                {
                                    int map = _matrix.Maps[gcy, gcx];
                                    if (map != GameMatrix.EMPTY) col = new MapFile(map, GameFamily, false, false).Collisions;
                                }
                                catch { }   // a map that can't be read has no collisions to show
                                _collisionCache[key] = col;
                            }
                            if (col == null) continue;
                            byte oob = col[n - 1, n - 1];
                            for (int ty = 0; ty < n; ty++)
                                for (int tx = 0; tx < n; tx++)
                                {
                                    if (col[ty, tx] != oob) continue;
                                    float x0 = gp.OriginX + tx * tileX;
                                    float z0 = gp.OriginZ + ty * tileZ;
                                    float cx = x0 + tileX * 0.5f, cz = z0 + tileZ * 0.5f;
                                    if (!m.TryBdhcSurfaceY(gcx, gcy, cx, cz, 0f, out float yc)) yc = m.SurfaceY(cx, cz);
                                    yc += 0.01f;
                                    AddFlatQuad(v, m, x0 + inset, z0 + inset, x0 + tileX - inset, z0 + tileZ - inset, yc, (0.20f, 0.55f, 0.95f));
                                }
                        }
                }

                if (_showWarp) foreach (Warp e in _file.Warps) Quad(e, MarkerColor(1));
                if (_showTrig) foreach (Trigger e in _file.Triggers) Quad(e, MarkerColor(2));
                if (_showSpawn) foreach (Spawnable e in _file.Spawnables) Quad(e, MarkerColor(3));

                // Overworlds → real sprite billboards (foot anchored on the surface). Selected
                // overworlds also get a white ground ring so the selection is obvious.
                float spriteH = tileX * m.Scale * 1.6f;
                bool modelsAsBuilt = ModelObjectsAsBuilt;
                if (!modelsAsBuilt) RebuildForModelObjectsSoon();
                if (_showOw)
                    foreach (Overworld ow in _file.Overworlds)
                    {
                        bool sel = ReferenceEquals(ow, _current);
                        if (sel) Quad(ow, (1f, 1f, 1f));
                        // Already in the scene as its model; a ring marks it when picked.
                        if (Overworld3DObjects.IsModel(ow.OverlayTableEntry) && modelsAsBuilt) continue;

                        OverworldSprites.SpritePixels pix = OverworldSprites.Get(ow.OverlayTableEntry, (ushort)Math.Max((short)0, ow.Orientation));
                        (float x, float y, float z) foot = Foot(ow);
                        if (pix != null && pix.Width > 0 && pix.Height > 0)
                        {
                            float halfH = spriteH * 0.5f;
                            // A script picks a dynamic overworld's sprite while playing; the outline says the picture is a stand-in.
                            if (IsVariableOverworld(ow.OverlayTableEntry))
                            {
                                int plain = pix.Height;
                                pix = OverworldSprites.Outlined(pix);
                                halfH *= pix.Height / (float)plain;
                            }
                            float halfW = halfH * (pix.Width / (float)pix.Height);
                            sprites.Add(new NsbmdGlControl.SpriteInstance
                            {
                                Cx = foot.x,
                                Cy = foot.y + halfH,
                                Cz = foot.z,
                                HalfW = halfW,
                                HalfH = halfH,
                                Rgba = pix.Rgba,
                                Width = pix.Width,
                                Height = pix.Height,
                            });
                        }
                        else Quad(ow, MarkerColor(0));   // fall back to a green quad if no sprite
                    }

                MarkerMesh = v.ToArray();
                MarkerVertexCount = v.Count / 8;
            }
            Sprites = sprites;
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            SpritesChanged?.Invoke(this, EventArgs.Empty);
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);   // keep the move-gizmo on the selected event
        }

        // ── Event world placement (shared by markers + the move gizmo) ────────────────────
        private (float x, float z) EventCellRaw(NsbmdRenderModel m, Event e)
        {
            if (m.TryCellPlacement(e.XMatrixPosition, e.YMatrixPosition, out NsbmdRenderModel.CellPlacement p))
                return (p.OriginX + (e.XMapPosition + 0.5f) / MapTiles * p.Width,
                        p.OriginZ + (e.YMapPosition + 0.5f) / MapTiles * p.Height);
            return (m.CellBaseX + (e.XMatrixPosition + (e.XMapPosition + 0.5f) / MapTiles) * m.CellStrideX,
                    m.CellBaseZ + (e.YMatrixPosition + (e.YMapPosition + 0.5f) / MapTiles) * m.CellStrideZ);
        }

        private float EventSurfaceY(NsbmdRenderModel m, float rawX, float rawZ, Event e)
        {
            // A height hint only; BDHC/mesh lookup chooses the actual floor near it.
            float yHint = HeightHint(e);
            if (m.TryBdhcSurfaceY(e.XMatrixPosition, e.YMatrixPosition, rawX, rawZ, yHint, out float bdhcY)) return bdhcY;
            return e.ZPosition == 0 ? m.SurfaceY(rawX, rawZ) : m.SurfaceY(rawX, rawZ, yHint);
        }

        // ── Animated preview handoff ──────────────────────────────────────────────────────
        /// <summary>The events being edited, for the preview window to animate.</summary>
        public EventFile Events => _file;

        /// <summary>The camera number this header asks for. </summary>
        public int CameraId
        {
            get
            {
                try { return _headerId < 0 ? 0 : MapHeader.GetMapHeader((ushort)_headerId).CameraAngleID; }
                catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.CameraId", ex); return 0; }
            }
        }

        /// <summary>The two music numbers this header carries, day and night.</summary>
        public int MusicDayId => HeaderValue(h => h.MusicDayID);
        public int MusicNightId => HeaderValue(h => h.MusicNightID);

        private int HeaderValue(Func<MapHeader, int> pick)
        {
            try { return _headerId < 0 ? 0 : pick(MapHeader.GetMapHeader((ushort)_headerId)); }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.HeaderValue", ex); return 0; }
        }

        /// <summary>The movements in this header's script file, so the preview can play them out.</summary>
        public IReadOnlyList<ScriptAction> ActionsFor(int movementNumber)
        {
            try
            {
                MapHeader header = _headerId < 0 ? null : MapHeader.GetMapHeader((ushort)_headerId);
                if (header == null) return null;
                return ScriptWalker.ActionsById(new ScriptFile(header.ScriptFileID)?.AllActions)?.Invoke(movementNumber);
            }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.ActionsFor", ex); return null; }
        }

        /// <summary>The area the scene belongs to, which is what names its terrain animation.</summary>
        public AreaData Area
        {
            get { try { return new AreaData(_areaDataId); } catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.Area", ex); return null; } }
        }

        /// <summary>Where the maps this event file spans are closed off, for the preview to walk against.</summary>
        public MapCollisionGrid Collision
        {
            get
            {
                MapCollisionGrid grid = new MapCollisionGrid();
                if (_matrix == null) return grid;
                foreach ((int x, int y) in (HeaderCells() ?? (ISet<(int x, int y)>)EventCells()))
                {
                    long key = ((long)y << 32) | (uint)x;
                    byte[,] col = null, types = null;
                    try
                    {
                        int map = _matrix.Maps[y, x];
                        if (map != GameMatrix.EMPTY)
                        {
                            MapFile mf = new MapFile(map, GameFamily, false, false);
                            col = mf.Collisions;
                            types = mf.Types;
                        }
                    }
                    catch { }   // a map that can't be read has no collisions to show
                    _collisionCache[key] = col;
                    grid.Add(x, y, col);
                    grid.AddTypes(x, y, types);
                }
                return grid;
            }
        }

        // ── Coordinates under the pointer ───────────────────────────────────────────
        private string _hoverCoords = "";

        /// <summary>The tile under the pointer, empty when it is over none. The cell is part of it because
        /// a local tile means nothing on its own once the view stitches several maps.</summary>
        public string HoverCoordsText { get => _hoverCoords; private set => Set(ref _hoverCoords, value); }

        /// <summary>Reads the tile under a screen point through the viewport's own projection, so it
        /// follows the camera rather than assuming a top-down view.</summary>
        public void UpdateHoverCoords(double px, double py,
                                      Func<float, float, float, (bool ok, float sx, float sy)> project)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || project == null) { HoverCoordsText = ""; return; }

            List<FieldTilePicker.CellQuad> cells = HoverCells();
            if (cells.Count == 0) { HoverCoordsText = ""; return; }

            (bool, float, float) ProjectRaw(float rawX, float rawZ)
            {
                (float nx, float ny, float nz) = m.ToNormalized(rawX, m.SurfaceY(rawX, rawZ), rawZ);
                return project(nx, ny, nz);
            }

            if (!FieldTilePicker.TileAtScreen(cells, ProjectRaw, px, py,
                                              out int cellX, out int cellY, out int col, out int row, MapTiles))
            {
                HoverCoordsText = "";
                return;
            }

            // Global is what an event stores; local is that tile within its own map.
            HoverCoordsText = $"Matrix {cellX}, {cellY}    Local {col}, {row}    Global "
                            + $"{cellX * MapTiles + col}, {cellY * MapTiles + row}";
        }

        public void ClearHoverCoords() => HoverCoordsText = "";

        /// <summary>The cells the pointer could be over, placed the way the scene placed them.</summary>
        private List<FieldTilePicker.CellQuad> HoverCells()
        {
            List<FieldTilePicker.CellQuad> list = new List<FieldTilePicker.CellQuad>();
            NsbmdRenderModel m = Model3D;
            if (m == null) return list;

            ISet<(int x, int y)> cells = HeaderCells() ?? (ISet<(int x, int y)>)EventCells();
            if (cells == null) return list;

            foreach ((int x, int y) in cells)
            {
                if (m.TryCellPlacement(x, y, out NsbmdRenderModel.CellPlacement p))
                    list.Add(new FieldTilePicker.CellQuad(x, y, p.OriginX, p.OriginZ, p.Width, p.Height));
                else if (m.CellStrideX != 0)
                    list.Add(new FieldTilePicker.CellQuad(x, y,
                        m.CellBaseX + x * m.CellStrideX, m.CellBaseZ + y * m.CellStrideZ,
                        m.CellStrideX, m.CellStrideZ));
            }
            return list;
        }

        /// <summary>Where a whole-matrix tile sits in the scene, for standing the player on it.</summary>
        public (float x, float y, float z) TileFoot(float tileX, float tileZ)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null) return (0f, 0f, 0f);

            // Whole tiles pick the cell; the fraction is how far across it the walker has got.
            int cellX = (int)Math.Floor(tileX / MapTiles), cellZ = (int)Math.Floor(tileZ / MapTiles);
            float inX = tileX - cellX * MapTiles, inZ = tileZ - cellZ * MapTiles;

            float rawX, rawZ;
            if (m.TryCellPlacement(cellX, cellZ, out NsbmdRenderModel.CellPlacement p))
            {
                rawX = p.OriginX + (inX + 0.5f) / MapTiles * p.Width;
                rawZ = p.OriginZ + (inZ + 0.5f) / MapTiles * p.Height;
            }
            else
            {
                rawX = m.CellBaseX + (cellX + (inX + 0.5f) / MapTiles) * m.CellStrideX;
                rawZ = m.CellBaseZ + (cellZ + (inZ + 0.5f) / MapTiles) * m.CellStrideZ;
            }

            float rawY = m.TryBdhcSurfaceY(cellX, cellZ, rawX, rawZ, 0f, out float y) ? y : m.SurfaceY(rawX, rawZ);
            return m.ToNormalized(rawX, rawY, rawZ);
        }

        /// <summary>
        /// Builds a walker for one of this header's scripts, with the header's own text archive so message
        /// commands can quote what they actually say.
        /// </summary>
        public ScriptWalker WalkerFor(int scriptNumber)
        {
            try
            {
                // A script id above 2000 does not live in the map's own file at all: the games keep whole
                // separate files for common scripts, trainers, hidden items and the rest, and read the id
                // relative to where that range starts (SetScriptDataSub in script.c).
                CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, scriptNumber);

                int scriptArchive, textArchive;
                if (common.Kind == CommonScriptId.Kind.Resolved)
                {
                    scriptArchive = common.ScriptArchiveId;
                    textArchive = common.TextArchiveId;
                }
                else
                {
                    MapHeader header = MapHeader.GetMapHeader((ushort)_headerId);
                    if (header == null) return null;
                    scriptArchive = header.ScriptFileID;
                    textArchive = header.TextArchiveID;
                }

                ScriptFile file = new ScriptFile(scriptArchive);
                TextArchive text = null;
                try { text = new TextArchive(textArchive); } catch (Exception ex) { AppLogger.Warn("Event editor: script text: " + ex.Message); }

                return new ScriptWalker(file.AllScripts, file.AllFunctions,
                    id => text?.Messages != null && id >= 0 && id < text.Messages.Count ? text.Messages[id] : null,
                    // The file's movements sit alongside its scripts, so a Movement can say what it does.
                    ScriptWalker.ActionsById(file.AllActions));
            }
            catch (Exception ex) { AppLogger.Error("Script walker failed: " + ex.Message); return null; }
        }

        private readonly Dictionary<int, TextArchive> _previewArchives = new Dictionary<int, TextArchive>();
        private readonly Dictionary<int, ScriptSource> _previewCommonScripts = new Dictionary<int, ScriptSource>();

        private string PreviewMessage(int archive, int id)
        {
            if (archive < 0) return null;
            try
            {
                if (!_previewArchives.TryGetValue(archive, out TextArchive text))
                    _previewArchives[archive] = text = new TextArchive(archive);
                return text?.Messages != null && id >= 0 && id < text.Messages.Count ? text.Messages[id] : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.PreviewMessage", ex); _previewArchives[archive] = null; return null; }
        }

        /// <summary>Hands the preview what it needs to follow scripts out of the map's own files.</summary>
        public void ConfigureScriptPreview(ViewModels.Battle.AnimatedPreviewViewModel preview)
        {
            if (preview == null) return;
            _previewArchives.Clear();
            _previewCommonScripts.Clear();

            preview.Family = GameFamily;
            preview.ArchiveText = PreviewMessage;
            preview.MenuText = id => PreviewMessage(FieldMenuEntriesArchive, id);
            preview.SharedArchive = which =>
                which >= 0 && which < RomInfo.FieldSharedMessageArchives.Length ? RomInfo.FieldSharedMessageArchives[which] : -1;
            preview.CommonScripts = id =>
            {
                if (_previewCommonScripts.TryGetValue(id, out ScriptSource cached)) return cached;
                ScriptSource source = null;
                try
                {
                    CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, id);
                    if (common.Kind == CommonScriptId.Kind.Resolved)
                    {
                        ScriptFile file = new ScriptFile(common.ScriptArchiveId);
                        int textArchive = common.TextArchiveId;
                        source = new ScriptSource
                        {
                            Scripts = file.AllScripts,
                            Functions = file.AllFunctions,
                            Messages = m => PreviewMessage(textArchive, m),
                            Actions = ScriptWalker.ActionsById(file.AllActions),
                            StartId = common.ManualUserId,
                        };
                    }
                }
                catch (Exception ex) { AppLogger.Error("Shared script for the preview failed: " + ex.Message); }
                _previewCommonScripts[id] = source;
                return source;
            };
        }

        /// <summary>The number to start the walker at. </summary>
        public int WalkerStartId(int scriptNumber)
        {
            CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, scriptNumber);
            return common.Kind == CommonScriptId.Kind.Resolved ? common.ManualUserId : scriptNumber;
        }

        /// <summary>Which file a script id really lives in, for the viewer to say so.</summary>
        public string ScriptHome(int scriptNumber)
        {
            CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, scriptNumber);
            switch (common.Kind)
            {
                case CommonScriptId.Kind.Resolved:
                    return $"Script {scriptNumber} lives in shared script file {common.ScriptArchiveId}, "
                         + $"as its script {common.ManualUserId}.";
                default:
                    return null;
            }
        }

        /// <summary>Where an event's feet sit in the scene, the same placement the markers use.</summary>
        public (float x, float y, float z) EventFoot(Event e)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || e == null) return (0f, 0f, 0f);
            (float rx, float rz) = EventCellRaw(m, e);
            return m.ToNormalized(rx, EventSurfaceY(m, rx, rz, e), rz);
        }

        // ── 3D edit mode (drag the selected event with the translate gizmo) ───────────────
        private bool _editMode3D;
        public bool EditMode3D
        {
            get => _editMode3D;
            set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;
        public float ModelScale => Model3D?.Scale ?? 1f;

        private (int x, int z)? _walkTile;

        /// <summary>The tile the dragged player is hovering over. </summary>
        public (int x, int z)? WalkTile
        {
            get => _walkTile;
            set
            {
                if (_walkTile == value) return;
                _walkTile = value;
                BuildWalkTint();
                WalkTileChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler WalkTileChanged;

        /// <summary>The tile grid and colours the shader tints the ground with. Off when nothing is hovered.</summary>
        public bool WalkTintOn { get; private set; }
        public float WalkTintStrength => 0.55f;
        public float WalkTintOx { get; private set; }
        public float WalkTintOz { get; private set; }
        public float WalkTintSx { get; private set; }
        public float WalkTintSz { get; private set; }
        public byte[] WalkTintRgba { get; private set; }

        /// <summary>
        /// A tile grid for the map the hovered tile belongs to, with every texel clear except that one.
        /// </summary>
        private void BuildWalkTint()
        {
            WalkTintOn = false;
            NsbmdRenderModel m = Model3D;
            if (_walkTile == null || m == null || m.CellStrideX == 0) return;

            int n = MapFile.MapSize;
            int gcx = FloorDiv(_walkTile.Value.x, n), gcy = FloorDiv(_walkTile.Value.z, n);
            if (!m.TryCellPlacement(gcx, gcy, out NsbmdRenderModel.CellPlacement cp)) return;

            int tx = _walkTile.Value.x - gcx * n, ty = _walkTile.Value.z - gcy * n;
            if (tx < 0 || ty < 0 || tx >= 32 || ty >= 32) return;

            byte[] rgba = new byte[32 * 32 * 4];        // all clear to start, so nothing else is touched
            int i = (ty * 32 + tx) * 4;
            rgba[i] = 255; rgba[i + 1] = 214; rgba[i + 2] = 79; rgba[i + 3] = 255;

            float tsx = cp.Width / n, tsz = cp.Height / n;
            WalkTintOx = (cp.OriginX - m.Cx) * m.Scale;
            WalkTintOz = (cp.OriginZ - m.Cz) * m.Scale;
            WalkTintSx = tsx * m.Scale;
            WalkTintSz = tsz * m.Scale;
            WalkTintRgba = rgba;
            WalkTintOn = true;
        }

        /// <summary>The tile the selected event stands on, or null when nothing is selected.</summary>
        public (int x, int z)? SelectedEventTile => _current == null
            ? ((int, int)?)null
            : (FieldInteraction.TileX(_current), FieldInteraction.TileZ(_current));

        public bool TrySelectedEventAnchorNorm(out float nx, out float ny, out float nz)
            => EventAnchorNorm(_current, out nx, out ny, out nz);

        private bool EventAnchorNorm(Event e, out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            NsbmdRenderModel m = Model3D;
            if (m == null || e == null) return false;
            (float rx, float rz) = EventCellRaw(m, e);
            float ry = EventSurfaceY(m, rx, rz, e);
            (float a, float b, float c) = m.ToNormalized(rx, ry, rz);
            nx = a; ny = b; nz = c;
            return true;
        }

        /// <summary>All visible event anchors (type 0=ow,1=warp,2=trigger,3=spawnable) in normalized
        /// space, so the view can pick the nearest one under the cursor.</summary>
        public IEnumerable<(int type, int index, float nx, float ny, float nz)> EventAnchorsNorm()
        {
            if (_file == null || Model3D == null) yield break;
            if (_showOw) for (int i = 0; i < _file.Overworlds.Count; i++) if (EventAnchorNorm(_file.Overworlds[i], out float x, out float y, out float z)) yield return (0, i, x, y, z);
            if (_showWarp) for (int i = 0; i < _file.Warps.Count; i++) if (EventAnchorNorm(_file.Warps[i], out float x, out float y, out float z)) yield return (1, i, x, y, z);
            if (_showTrig) for (int i = 0; i < _file.Triggers.Count; i++) if (EventAnchorNorm(_file.Triggers[i], out float x, out float y, out float z)) yield return (2, i, x, y, z);
            if (_showSpawn) for (int i = 0; i < _file.Spawnables.Count; i++) if (EventAnchorNorm(_file.Spawnables[i], out float x, out float y, out float z)) yield return (3, i, x, y, z);
        }

        public void SelectEvent(int type, int index)
        {
            switch (type)
            {
                case 0: SelectedOverworldIndex = index; break;
                case 1: SelectedWarpIndex = index; break;
                case 2: SelectedTriggerIndex = index; break;
                case 3: SelectedSpawnableIndex = index; break;
            }
            // Picked types number the same as the tabs.
            SelectedTabIndex = type;
        }

        // Event positions are INTEGER tiles (no fraction field), so a mouse drag accumulates sub-tile
        // movement here and only steps the tile when it crosses a whole-tile boundary, exactly how the
        // building gizmo carries its fraction. Reset at the start of each drag via BeginGizmoDrag().
        private float _dragAccumX, _dragAccumY, _dragAccumZ;
        public void BeginGizmoDrag() { _dragAccumX = 0f; _dragAccumY = 0f; _dragAccumZ = 0f; }

        /// <summary>Moves the selected event by a raw-space delta along one world axis (0=X,1=Y,2=Z).
        /// X/Z step the in-map tile in whole-tile increments (carrying the remainder), rolling over into
        /// the neighbouring matrix cell at the map edge; Y edits the event height (zPosition).</summary>
        public void NudgeSelectedEventRaw(int axis, float rawDelta)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || _current == null || rawDelta == 0f) return;
            if (axis == 1)
            {
                if (ZIsFixedPoint)
                {
                    long nz = _current.ZPosition + (long)Math.Round(rawDelta * 262144f);
                    _current.ZPosition = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, nz));
                }
                else
                {
                    // Triggers and spawnables store whole height units, so collect the drag until it makes one.
                    _dragAccumY += rawDelta * 4f;
                    int step = (int)_dragAccumY;
                    if (step == 0) return;
                    _dragAccumY -= step;
                    long nz = _current.ZPosition + step;
                    _current.ZPosition = _current is Trigger
                        ? (int)Math.Clamp(nz, ushort.MinValue, ushort.MaxValue)
                        : (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, nz));
                }
            }
            else if (m.TryCellPlacement(_current.XMatrixPosition, _current.YMatrixPosition, out NsbmdRenderModel.CellPlacement p))
            {
                if (axis == 0)
                {
                    float per = p.Width / MapTiles; if (per <= 0) return;     // raw units per tile
                    _dragAccumX += rawDelta / per;
                    int step = (int)_dragAccumX;                              // whole tiles to move now
                    if (step != 0) { _dragAccumX -= step; StepEventTile(ref step, isX: true); }
                }
                else
                {
                    float per = p.Height / MapTiles; if (per <= 0) return;
                    _dragAccumZ += rawDelta / per;
                    int step = (int)_dragAccumZ;
                    if (step != 0) { _dragAccumZ -= step; StepEventTile(ref step, isX: false); }
                }
            }
            _suppress = true;
            XMap = _current.XMapPosition; YMap = _current.YMapPosition; ZPos = ToDisplayZ(_current.ZPosition);
            OnPropertyChanged(nameof(ZPosNote));
            XMatrix = _current.XMatrixPosition; YMatrix = _current.YMatrixPosition;
            _suppress = false;
            Dirty();
            RefreshMarkers();   // also raises GizmoTargetChanged
        }

        /// <summary>Moves the selected event by whole tiles along X / Z (for arrow keys), rolling over
        /// into the neighbouring matrix cell at the map edges.</summary>
        public void NudgeSelectedEventTiles(int dx, int dz)
        {
            if (_current == null) return;
            if (dx != 0) { int s = dx; StepEventTile(ref s, isX: true); }
            if (dz != 0) { int s = dz; StepEventTile(ref s, isX: false); }
            _suppress = true;
            XMap = _current.XMapPosition; YMap = _current.YMapPosition;
            XMatrix = _current.XMatrixPosition; YMatrix = _current.YMatrixPosition;
            _suppress = false;
            Dirty();
            RefreshMarkers();
        }

        public bool HasSelectedEvent => _current != null;

        /// <summary>Adds <paramref name="step"/> tiles to the event's X (or Y) position, rolling whole
        /// maps over into the neighbouring matrix cell at the 0/31 edges.</summary>
        private void StepEventTile(ref int step, bool isX)
        {
            int tile = isX ? _current.XMapPosition : _current.YMapPosition;
            int mat = isX ? _current.XMatrixPosition : _current.YMatrixPosition;
            tile += step;
            while (tile < 0) { mat--; tile += MapTiles; }
            while (tile >= MapTiles) { mat++; tile -= MapTiles; }
            if (mat < 0) { mat = 0; tile = 0; }
            if (isX) { _current.XMapPosition = (short)tile; _current.XMatrixPosition = (ushort)mat; }
            else { _current.YMapPosition = (short)tile; _current.YMatrixPosition = (ushort)mat; }
        }

        private static void AddMarker(List<float> v, NsbmdRenderModel m, float cx, float cy, float cz,
            float halfX, float halfZ, (float r, float g, float b) col)
        {
            (float x, float y, float z) a = m.ToNormalized(cx - halfX, cy, cz - halfZ);
            (float x, float y, float z) b = m.ToNormalized(cx + halfX, cy, cz - halfZ);
            (float x, float y, float z) c = m.ToNormalized(cx + halfX, cy, cz + halfZ);
            (float x, float y, float z) d = m.ToNormalized(cx - halfX, cy, cz + halfZ);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        /// <summary>Whole-number division that keeps going the right way for tiles left of the origin.</summary>
        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);

        private static void AddFlatQuad(List<float> v, NsbmdRenderModel m, float x0, float z0, float x1, float z1, float y,
            (float r, float g, float b) col)
        {
            (float x, float y, float z) a = m.ToNormalized(x0, y, z0);
            (float x, float y, float z) b = m.ToNormalized(x1, y, z0);
            (float x, float y, float z) c = m.ToNormalized(x1, y, z1);
            (float x, float y, float z) d = m.ToNormalized(x0, y, z1);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        // ── Save / import / export ─────────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null || _selectedIndex < 0) return;
            _archive.Save(_selectedIndex, _file.ToByteArray());
            SetClean();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved event file {_selectedIndex}.";
            AppEvents.RaiseEventFileSaved(this, _selectedIndex);
        }

        /// <summary>
        /// Imports events from a file. Into an empty file everything comes in; otherwise <paramref name="choose"/> picks
        /// which events, and whether this file's own go first.
        /// </summary>
        public async Task ImportAsync(Func<EventFile, Task<EventImportChoice>> choose)
        {
            if (_selectedIndex < 0 || _file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import event file", new[] { filter });
            if (path == null) return;
            EventFile source;
            try { using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read)) source = new EventFile(fs); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Event editor error");
                return;
            }

            EventImportChoice choice;
            if (EventCount(source) == 0)
            {
                if (EventCount(_file) == 0) { StatusText = "That file has no events."; return; }
                if (!await DialogHelper.AskYesNoAsync("That file has no events. Importing it removes every event in this one.", "Import events", _owner)) return;
                choice = EventImportChoice.All(source, clearFirst: true);
            }
            else if (EventCount(_file) == 0) choice = EventImportChoice.All(source, clearFirst: false);
            else
            {
                choice = await choose(source);
                if (choice == null) return;
            }

            ClearSelection();
            if (choice.ClearFirst) { _file.Overworlds.Clear(); _file.Warps.Clear(); _file.Triggers.Clear(); _file.Spawnables.Clear(); }
            int renumbered = 0;
            foreach (int i in choice.Overworlds)
            {
                Overworld ow = source.Overworlds[i];
                // Scripts and hide flags find an overworld by its id, so two can't share one.
                if (_file.Overworlds.Any(o => o.OwID == ow.OwID)) { ow.OwID = (ushort)FreeOverworldId(); renumbered++; }
                _file.Overworlds.Add(ow);
            }
            foreach (int i in choice.Warps) _file.Warps.Add(source.Warps[i]);
            foreach (int i in choice.Triggers) _file.Triggers.Add(source.Triggers[i]);
            foreach (int i in choice.Spawnables) _file.Spawnables.Add(source.Spawnables[i]);
            RefreshLists(); Dirty(); RefreshMarkers();
            int count = choice.Overworlds.Length + choice.Warps.Length + choice.Triggers.Length + choice.Spawnables.Length;
            SaveNotice.Imported($"{count} events", path);
            if (renumbered > 0) StatusText = $"{renumbered} overworlds got a new ID.";
        }

        private static int EventCount(EventFile f) =>
            (f.Overworlds?.Count ?? 0) + (f.Warps?.Count ?? 0) + (f.Triggers?.Count ?? 0) + (f.Spawnables?.Count ?? 0);

        // The previous events must not stay editable through the shared boxes or the gizmo.
        private void ClearSelection()
        {
            _current = null; _spawn = null; _ow = null; _warp = null; _trig = null;
            _selSpawn = _selOw = _selWarp = _selTrig = -1;
            foreach (string n in new[] { nameof(HasSelectedEvent), nameof(HasSpawn), nameof(HasOw), nameof(HasWarp), nameof(HasTrig),
                                      nameof(SelectedSpawnableIndex), nameof(SelectedOverworldIndex), nameof(SelectedWarpIndex), nameof(SelectedTriggerIndex) })
                OnPropertyChanged(n);
        }

        /// <summary>Builds a complete text dump of the current 3D scene: matrix layout, per-cell map
        /// placements (origin/size + the gap/overlap to each neighbour), and every event's exact computed
        /// world position, so a render screenshot can be correlated against the numbers to find stitching/
        /// placement issues. Paired with a PNG of the live render by the view.</summary>
        public string BuildDebugReport()
        {
            StringBuilder sb = new System.Text.StringBuilder();
            NsbmdRenderModel m = Model3D;
            sb.AppendLine("=== DSPRE Event Editor 3D Debug Dump ===");
            sb.AppendLine($"Timestamp:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Event file:  {_selectedIndex}");
            sb.AppendLine($"Matrix id:   {_matrixId}    Area data: {_areaDataId}");
            sb.AppendLine($"Stitch mode: {(_stitchGrid ? "Grid" : "Continuous")} (both now use the DS-true fixed 32-tile grid)");
            if (_matrix != null) sb.AppendLine($"Matrix size: {_matrix.Width} x {_matrix.Height}");
            sb.AppendLine("Units: 1 matrix cell = 32 tiles; Grid stride = 8.0 raw units (0.25/tile).");
            sb.AppendLine();

            if (m == null) { sb.AppendLine("(No 3D model is currently built.)"); return sb.ToString(); }

            sb.AppendLine("--- Model bounds (raw space) ---");
            sb.AppendLine($"Scale={m.Scale:F4}  Center=({m.Cx:F3}, {m.Cy:F3}, {m.Cz:F3})");
            sb.AppendLine($"Raw bounds  X[{m.RawMinX:F3}, {m.RawMaxX:F3}]  Y[{m.RawMinY:F3}, {m.RawMaxY:F3}]  Z[{m.RawMinZ:F3}, {m.RawMaxZ:F3}]");
            sb.AppendLine($"Map  bounds X[{m.MapMinX:F3}, {m.MapMaxX:F3}]  Z[{m.MapMinZ:F3}, {m.MapMaxZ:F3}]");
            sb.AppendLine($"Representative CellStride X={m.CellStrideX:F3} Z={m.CellStrideZ:F3}");
            sb.AppendLine($"DefaultSurfaceY (fallback) = {m.DefaultSurfaceY:F3}");
            sb.AppendLine($"BDHC cells: {m.CellBdhc?.Count ?? 0}");
            sb.AppendLine();

            sb.AppendLine("--- Per-cell map placement (raw space) ---");
            sb.AppendLine("  cell    map | originX  originZ |  width  height | rightGap bottomGap   (gap<0 = OVERLAP)");
            if (_matrix != null && m.CellPlacements != null)
            {
                for (int cy = 0; cy < _matrix.Height; cy++)
                    for (int cx = 0; cx < _matrix.Width; cx++)
                    {
                        int map; try { map = _matrix.Maps[cy, cx]; } catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.BuildDebugReport", ex); continue; }
                        if (map == GameMatrix.EMPTY) continue;
                        if (!m.TryCellPlacement(cx, cy, out NsbmdRenderModel.CellPlacement p)) { sb.AppendLine($"({cx,2},{cy,2}) {map,5} | (no placement in scene)"); continue; }
                        string rg = m.TryCellPlacement(cx + 1, cy, out NsbmdRenderModel.CellPlacement pr) ? (pr.OriginX - (p.OriginX + p.Width)).ToString("F3") : "  -";
                        string bg = m.TryCellPlacement(cx, cy + 1, out NsbmdRenderModel.CellPlacement pb) ? (pb.OriginZ - (p.OriginZ + p.Height)).ToString("F3") : "  -";
                        sb.AppendLine($"({cx,2},{cy,2}) {map,5} | {p.OriginX,7:F3} {p.OriginZ,7:F3} | {p.Width,6:F3} {p.Height,6:F3} | {rg,8} {bg,9}");
                    }
            }
            sb.AppendLine();

            sb.AppendLine("--- Events (exact computed world position) ---");
            sb.AppendLine("type        idx | matrix  map(x,y) | z(fixed) yHint(raw) |   rawX    rawZ  surfaceY source");
            if (_file != null)
            {
                void Dump(string type, System.Collections.IEnumerable list)
                {
                    int i = 0;
                    foreach (Event e in list)
                    {
                        (float rx, float rz) = EventCellRaw(m, e);
                        float yHint = HeightHint(e);
                        bool bdhc = m.TryBdhcSurfaceY(e.XMatrixPosition, e.YMatrixPosition, rx, rz, yHint, out float sy);
                        if (!bdhc) sy = EventSurfaceY(m, rx, rz, e);
                        sb.AppendLine($"{type,-10} {i,3} | ({e.XMatrixPosition},{e.YMatrixPosition})  ({e.XMapPosition,2},{e.YMapPosition,2}) | {e.ZPosition,8} {yHint,7:F3} | {rx,7:F3} {rz,7:F3} {sy,8:F3} {(bdhc ? "bdhc" : "mesh")}");
                        i++;
                    }
                }
                Dump("overworld", _file.Overworlds);
                Dump("warp", _file.Warps);
                Dump("trigger", _file.Triggers);
                Dump("spawnable", _file.Spawnables);
            }

            return sb.ToString();
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export event file", new[] { filter }, $"event_{_selectedIndex:D4}.ev");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _file.ToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Event editor error"); }
        }
    }

    /// <summary>Which events of an imported file to add, by index in that file.</summary>
    public sealed class EventImportChoice
    {
        public bool ClearFirst;
        public int[] Overworlds, Warps, Triggers, Spawnables;

        public static EventImportChoice All(EventFile f, bool clearFirst) => new EventImportChoice
        {
            ClearFirst = clearFirst,
            Overworlds = Enumerable.Range(0, f.Overworlds?.Count ?? 0).ToArray(),
            Warps = Enumerable.Range(0, f.Warps?.Count ?? 0).ToArray(),
            Triggers = Enumerable.Range(0, f.Triggers?.Count ?? 0).ToArray(),
            Spawnables = Enumerable.Range(0, f.Spawnables?.Count ?? 0).ToArray(),
        };
    }
}
