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

        // What the watcher has said the game's variables hold, so a trigger is only asked about once.
        private readonly Dictionary<ushort, int> _variables = new Dictionary<ushort, int>();

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
            if (_spokenLines.Count > 0) LayOutMessage();
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
            _arriving.Clear();
            foreach (var t in FieldLevelScripts.OnArrival(_levelScripts)) _arriving.Enqueue(t);
            RunNextArrivalScript();
        }

        /// <summary>Starts the next script the map runs on arrival. </summary>
        private void RunNextArrivalScript()
        {
            while (_arriving.Count > 0 && !ScriptRunning)
            {
                var t = _arriving.Dequeue();
                ScriptLines.Add($"{FieldLevelScripts.WhenItRuns(t)}, so the map starts script {t.scriptTriggered}.");
                RunScript(t.scriptTriggered, "");
            }
        }

        private readonly Queue<LevelScriptTrigger> _arriving = new Queue<LevelScriptTrigger>();

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
            _variables.TryGetValue((ushort)variable, out int v) ? v : 0;

        /// <summary>
        /// Sets one of the map's variables, which is how somebody makes a watching level script go off
        /// without having to play the game up to that point.
        /// </summary>
        public void SetVariable(int variable, int value)
        {
            _variables[(ushort)variable] = value;
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
                    _runner?.Stop(); _shake = null; _cameraMove = null;
                    foreach (var npc in _npcs) npc.Motion?.StopScript();
                    _firedWatchers.Clear();
                    _arriving.Clear();
                }
                else
                {
                    // Stepping onto the map is arriving on it, which is when the games run the level
                    // scripts that set the place up.
                    RunArrivalLevelScripts();
                }
                OnPropertyChanged(nameof(CanStepInto));
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

        /// <summary>Stands the player next to a tile, facing it. </summary>
        public void StandBeside(int ox, int oz)
        {
            // Standing one tile away in each direction, looking back the other way.
            var tries = new (int dx, int dz, MoveFacing look)[]
            {
                (0, 1, MoveFacing.Up),        // below it, looking up
                (0, -1, MoveFacing.Down),
                (1, 0, MoveFacing.Left),
                (-1, 0, MoveFacing.Right),
            };

            foreach (var (dx, dz, look) in tries)
            {
                int x = ox + dx, z = oz + dz;
                bool blocked = _collision != null && !_collision.IsEmpty && _collision.IsBlocked(x, z);
                if (blocked || SomebodyOn(x, z, null, false)) continue;
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
            bool Free(int tx, int tz) =>
                (_collision == null || _collision.IsEmpty || !_collision.IsBlocked(tx, tz))
                && !SomebodyOn(tx, tz, null, false);

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
                    StartBesideNames.Add($"Beside trigger {i}, script {t.scriptNumber}");
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

        /// <summary>Whether somebody is standing on a tile, or on their way onto it. </summary>
        private bool SomebodyOn(int x, int z, Npc except, bool countPlayer)
        {
            foreach (var npc in _npcs)
            {
                if (npc == except) continue;
                if (!IsPresent(npc.Event) || !npc.Motion.Visible) continue;
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
            foreach (var f in EventFlags) if (f.IsSet) _flagsSet.Add(f.Number);

            OnPropertyChanged(nameof(HiddenCount));
            OnPropertyChanged(nameof(HiddenSummary));
            StatusText = Describe();
            Rebuild();
        }

        /// <summary>How many people the flags are currently taking off the map.</summary>
        public int HiddenCount => _npcs.Count(n => !IsPresent(n.Event));

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
        private static int PictureFor(ushort entry, MoveFacing facing, int cell, bool walking) =>
            FieldSpriteAnimation.PictureFor(OverworldSprites.FrameCount(entry), (int)facing, cell, walking);

        /// <summary>Whether any overworld here is gated on a flag at all.</summary>
        public bool HasEventFlags => EventFlags.Count > 0;

        /// <summary>The events being previewed.</summary>
        public EventFile Events => _events;

        // ── playing a script out on the clock ───────────────────────────
        private FieldScriptRunner _runner;
        private FieldCameraShake _shake;
        private Func<int, IReadOnlyList<ScriptAction>> _actionsFor;

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
        public string ScriptProgressText => _runner == null || !_runner.Running
            ? "" : $"Step {_runner.StepIndex} of {_runner.StepCount}";

        private FieldScriptRunner Runner => _runner ??= new FieldScriptRunner(new FieldScriptRunner.Hooks
        {
            StartMovement = StartMovement,
            PlaySound = (kind, id) => { if (_playSounds) PlaySound?.Invoke(kind, id); },
            ShakeCamera = (x, y, count, frames) => _shake = new FieldCameraShake(x, y, count, frames),
            MoveCamera = row =>
            {
                if (!FieldCameraMove.Exists(row)) return 0;
                _cameraMove = new FieldCameraMove(row, CameraEntry.PitchDegrees);
                return _cameraMove.TotalFrames;
            },
            ShowMessage = text => { ShowMessage(Spoken(text)); return MessageVisible; },
            Report = step => ScriptLines.Add(step.Text),
        });

        /// <summary>
        /// Sets an overworld walking through a movement, and says how long it will take so the script waits
        /// for it.
        /// </summary>
        private int StartMovement(int overworldId, int movementNumber)
        {
            var actions = _actionsFor?.Invoke(movementNumber);
            var steps = FieldMovementScript.Parse(actions);
            if (steps.Count == 0) return 0;

            var npc = _npcs.FirstOrDefault(n => n.Event.owID == overworldId);
            if (npc == null) return 0;

            npc.Motion.PlayScript(steps);
            return FieldMovementScript.TotalFrames(steps);
        }

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

        public FieldCameraEntry CameraEntry => FieldCamera.Entry(_cameraId);

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
                OnPropertyChanged(nameof(HasQuestion));
                OnPropertyChanged(nameof(QuestionPrompt));
                AnswerOptions.Clear();
                if (value != null) foreach (var o in value.Options) AnswerOptions.Add(o.Label);
                OnPropertyChanged(nameof(AcceptsTypedAnswer));
            }
        }
        public bool HasQuestion => _question != null;
        public string QuestionPrompt => _question?.Prompt ?? "";
        public bool AcceptsTypedAnswer => _question?.AcceptsAnyNumber == true;
        public ObservableCollection<string> AnswerOptions { get; } = new ObservableCollection<string>();

        private string _typedAnswer = "0";
        public string TypedAnswer { get => _typedAnswer; set => Set(ref _typedAnswer, value); }

        /// <summary>Where the player is standing in the scene.</summary>
        public (float x, float y, float z) PlayerWorldPosition()
        {
            if (Player == null || _tileToWorld == null) return (0f, 0f, 0f);
            return _tileToWorld(Player.DrawX, Player.DrawZ);
        }

        // Only the camera's height lags; it keeps up with the player across the ground.
        private readonly Queue<float> _cameraTrail = new Queue<float>();

        /// <summary>
        /// Where the camera should be looking: right on the player across the ground, but at the height
        /// they were six frames back, so a flight of steps does not make the whole view bob.
        /// </summary>
        public (float x, float y, float z) CameraTarget()
        {
            var now = PlayerWorldPosition();
            if (_cameraTrail.Count == 0) return now;
            return (now.x, _cameraTrail.Peek(), now.z);
        }

        private void RememberCameraTrail()
        {
            if (Player == null || _tileToWorld == null) return;
            _cameraTrail.Enqueue(PlayerWorldPosition().y);
            // The games keep one more than the delay, so the oldest one held is six frames old.
            while (_cameraTrail.Count > FieldCamera.TrailFrames + 1) _cameraTrail.Dequeue();
        }

        /// <summary>Moves the player, and says what happened.</summary>
        public string Move(MoveFacing dir)
        {
            if (Player == null || _question != null) return null;
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
                ScriptLines.Add($"This is a way through to header {warp.header}, arriving at its warp {warp.anchor}. "
                                + "The preview stays where it is.");
                OpenDoorAt(Player.TileX, Player.TileZ);
            }

            // Whether a trigger would really go off depends on a variable the preview has no way of
            // knowing, so rather than guess it asks outright whether to set it off.
            var waiting = FieldInteraction.TriggerAt(_events, Player.TileX, Player.TileZ, null);
            if (waiting == null) return;

            _pendingTrigger = waiting;
            Question = new ScriptQuestion
            {
                Kind = ScriptQuestion.QuestionKind.YesNo,
                Subject = $"the trigger on this tile",
                Prompt = $"There is a trigger here. It runs script {waiting.scriptNumber} when variable "
                       + $"{waiting.variableWatched} is {waiting.expectedVarValue}. Set it off?",
                Options = new[] { ("Set it off", 1L), ("Leave it", 0L) },
            };
            ScriptLines.Add(Question.Prompt);
        }

        /// <summary>Talks to whatever the player is facing and runs its script. </summary>
        public void Interact()
        {
            if (Player == null || _question != null) return;

            // A box already open means the player is reading; the key press turns the page instead.
            if (MessageVisible) { AdvanceMessage(); return; }

            ScriptLines.Clear();
            ClearMessage();

            var (x, z) = FieldInteraction.TalkTile(Player, _collision);
            var ow = FieldInteraction.OverworldAt(_events, x, z, FlagIsSet);
            if (ow != null) { RunScript(ow.scriptNumber, $"You talk to overworld {ow.owID}."); return; }

            var sign = FieldInteraction.SpawnableAt(_events, x, z, Player.Facing);
            if (sign != null)
            {
                string what = (SpawnableKind)sign.type == SpawnableKind.Signboard ? "read the sign"
                            : (SpawnableKind)sign.type == SpawnableKind.HiddenItem ? "find something hidden"
                            : "look at it";
                RunScript(sign.scriptNumber, $"You {what}.");
                return;
            }

            ScriptLines.Add("There is nothing there to talk to.");
        }

        /// <summary>Starts the script viewer on one script, whatever kind of event asked for it.</summary>
        private void RunScript(int scriptNumber, string opening)
        {
            ScriptLines.Add(opening);

            int? trainer = TrainerScripts.TrainerIdFor(scriptNumber);
            if (trainer != null)
                ScriptLines.Add($"Script {scriptNumber} is one of the trainer scripts, which stands for "
                    + $"trainer {trainer}{(TrainerScripts.IsDouble(scriptNumber) ? ", a two against two battle" : "")}.");

            // Say which file it comes from when it is not the map's own.
            string home = _scriptHome?.Invoke(scriptNumber);
            if (home != null) ScriptLines.Add(home);

            if (_walkerFor == null) { _walker = null; Question = null; return; }

            _walker = _walkerFor(scriptNumber);
            if (_walker == null) { ScriptLines.Add("That script could not be read."); return; }

            _walker.Start(_walkerStartId?.Invoke(scriptNumber) ?? scriptNumber);

            Question = _walker.Pending;
            _shake = null;
            _cameraMove = null;
            Runner.Play(_walker.Steps);
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptProgressText));
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

        /// <summary>Answers the question the script stopped on, and lets it carry on.</summary>
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
                    _variables[trigger.variableWatched] = trigger.expectedVarValue;
                    RunScript(trigger.scriptNumber, "The trigger goes off.");
                }
                else
                {
                    ScriptLines.Add("The trigger is left alone.");
                }
                return;
            }

            if (_walker == null) return;
            _walker.Answer(value);
            ShowWalkerState();
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

        private void ShowWalkerState()
        {
            ScriptLines.Clear();
            foreach (var step in _walker.Steps) ScriptLines.Add(step.Text);
            Question = _walker.Pending;
            ShowMessagesFrom(_walker.Steps);
        }

        // ── the box an NPC talks from ────────────────────────────────
        private readonly List<FieldMessageFrame> _frames = new List<FieldMessageFrame>();
        private int _frameIndex;

        /// <summary>Measures a run of letters. The window points this at the ROM's own font.</summary>
        public Func<string, int> MeasureText { get; set; } = t => (t ?? "").Length * 6;

        private FieldMessageFrame Current =>
            _frameIndex >= 0 && _frameIndex < _frames.Count ? _frames[_frameIndex] : null;

        /// <summary>What the box is showing, or null when there is no box.</summary>
        public string MessageText => Current?.Text;

        public bool MessageVisible => Current != null;

        /// <summary>Whether the box is waiting for the player before it goes on.</summary>
        public bool MessageHasMore => Current != null && Current.Wait != MessageWait.None;

        /// <summary>What pressing will do, for the preview to say out loud.</summary>
        public string MessageWaitText
        {
            get
            {
                var f = Current;
                if (f == null) return "";
                switch (f.Wait)
                {
                    case MessageWait.Clear: return "A or B clears the box and starts again";
                    case MessageWait.Scroll: return "A or B scrolls up a line";
                    case MessageWait.Simple: return "A or B carries on";
                    default: return "A or B closes the box";
                }
            }
        }

        /// <summary>Says so when the text will not fit the box the way it is written.</summary>
        public string MessageWarning
        {
            get
            {
                var f = Current;
                if (f == null) return null;
                if (f.TooWide && f.TooManyLines) return "This runs past the edge and past the bottom of the box.";
                if (f.TooWide) return "A line here runs past the edge of the box.";
                if (f.TooManyLines) return "There are more lines here than the box can show.";
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

        // What the script actually said, gaps and all.
        private readonly List<string> _spokenLines = new List<string>();

        private void ClearMessage()
        {
            _frames.Clear();
            _spokenLines.Clear();
            _frameIndex = 0;
            RaiseMessageChanged();
        }

        private void ShowMessagesFrom(IReadOnlyList<ScriptStep> steps)
        {
            if (steps == null) { ClearMessage(); return; }
            ShowMessages(steps.Where(s => s.Kind == ScriptStepKind.Message).Select(s => Spoken(s.Text)));
        }

        /// <summary>Puts one thing in the box, played out the way the games would play it.</summary>
        public void ShowMessage(string text) => ShowMessages(new[] { text });

        /// <summary>Puts several things in the box, one after another.</summary>
        public void ShowMessages(IEnumerable<string> texts)
        {
            _spokenLines.Clear();
            if (texts != null)
                foreach (string t in texts)
                    if (!string.IsNullOrWhiteSpace(t))
                        _spokenLines.Add(t);
            _frameIndex = 0;
            LayOutMessage();
        }

        /// <summary>Lays the box out from what the script said, with the words put in. </summary>
        private void LayOutMessage()
        {
            _frames.Clear();
            foreach (string t in _spokenLines)
                _frames.AddRange(FieldMessageScript.Frames(ExpandVars(t), MeasureText));
            if (_frameIndex >= _frames.Count) _frameIndex = Math.Max(0, _frames.Count - 1);
            RaiseMessageChanged();
        }

        /// <summary>
        /// The walker writes a message step as a sentence with the words quoted inside it.
        /// </summary>
        public static string Spoken(string stepText)
        {
            if (string.IsNullOrEmpty(stepText)) return "";
            foreach (var (open, close) in new[] { ('\u201c', '\u201d'), ('"', '"') })
            {
                int a = stepText.IndexOf(open);
                int b = stepText.LastIndexOf(close);
                if (a >= 0 && b > a) return stepText.Substring(a + 1, b - a - 1);
            }
            return stepText;
        }

        /// <summary>The player presses: on to the next thing the box does, or shut it.</summary>
        public void AdvanceMessage()
        {
            if (!MessageVisible) return;
            if (MessageHasMore) _frameIndex++;
            else
            {
                _frames.Clear();
                _frameIndex = 0;
                _runner?.ReaderMovedOn();     // the script was waiting on this being read
            }
            RaiseMessageChanged();
        }

        private void RaiseMessageChanged()
        {
            OnPropertyChanged(nameof(MessageText));
            OnPropertyChanged(nameof(MessageVisible));
            OnPropertyChanged(nameof(MessageHasMore));
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
            int people = _npcs.Count(n => IsPresent(n.Event));
            string water;
            if (_animatedMaterials.Count > 0)
                water = $"{_animatedMaterials.Count} moving surface{(_animatedMaterials.Count == 1 ? "" : "s")}";
            else if (_terrain == null) water = "this area plays no terrain animation";
            else water = "the area's terrain animation doesn't touch anything on this map";

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
            if (!_playing || frames <= 0 || _scene == null) return;
            foreach (var npc in _npcs) npc.Motion?.Advance(frames);

            if (_shake != null)
            {
                _shake.Advance(frames);
                if (!_shake.Running) _shake = null;
            }
            _cameraMove?.Advance(frames);
            if (_runner != null && _runner.Running)
            {
                _runner.Advance(frames);
                OnPropertyChanged(nameof(ScriptProgressText));
                if (!_runner.Running)
                {
                    OnPropertyChanged(nameof(ScriptRunning));
                    // Whatever the map still had queued up gets its turn now.
                    if (_arriving.Count > 0) RunNextArrivalScript();
                    else CheckLevelScriptWatchers();
                }
            }

            if (Player != null)
            {
                bool wasWalking = Player.IsWalking;
                for (int i = 0; i < frames; i++) { Player.Advance(1); RememberCameraTrail(); }
                // Whatever is under the tile only happens once the player has actually arrived on it.
                if (wasWalking && !Player.IsWalking) ArriveOnTile();
            }

            foreach (var shot in _playingOnce)
            {
                shot.Frame += frames;
                int length = shot.Joint?.FrameCount ?? shot.Pattern?.FrameCount ?? 1;
                if (shot.Frame >= length) { shot.Frame = length - 1; shot.Done = true; }
            }
            _playingOnce.RemoveAll(x => x.Done && x.Frame <= 0);
            Frame = _frame + frames;
            Rebuild();
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

            bool Free(int x, int z) =>
                (_collision == null || _collision.IsEmpty || !_collision.IsBlocked(x, z))
                && !SomebodyOn(x, z, null, false);

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
                    if (!IsPresent(npc.Event) || !npc.Motion.Visible) continue;
                    var pix = OverworldSprites.Get(npc.Event.overlayTableEntry, (ushort)npc.Motion.Facing,
                                                   PictureFor(npc.Event.overlayTableEntry, npc.Motion.Facing,
                                                              npc.Motion.AnimationCell, npc.Motion.IsWalking));
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
                                                   (int)_startFacing, 0, false));
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

            if (_stepInto && Player != null && _tileToWorld != null)
            {
                var pix = OverworldSprites.Get(PlayerSpriteEntry, (ushort)Player.Facing,
                                               PictureFor(PlayerSpriteEntry, Player.Facing,
                                                          Player.AnimationCell, Player.IsWalking));
                if (pix != null && pix.Width > 0 && pix.Height > 0)
                {
                    var foot = _tileToWorld(Player.DrawX, Player.DrawZ);
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

            Sprites = sprites;

            FrameAdvanced?.Invoke(this, EventArgs.Empty);
        }
    }
}
