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
    /// <summary>Drives the animated preview: a map running as it would in game.</summary>
    public sealed partial class AnimatedPreviewViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        /// <summary>An 8-frame walking step is 3.75 tiles a second.</summary>
        public const int FramesPerSecond = 30;

        public const ushort PlayerSpriteEntry = 0;

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
        private SceneBuildingAnimator _buildings;
        private int _movingBuildings;
        private int _jointBuildings;
        private int _colourBuildings;
        private int _doorBuildings;
        private int _timeBuildings;
        private float _tileX, _tileZ;

        public NsbmdRenderModel Scene => _scene;

        public IReadOnlyList<NsbmdGlControl.SpriteInstance> Sprites { get; private set; }
            = Array.Empty<NsbmdGlControl.SpriteInstance>();

        public Dictionary<int, float[]> TextureMatrices { get; private set; }

        public Dictionary<int, string> TextureSwaps { get; private set; }

        public Dictionary<int, float[]> MovedParts { get; private set; }

        public Dictionary<int, float> MaterialFades { get; private set; }

        public IReadOnlyList<MoveFacing> Facings => _npcs.Select(n => n.Motion.Facing).ToArray();

        public event EventHandler FrameAdvanced;

        private int _frame;
        public int Frame { get => _frame; private set { if (Set(ref _frame, value)) { OnPropertyChanged(nameof(TimeText)); } } }

        public string TimeText => $"{_frame / (float)FramesPerSecond:0.0} s";

        private bool _playing = true;
        public bool Playing { get => _playing; set { if (Set(ref _playing, value)) OnPropertyChanged(nameof(PlayPauseText)); } }
        public string PlayPauseText => _playing ? "Pause" : "Play";

        private double _speed = 1.0;
        public double Speed { get => _speed; set => Set(ref _speed, value); }
        public ObservableCollection<string> SpeedNames { get; } =
            new ObservableCollection<string> { "0.25×", "0.5×", "1×", "2×" };
        private static readonly double[] SpeedValues = { 0.25, 0.5, 1.0, 2.0 };

        private int _speedIndex = 2;
        public int SpeedIndex { get => _speedIndex; set { if (Set(ref _speedIndex, value) && value >= 0 && value < SpeedValues.Length) Speed = SpeedValues[value]; } }

        private bool _showPeople = true;
        public bool ShowPeople { get => _showPeople; set { if (Set(ref _showPeople, value)) Rebuild(); } }

        public FieldPlayer Player { get; private set; }

        private bool _showLevelScripts;
        public bool ShowLevelScripts
        {
            get => _showLevelScripts;
            set => Set(ref _showLevelScripts, value);
        }

        private bool _showStringVars;

        public bool ShowStringVars
        {
            get => _showStringVars;
            set => Set(ref _showStringVars, value);
        }

        public ObservableCollection<StringVarEntry> StringVars { get; } = new ObservableCollection<StringVarEntry>();

        public bool HasStringVars => StringVars.Count > 0;

        public string StringVarSummary => StringVars.Count == 0
            ? "No message on this map has a placeholder."
            : $"{Plural.Of(StringVars.Count, "placeholder")} in this map's messages";
    }
}
