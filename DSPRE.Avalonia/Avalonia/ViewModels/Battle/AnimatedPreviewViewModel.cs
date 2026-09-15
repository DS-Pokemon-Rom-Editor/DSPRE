using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>
    /// Drives the animated preview: a map running the way it would in game, with its water moving and its
    /// people turning and wandering.
    /// </summary>
    public sealed class AnimatedPreviewViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        /// <summary>
        /// The field runs at thirty frames a second, which is what every timing in the games is written
        /// against: a one-frame wait is documented as a thirtieth of a second, and a normal walking step of
        /// eight frames as 3.75 tiles per second.
        /// </summary>
        public const int FramesPerSecond = 30;

        /// <summary>The overworld sprite the player is drawn with (the hero's own entry).</summary>
        public const ushort PlayerSpriteEntry = 0;

        /// <summary>One of the map's people: who they are, how they move, and where they stand.</summary>
        public sealed class Npc
        {
            public Overworld Event;
            public OverworldAnimator Motion;
            public float FootX, FootY, FootZ;   // where it stands, in normalized render space
            /// <summary>Set once a script adds or removes them; until then their flag decides.</summary>
            public bool? OnMap;
        }

        public AnimatedPreviewViewModel()
        {
            GameState.Changed += (_, _) => GameStateChanged();
        }

        private NsbmdRenderModel _scene;
        private EventFile _events;
        private int _seed;
        private bool _indoor;
        private MapCollisionGrid _collision;
        private Func<float, float, (float x, float y, float z)> _tileToWorld;
        private Func<int, ScriptWalker> _walkerFor;
        private Func<int, int> _walkerStartId;
        private Func<int, string> _scriptHome;
        private ScriptWalker _walker;
        private Func<Overworld, (float x, float y, float z)> _footFinder;
        private TextureSrtAnimation _terrain;
        private readonly List<Npc> _npcs = new List<Npc>();
        // material key → the animation driving it and which of its materials to read
        private readonly Dictionary<int, (TextureSrtAnimation anim, int material)> _animatedMaterials
            = new Dictionary<int, (TextureSrtAnimation, int)>();
        // material key → the swapping animation driving it and which of its materials to read
        private readonly Dictionary<int, (TexturePatternAnimation anim, int material)> _swappedMaterials
            = new Dictionary<int, (TexturePatternAnimation, int)>();
        private int _movingBuildings;
        private int _jointBuildings;
        private int _colourBuildings;
        private int _doorBuildings;
        private int _timeBuildings;
        // material key → the fading animation driving it and which of its materials to read
        private readonly Dictionary<int, (MaterialColourAnimation anim, int material)> _fadedMaterials
            = new Dictionary<int, (MaterialColourAnimation, int)>();
        // Buildings whose parts move, with the animation driving them.
        private readonly List<(NsbmdRenderModel.BuildingMaterials building, JointAnimation anim)> _jointed
            = new List<(NsbmdRenderModel.BuildingMaterials, JointAnimation)>();
        private float _tileX, _tileZ;

        public NsbmdRenderModel Scene => _scene;

        /// <summary>Sprites for this frame, rebuilt as the people turn and move.</summary>
        public IReadOnlyList<NsbmdGlControl.SpriteInstance> Sprites { get; private set; }
            = Array.Empty<NsbmdGlControl.SpriteInstance>();

        /// <summary>Texture transforms for this frame, or null when the map has no moving water.</summary>
        public Dictionary<int, float[]> TextureMatrices { get; private set; }

        /// <summary>Which texture each swapping material shows this frame, or null when none do.</summary>
        public Dictionary<int, string> TextureSwaps { get; private set; }

        /// <summary>Rebuilt triangles for the building parts that move, or null when none do.</summary>
        public Dictionary<int, float[]> MovedParts { get; private set; }

        /// <summary>How see-through each fading material is this frame, or null when none fade.</summary>
        public Dictionary<int, float> MaterialFades { get; private set; }

        /// <summary>Which way each person is facing this frame, in the order the events are stored.</summary>
        public IReadOnlyList<MoveFacing> Facings => _npcs.Select(n => n.Motion.Facing).ToArray();

        public event EventHandler FrameAdvanced;

        private int _frame;
        public int Frame { get => _frame; private set { if (Set(ref _frame, value)) { OnPropertyChanged(nameof(TimeText)); } } }

        public string TimeText => $"{_frame / (float)FramesPerSecond:0.0} s";

        private bool _playing = true;
        public bool Playing { get => _playing; set { if (Set(ref _playing, value)) OnPropertyChanged(nameof(PlayPauseText)); } }
        public string PlayPauseText => _playing ? "Pause" : "Play";

        private double _speed = 1.0;
        /// <summary>How fast the preview runs, so slow motion can show what a fast animation is doing.</summary>
        public double Speed { get => _speed; set => Set(ref _speed, value); }
        public ObservableCollection<string> SpeedNames { get; } =
            new ObservableCollection<string> { "0.25×", "0.5×", "1×", "2×" };
        private static readonly double[] SpeedValues = { 0.25, 0.5, 1.0, 2.0 };

        private int _speedIndex = 2;
        public int SpeedIndex { get => _speedIndex; set { if (Set(ref _speedIndex, value) && value >= 0 && value < SpeedValues.Length) Speed = SpeedValues[value]; } }

        private bool _showPeople = true;
        public bool ShowPeople { get => _showPeople; set { if (Set(ref _showPeople, value)) Rebuild(); } }

        // ── Stepping in ────────────────────────────────────────────────────────────────── The player
        // walks the map the way they do in game, and talking to somebody walks that person's script and
        // says what it would do.
        public FieldPlayer Player { get; private set; }

        private bool _showLevelScripts;
        /// <summary>Whether the side panel listing what the map runs by itself is on show.</summary>
        public bool ShowLevelScripts
        {
            get => _showLevelScripts;
            set => Set(ref _showLevelScripts, value);
        }

        private bool _showStringVars;

        /// <summary>Whether the panel of words the messages leave gaps for is on show.</summary>
        public bool ShowStringVars
        {
            get => _showStringVars;
            set => Set(ref _showStringVars, value);
        }

        /// <summary>
        /// The gaps this map's messages leave for words the game fills in, with what the preview will put
        /// in them.
        /// </summary>
        public ObservableCollection<StringVarEntry> StringVars { get; } = new ObservableCollection<StringVarEntry>();

        public bool HasStringVars => StringVars.Count > 0;

        public string StringVarSummary => StringVars.Count == 0
            ? "No message here leaves a gap for a word."
            : $"{StringVars.Count} {(StringVars.Count == 1 ? "gap" : "gaps")} across this map's messages";

        /// <summary>One editable word, wrapping the gap it fills.</summary>
        public sealed class StringVarEntry : INotifyPropertyChanged
        {
            private readonly Action _changed;
            public StringVarEntry(FieldStringVar v, Action changed) { Var = v; _value = v.Value; _changed = changed; }

            public FieldStringVar Var { get; }
            public string Label => Var.Label;

            private string _value;
            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    Var.Value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    _changed?.Invoke();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private readonly Dictionary<string, StringVarEntry> _stringVarByKey = new Dictionary<string, StringVarEntry>();

        /// <summary>Hands the preview the gaps found in the map's messages.</summary>
        public void SetStringVars(IEnumerable<FieldStringVar> vars)
        {
            StringVars.Clear();
            _stringVarByKey.Clear();
            if (vars != null)
                foreach (var v in vars)
                {
                    var e = new StringVarEntry(v, StringVarsChanged);
                    StringVars.Add(e);
                    _stringVarByKey[v.Key] = e;
                }
            OnPropertyChanged(nameof(HasStringVars));
            OnPropertyChanged(nameof(StringVarSummary));
        }

        private void StringVarsChanged()
        {
            // Whatever is on screen should read the new word without waiting for the next message.
            LayOutMessage();
            StatusText = Describe();
        }

        /// <summary>
        /// Puts the map's words into a line so the box reads the way the game would show it, rather than
        /// leaving the raw tag on screen.
        /// </summary>
        public string ExpandVars(string line) =>
            FieldStringVars.Expand(line, (family, kind, buffer) =>
                _stringVarByKey.TryGetValue(FieldStringVars.KeyOf(family, kind, buffer), out var e)
                    ? e.Value
                    : FieldStringVars.SuggestFor(kind, buffer, null));

        /// <summary>What this map runs by itself, out of the header's level script file.</summary>
        public LevelScriptFile LevelScripts
        {
            get => _levelScripts;
            set { _levelScripts = value; BuildLevelScriptList(); }
        }
        private LevelScriptFile _levelScripts;

        /// <summary>The entries that run on arriving, one line each saying when and which script.</summary>
        public ObservableCollection<string> LevelScriptArrivals { get; } = new ObservableCollection<string>();

        /// <summary>The entries that sit watching a variable, with the value set so one can be tried out.</summary>
        public ObservableCollection<LevelScriptWatcher> LevelScriptWatchers { get; } =
            new ObservableCollection<LevelScriptWatcher>();

        public bool HasLevelScripts => LevelScriptArrivals.Count > 0 || LevelScriptWatchers.Count > 0;

        /// <summary>One level script that waits for a variable to hold a value. </summary>
        public sealed class LevelScriptWatcher : INotifyPropertyChanged
        {
            private readonly Action<int, int> _set;
            public LevelScriptWatcher(VariableValueTrigger trigger, Action<int, int> set)
            { Trigger = trigger; _set = set; }

            public VariableValueTrigger Trigger { get; }

            public string Label =>
                $"{FieldScriptValues.Describe(Trigger.variableToWatch)} = {Trigger.expectedValue}"
                + $"  ·  script {Trigger.scriptTriggered}";

            private int _value;
            public int Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    _set?.Invoke(Trigger.variableToWatch, value);
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>The watchers that have already gone off, so each only runs once per visit.</summary>
        private readonly HashSet<VariableValueTrigger> _firedWatchers = new HashSet<VariableValueTrigger>();

        private void BuildLevelScriptList()
        {
            LevelScriptArrivals.Clear();
            LevelScriptWatchers.Clear();
            foreach (var t in FieldLevelScripts.OnArrival(_levelScripts))
                LevelScriptArrivals.Add($"{FieldLevelScripts.WhenItRuns(t)}: script {t.scriptTriggered}");
            foreach (var t in FieldLevelScripts.Watchers(_levelScripts))
                LevelScriptWatchers.Add(new LevelScriptWatcher(t, SetVariable));
            OnPropertyChanged(nameof(HasLevelScripts));
            OnPropertyChanged(nameof(LevelScriptSummary));
        }

        public string LevelScriptSummary
        {
            get
            {
                int n = LevelScriptArrivals.Count + LevelScriptWatchers.Count;
                if (n == 0) return "This map runs nothing by itself.";
                return $"{n} level script {(n == 1 ? "entry" : "entries")}";
            }
        }

        /// <summary>
        /// What the games run as you arrive on a map: the two field-setup passes and then the map change,
        /// in that order.
        /// </summary>
        private void RunArrivalLevelScripts()
        {
            _firedWatchers.Clear();
            foreach (var t in FieldLevelScripts.OnArrival(_levelScripts))
                RunWhileLoading(t.scriptTriggered,
                    $"{FieldLevelScripts.WhenItRuns(t)}, so the map runs script {t.scriptTriggered}.");

            // These run before anybody is put on the map, so a flag they set decides who is there.
            foreach (var npc in _npcs)
                if (npc.OnMap == null && npc.Event.flag != 0 && GameState.TryGetFlag(npc.Event.flag, out bool set))
                    npc.OnMap = !set;
            Rebuild();
        }

        /// <summary>
        /// The engine gives the variable-watching level scripts a chance on every step you take, in the
        /// same check that does trainer line of sight (ev_check.c:505).
        /// </summary>
        private void CheckLevelScriptWatchers()
        {
            // Only while you are walking about: the engine checks these as part of the step you take.
            if (_levelScripts == null || !_stepInto || ScriptRunning) return;

            foreach (var t in FieldLevelScripts.Watchers(_levelScripts))
            {
                if (_firedWatchers.Contains(t)) continue;
                if (VariableValue(t.variableToWatch) != t.expectedValue) continue;
                _firedWatchers.Add(t);
                ScriptLines.Add($"{FieldScriptValues.Describe(t.variableToWatch)} holds {t.expectedValue}, "
                                + $"so the map starts script {t.scriptTriggered}.");
                RunScript(t.scriptTriggered, "");
                return;                          // the engine takes the first one and stops
            }
        }

        private int VariableValue(int variable) =>
            GameState.TryGetVar(variable, out long v) ? (int)v : 0;

        /// <summary>
        /// Sets one of the map's variables, which is how somebody makes a watching level script go off
        /// without having to play the game up to that point.
        /// </summary>
        public void SetVariable(int variable, int value)
        {
            GameState.SetVar(variable, value);
            CheckLevelScriptWatchers();
        }
        private Trigger _pendingTrigger;

        private bool _stepInto;
        public bool StepInto
        {
            get => _stepInto;
            set
            {
                if (!Set(ref _stepInto, value)) return;
                if (!value)
                {
                    ScriptLines.Clear(); Question = null; _walker = null; _pendingTrigger = null;
                    ClearMessage();
                    _runner?.Stop(); _shake = null; _cameraMove = null; _cameraObject = null; _talkTarget = null;
                    foreach (var npc in _npcs) { npc.Motion?.StopScript(); if (npc.Motion != null) npc.Motion.Paused = false; npc.OnMap = null; }
                    _firedWatchers.Clear();
                    ResetTouchScreen();
                }
                else
                {
                    // Stepping onto the map is arriving on it, which is when the games run the level
                    // scripts that set the place up.
                    RunArrivalLevelScripts();
                }
                OnPropertyChanged(nameof(CanStepInto));
                OnPropertyChanged(nameof(ShowTouchScreen));
                OnPropertyChanged(nameof(ScriptStatusText));
                Rebuild();
            }
        }

        /// <summary>Stepping in needs somewhere to stand, which needs the map's own tile grid.</summary>
        public bool CanStepInto => Player != null || StartTile != null;

        private (int x, int z)? _startTile;

        /// <summary>Where to stand when stepping in, in whole-matrix tiles. </summary>
        public (int x, int z)? StartTile
        {
            get => _startTile;
            set
            {
                if (Nullable.Equals(_startTile, value)) return;
                _startTile = value;
                OnPropertyChanged(nameof(StartTile));
                OnPropertyChanged(nameof(StartTileText));
                Player = MakePlayer();
                OnPropertyChanged(nameof(Player));
                OnPropertyChanged(nameof(CanStepInto));
                Rebuild();
            }
        }

        /// <summary>Where the walk begins, for the toolbar to show.</summary>
        public string StartTileText => _startTile == null
            ? "Starts in the middle of the map"
            : $"Starts at tile {_startTile.Value.x}, {_startTile.Value.z}";

        /// <summary>Puts the walk back to starting in the middle of the map's people.</summary>
        public void ClearStartTile() { _startFacing = MoveFacing.Down; StartTile = null; }

        private MoveFacing _startFacing = MoveFacing.Down;

        /// <summary>Who a walk can be started next to, in the order the events are stored.</summary>
        public ObservableCollection<string> StartBesideNames { get; } = new ObservableCollection<string>();

        private int _startBesideIndex = -1;

        private bool _placingStart;

        /// <summary>Whether dragging on the map moves where the walk starts. </summary>
        public bool PlacingStart
        {
            get => _placingStart;
            set { if (Set(ref _placingStart, value)) OnPropertyChanged(nameof(PlaceHint)); }
        }

        public string PlaceHint => _placingStart
            ? "Drag on the map to say where the walk starts."
            : "";

        /// <summary>Where each entry of the start list puts you, in whole-matrix tiles.</summary>
        private readonly List<(int x, int z)?> _startPlaces = new List<(int x, int z)?>();

        /// <summary>Where the walk begins. </summary>
        public int StartBesideIndex
        {
            get => _startBesideIndex;
            set
            {
                if (!Set(ref _startBesideIndex, value)) return;
                if (value <= 0 || value >= _startPlaces.Count) { ClearStartTile(); return; }

                var place = _startPlaces[value];
                if (place == null) { ClearStartTile(); return; }
                StandBeside(place.Value.x, place.Value.z);
            }
        }

        /// <summary>Whether the player could stand on a tile: open, dry, and nobody already on it.</summary>
        private bool CanStand(int x, int z) =>
            (_collision == null || _collision.IsEmpty
             || (!_collision.IsBlocked(x, z) && !FieldTileBehaviors.IsWater(_collision.TypeAt(x, z), _family)))
            && !SomebodyOn(x, z, null, false);

        /// <summary>Stands the player next to a tile, facing it. </summary>
        public void StandBeside(int ox, int oz)
        {
            // Standing one tile away in each direction, looking back the other way.
            var tries = new List<(int dx, int dz, MoveFacing look)>
            {
                (0, 1, MoveFacing.Up),        // below it, looking up
                (0, -1, MoveFacing.Down),
                (1, 0, MoveFacing.Left),
                (-1, 0, MoveFacing.Right),
            };

            // Somebody standing there is usually spoken to from the side they face, so that side goes first.
            var there = _npcs.FirstOrDefault(n => FieldInteraction.TileX(n.Event) == ox && FieldInteraction.TileZ(n.Event) == oz);
            if (there != null)
            {
                var (fx, fz) = FieldPlayer.Step(there.Motion.Facing);
                int front = tries.FindIndex(t => t.dx == fx && t.dz == fz);
                if (front > 0) { var pick = tries[front]; tries.RemoveAt(front); tries.Insert(0, pick); }
            }

            foreach (var (dx, dz, look) in tries)
            {
                int x = ox + dx, z = oz + dz;
                if (!CanStand(x, z)) continue;
                _startFacing = look;
                StartTile = (x, z);
                return;
            }

            // Nowhere free beside it: stand on the spot anyway so the map still opens somewhere useful.
            _startFacing = MoveFacing.Down;
            StartTile = (ox, oz + 1);
        }

        /// <summary>Stands the player on a tile outright, rather than next to something. </summary>
        public void StandOn(int x, int z, MoveFacing facing = MoveFacing.Down)
        {
            var free = NearestFreeTile(x, z);
            if (free == null) return;                  // nowhere near it will do; leave the marker be

            _startFacing = facing;
            StartTile = free;
            // The list no longer says where you are, so put it back to its own entry.
            if (_startBesideIndex != 0) { _startBesideIndex = 0; OnPropertyChanged(nameof(StartBesideIndex)); }
            if (_stepInto) { Player = MakePlayer(); Rebuild(); }
        }

        /// <summary>
        /// The tile itself when you could stand on it, otherwise the closest one nearby that you could.
        /// </summary>
        private (int x, int z)? NearestFreeTile(int x, int z, int reach = 3)
        {
            bool Free(int tx, int tz) => CanStand(tx, tz);

            if (Free(x, z)) return (x, z);

            for (int r = 1; r <= reach; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        if (Free(x + dx, z + dz)) return (x + dx, z + dz);
                    }
            return null;
        }

        private void BuildStartBesideList()
        {
            StartBesideNames.Clear();
            _startPlaces.Clear();

            StartBesideNames.Add("Wherever the marker is");
            _startPlaces.Add(null);

            foreach (var npc in _npcs)
            {
                StartBesideNames.Add($"Beside overworld {npc.Event.owID}");
                _startPlaces.Add((FieldInteraction.TileX(npc.Event), FieldInteraction.TileZ(npc.Event)));
            }

            // Triggers, warps and the things you find by standing on them are worth starting next to as
            // well: a trigger is the whole reason to walk onto a particular square.
            if (_events != null)
            {
                for (int i = 0; i < _events.triggers.Count; i++)
                {
                    var t = _events.triggers[i];
                    StartBesideNames.Add(t.scriptNumber == EventFile.NoScript
                        ? $"Beside trigger {i}, no script"
                        : $"Beside trigger {i}, script {t.scriptNumber}");
                    _startPlaces.Add((FieldInteraction.TileX(t), FieldInteraction.TileZ(t)));
                }
                for (int i = 0; i < _events.warps.Count; i++)
                {
                    var w = _events.warps[i];
                    StartBesideNames.Add($"Beside warp {i}");
                    _startPlaces.Add((FieldInteraction.TileX(w), FieldInteraction.TileZ(w)));
                }
                for (int i = 0; i < _events.spawnables.Count; i++)
                {
                    var sp = _events.spawnables[i];
                    StartBesideNames.Add($"Beside spawnable {i}");
                    _startPlaces.Add((FieldInteraction.TileX(sp), FieldInteraction.TileZ(sp)));
                }
            }

            _startBesideIndex = 0;
            OnPropertyChanged(nameof(StartBesideIndex));
        }

        /// <summary>
        /// The tile nearest a point on screen, given something that can project a tile to screen pixels.
        /// </summary>
        public (int x, int z)? TileAtScreen(double px, double py,
                                            Func<float, float, float, (float sx, float sy)?> project,
                                            double withinPixels = 80)
        {
            if (_collision == null || _collision.IsEmpty || _tileToWorld == null || project == null) return null;

            (int x, int z)? best = null;
            double bestD = withinPixels * withinPixels;
            foreach (var (x, z) in _collision.Tiles)
            {
                var foot = _tileToWorld(x, z);
                var at = project(foot.x, foot.y, foot.z);
                if (at == null) continue;
                double dx = px - at.Value.sx, dy = py - at.Value.sy;
                double d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = (x, z); }
            }
            return best;
        }

        /// <summary>
        /// One of the flags the overworlds on this map are gated on, with a switch to pretend it is set.
        /// </summary>
        public sealed class EventFlagSwitch : INotifyPropertyChanged
        {
            private readonly Action _changed;
            public EventFlagSwitch(ushort number, int users, Action changed)
            { Number = number; Users = users; _changed = changed; }

            public ushort Number { get; }
            public int Users { get; }

            private bool _isSet;
            public bool IsSet
            {
                get => _isSet;
                set
                {
                    if (_isSet == value) return;
                    _isSet = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet)));
                    _changed?.Invoke();
                }
            }

            public string Label => $"Flag {Number}  ·  {Users} {(Users == 1 ? "person" : "people")}";

            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>Every flag the map's overworlds are gated on, lowest first.</summary>
        public ObservableCollection<EventFlagSwitch> EventFlags { get; } = new ObservableCollection<EventFlagSwitch>();

        private readonly HashSet<ushort> _flagsSet = new HashSet<ushort>();

        public bool FlagIsSet(ushort flag) => _flagsSet.Contains(flag);

        /// <summary>Whether this overworld is on the map, going by its flag.</summary>
        public bool IsPresent(Overworld ow) => FieldInteraction.IsPresent(ow, FlagIsSet);

        /// <summary>Whether somebody is on the map now: their flag, unless a script has added or removed them.</summary>
        private bool IsOnMap(Npc n) => n.OnMap ?? IsPresent(n.Event);

        /// <summary>Whether somebody is standing on a tile, or on their way onto it. </summary>
        private bool SomebodyOn(int x, int z, Npc except, bool countPlayer)
        {
            foreach (var npc in _npcs)
            {
                if (npc == except) continue;
                if (!IsOnMap(npc) || !npc.Motion.Visible) continue;
                int hx = FieldInteraction.TileX(npc.Event), hz = FieldInteraction.TileZ(npc.Event);
                if (hx + npc.Motion.OffsetX == x && hz + npc.Motion.OffsetZ == z) return true;
                if (hx + npc.Motion.FromOffsetX == x && hz + npc.Motion.FromOffsetZ == z) return true;
            }

            // The player is only on the map once you have stepped in.
            if (countPlayer && _stepInto && Player != null)
            {
                if (Player.TileX == x && Player.TileZ == z) return true;
                if (Player.FromX == x && Player.FromZ == z) return true;
            }
            return false;
        }

        private void FlagsChanged()
        {
            _flagsSet.Clear();
            foreach (var f in EventFlags)
            {
                if (f.IsSet) _flagsSet.Add(f.Number);
                // A flag ticked here is one a script checking it should find set.
                if (f.IsSet) GameState.SetFlag(f.Number, true);
                else if (GameState.TryGetFlag(f.Number, out bool was) && was) GameState.SetFlag(f.Number, false);
            }

            OnPropertyChanged(nameof(HiddenCount));
            OnPropertyChanged(nameof(HiddenSummary));
            StatusText = Describe();
            Rebuild();
        }

        /// <summary>How many people the flags are currently taking off the map.</summary>
        public int HiddenCount => _npcs.Count(n => !IsOnMap(n));

        public string HiddenSummary => HiddenCount == 0
            ? "Everybody is on the map."
            : $"{HiddenCount} {(HiddenCount == 1 ? "person is" : "people are")} away.";

        /// <summary>The map's people, for checking where they have got to.</summary>
        public IReadOnlyList<Npc> Npcs => _npcs;

        /// <summary>How wide one tile is in the units the scene is drawn in.</summary>
        public float TileWidth => _tileX;

        // A sprite is drawn at the size its own art says: sixteen pixels of overworld art make a tile, so
        // an ordinary 32 by 32 sprite stands two tiles tall and two wide.
        private float HalfHeightOf(OverworldSprites.SpritePixels pix) =>
            _tileX * pix.Height / (OverworldSprites.PixelsPerTile * 2f);

        private float HalfWidthOf(OverworldSprites.SpritePixels pix) =>
            _tileX * pix.Width / (OverworldSprites.PixelsPerTile * 2f);

        /// <summary>Which of the player's two walking poses to draw. </summary>
        /// <summary>
        /// Which picture of somebody's sprite bank to draw, given how long they have been walking. The
        /// bank is read for how many pictures it has so a person, a following Pokemon and the hero each
        /// get paced the way the games pace them.
        /// </summary>
        private static int PictureFor(ushort entry, MoveFacing facing, FieldWalkCycle cycle) =>
            FieldSpriteAnimation.PictureFor(OverworldSprites.FrameCount(entry), (int)facing, cycle);

        /// <summary>Whether any overworld here is gated on a flag at all.</summary>
        public bool HasEventFlags => EventFlags.Count > 0;

        /// <summary>The events being previewed.</summary>
        public EventFile Events => _events;

        // ── playing a script out on the clock ───────────────────────────
        private FieldScriptRunner _runner;
        private FieldCameraShake _shake;
        private Func<int, IReadOnlyList<ScriptAction>> _actionsFor;

        // Counts field frames whether or not the scene is animating, so script waits keep time.
        private int _scriptFrame;

        /// <summary>Plays a sound. The window points this at the ROM's own sound archive.</summary>
        public Action<ScriptEffectKind, int> PlaySound { get; set; }

        private bool _playSounds = true;

        /// <summary>Whether the preview makes any noise at all.</summary>
        public bool PlaySounds
        {
            get => _playSounds;
            set { if (Set(ref _playSounds, value) && !value) PlaySound?.Invoke(ScriptEffectKind.MusicStop, 0); }
        }

        private bool _playMapMusic;

        /// <summary>Whether the map's own music plays while the preview runs.</summary>
        public bool PlayMapMusic
        {
            get => _playMapMusic;
            set { if (Set(ref _playMapMusic, value)) MapMusicChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>Raised when the map music is switched on or off, or the time of day changes it.</summary>
        public event EventHandler MapMusicChanged;

        /// <summary>The music this header would play. </summary>
        public int MapMusicId => FieldTimeOfDay.IsNight(_timeOfDay) ? MusicNightId : MusicDayId;

        /// <summary>The header's two music numbers, set by whoever opened the preview.</summary>
        public int MusicDayId { get; set; }
        public int MusicNightId { get; set; }

        private FieldCameraMove _cameraMove;

        /// <summary>How far down the camera looks now, which a script can ease to somewhere else.</summary>
        public float CameraPitchDegrees => _cameraMove?.PitchDegrees ?? CameraEntry.PitchDegrees;

        /// <summary>How far a script has slid the view from where the header put it, in tiles.</summary>
        public float CameraShiftX => _cameraMove?.ShiftXInTiles ?? 0f;
        public float CameraShiftY => _cameraMove?.ShiftYInTiles ?? 0f;
        public float CameraShiftZ => _cameraMove?.ShiftZInTiles ?? 0f;

        /// <summary>How far the shaking has pushed the view, in tiles.</summary>
        public float ShakeOffsetX => _shake?.OffsetX ?? 0f;
        public float ShakeOffsetY => _shake?.OffsetY ?? 0f;

        /// <summary>Whether a script is playing out right now.</summary>
        public bool ScriptRunning => _runner != null && _runner.Running;

        /// <summary>What the script is doing, for the panel.</summary>
        public string ScriptProgressText
        {
            get
            {
                if (!ScriptRunning) return "";
                switch (_runner.Waiting)
                {
                    case FieldScriptRunner.WaitKind.Message: return MessageHasMore ? "Waiting for A to turn the page" : "Printing a message";
                    case FieldScriptRunner.WaitKind.Button: return "Waiting for A or B";
                    case FieldScriptRunner.WaitKind.Movement: return "Waiting for everyone to finish moving";
                    case FieldScriptRunner.WaitKind.Frames: return $"Waiting {_runner.HoldingFrames} frames";
                    case FieldScriptRunner.WaitKind.Question: return "Waiting for an answer";
                    case FieldScriptRunner.WaitKind.Sound: return "Waiting for a sound to finish";
                    default: return "Running";
                }
            }
        }

        /// <summary>What the preview has been told about flags and variables, kept across every script it runs.</summary>
        public ScriptGameState GameState { get; } = new ScriptGameState();

        /// <summary>Finds the file a shared script lives in. The window points this at the ROM.</summary>
        public Func<int, ScriptSource> CommonScripts { get; set; }

        /// <summary>Reads a message out of any archive: archive, then message.</summary>
        public Func<int, int, string> ArchiveText { get; set; }

        /// <summary>Which archive each of the four shared message archives is.</summary>
        public Func<int, int> SharedArchive { get; set; }

        /// <summary>Reads one of the shared menu entries.</summary>
        public Func<int, string> MenuText { get; set; }

        private FieldScriptRunner Runner => _runner ??= new FieldScriptRunner(new FieldScriptRunner.Hooks
        {
            StartMovement = StartMovement,
            MovementsRunning = () => (Player?.IsScripted ?? false)
                                     || _npcs.Any(n => n.Motion.IsScripted)
                                     || (_cameraObject?.Motion.IsScripted ?? false),
            PlaySound = StartSound,
            SoundPlaying = kind => _soundEnds.TryGetValue(kind, out int end) && end > _scriptFrame,
            ShakeCamera = (x, y, count, frames) => _shake = new FieldCameraShake(x, y, count, frames),
            MoveCamera = row =>
            {
                if (!FieldCameraMove.Exists(row)) return 0;
                _cameraMove = new FieldCameraMove(row, CameraEntry.PitchDegrees);
                return _cameraMove.TotalFrames;
            },
            ShowMessage = StartMessage,
            MessagePrinting = () => _printer != null && !_printer.Finished,
            OpenMessage = () => { _printer = null; _boxText = ""; _boxOpen = true; RaiseMessageChanged(); },
            CloseMessage = keepWords =>
            {
                if (_printer != null) _boxText = _printer.Text;
                _printer = null;
                // The frozen close leaves the tiles drawn until something draws over them.
                if (!keepWords) { _boxOpen = false; _boxText = null; }
                RaiseMessageChanged();
            },
            Ask = q => Question = q,
            Apply = ApplyEffect,
            Report = step => ScriptLines.Add(step.Text),
        });

        // When each kind of sound started, and when it ends once the window knows how long it is.
        private readonly Dictionary<ScriptEffectKind, int> _soundEnds = new Dictionary<ScriptEffectKind, int>();
        private readonly Dictionary<ScriptEffectKind, int> _soundStarts = new Dictionary<ScriptEffectKind, int>();

        private void StartSound(ScriptEffectKind kind, int id)
        {
            if (!_playSounds || PlaySound == null) { _soundEnds.Remove(kind); return; }
            // Until the window has worked out how long it is, it counts as still playing.
            _soundStarts[kind] = _scriptFrame;
            _soundEnds[kind] = int.MaxValue;
            PlaySound(kind, id);
        }

        /// <summary>The window says how many frames a sound it was asked to play lasts.</summary>
        public void SoundLength(ScriptEffectKind kind, int frames)
        {
            if (_soundStarts.TryGetValue(kind, out int start)) _soundEnds[kind] = start + Math.Max(0, frames);
        }

        /// <summary>The overworld a script means by a number, which may be a variable holding one.</summary>
        private int ResolveObject(int number)
        {
            if (!FieldScriptValues.IsVariable(number)) return number;
            return GameState.TryGetVar(number, out long v) ? (int)v : -1;
        }

        private Npc NpcById(int id) => _npcs.FirstOrDefault(n => n.Event.owID == id && IsOnMap(n));

        /// <summary>
        /// Sets somebody walking through a movement, and says how long it will take. The script does not
        /// wait for it unless it asks to.
        /// </summary>
        private int StartMovement(int overworldId, int movementNumber)
        {
            var actions = _walker?.ActionsFor(movementNumber) ?? _actionsFor?.Invoke(movementNumber);
            var steps = FieldMovementScript.Parse(actions);
            if (steps.Count == 0) return 0;

            int who = ResolveObject(overworldId);
            if (who == ScriptWalker.PlayerObject)
            {
                if (Player == null) return 0;
                Player.PlayScript(steps);
            }
            else if (who == ScriptWalker.CameraObject)
            {
                if (_cameraObject == null) return 0;
                _cameraObject.Motion.PlayScript(steps);
            }
            else
            {
                var npc = NpcById(who);
                if (npc == null) return 0;
                npc.Motion.PlayScript(steps);
            }
            return FieldMovementScript.TotalFrames(steps);
        }

        private static MoveFacing Opposite(MoveFacing f) => f switch
        {
            MoveFacing.Up => MoveFacing.Down,
            MoveFacing.Down => MoveFacing.Up,
            MoveFacing.Left => MoveFacing.Right,
            _ => MoveFacing.Left,
        };

        /// <summary>Turning, locking and showing or hiding people, as a script asks.</summary>
        private void ApplyEffect(ScriptEffect e)
        {
            switch (e.Kind)
            {
                case ScriptEffectKind.FacePlayer:
                    // It turns against the way the player faces, not towards where the player stands.
                    if (_talkTarget != null && Player != null) _talkTarget.Motion.Face(Opposite(Player.Facing));
                    break;

                case ScriptEffectKind.Lock:
                case ScriptEffectKind.Release:
                {
                    bool paused = e.Kind == ScriptEffectKind.Lock;
                    if (e.A < 0) foreach (var n in _npcs) n.Motion.Paused = paused;
                    else if (NpcById(ResolveObject(e.A)) is Npc one) one.Motion.Paused = paused;
                    break;
                }

                case ScriptEffectKind.ShowObject:
                {
                    int id = ResolveObject(e.A);
                    var npc = _npcs.FirstOrDefault(n => n.Event.owID == id);
                    if (npc == null) break;
                    if (e.B == 1)
                    {
                        // AddObject only brings somebody back while their flag is clear.
                        bool hidden = npc.Event.flag != 0 && (GameState.TryGetFlag(npc.Event.flag, out bool set) ? set : FlagIsSet(npc.Event.flag));
                        if (!hidden) npc.OnMap = true;
                    }
                    else
                    {
                        npc.OnMap = false;
                        if (npc.Event.flag != 0) GameState.SetFlag(npc.Event.flag, true);
                    }
                    break;
                }

                case ScriptEffectKind.TouchScreen:
                    _touchSwapFrames = FieldScriptRunner.TouchScreenSwapFrames;
                    _touchSwapToChoices = e.A == 1;
                    OnPropertyChanged(nameof(TouchScreenBrightness));
                    break;

                case ScriptEffectKind.CameraObject:
                    _cameraObject = e.C == 1
                        ? new CameraObjectState { TileX = e.A, TileZ = e.B, Motion = new OverworldAnimator(null, MoveFacing.Down) }
                        : null;
                    break;
            }
        }

        /// <summary>The invisible thing a script can hand the camera to, and walk about to pan it.</summary>
        private sealed class CameraObjectState
        {
            public int TileX, TileZ;
            public OverworldAnimator Motion;
        }

        private CameraObjectState _cameraObject;

        private bool _showFlags;
        public bool ShowFlags { get => _showFlags; set => Set(ref _showFlags, value); }

        private void BuildFlagList(EventFile events)
        {
            EventFlags.Clear();
            _flagsSet.Clear();
            if (events?.overworlds == null) return;

            // Flag 0 is the one nothing ever sets, so an overworld carrying it is simply always there.
            foreach (var g in events.overworlds.Where(o => o.flag != 0)
                                               .GroupBy(o => o.flag)
                                               .OrderBy(g => g.Key))
                EventFlags.Add(new EventFlagSwitch(g.Key, g.Count(), FlagsChanged));
        }

        private int _cameraId;

        /// <summary>
        /// The header's camera number, which is the row the games look up in their own camera table.
        /// </summary>
        public int CameraId
        {
            get => _cameraId;
            set { if (Set(ref _cameraId, value)) { OnPropertyChanged(nameof(CameraEntry)); OnPropertyChanged(nameof(CameraDescription)); } }
        }

        private RomInfo.GameFamilies _family = RomInfo.gameFamily;

        /// <summary>Which game is being previewed: the two keep different camera tables and menus.</summary>
        public RomInfo.GameFamilies Family
        {
            get => _family;
            set
            {
                if (!Set(ref _family, value)) return;
                OnPropertyChanged(nameof(CameraEntry));
                OnPropertyChanged(nameof(CameraDescription));
                OnPropertyChanged(nameof(IsHeartGold));
                OnPropertyChanged(nameof(IsPlatinum));
                OnPropertyChanged(nameof(ShowTouchScreen));
            }
        }

        /// <summary>HeartGold and SoulSilver put some of their menus on the touch screen.</summary>
        public bool IsHeartGold => _family == RomInfo.GameFamilies.HGSS;

        public bool IsPlatinum => _family == RomInfo.GameFamilies.Plat;

        // ── HeartGold and SoulSilver's touch screen ─────────────────────────────
        private bool _touchShowsChoices;
        private int _touchSwapFrames;
        private bool _touchSwapToChoices;
        private int _touchBlink = -1;
        private int _touchLabelShown = -1;

        /// <summary>Whether the Poké Ball screen touch questions use is up instead of the touch menu.</summary>
        public bool TouchScreenShowsChoices => _touchShowsChoices;

        /// <summary>How bright the touch screen is, dipping to black and back while it swaps.</summary>
        public double TouchScreenBrightness
        {
            get
            {
                if (_touchSwapFrames <= 0) return 1;
                double half = FieldScriptRunner.TouchScreenSwapFrames / 2.0;
                return Math.Abs(_touchSwapFrames - half) / half;
            }
        }

        /// <summary>Whether the red frame is up round the touch entry the cursor is on; it blinks when one is picked.</summary>
        public bool TouchCursorShown => global::DSPRE.Avalonia.Data.HgssTouchScreen.BlinkShows(_touchBlink);

        /// <summary>Whether the touch question on show is a yes/no rather than a list.</summary>
        public bool TouchChoiceIsYesNo => _question?.Kind == ScriptQuestion.QuestionKind.YesNo;

        /// <summary>The entries of the touch question on show, or none.</summary>
        public IReadOnlyList<string> TouchChoiceItems => HasTouchChoice ? ChoiceAllItems : Array.Empty<string>();

        /// <summary>The words the touch menu's A button shows: NEXT while a message is up, TALK facing somebody, CHECK otherwise.</summary>
        public int TouchALabel
        {
            get
            {
                if (MessageVisible) return global::DSPRE.Avalonia.Data.HgssTouchScreen.NextMessage;
                if (Player == null) return global::DSPRE.Avalonia.Data.HgssTouchScreen.CheckMessage;
                var (x, z) = FieldInteraction.TalkTile(Player, _collision);
                return NpcAt(x, z) != null ? global::DSPRE.Avalonia.Data.HgssTouchScreen.TalkMessage : global::DSPRE.Avalonia.Data.HgssTouchScreen.CheckMessage;
            }
        }

        private void ResetTouchScreen()
        {
            _touchShowsChoices = false;
            _touchSwapFrames = 0;
            _touchBlink = -1;
            OnPropertyChanged(nameof(TouchScreenShowsChoices));
            OnPropertyChanged(nameof(TouchScreenBrightness));
            OnPropertyChanged(nameof(TouchCursorShown));
        }

        /// <summary>A touch list writes the entry's description into the top screen's message box as the cursor moves.</summary>
        private void ShowDescription(string text)
        {
            _printer = null;
            _boxText = ExpandVars(text);
            _boxOpen = true;
            RaiseMessageChanged();
        }

        /// <summary>A touch on one of the touch screen's entries, which picks it straight away.</summary>
        public void TouchChoice(int index)
        {
            if (_question == null || !_question.OnTouchScreen || _touchBlink >= 0) return;
            if (index < 0 || index >= _question.Options.Count) return;
            _choiceCursor = index;
            foreach (var entry in ChoiceEntries) entry.IsSelected = entry.Index == index;
            OnPropertyChanged(nameof(ChoiceCursor));
            OnPropertyChanged(nameof(ChoiceCursorRow));
            ConfirmChoice();
        }

        private bool _hasPoketch = true;

        /// <summary>Whether the player has been given the Pokétch, which the preview cannot read from a save.</summary>
        public bool HasPoketch { get => _hasPoketch; set => Set(ref _hasPoketch, value); }

        /// <summary>Known once a script has asked, and it picks the Pokétch's casing.</summary>
        public bool PlayerIsFemale => GameState.TryGetFact("CheckPlayerGender", out long gender) && gender == 1;

        public FieldCameraEntry CameraEntry => FieldCamera.Entry(_cameraId, _family);

        /// <summary>What the step-in camera is doing, for the toolbar.</summary>
        public string CameraDescription
        {
            get
            {
                var c = CameraEntry;
                string kind = c.Orthographic ? "flat" : $"{c.FieldOfViewDegrees:0.#} degrees";
                return $"Camera {c.Id}, {c.Name}: {c.DistanceInTiles:0.#} tiles back, "
                     + $"{c.PitchDegrees:0.#} degrees down, {kind}";
            }
        }

        public ObservableCollection<string> ScriptLines { get; } = new ObservableCollection<string>();

        private ScriptQuestion _question;
        public ScriptQuestion Question
        {
            get => _question;
            private set
            {
                Set(ref _question, value);
                AnswerOptions.Clear();
                if (value != null) foreach (var o in value.Options) AnswerOptions.Add(o.Label);
                _choiceCursor = value == null ? 0 : Math.Min(Math.Max(0, value.InitialCursor), Math.Max(0, value.Options.Count - 1));
                _choiceScroll = 0;
                KeepCursorOnShow();
                TypedAnswer = "0";
                OnPropertyChanged(nameof(HasQuestion));
                OnPropertyChanged(nameof(HasStatePrompt));
                OnPropertyChanged(nameof(HasChoiceWindow));
                OnPropertyChanged(nameof(HasTouchChoice));
                OnPropertyChanged(nameof(QuestionPrompt));
                OnPropertyChanged(nameof(QuestionTitle));
                OnPropertyChanged(nameof(AcceptsTypedAnswer));
                OnPropertyChanged(nameof(ChoiceItems));
                OnPropertyChanged(nameof(ChoiceAllItems));
                OnPropertyChanged(nameof(TouchChoiceItems));
                OnPropertyChanged(nameof(TouchChoiceIsYesNo));
                OnPropertyChanged(nameof(ChoiceCursor));
                OnPropertyChanged(nameof(ChoiceCursorRow));
                OnPropertyChanged(nameof(ChoiceCursorX));
                OnPropertyChanged(nameof(ChoiceRowOffset));
                OnPropertyChanged(nameof(ChoiceLeft));
                OnPropertyChanged(nameof(ChoiceTop));
                OnPropertyChanged(nameof(ChoiceIndent));
                OnPropertyChanged(nameof(ChoiceWidthTiles));
                OnPropertyChanged(nameof(ChoiceHeightTiles));
                OnPropertyChanged(nameof(ScriptProgressText));
                OnPropertyChanged(nameof(ScriptStatusText));
                RebuildChoiceEntries();
            }
        }

        /// <summary>One entry of a menu on show, for the touch screen buttons.</summary>
        public sealed class ChoiceEntry : INotifyPropertyChanged
        {
            public int Index { get; init; }
            public string Label { get; init; }
            private bool _selected;
            public bool IsSelected
            {
                get => _selected;
                set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ObservableCollection<ChoiceEntry> ChoiceEntries { get; } = new ObservableCollection<ChoiceEntry>();

        private void RebuildChoiceEntries()
        {
            ChoiceEntries.Clear();
            if (_question == null || !_question.IsInGame) return;
            for (int i = 0; i < _question.Options.Count; i++)
                ChoiceEntries.Add(new ChoiceEntry { Index = i, Label = _question.Options[i].Label, IsSelected = i == _choiceCursor });
        }

        private bool ChoiceIsList => _question?.Kind == ScriptQuestion.QuestionKind.Menu && _question.IsList;

        /// <summary>How far the words sit in from the writing area's left edge, leaving room for the cursor.</summary>
        public int ChoiceIndent => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? 8 : ChoiceIsList ? 12 : 11;

        /// <summary>Where the cursor sits: at the edge in a menu, two pixels in on a list.</summary>
        public int ChoiceCursorX => ChoiceIsList ? 2 : 0;

        /// <summary>A list's rows start a pixel lower than a menu's.</summary>
        public int ChoiceRowOffset => ChoiceIsList ? 1 : 0;

        /// <summary>How many rows a list shows before it scrolls.</summary>
        public const int ListRows = 8;

        /// <summary>The yes/no window is six tiles by four; a menu sizes itself to its entries.</summary>
        public int ChoiceWidthTiles => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? 6 : 0;
        public int ChoiceHeightTiles => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? 4
            : ChoiceIsList ? Math.Min(_question.Options.Count, ListRows) * 2 : 0;

        /// <summary>Whether the bottom screen is drawn under the top one.</summary>
        public bool ShowTouchScreen => _stepInto && (IsHeartGold || IsPlatinum);

        /// <summary>A line saying what is going on, at the top of the side panel.</summary>
        public string ScriptStatusText => ScriptRunning ? ScriptProgressText
            : HasStatePrompt ? "Waiting for an answer"
            : "Arrow keys act as the D-pad. Enter acts as A, X acts as B";

        /// <summary>Stops the script where it is, taking the box and any question down.</summary>
        public void StopScript()
        {
            _runner?.Stop();
            _pendingTrigger = null;
            Question = null;
            ClearMessage();
            foreach (var npc in _npcs) { npc.Motion?.StopScript(); if (npc.Motion != null) npc.Motion.Paused = false; }
            _cameraObject = null;
            ResetTouchScreen();
            ScriptLines.Add("Stopped.");
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptStatusText));
            Rebuild();
        }

        public bool HasQuestion => _question != null;

        /// <summary>The preview needs telling something the game would already know.</summary>
        public bool HasStatePrompt => _question != null && !_question.IsInGame;

        /// <summary>A yes/no box or a menu, drawn on the top screen where the game draws it.</summary>
        public bool HasChoiceWindow => _question != null && _question.IsInGame && !_question.OnTouchScreen;

        /// <summary>A yes/no the game asks with buttons on the touch screen.</summary>
        public bool HasTouchChoice => _question != null && _question.IsInGame && _question.OnTouchScreen;

        public string QuestionPrompt => _question?.Prompt ?? "";

        public string QuestionTitle => _question == null ? ""
            : _question.FromPreview ? "Step on it?"
            : _question.Kind == ScriptQuestion.QuestionKind.Flag ? "Is this flag set?"
            : _question.Kind == ScriptQuestion.QuestionKind.Fact ? "Something the game knows"
            : "What does this variable hold?";

        public bool AcceptsTypedAnswer => _question?.AcceptsAnyNumber == true;
        public ObservableCollection<string> AnswerOptions { get; } = new ObservableCollection<string>();

        private string _typedAnswer = "0";
        public string TypedAnswer { get => _typedAnswer; set => Set(ref _typedAnswer, value); }

        /// <summary>The rows of the yes/no box or menu on show; a long list shows eight at a time.</summary>
        public IReadOnlyList<string> ChoiceItems =>
            _question == null ? Array.Empty<string>()
            : ChoiceIsList ? _question.Options.Skip(_choiceScroll).Take(ListRows).Select(o => o.Label).ToList()
            : _question.Options.Select(o => o.Label).ToList();

        /// <summary>Every entry, which is what the window's width is measured from.</summary>
        public IReadOnlyList<string> ChoiceAllItems =>
            _question == null ? Array.Empty<string>() : _question.Options.Select(o => o.Label).ToList();

        private int _choiceCursor;
        private int _choiceScroll;

        /// <summary>The row the cursor is on, counted from the first row on show.</summary>
        public int ChoiceCursorRow => _choiceCursor - _choiceScroll;

        private void KeepCursorOnShow()
        {
            if (!ChoiceIsList) { _choiceScroll = 0; return; }
            if (_choiceCursor < _choiceScroll) _choiceScroll = _choiceCursor;
            if (_choiceCursor >= _choiceScroll + ListRows) _choiceScroll = _choiceCursor - ListRows + 1;
        }

        /// <summary>Which entry the cursor is on.</summary>
        public int ChoiceCursor
        {
            get => _choiceCursor;
            set
            {
                if (_question == null) return;
                int n = _question.Options.Count;
                if (n == 0) return;
                int clamped = Math.Min(Math.Max(0, value), n - 1);
                if (!Set(ref _choiceCursor, clamped)) return;
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
                foreach (var e in ChoiceEntries) e.IsSelected = e.Index == clamped;
                int scroll = _choiceScroll;
                KeepCursorOnShow();
                if (scroll != _choiceScroll) OnPropertyChanged(nameof(ChoiceItems));
                OnPropertyChanged(nameof(ChoiceCursorRow));
                string about = _question.Descriptions != null && clamped < _question.Descriptions.Count ? _question.Descriptions[clamped] : null;
                if (_question.OnTouchScreen && !string.IsNullOrEmpty(about)) ShowDescription(about);
            }
        }

        /// <summary>SEQ_SE_CONFIRM in Platinum and SEQ_SE_DP_SELECT in HGSS, which are the same sequence.</summary>
        public const int MenuSound = 1500;

        /// <summary>Where the window sits on the top screen, in tiles, to its top-left writing corner.</summary>
        public int ChoiceLeft => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? YesNoLeft : _question?.X ?? 0;
        public int ChoiceTop => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? YesNoTop : _question?.Y ?? 0;

        /// <summary>The yes/no window's writing area starts at tile 25, 13 in both games.</summary>
        public const int YesNoLeft = 25, YesNoTop = 13;

        /// <summary>Moves the cursor up or down. Menus of four or more wrap round.</summary>
        public void MoveChoiceCursor(int delta)
        {
            if (_question == null || !_question.IsInGame) return;
            int n = _question.Options.Count;
            if (n == 0 || _touchBlink >= 0) return;
            if (_question.OnTouchScreen)
            {
                int to = global::DSPRE.Avalonia.Data.HgssTouchScreen.Neighbour(n, TouchChoiceIsYesNo, _choiceCursor, 0, Math.Sign(delta));
                if (to >= 0) ChoiceCursor = to;
                return;
            }
            int next = _choiceCursor + delta;
            if (n >= 4 && !ChoiceIsList) next = ((next % n) + n) % n;
            ChoiceCursor = next;
        }

        /// <summary>Left and right on a list jump a page at a time.</summary>
        public void PageChoice(int pages)
        {
            if (_question != null && _question.OnTouchScreen)
            {
                if (_touchBlink >= 0) return;
                int to = global::DSPRE.Avalonia.Data.HgssTouchScreen.Neighbour(_question.Options.Count, TouchChoiceIsYesNo, _choiceCursor, Math.Sign(pages), 0);
                if (to >= 0) ChoiceCursor = to;
                return;
            }
            if (!ChoiceIsList) return;
            ChoiceCursor = _choiceCursor + pages * ListRows;
        }

        /// <summary>A on the yes/no box or menu.</summary>
        public void ConfirmChoice()
        {
            if (_question == null || !_question.IsInGame || _touchBlink >= 0) return;
            PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
            // A touch choice blinks its red frame before the choice counts.
            if (_question.OnTouchScreen) { _touchBlink = 0; OnPropertyChanged(nameof(TouchCursorShown)); return; }
            AnswerOption(_choiceCursor);
        }

        /// <summary>B on the yes/no box, which answers no, or on a menu that lets you back out.</summary>
        public void CancelChoice()
        {
            if (_question == null || !_question.IsInGame || _touchBlink >= 0) return;
            if (_question.OnTouchScreen)
            {
                // B picks NO, or a list's last entry when the list can be backed out of.
                if (_question.Kind == ScriptQuestion.QuestionKind.Menu && !_question.Cancellable) return;
                TouchChoice(_question.Options.Count - 1);
                return;
            }
            if (_question.Kind == ScriptQuestion.QuestionKind.YesNo)
            {
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
                AnswerQuestion(1);
            }
            else if (_question.Cancellable)
            {
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
                AnswerQuestion(ScriptWalker.MenuCancelled);
            }
            else if (!ChoiceIsList)
            {
                // A menu that cannot be backed out of still beeps; the script just never hears about it.
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
            }
        }

        /// <summary>One flag or variable the preview knows, shown so it can be changed or forgotten.</summary>
        public sealed class GameStateEntry : INotifyPropertyChanged
        {
            private readonly ScriptGameState _state;
            public GameStateEntry(ScriptGameState state, bool isFlag, int number, string fact = null)
            { _state = state; IsFlag = isFlag; Number = number; Fact = fact; }

            public bool IsFlag { get; }
            public int Number { get; }
            /// <summary>Set for something the save would know, remembered by what was asked.</summary>
            public string Fact { get; }
            public bool IsVariable => !IsFlag;

            public string Label => Fact != null ? Fact
                : IsFlag ? (Number >= 0x10000 ? $"Trainer flag {Number - 0x10000}" : $"Flag {Number}")
                : FieldScriptValues.Describe(Number);

            public bool IsSet
            {
                get => _state.TryGetFlag(Number, out bool set) && set;
                set => _state.SetFlag(Number, value);
            }

            public string Value
            {
                get => Fact != null ? (_state.TryGetFact(Fact, out long f) ? f.ToString() : "")
                     : _state.TryGetVar(Number, out long v) ? v.ToString() : "";
                set
                {
                    if (!long.TryParse(value, out long v)) return;
                    if (Fact != null) _state.SetFact(Fact, v);
                    else _state.SetVar(Number, v);
                }
            }

            public void Forget()
            {
                if (Fact != null) _state.ForgetFact(Fact);
                else if (IsFlag) _state.ForgetFlag(Number);
                else _state.ForgetVar(Number);
            }

            public event PropertyChangedEventHandler PropertyChanged;
            internal void Refresh()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        /// <summary>Every flag and variable the preview has been told about or seen a script set.</summary>
        public ObservableCollection<GameStateEntry> GameStateEntries { get; } = new ObservableCollection<GameStateEntry>();

        public bool HasGameState => GameStateEntries.Count > 0;

        private void GameStateChanged()
        {
            OnPropertyChanged(nameof(PlayerIsFemale));
            var wanted = GameState.Facts.Keys.OrderBy(k => k).Select(k => (false, -1, k))
                .Concat(GameState.Flags.Keys.OrderBy(k => k).Select(k => (true, k, (string)null)))
                .Concat(GameState.Vars.Keys.OrderBy(k => k).Select(k => (false, k, (string)null)))
                .ToList();

            bool same = wanted.Count == GameStateEntries.Count
                        && wanted.Select((w, i) => GameStateEntries[i].IsFlag == w.Item1 && GameStateEntries[i].Number == w.Item2
                                                   && GameStateEntries[i].Fact == w.Item3).All(x => x);
            if (same)
            {
                foreach (var e in GameStateEntries) e.Refresh();
                return;
            }

            GameStateEntries.Clear();
            foreach (var (isFlag, number, fact) in wanted) GameStateEntries.Add(new GameStateEntry(GameState, isFlag, number, fact));
            OnPropertyChanged(nameof(HasGameState));
        }

        /// <summary>Forgets everything the preview has been told, so the next script asks again.</summary>
        public void ForgetGameState() => GameState.Clear();

        /// <summary>Where the player is standing in the scene.</summary>
        public (float x, float y, float z) PlayerWorldPosition()
        {
            if (Player == null || _tileToWorld == null) return (0f, 0f, 0f);
            return _tileToWorld(Player.DrawX, Player.DrawZ);
        }

        /// <summary>What the camera follows: the player, or the object a script handed it to.</summary>
        private (float x, float y, float z) FollowedPosition()
        {
            if (_cameraObject != null && _tileToWorld != null)
                return _tileToWorld(_cameraObject.TileX + _cameraObject.Motion.DrawOffsetX,
                                    _cameraObject.TileZ + _cameraObject.Motion.DrawOffsetZ);
            return PlayerWorldPosition();
        }

        // Only the camera's height lags; it keeps up with the player across the ground.
        private readonly Queue<float> _cameraTrail = new Queue<float>();

        /// <summary>
        /// Where the camera should be looking: right on the player across the ground, but at the height
        /// they were six frames back, so a flight of steps does not make the whole view bob.
        /// </summary>
        public (float x, float y, float z) CameraTarget()
        {
            var now = FollowedPosition();
            if (_cameraTrail.Count == 0) return now;
            return (now.x, _cameraTrail.Peek(), now.z);
        }

        private void RememberCameraTrail()
        {
            if (Player == null || _tileToWorld == null) return;
            _cameraTrail.Enqueue(FollowedPosition().y);
            // The games keep one more than the delay, so the oldest one held is six frames old.
            while (_cameraTrail.Count > FieldCamera.TrailFrames + 1) _cameraTrail.Dequeue();
        }

        /// <summary>Moves the player, and says what happened.</summary>
        public string Move(MoveFacing dir)
        {
            if (Player == null || _question != null) return null;

            if (ScriptRunning)
            {
                // WaitButton also carries on at a direction, and turns the player to face it.
                if (_runner.ButtonTakesPad)
                {
                    if (_runner.ButtonTurnsPlayer) Player.Face(dir);
                    _runner.Pressed(pad: true);
                    Rebuild();
                }
                return null;
            }

            CloseLeftoverBox();
            var result = Player.Go(dir);
            Rebuild();

            // Every step you take is also a chance for one of the map's own scripts to start
            // (ev_check.c:505 checks these in the same pass as trainer line of sight).
            if (result == StepResult.Walked) CheckLevelScriptWatchers();

            switch (result)
            {
                case StepResult.Blocked: return "There is something in the way.";
                case StepResult.BlockedByEvent: return "Somebody is standing there.";
                default: return null;
            }
        }

        /// <summary>
        /// What happens by standing somewhere rather than pressing anything: a warp says where it goes, and
        /// a trigger runs its script once its watched variable holds the value it waits for.
        /// </summary>
        private void ArriveOnTile()
        {
            var warp = FieldInteraction.WarpAt(_events, Player.TileX, Player.TileZ);
            if (warp != null)
            {
                ScriptLines.Add($"A way through to header {warp.header}, warp {warp.anchor}. "
                                + "The preview stays put.");
                OpenDoorAt(Player.TileX, Player.TileZ);
            }

            var waiting = FieldInteraction.TriggerAt(_events, Player.TileX, Player.TileZ, null);
            if (waiting == null) return;

            // A trigger only goes off when its variable holds the value it waits for.
            if (GameState.TryGetVar(waiting.variableWatched, out long held))
            {
                if (held == waiting.expectedVarValue) { ScriptLines.Clear(); RunScript(waiting.scriptNumber, "The trigger goes off."); }
                return;
            }

            _pendingTrigger = waiting;
            Question = new ScriptQuestion
            {
                Kind = ScriptQuestion.QuestionKind.YesNo,
                FromPreview = true,
                Subject = "the trigger on this tile",
                Prompt = (waiting.scriptNumber == EventFile.NoScript
                            ? "There is a trigger here with no script. It goes off when "
                            : $"There is a trigger here. It runs script {waiting.scriptNumber} when ")
                       + $"{FieldScriptValues.Describe(waiting.variableWatched)} is {waiting.expectedVarValue}.",
                Options = new[] { ("Set it off", 1L), ("Leave it", 0L) },
            };
            ScriptLines.Add(Question.Prompt);
        }

        private Npc _talkTarget;

        /// <summary>Who is standing on a tile right now, wherever they have wandered to.</summary>
        private Npc NpcAt(int x, int z) => _npcs.FirstOrDefault(n =>
            IsOnMap(n) && n.Motion.Visible
            && FieldInteraction.TileX(n.Event) + n.Motion.OffsetX == x
            && FieldInteraction.TileZ(n.Event) + n.Motion.OffsetZ == z);

        /// <summary>A while a script runs, otherwise talks to whatever the player is facing.</summary>
        public void Interact()
        {
            if (Player == null || _question != null) return;
            if (ScriptRunning) { PressA(); return; }
            if (CloseLeftoverBox()) return;

            ScriptLines.Clear();

            var (x, z) = FieldInteraction.TalkTile(Player, _collision);
            var npc = NpcAt(x, z);
            if (npc != null)
            {
                _talkTarget = npc;
                RunScript(npc.Event.scriptNumber, $"You talk to overworld {npc.Event.owID}.");
                return;
            }

            var sign = FieldInteraction.SpawnableAt(_events, x, z, Player.Facing);
            if (sign != null)
            {
                _talkTarget = null;
                string what = (SpawnableKind)sign.type == SpawnableKind.Signboard ? "read the sign"
                            : (SpawnableKind)sign.type == SpawnableKind.HiddenItem ? "find something hidden"
                            : "look at it";
                RunScript(sign.scriptNumber, $"You {what}.");
                return;
            }

            ScriptLines.Add("There is nothing there to talk to.");
        }

        /// <summary>
        /// A box a finished script left up stays drawn in the games until something draws over it; the
        /// preview takes it down the next time you do anything.
        /// </summary>
        private bool CloseLeftoverBox()
        {
            if (ScriptRunning || !_boxOpen) return false;
            ClearMessage();
            return true;
        }

        private void ConfigureWalker(ScriptWalker w)
        {
            w.State = GameState;
            w.CommonScripts = CommonScripts;
            w.ArchiveText = ArchiveText;
            w.SharedArchive = SharedArchive;
            w.MenuText = MenuText;
            w.PlayerPosition = () => Player == null ? ((int, int)?)null : (Player.TileX, Player.TileZ);
        }

        /// <summary>Starts a script playing out, whatever kind of event asked for it.</summary>
        private void RunScript(int scriptNumber, string opening)
        {
            if (!string.IsNullOrEmpty(opening)) ScriptLines.Add(opening);

            int? trainer = TrainerScripts.TrainerIdFor(scriptNumber);
            if (trainer != null)
                ScriptLines.Add($"Script {scriptNumber} is the trainer script for "
                    + $"trainer {trainer}{(TrainerScripts.IsDouble(scriptNumber) ? ", a double battle" : "")}.");

            // Say which file it comes from when it is not the map's own.
            string home = _scriptHome?.Invoke(scriptNumber);
            if (home != null) ScriptLines.Add(home);

            if (_walkerFor == null) { _walker = null; Question = null; return; }

            _walker = _walkerFor(scriptNumber);
            if (_walker == null) { ScriptLines.Add("That script could not be read."); return; }

            ConfigureWalker(_walker);
            _walker.Begin(_walkerStartId?.Invoke(scriptNumber) ?? scriptNumber);
            // The engine notes who was spoken to before the script's first command runs.
            if (_talkTarget != null) GameState.SetVar(ScriptWalker.LastTalkedVar, _talkTarget.Event.owID);

            Question = null;
            _shake = null;
            _cameraMove = null;
            Runner.Play(_walker);
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptProgressText));
            OnPropertyChanged(nameof(ScriptStatusText));
        }

        /// <summary>
        /// Runs a script straight through with nobody to ask, the way the ones that set a map up as it loads
        /// run before anything is on screen.
        /// </summary>
        private void RunWhileLoading(int scriptNumber, string opening)
        {
            ScriptLines.Add(opening);
            var w = _walkerFor?.Invoke(scriptNumber);
            if (w == null) return;

            ConfigureWalker(w);
            w.GuessUnknowns = true;
            w.Begin(_walkerStartId?.Invoke(scriptNumber) ?? scriptNumber);
            while (w.Next()) { }
            // Anything that still stopped to ask is a yes/no or a menu, which cannot come up while loading.
            while (w.Pending != null && !w.Finished) { w.Answer(0); while (w.Next()) { } }

            foreach (var s in w.Steps)
            {
                ScriptLines.Add(s.Text);
                if (s.Effect?.Kind == ScriptEffectKind.ShowObject) ApplyEffect(s.Effect);
            }
            Rebuild();
        }

        /// <summary>Plays the door on a tile, which is the only time the games play one. </summary>
        private void OpenDoorAt(int tileX, int tileZ)
        {
            if (_scene == null) return;

            foreach (var b in _scene.Buildings)
            {
                if (Math.Abs(b.TileX - tileX) > 1 || Math.Abs(b.TileZ - tileZ) > 1) continue;

                var (joints, patterns) = BuildingAnimationSet.DoorAnimations(b.ModelId, _indoor);
                if (joints.Count == 0 && patterns.Count == 0) continue;

                foreach (var j in joints) _playingOnce.Add(new OneShot { Building = b, Joint = j, Frame = 0 });
                foreach (var t in patterns)
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (!_scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = t.IndexOf(name);
                        if (m < 0 || t.IsStatic(m)) continue;
                        _playingOnce.Add(new OneShot { Building = b, Pattern = t, MaterialKey = k, Material = m, Frame = 0 });
                    }

                string sound = BuildingAnimationSet.DoorSound(b.ModelId, _indoor, opening: true);
                if (sound != null) ScriptLines.Add($"The door opens, with the sound of {sound}.");
                return;
            }
        }

        /// <summary>An animation playing through once because something set it off, rather than looping.</summary>
        private sealed class OneShot
        {
            public NsbmdRenderModel.BuildingMaterials Building;
            public JointAnimation Joint;
            public TexturePatternAnimation Pattern;
            public int MaterialKey, Material;
            public int Frame;
            public bool Done;
        }

        private readonly List<OneShot> _playingOnce = new List<OneShot>();

        /// <summary>Answers the question on show, and lets the script carry on.</summary>
        public void AnswerQuestion(long value)
        {
            if (_question == null) return;

            // A trigger asked this, not a running script.
            if (_pendingTrigger != null)
            {
                var trigger = _pendingTrigger;
                _pendingTrigger = null;
                Question = null;

                if (value != 0)
                {
                    // Setting it off means the variable held what it was waiting for.
                    GameState.SetVar(trigger.variableWatched, trigger.expectedVarValue);
                    ScriptLines.Clear();
                    RunScript(trigger.scriptNumber, "The trigger goes off.");
                }
                else
                {
                    ScriptLines.Add("The trigger is left alone.");
                }
                return;
            }

            if (_walker == null) { Question = null; return; }
            _walker.Answer(value);
            Question = null;
        }

        public void AnswerTyped()
        {
            if (long.TryParse(TypedAnswer, out long v)) AnswerQuestion(v);
        }

        public void AnswerOption(int index)
        {
            if (_question != null && index >= 0 && index < _question.Options.Count)
                AnswerQuestion(_question.Options[index].Value);
        }

        // ── the box an NPC talks from ────────────────────────────────
        private FieldTextPrinter _printer;
        private bool _boxOpen;
        private string _boxText;
        private List<FieldMessageFrame> _frames = new List<FieldMessageFrame>();
        private bool _aPressed, _aHeld;

        /// <summary>Measures a run of letters. The window points this at the ROM's own font.</summary>
        public Func<string, int> MeasureText { get; set; } = t => (t ?? "").Length * 6;

        public ObservableCollection<string> TextSpeedNames { get; } =
            new ObservableCollection<string> { "Slow text", "Mid text", "Fast text" };

        private static readonly FieldTextSpeed[] TextSpeeds = { FieldTextSpeed.Slow, FieldTextSpeed.Mid, FieldTextSpeed.Fast };

        private int _textSpeedIndex = 1;

        /// <summary>The Options menu's text speed. New games start on mid.</summary>
        public int TextSpeedIndex
        {
            get => _textSpeedIndex;
            set { if (value >= 0 && value < TextSpeeds.Length) Set(ref _textSpeedIndex, value); }
        }

        public FieldTextSpeed TextSpeed => TextSpeeds[_textSpeedIndex];

        /// <summary>What the box is showing, or null when there is no box.</summary>
        public string MessageText => !_boxOpen ? null : _printer?.Text ?? _boxText ?? "";

        public bool MessageVisible => _boxOpen;

        /// <summary>Whether the arrow is up, waiting for a press to turn the page.</summary>
        public bool MessageHasMore => _printer?.WaitingForPress == true;

        /// <summary>How far the arrow has bobbed down, in DS pixels.</summary>
        public int MessageArrowOffset => _printer?.ArrowOffset ?? 0;

        /// <summary>How far the lines have slid up part way through a scroll, in DS pixels.</summary>
        public int MessageScrollPixels => _printer?.ScrollPixels ?? 0;

        /// <summary>What pressing will do, for the preview to say out loud.</summary>
        public string MessageWaitText
        {
            get
            {
                if (!ScriptRunning) return _boxOpen ? "Any key takes the box down" : "";
                if (MessageHasMore) return "A turns the page";
                if (_printer != null && !_printer.Finished) return "A hurries the text along";
                if (_runner.Waiting == FieldScriptRunner.WaitKind.Button)
                    return _runner.ButtonTakesPad ? "A, B or a direction carries on" : "A or B carries on";
                return "";
            }
        }

        /// <summary>Says so when the text will not fit the box the way it is written.</summary>
        public string MessageWarning
        {
            get
            {
                if (!_boxOpen || _frames.Count == 0) return null;
                bool wide = _frames.Any(f => f.TooWide), tall = _frames.Any(f => f.TooManyLines);
                if (wide && tall) return "This runs past the edge and past the bottom of the box.";
                if (wide) return "A line here runs past the edge of the box.";
                if (tall) return "There are more lines here than the box can show.";
                return null;
            }
        }

        public bool HasMessageWarning => MessageWarning != null;

        /// <summary>The twenty borders the games let the player pick between in Options.</summary>
        public ObservableCollection<string> BorderNames { get; } = new ObservableCollection<string>();

        private int _borderIndex;

        /// <summary>Which border the message box is drawn with.</summary>
        public int BorderIndex
        {
            get => _borderIndex;
            set { if (Set(ref _borderIndex, value)) BorderChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>Raised when a different border is picked, so the window can read it out of the ROM.</summary>
        public event EventHandler BorderChanged;

        /// <summary>Says when the letters are a stand-in rather than the game's own.</summary>
        public string MessageFontNote { get; set; }
        public bool HasMessageFontNote => !string.IsNullOrEmpty(MessageFontNote);

        // What the script actually said, so a changed word can be put back in.
        private string _spoken;

        private void ClearMessage()
        {
            _printer = null;
            _boxOpen = false;
            _boxText = null;
            _spoken = null;
            _frames = new List<FieldMessageFrame>();
            RaiseMessageChanged();
        }

        /// <summary>Starts printing a message into the box, the way a message command does.</summary>
        private bool StartMessage(ScriptEffect effect)
        {
            if (string.IsNullOrEmpty(effect?.Text)) return false;
            _spoken = effect.Text;
            _frames = FieldMessageScript.Frames(ExpandVars(effect.Text), MeasureText);
            _printer = new FieldTextPrinter(_frames, TextSpeed, skippable: effect.B != 1, instant: effect.A == 1)
            {
                PageTurned = () => { if (_playSounds) PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound); },
            };
            _boxOpen = true;
            if (_printer.Finished) { _boxText = _printer.Text; _printer = null; }
            RaiseMessageChanged();
            return true;
        }

        /// <summary>Puts one thing in the box and prints it, the way a message command would.</summary>
        public void ShowMessage(string text) => StartMessage(new ScriptEffect(ScriptEffectKind.Message) { Text = text });

        /// <summary>Lays the box out again with the words put in, for when a word changes.</summary>
        private void LayOutMessage()
        {
            if (_spoken == null || _printer != null) return;
            var frames = FieldMessageScript.Frames(ExpandVars(_spoken), MeasureText);
            if (frames.Count == 0) return;
            _frames = frames;
            _boxText = frames[frames.Count - 1].Text;
            RaiseMessageChanged();
        }

        /// <summary>
        /// The walker writes a message step as a sentence with the words quoted inside it.
        /// </summary>
        public static string Spoken(string stepText)
        {
            if (string.IsNullOrEmpty(stepText)) return "";
            foreach (var (open, close) in new[] { ('“', '”'), ('"', '"') })
            {
                int a = stepText.IndexOf(open);
                int b = stepText.LastIndexOf(close);
                if (a >= 0 && b > a) return stepText.Substring(a + 1, b - a - 1);
            }
            return stepText;
        }

        /// <summary>A or B went down. It is read on the next field frame, the way the games read the keys.</summary>
        public void PressA()
        {
            _aPressed = true;
            _aHeld = true;
        }

        /// <summary>A or B came back up, which stops the text hurrying.</summary>
        public void ReleaseA() => _aHeld = false;

        private void RaiseMessageChanged()
        {
            OnPropertyChanged(nameof(MessageText));
            OnPropertyChanged(nameof(MessageVisible));
            OnPropertyChanged(nameof(MessageHasMore));
            OnPropertyChanged(nameof(MessageArrowOffset));
            OnPropertyChanged(nameof(MessageScrollPixels));
            OnPropertyChanged(nameof(MessageWaitText));
            OnPropertyChanged(nameof(MessageWarning));
            OnPropertyChanged(nameof(HasMessageWarning));
            OnPropertyChanged(nameof(MessageFontNote));
            OnPropertyChanged(nameof(HasMessageFontNote));
        }

        // Which part of the day the preview is showing, which decides what the buildings that change
        // with the clock are doing.
        private FieldTimeZone _timeOfDay = FieldTimeOfDay.Now;
        public FieldTimeZone TimeOfDay
        {
            get => _timeOfDay;
            set
            {
                if (!Set(ref _timeOfDay, value)) return;
                OnPropertyChanged(nameof(TimeOfDayName));
                Reload();
            }
        }

        public string TimeOfDayName => $"{FieldTimeOfDay.Name(_timeOfDay)} ({FieldTimeOfDay.Hours(_timeOfDay)})";

        public ObservableCollection<string> TimesOfDay { get; } = new ObservableCollection<string>(
            new[] { FieldTimeZone.Morning, FieldTimeZone.Noon, FieldTimeZone.Evening, FieldTimeZone.Night, FieldTimeZone.Midnight }
                .Select(z => $"{FieldTimeOfDay.Name(z)}  ·  {FieldTimeOfDay.Hours(z)}"));

        public int TimeOfDayIndex
        {
            get => (int)_timeOfDay;
            set { if (value >= 0 && value <= 4) TimeOfDay = (FieldTimeZone)value; }
        }

        /// <summary>Rebuilds what animates, for when the time of day changes.</summary>
        private void Reload()
        {
            if (_scene == null) return;
            int keepFrame = _frame;
            Load(_scene, _terrain, _events, _indoor, _collision, _seed, _tileToWorld, _walkerFor,
                 _walkerStartId, _scriptHome);
            PlaceNpcs(_footFinder);
            Frame = keepFrame;
            Rebuild();
        }

        private bool _animateTerrain = true;
        public bool AnimateTerrain { get => _animateTerrain; set { if (Set(ref _animateTerrain, value)) Rebuild(); } }

        private string _statusText = "Nothing to preview";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        /// <summary>
        /// Takes the scene the editor is already showing, the terrain animation its area plays (null for
        /// none) and its events, and works out what in it can move.
        /// </summary>
        public void Load(NsbmdRenderModel scene, TextureSrtAnimation terrain, EventFile events, int seed = 0)
            => Load(scene, terrain, events, false, null, seed);

        /// <param name="indoor">Indoor areas take their building animations from a separate list.</param>
        /// <param name="collision">Where the map is closed off, so people stop at walls the way they do
        /// in game. Null lets them walk anywhere their movement range allows.</param>
        public void Load(NsbmdRenderModel scene, TextureSrtAnimation terrain, EventFile events,
                         bool indoor, MapCollisionGrid collision, int seed,
                         Func<float, float, (float x, float y, float z)> tileToWorld = null,
                         Func<int, ScriptWalker> walkerFor = null,
                         Func<int, int> walkerStartId = null,
                         Func<int, string> scriptHome = null,
                         Func<int, IReadOnlyList<ScriptAction>> actionsFor = null)
        {
            _scene = scene; _terrain = terrain; _events = events; _seed = seed;
            _frame = 0;
            _npcs.Clear();
            _animatedMaterials.Clear();
            _swappedMaterials.Clear();
            _movingBuildings = 0;
            _jointBuildings = 0;
            _colourBuildings = 0;
            _doorBuildings = 0;
            _timeBuildings = 0;
            _fadedMaterials.Clear();
            _jointed.Clear();
            _playingOnce.Clear();
            _cameraTrail.Clear();
            _talkTarget = null;
            _cameraObject = null;
            _indoor = indoor;
            _collision = collision;
            _tileToWorld = tileToWorld;
            _walkerFor = walkerFor;
            _walkerStartId = walkerStartId;
            _scriptHome = scriptHome;
            _actionsFor = actionsFor;

            if (scene == null) { StatusText = "Nothing to preview"; return; }

            // People are placed with the same normalised coordinates the scene is drawn in, so a tile has
            // to be measured in those too.
            _tileX = scene.CellStrideX / MapFile.mapSize * scene.Scale;
            _tileZ = scene.CellStrideZ / MapFile.mapSize * scene.Scale;

            // The animation drives materials by name (river, sea_on and so on), the same way the game
            // does. Match those names to the parts of the scene built from them.
            if (_terrain != null)
                foreach (var kv in scene.MaterialNameByKey)
                {
                    int m = _terrain.IndexOf(kv.Value);
                    if (m >= 0 && !_terrain.IsStatic(m)) _animatedMaterials[kv.Key] = (_terrain, m);
                }

            // Buildings animate too, and each one names its own animations, so a building's animation is
            // matched only against that building's own materials.
            foreach (var b in scene.Buildings)
            {
                bool moves = false;
                bool counted = false;

                // A door only opens when something opens it, and a time-of-day animation only runs at
                // the right hour, so neither is played here. Say so rather than leaving them out quietly.
                var waits = BuildingAnimationSet.WaitsFor(b.ModelId, indoor);
                if (waits.Door) _doorBuildings++;
                if (waits.TimeOfDay) _timeBuildings++;
                foreach (var anim in BuildingAnimationSet.ScrollingFor(b.ModelId, indoor, _timeOfDay))
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (_animatedMaterials.ContainsKey(k)) continue;
                        if (!scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = anim.IndexOf(name);
                        if (m < 0 || anim.IsStatic(m)) continue;
                        _animatedMaterials[k] = (anim, m);
                        moves = true;
                    }
                foreach (var anim in BuildingAnimationSet.PatternsFor(b.ModelId, indoor, _timeOfDay))
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (_swappedMaterials.ContainsKey(k)) continue;
                        if (!scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = anim.IndexOf(name);
                        if (m < 0 || anim.IsStatic(m)) continue;
                        _swappedMaterials[k] = (anim, m);
                        moves = true;
                    }
                if (moves) _movingBuildings++;
                foreach (var fade in BuildingAnimationSet.FadesFor(b.ModelId, indoor, _timeOfDay))
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (_fadedMaterials.ContainsKey(k)) continue;
                        if (!scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = fade.IndexOf(name);
                        if (m < 0 || fade.IsStatic(m)) continue;
                        _fadedMaterials[k] = (fade, m);
                        moves = true;
                        counted = true;
                    }
                if (counted) _colourBuildings++;
                foreach (var joint in BuildingAnimationSet.JointsFor(b.ModelId, indoor, _timeOfDay))
                {
                    _jointed.Add((b, joint));
                    _jointBuildings++;
                    moves = true;
                }
            }

            // People: every overworld gets its own motion, seeded so replaying the preview looks the same.
            if (events?.overworlds != null)
            {
                int n = 0;
                foreach (var ow in events.overworlds)
                {
                    var move = OverworldMovements.Find((byte)ow.movement);
                    var facing = (MoveFacing)Math.Min(Math.Max((int)ow.orientation, 0), 3);
                    // The glancing and spinning trainer types take their pace from param1 rather than
                    // the usual wait. Those are the only types that read it at all.
                    OverworldEventType type = null;
                    try { type = OverworldEventTypes.For(RomInfo.gameFamily).FirstOrDefault(t => t.Value == ow.type); } catch { }
                    int interval = type?.Param1Label != null ? ow.param1 : 0;
                    // The engine refuses a step into a closed-off tile, so the preview asks the same
                    // question, in whole-matrix tiles measured from where the event stands.
                    int homeX = ow.xMatrixPosition * MapFile.mapSize + ow.xMapPosition;
                    int homeZ = ow.yMatrixPosition * MapFile.mapSize + ow.yMapPosition;
                    var npc = new Npc { Event = ow };
                    Func<int, int, bool> blocked = (dx, dz) =>
                        (collision != null && !collision.IsEmpty && collision.IsBlocked(homeX + dx, homeZ + dz))
                        || SomebodyOn(homeX + dx, homeZ + dz, npc, true);

                    npc.Motion = new OverworldAnimator(move, facing, ow.xRange, ow.yRange, interval,
                                                       seed + n++, blocked);
                    _npcs.Add(npc);
                }
            }

            BuildFlagList(events);
            OnPropertyChanged(nameof(HasEventFlags));
            OnPropertyChanged(nameof(HiddenSummary));
            _startTile = null;
            _startFacing = MoveFacing.Down;
            Player = MakePlayer();
            BuildStartBesideList();
            OnPropertyChanged(nameof(CanStepInto));

            StatusText = Describe();
            Rebuild();
        }

        private string Describe()
        {
            int people = _npcs.Count(n => IsOnMap(n));
            string water;
            if (_animatedMaterials.Count > 0)
                water = $"{_animatedMaterials.Count} moving surface{(_animatedMaterials.Count == 1 ? "" : "s")}";
            else if (_terrain == null) water = "no terrain animation here";
            else water = "terrain animation touches nothing on this map";

            string buildings = _movingBuildings > 0
                ? $", {_movingBuildings} moving building{(_movingBuildings == 1 ? "" : "s")}" : "";
            string joints = _jointBuildings > 0
                ? $", {_jointBuildings} with moving parts" : "";
            string colours = _colourBuildings > 0
                ? $", {_colourBuildings} fading in and out" : "";
            string doors = _doorBuildings > 0
                ? $", {_doorBuildings} door{(_doorBuildings == 1 ? "" : "s")} that only open when used" : "";
            string times = _timeBuildings > 0
                ? $", {_timeBuildings} showing their {FieldTimeOfDay.Name(_timeOfDay).ToLowerInvariant()} animation" : "";
            int away = HiddenCount;
            string hidden = away > 0 ? $", {away} away on a flag" : "";
            return $"{water}{buildings}{joints}{colours}{doors}{times}, "
                 + $"{people} {(people == 1 ? "person" : "people")}{hidden}";
        }

        /// <summary>Places each person's feet on the ground, which only has to happen when the scene changes.</summary>
        public void PlaceNpcs(Func<Overworld, (float x, float y, float z)> footFinder)
        {
            if (footFinder == null) return;
            _footFinder = footFinder;
            foreach (var npc in _npcs)
            {
                var (x, y, z) = footFinder(npc.Event);
                npc.FootX = x; npc.FootY = y; npc.FootZ = z;
            }
            Rebuild();
        }

        /// <summary>Moves the clock on by however many game frames have passed and rebuilds what is drawn.</summary>
        public void Advance(int frames)
        {
            if (!_playing || frames <= 0) return;
            bool wasRunning = ScriptRunning;
            for (int i = 0; i < frames; i++) FieldFrame();
            Frame = _frame + frames;
            Rebuild();
            if (wasRunning || ScriptRunning || _boxOpen)
            {
                RaiseMessageChanged();
                OnPropertyChanged(nameof(ScriptProgressText));
                OnPropertyChanged(nameof(ScriptStatusText));
            }
        }

        /// <summary>
        /// One pass of the field: the keys are read, the script runs until something makes it wait, then
        /// everybody moves, then the printer runs twice.
        /// </summary>
        private void FieldFrame()
        {
            _scriptFrame++;
            bool press = _aPressed;
            _aPressed = false;

            if (_runner != null && _runner.Running)
            {
                if (press) _runner.Pressed();
                _runner.Advance(1);
                if (!_runner.Running) ScriptFinished();
            }

            foreach (var npc in _npcs) npc.Motion?.Advance(1);
            _cameraObject?.Motion.Advance(1);

            if (_touchSwapFrames > 0)
            {
                _touchSwapFrames--;
                if (_touchSwapFrames == FieldScriptRunner.TouchScreenSwapFrames / 2)
                {
                    _touchShowsChoices = _touchSwapToChoices;
                    OnPropertyChanged(nameof(TouchScreenShowsChoices));
                }
                OnPropertyChanged(nameof(TouchScreenBrightness));
            }
            if (_touchBlink >= 0)
            {
                _touchBlink++;
                OnPropertyChanged(nameof(TouchCursorShown));
                if (_touchBlink >= global::DSPRE.Avalonia.Data.HgssTouchScreen.BlinkFrames)
                {
                    _touchBlink = -1;
                    OnPropertyChanged(nameof(TouchCursorShown));
                    AnswerOption(_choiceCursor);
                }
            }
            int touchLabel = TouchALabel;
            if (touchLabel != _touchLabelShown) { _touchLabelShown = touchLabel; OnPropertyChanged(nameof(TouchALabel)); }

            if (_shake != null)
            {
                _shake.Advance(1);
                if (!_shake.Running) _shake = null;
            }
            _cameraMove?.Advance(1);

            if (Player != null)
            {
                bool walking = Player.IsWalking && !Player.IsScripted;
                Player.Advance(1);
                RememberCameraTrail();
                // Whatever is under the tile only happens once the player has actually arrived on it.
                if (walking && !Player.IsWalking) ArriveOnTile();
            }

            if (_printer != null)
            {
                _printer.Frame(press, _aHeld);
                if (_printer.Finished) _boxText = _printer.Text;
            }

            foreach (var shot in _playingOnce)
            {
                shot.Frame++;
                int length = shot.Joint?.FrameCount ?? shot.Pattern?.FrameCount ?? 1;
                if (shot.Frame >= length) { shot.Frame = length - 1; shot.Done = true; }
            }
            _playingOnce.RemoveAll(x => x.Done && x.Frame <= 0);
        }

        private void ScriptFinished()
        {
            if (_printer != null) { _boxText = _printer.Text; _printer = null; }
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptProgressText));
            OnPropertyChanged(nameof(ScriptStatusText));
            RaiseMessageChanged();
            CheckLevelScriptWatchers();
        }

        /// <summary>Winds the clock back to the start and gives everyone their original facing again.</summary>
        public void Restart()
        {
            Load(_scene, _terrain, _events, _indoor, _collision, _seed, _tileToWorld, _walkerFor,
                 _walkerStartId, _scriptHome);
            PlaceNpcs(_footFinder);
        }

        /// <summary>
        /// Stands the player on an open tile near the middle of what the events cover, since a preview has
        /// no save file to say where they really are.
        /// </summary>
        private FieldPlayer MakePlayer()
        {
            bool haveEvents = _events?.overworlds != null && _events.overworlds.Count > 0;
            if (!haveEvents && StartTile == null) return null;

            // Start where the watcher asked, if they picked somewhere; otherwise stand in the middle of
            // whoever is on the map, which is usually near enough to whatever they came to look at.
            int cx, cz;
            if (StartTile != null) { cx = StartTile.Value.x; cz = StartTile.Value.z; }
            else
            {
                cx = (int)_events.overworlds.Average(o => o.xMatrixPosition * MapFile.mapSize + o.xMapPosition);
                cz = (int)_events.overworlds.Average(o => o.yMatrixPosition * MapFile.mapSize + o.yMapPosition);
            }

            bool Free(int x, int z) => CanStand(x, z);

            // Spiral out from the middle until an open tile turns up.
            for (int r = 0; r < MapFile.mapSize; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        if (Free(cx + dx, cz + dz))
                            return new FieldPlayer(cx + dx, cz + dz, _startFacing, _collision,
                                                   (x, z) => SomebodyOn(x, z, null, false));
                    }
            return null;
        }

        /// <summary>
        /// The mark an emote puts up: two tiles over the head, bouncing up and settling before it holds.
        /// </summary>
        private void AddEmote(List<NsbmdGlControl.SpriteInstance> sprites, float x, float y, float z, int frame, string name)
        {
            var pix = FieldEmoteMarks.For(name);
            if (pix == null) return;
            int bounce = frame >= 1 && frame - 1 < FieldMovementScript.EmoteBounce.Length ? FieldMovementScript.EmoteBounce[frame - 1] : 0;
            if (frame < 1) return;          // the mark appears on the frame after the action starts
            float unit = _tileX / 16f;
            float halfW = _tileX * pix.Width / (OverworldSprites.PixelsPerTile * 2f);
            float halfH = _tileX * pix.Height / (OverworldSprites.PixelsPerTile * 2f);
            sprites.Add(new NsbmdGlControl.SpriteInstance
            {
                Cx = x,
                Cy = y + (32 + bounce) * unit + halfH,
                Cz = z + unit,
                HalfW = halfW,
                HalfH = halfH,
                Rgba = pix.Rgba,
                Width = pix.Width,
                Height = pix.Height,
            });
        }

        private void Rebuild()
        {
            if (_scene == null) return;

            TextureSwaps = null;
            if (_animateTerrain && _swappedMaterials.Count > 0)
            {
                var swaps = new Dictionary<int, string>(_swappedMaterials.Count);
                foreach (var kv in _swappedMaterials)
                {
                    var swap = kv.Value.anim.Evaluate(kv.Value.material, _frame);
                    if (swap.IsSet) swaps[kv.Key] = swap.TextureName;
                }
                if (swaps.Count > 0) TextureSwaps = swaps;
            }

            TextureMatrices = null;
            if (_animateTerrain && _animatedMaterials.Count > 0)
            {
                var mats = new Dictionary<int, float[]>(_animatedMaterials.Count);
                foreach (var kv in _animatedMaterials)
                    mats[kv.Key] = kv.Value.anim.Evaluate(kv.Value.material, _frame).ToMatrix3();
                TextureMatrices = mats;
            }

            MaterialFades = null;
            if (_animateTerrain && _fadedMaterials.Count > 0)
            {
                var fades = new Dictionary<int, float>(_fadedMaterials.Count);
                foreach (var kv in _fadedMaterials)
                {
                    float? v = kv.Value.anim.Evaluate(kv.Value.material, _frame);
                    if (v.HasValue) fades[kv.Key] = v.Value;
                }
                if (fades.Count > 0) MaterialFades = fades;
            }

            MovedParts = null;
            if (_animateTerrain && _jointed.Count > 0)
            {
                var moved = new Dictionary<int, float[]>();
                foreach (var (building, anim) in _jointed)
                {
                    int frame = _frame % Math.Max(1, anim.FrameCount);
                    var rebuilt = NsbmdGeometry.RebuildBuilding(_scene, building,
                        (objectId, part) => anim.MatrixFor(objectId, frame, part, building.Model?.modelScale ?? 1f));
                    foreach (var kv in rebuilt) moved[kv.Key] = kv.Value;
                }
                if (moved.Count > 0) MovedParts = moved;
            }

            // A door that is part-way through opening overrides whatever else drives its parts.
            if (_playingOnce.Count > 0)
            {
                var moved = MovedParts != null ? new Dictionary<int, float[]>(MovedParts) : new Dictionary<int, float[]>();
                var swaps = TextureSwaps != null ? new Dictionary<int, string>(TextureSwaps) : new Dictionary<int, string>();

                foreach (var shot in _playingOnce)
                {
                    if (shot.Joint != null)
                        foreach (var kv in NsbmdGeometry.RebuildBuilding(_scene, shot.Building,
                                     (id, part) => shot.Joint.MatrixFor(id, shot.Frame, part, shot.Building.Model?.modelScale ?? 1f)))
                            moved[kv.Key] = kv.Value;

                    if (shot.Pattern != null)
                    {
                        var swap = shot.Pattern.Evaluate(shot.Material, shot.Frame);
                        if (swap.IsSet) swaps[shot.MaterialKey] = swap.TextureName;
                    }
                }
                if (moved.Count > 0) MovedParts = moved;
                if (swaps.Count > 0) TextureSwaps = swaps;
            }

            var sprites = new List<NsbmdGlControl.SpriteInstance>();
            if (_showPeople)
                foreach (var npc in _npcs)
                {
                    if (!IsOnMap(npc) || !npc.Motion.Visible) continue;
                    var pix = OverworldSprites.Get(npc.Event.overlayTableEntry, (ushort)npc.Motion.Facing,
                                                   PictureFor(npc.Event.overlayTableEntry, npc.Motion.Facing, npc.Motion.Cycle));
                    if (pix == null || pix.Width <= 0 || pix.Height <= 0) continue;
                    float halfW = HalfWidthOf(pix), halfH = HalfHeightOf(pix);
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        // Drawn where it actually is, which is between two tiles while it is walking.
                        Cx = npc.FootX + npc.Motion.DrawOffsetX * _tileX,
                        Cy = npc.FootY + halfH + npc.Motion.HopHeight * _tileX,
                        Cz = npc.FootZ + npc.Motion.DrawOffsetZ * _tileZ,
                        HalfW = halfW,
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                    });
                }
            // Before you step in, the starting point is shown as the player standing there, so picking one
            // off the list or dragging the marker about says plainly where the walk would begin.
            if (!_stepInto && _startTile != null && _tileToWorld != null)
            {
                var pix = OverworldSprites.Get(PlayerSpriteEntry, (ushort)_startFacing,
                                               FieldSpriteAnimation.PictureFor(
                                                   OverworldSprites.FrameCount(PlayerSpriteEntry),
                                                   (int)_startFacing, null));
                if (pix != null && pix.Width > 0 && pix.Height > 0)
                {
                    var foot = _tileToWorld(_startTile.Value.x, _startTile.Value.z);
                    float halfH = HalfHeightOf(pix);
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        Cx = foot.x,
                        Cy = foot.y + halfH,
                        Cz = foot.z,
                        HalfW = HalfWidthOf(pix),
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                    });
                }
            }

            if (_stepInto && Player != null && _tileToWorld != null && Player.Visible)
            {
                var pix = OverworldSprites.Get(PlayerSpriteEntry, (ushort)Player.Facing,
                                               PictureFor(PlayerSpriteEntry, Player.Facing, Player.Cycle));
                if (pix != null && pix.Width > 0 && pix.Height > 0)
                {
                    var foot = _tileToWorld(Player.DrawX, Player.DrawZ);
                    float halfH = HalfHeightOf(pix);
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        Cx = foot.x,
                        Cy = foot.y + halfH + Player.HopHeight * _tileX,
                        Cz = foot.z,
                        HalfW = HalfWidthOf(pix),
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                    });
                }
            }

            if (_showPeople)
            {
                foreach (var npc in _npcs)
                    if (IsOnMap(npc) && npc.Motion.EmoteFrame >= 0)
                        AddEmote(sprites, npc.FootX + npc.Motion.DrawOffsetX * _tileX, npc.FootY,
                                 npc.FootZ + npc.Motion.DrawOffsetZ * _tileZ, npc.Motion.EmoteFrame, npc.Motion.EmoteName);
            }
            if (_stepInto && Player != null && _tileToWorld != null && Player.EmoteFrame >= 0)
            {
                var foot = _tileToWorld(Player.DrawX, Player.DrawZ);
                AddEmote(sprites, foot.x, foot.y, foot.z, Player.EmoteFrame, Player.EmoteName);
            }

            Sprites = sprites;

            FrameAdvanced?.Invoke(this, EventArgs.Empty);
        }
    }
}
