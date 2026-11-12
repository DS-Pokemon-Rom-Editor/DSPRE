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
    public sealed partial class AnimatedPreviewViewModel
    {
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

        public void SetStringVars(IEnumerable<FieldStringVar> vars)
        {
            StringVars.Clear();
            _stringVarByKey.Clear();
            if (vars != null)
                foreach (FieldStringVar v in vars)
                {
                    StringVarEntry e = new StringVarEntry(v, StringVarsChanged);
                    StringVars.Add(e);
                    _stringVarByKey[v.Key] = e;
                }
            OnPropertyChanged(nameof(HasStringVars));
            OnPropertyChanged(nameof(StringVarSummary));
        }

        private void StringVarsChanged()
        {
            LayOutMessage();
            StatusText = Describe();
        }

        public string ExpandVars(string line) =>
            FieldStringVars.Expand(line, (family, kind, buffer) =>
                _stringVarByKey.TryGetValue(FieldStringVars.KeyOf(family, kind, buffer), out StringVarEntry e)
                    ? e.Value
                    : FieldStringVars.SuggestFor(kind, buffer, null));

        public LevelScriptFile LevelScripts
        {
            get => _levelScripts;
            set { _levelScripts = value; BuildLevelScriptList(); }
        }
        private LevelScriptFile _levelScripts;

        public ObservableCollection<string> LevelScriptArrivals { get; } = new ObservableCollection<string>();

        public ObservableCollection<LevelScriptWatcher> LevelScriptWatchers { get; } =
            new ObservableCollection<LevelScriptWatcher>();

        public bool HasLevelScripts => LevelScriptArrivals.Count > 0 || LevelScriptWatchers.Count > 0;

        public sealed class LevelScriptWatcher : INotifyPropertyChanged
        {
            private readonly Action<int, int> _set;
            public LevelScriptWatcher(VariableValueTrigger trigger, Action<int, int> set)
            { Trigger = trigger; _set = set; }

            public VariableValueTrigger Trigger { get; }

            public string Label =>
                $"{FieldScriptValues.Describe(Trigger.VariableToWatch)} = {Trigger.ExpectedValue}"
                + $"  ·  script {Trigger.ScriptTriggered}";

            private int _value;
            public int Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    _set?.Invoke(Trigger.VariableToWatch, value);
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        // Watchers that have gone off, so each runs once per visit.
        private readonly HashSet<VariableValueTrigger> _firedWatchers = new HashSet<VariableValueTrigger>();

        private void BuildLevelScriptList()
        {
            LevelScriptArrivals.Clear();
            LevelScriptWatchers.Clear();
            foreach (LevelScriptTrigger t in FieldLevelScripts.OnArrival(_levelScripts))
                LevelScriptArrivals.Add($"{FieldLevelScripts.WhenItRuns(t)}: script {t.ScriptTriggered}");
            foreach (VariableValueTrigger t in FieldLevelScripts.Watchers(_levelScripts))
                LevelScriptWatchers.Add(new LevelScriptWatcher(t, SetVariable));
            OnPropertyChanged(nameof(HasLevelScripts));
            OnPropertyChanged(nameof(LevelScriptSummary));
        }

        public string LevelScriptSummary
        {
            get
            {
                int n = LevelScriptArrivals.Count + LevelScriptWatchers.Count;
                if (n == 0) return "This map has no level scripts.";
                return $"{n} level script {(n == 1 ? "entry" : "entries")}";
            }
        }

        // On arrival the games run the map change, then the two field-setup passes, in that order.
        private void RunArrivalLevelScripts()
        {
            _firedWatchers.Clear();
            foreach (LevelScriptTrigger t in FieldLevelScripts.OnArrival(_levelScripts))
                RunWhileLoading(t.ScriptTriggered,
                    $"{FieldLevelScripts.WhenItRuns(t)}, so the map runs script {t.ScriptTriggered}.");

            // These run before anybody is put on the map, so a flag they set decides who is there.
            foreach (Npc npc in _npcs)
                if (npc.OnMap == null && npc.Event.Flag != 0 && GameState.TryGetFlag(npc.Event.Flag, out bool set))
                    npc.OnMap = !set;
            Rebuild();
        }

        // The engine tries these on every step, in the same check as trainer line of sight.
        private void CheckLevelScriptWatchers()
        {
            if (_levelScripts == null || !_stepInto || ScriptRunning) return;

            foreach (VariableValueTrigger t in FieldLevelScripts.Watchers(_levelScripts))
            {
                if (_firedWatchers.Contains(t)) continue;
                if (!FieldLevelScripts.IsSatisfied(t, VariableValue)) continue;
                _firedWatchers.Add(t);
                ScriptLines.Add($"{FieldScriptValues.Describe(t.VariableToWatch)} holds {VariableValue(t.VariableToWatch)}, "
                                + $"so the map starts script {t.ScriptTriggered}.");
                RunScript(t.ScriptTriggered, "");
                return;                          // the engine takes the first one and stops
            }
        }

        private int VariableValue(int variable) =>
            GameState.TryGetVar(variable, out long v) ? (int)v : 0;

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
                    foreach (Npc npc in _npcs) { npc.Motion?.StopScript(); if (npc.Motion != null) npc.Motion.Paused = false; npc.OnMap = null; }
                    _firedWatchers.Clear();
                    ResetTouchScreen();
                }
                else
                {
                    // Stepping in counts as arriving, so the map's setup level scripts run.
                    RunArrivalLevelScripts();
                }
                OnPropertyChanged(nameof(CanStepInto));
                OnPropertyChanged(nameof(ShowTouchScreen));
                OnPropertyChanged(nameof(ScriptStatusText));
                Rebuild();
            }
        }

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

        public ObservableCollection<EventFlagSwitch> EventFlags { get; } = new ObservableCollection<EventFlagSwitch>();

        private readonly HashSet<ushort> _flagsSet = new HashSet<ushort>();

        public bool FlagIsSet(ushort flag) => _flagsSet.Contains(flag);

        public bool IsPresent(Overworld ow) => FieldInteraction.IsPresent(ow, FlagIsSet);

        private bool IsOnMap(Npc n) => n.OnMap ?? IsPresent(n.Event);

        private bool SomebodyOn(int x, int z, Npc except, bool countPlayer)
        {
            foreach (Npc npc in _npcs)
            {
                if (npc == except) continue;
                if (!IsOnMap(npc) || !npc.Motion.Visible) continue;
                int hx = FieldInteraction.TileX(npc.Event), hz = FieldInteraction.TileZ(npc.Event);
                if (hx + npc.Motion.OffsetX == x && hz + npc.Motion.OffsetZ == z) return true;
                if (hx + npc.Motion.FromOffsetX == x && hz + npc.Motion.FromOffsetZ == z) return true;
            }

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
            foreach (EventFlagSwitch f in EventFlags)
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

        public int HiddenCount => _npcs.Count(n => !IsOnMap(n));

        public string HiddenSummary => HiddenCount == 0
            ? "Everybody is on the map."
            : $"{HiddenCount} {(HiddenCount == 1 ? "person is hidden by a flag" : "people are hidden by flags")}.";

        public IReadOnlyList<Npc> Npcs => _npcs;

        public float TileWidth => _tileX;

        // Sixteen pixels of overworld art make a tile, so a 32 by 32 sprite is two tiles each way.
        private float HalfHeightOf(OverworldSprites.SpritePixels pix) =>
            _tileX * pix.Height / (OverworldSprites.PixelsPerTile * 2f);

        private float HalfWidthOf(OverworldSprites.SpritePixels pix) =>
            _tileX * pix.Width / (OverworldSprites.PixelsPerTile * 2f);

        // The bank's picture count paces a person, a following Pokemon and the hero as the games do.
        private static int PictureFor(ushort entry, MoveFacing facing, FieldWalkCycle cycle) =>
            FieldSpriteAnimation.PictureFor(OverworldSprites.FrameCount(entry), (int)facing, cycle);

        public bool HasEventFlags => EventFlags.Count > 0;

        public EventFile Events => _events;

        public sealed class GameStateEntry : INotifyPropertyChanged
        {
            private readonly ScriptGameState _state;
            public GameStateEntry(ScriptGameState state, bool isFlag, int number, string fact = null)
            { _state = state; IsFlag = isFlag; Number = number; Fact = fact; }

            public bool IsFlag { get; }
            public int Number { get; }
            /// <summary>Set when the answer comes from the save, keyed by the question asked.</summary>
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

        public ObservableCollection<GameStateEntry> GameStateEntries { get; } = new ObservableCollection<GameStateEntry>();

        public bool HasGameState => GameStateEntries.Count > 0;

        private void GameStateChanged()
        {
            OnPropertyChanged(nameof(PlayerIsFemale));
            List<(bool, int, string)> wanted = GameState.Facts.Keys.OrderBy(k => k).Select(k => (false, -1, k))
                .Concat(GameState.Flags.Keys.OrderBy(k => k).Select(k => (true, k, (string)null)))
                .Concat(GameState.Vars.Keys.OrderBy(k => k).Select(k => (false, k, (string)null)))
                .ToList();

            bool same = wanted.Count == GameStateEntries.Count
                        && wanted.Select((w, i) => GameStateEntries[i].IsFlag == w.Item1 && GameStateEntries[i].Number == w.Item2
                                                   && GameStateEntries[i].Fact == w.Item3).All(x => x);
            if (same)
            {
                foreach (GameStateEntry e in GameStateEntries) e.Refresh();
                return;
            }

            GameStateEntries.Clear();
            foreach ((bool isFlag, int number, string fact) in wanted) GameStateEntries.Add(new GameStateEntry(GameState, isFlag, number, fact));
            OnPropertyChanged(nameof(HasGameState));
        }

        public void ForgetGameState() => GameState.Clear();
    }
}
