using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class ModelBrowserViewModel
    {

        public ObservableCollection<string> AnimationChoices { get; } = new();
        public bool HasAnimationChoice => AnimationChoices.Count > 1;

        private readonly List<int> _animationEntries = new();

        // How many rows matched by name.
        private int _namedForThisModel;

        private enum Belonging { Table, Name, Elsewhere }

        private Belonging WhoseMovement()
        {
            if (_selected == null || _animationIndex <= 0 || _animationIndex >= _animationEntries.Count)
                return Belonging.Elsewhere;
            if (ModelAssets.OwnAnimations(_selected.Archive, ShowingIndex)
                           .Contains(_animationEntries[_animationIndex])) return Belonging.Table;
            return AnimationChoices[_animationIndex].StartsWith(NamedPrefix)
                ? Belonging.Name : Belonging.Elsewhere;
        }

        private const string NamedPrefix = "Named for this model";

        private static string Called(DSPRE.ROMFiles.JointAnimation anim, int number)
        {
            string name = anim?.Name;
            if (string.IsNullOrWhiteSpace(name)) return $"Movement {number}";
            return $"{name} (movement {number})";
        }

        private void BuildAnimationChoices(ModelAssets.Archive a)
        {
            AnimationChoices.Clear();
            _animationEntries.Clear();
            Playing = false;
            MovementNote = "";
            OnPropertyChanged(nameof(HasMovementNote));

            AnimationChoices.Add("Still");
            _animationEntries.Add(int.MinValue);

            if (ModelAssets.AnimationFor(a, ShowingIndex, -1) != null)
            {
                AnimationChoices.Add("Its own movement");
                _animationEntries.Add(-1);
            }

            IReadOnlyList<int> own = ModelAssets.OwnAnimations(a, ShowingIndex);
            int startOn = 0;
            foreach (int code in own)
            {
                JointAnimation anim = ModelAssets.AnimationFor(a, ShowingIndex, code);
                if (anim == null) continue;
                if (startOn == 0) startOn = AnimationChoices.Count;
                AnimationChoices.Add($"Used by the game: {Called(anim, code)}, {anim.FrameCount} frames");
                _animationEntries.Add(code);
            }

            // Then movements named for the model, best match first. The rest follow.
            string modelName = ModelAssets.NameOf(a, ShowingIndex);
            List<(int code, int howWell, JointAnimation anim)> named = new List<(int code, int howWell, JointAnimation anim)>();
            List<(int code, JointAnimation anim)> rest = new List<(int code, JointAnimation anim)>();

            int n = ModelAssets.AnimationCount(a);
            for (int i = 0; i < n; i++)
            {
                if (own.Contains(i)) continue;
                JointAnimation anim = ModelAssets.AnimationFor(a, ShowingIndex, i);
                if (anim == null) continue;
                int howWell = ModelAssets.NameMatch(modelName, anim.Name);
                if (howWell > 0) named.Add((i, howWell, anim));
                else rest.Add((i, anim));
            }

            _namedForThisModel = named.Count;
            foreach ((int code, int howWell, JointAnimation anim) in named.OrderByDescending(x => x.howWell).ThenBy(x => x.code))
            {
                if (startOn == 0) startOn = AnimationChoices.Count;
                AnimationChoices.Add($"{NamedPrefix}: {Called(anim, code)}, {anim.FrameCount} frames");
                _animationEntries.Add(code);
            }
            foreach ((int code, JointAnimation anim) in rest)
            {
                AnimationChoices.Add($"{Called(anim, code)} ({anim.FrameCount} frames)");
                _animationEntries.Add(code);
            }

            _animationIndex = -1;
            OnPropertyChanged(nameof(AnimationIndex));
            _movementJustPicked = true;
            _animationIndex = startOn;
            OnPropertyChanged(nameof(AnimationIndex));
            OnPropertyChanged(nameof(HasAnimationChoice));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlayHelp));
            OnPropertyChanged(nameof(MovementSource));
            OnPropertyChanged(nameof(HasMovementSource));
        }

        private int _animationIndex;
        public int AnimationIndex
        {
            get => _animationIndex;
            set
            {
                if (!Set(ref _animationIndex, value) || _selected == null) return;
                _frame = 0;
                _movementJustPicked = true;
                if (!CanPlay) Playing = false;
                Draw();
                RaiseAll();
                if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool _movementJustPicked;

        // The still model, so every frame of a movement is placed the same way.
        private NsbmdRenderModel _placeLike;

        private JointAnimation Chosen()
        {
            if (_selected == null) return null;
            if (_animationIndex <= 0 || _animationIndex >= _animationEntries.Count) return null;
            return ModelAssets.AnimationFor(_selected.Archive, _selected.Index, _animationEntries[_animationIndex]);
        }

        public bool CanPlay => (_animationIndex > 0 && _animationIndex < _animationEntries.Count)
                               || CompanionFrames > 1;

        public string PlayHelp => CanPlay
            ? "Play it. Buildings mostly animate on a clock, so this shows what the animation does, not when "
            + "the game plays it."
            : "Pick a movement or one of the other animations first. Still means nothing is moving.";

        /// <summary>Whether the movement showing is this model's or one tried on it.</summary>
        public string MovementSource
        {
            get
            {
                if (_selected == null || _animationIndex <= 0
                    || _animationIndex >= _animationEntries.Count) return "";
                switch (WhoseMovement())
                {
                    case Belonging.Table: return "The movement the game gives this model.";
                    case Belonging.Name:
                        return "Named after this model, so very likely its own.";
                    default:
                        IReadOnlyList<int> own = ModelAssets.OwnAnimations(_selected.Archive, ShowingIndex);
                        if (own.Count == 0 && _namedForThisModel == 0) return "";
                        return "Borrowed from something else.";
                }
            }
        }

        public bool HasMovementSource => !string.IsNullOrEmpty(MovementSource);

        private bool _playing;
        public bool Playing
        {
            get => _playing;
            set
            {
                if (!Set(ref _playing, value)) return;
                OnPropertyChanged(nameof(PlayLabel));
                PlayingChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when the view should start or stop stepping the frames.</summary>
        public event EventHandler PlayingChanged;

        public string PlayLabel => _playing ? "Stop" : "Play";

        private int _frame;

        private string _movementNote = "";
        /// <summary>Set when a movement leaves this model exactly where it was.</summary>
        public string MovementNote { get => _movementNote; private set => Set(ref _movementNote, value); }
        public bool HasMovementNote => !string.IsNullOrEmpty(_movementNote);

        private void CheckTheMovementDoesSomething(NSBMD nsbmd, JointAnimation anim)
        {
            MovementNote = "";
            OnPropertyChanged(nameof(HasMovementNote));
            if (anim == null || nsbmd?.Models == null || nsbmd.Models.Length == 0) return;

            try
            {
                NSBMDModel model = nsbmd.Models[0];
                float scale = model.ModelScale;
                NsbmdRenderModel still = NsbmdGeometry.BuildModel(model, showMissingTextures: true);
                bool differs = false;
                for (int f = 1; f < anim.FrameCount && !differs; f++)
                {
                    NsbmdRenderModel at = NsbmdGeometry.BuildModel(model,
                        (objectId, part) => anim.MatrixFor(objectId, f, part, scale), still, showMissingTextures: true);
                    differs = Differs(still, at);
                }
                if (!differs)
                    MovementNote = WhoseMovement() switch
                    {
                        Belonging.Table => "The game gives this model this movement, but it moves nothing "
                                         + "on it.",
                        Belonging.Name => "Named after this model, but it moves nothing on it.",
                        _ => "Moves nothing on this model. It was written for something else.",
                    };
            }
            catch (Exception ex) { AppLogger.Error("ModelBrowser.CheckTheMovement failed: " + ex.Message); }
            OnPropertyChanged(nameof(HasMovementNote));
        }

        private static bool Differs(NsbmdRenderModel a, NsbmdRenderModel b)
        {
            if (a?.Parts == null || b?.Parts == null || a.Parts.Count != b.Parts.Count) return true;
            for (int m = 0; m < a.Parts.Count; m++)
            {
                float[] va = a.Parts[m].Vertices; float[] vb = b.Parts[m].Vertices;
                if (va == null || vb == null || va.Length != vb.Length) return true;
                for (int i = 0; i < va.Length; i++)
                    if (Math.Abs(va[i] - vb[i]) > 1e-6f) return true;
            }
            return false;
        }

        /// <summary>Moves on one frame and redraws. Called by the view on a timer.</summary>
        public void Step()
        {
            JointAnimation anim = Chosen();
            int length = Math.Max(anim?.FrameCount ?? 0, CompanionFrames);
            if (length <= 0) { Playing = false; return; }
            _frame = (_frame + 1) % length;
            OnPropertyChanged(nameof(Frame));
            DrawNow();
        }

        private void DrawNow()
        {
            Draw();
            OnPropertyChanged(nameof(FrameSummary));
            OnPropertyChanged(nameof(FrameCount));
            if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
        }

        public int FrameCount
        {
            get
            {
                JointAnimation anim = Chosen();
                return Math.Max(1, Math.Max(anim?.FrameCount ?? 0, CompanionFrames));
            }
        }

        public int Frame
        {
            get => _frame;
            set
            {
                int at = Math.Clamp(value, 0, Math.Max(0, FrameCount - 1));
                if (_frame == at) return;
                _frame = at;
                OnPropertyChanged(nameof(Frame));
                DrawNow();
            }
        }

        public string FrameSummary
        {
            get
            {
                JointAnimation anim = Chosen();
                int length = Math.Max(anim?.FrameCount ?? 0, CompanionFrames);
                return length <= 1 ? "" : $"Frame {_frame + 1} of {length}";
            }
        }

        private void Draw()
        {
            ModelAssets.Archive a = _selected.Archive;
            Model3D = null;
            Whynot = "";
            try
            {
                NSBMD nsbmd = ModelAssets.LoadModel(a, ShowingIndex);
                if (nsbmd == null || nsbmd.Models == null || nsbmd.Models.Length == 0)
                {
                    Whynot = "This model couldn't be opened.";
                    return;
                }

                int textureSet = _textureChoice >= 0 && _textureChoice < _textureSetEntries.Count
                    ? _textureSetEntries[_textureChoice] : -1;
                byte[] textures = ModelAssets.TexturesFor(a, ShowingIndex, textureSet);
                bool dressed = ModelAssets.Dress(nsbmd, textures);

                JointAnimation anim = Chosen();
                if (_movementJustPicked)
                {
                    _movementJustPicked = false;
                    _placeLike = NsbmdGeometry.BuildModel(nsbmd.Models[0], showMissingTextures: true);
                    CheckTheMovementDoesSomething(nsbmd, anim);
                }
                float scale = nsbmd.Models[0].ModelScale;
                Model3D = anim == null
                    ? NsbmdGeometry.BuildModel(nsbmd.Models[0], showMissingTextures: true)
                    : NsbmdGeometry.BuildModel(nsbmd.Models[0],
                        (objectId, part) => anim.MatrixFor(objectId, _frame % Math.Max(1, anim.FrameCount), part, scale),
                        _placeLike, showMissingTextures: true);
                if (Model3D == null) { Whynot = "This model couldn't be opened."; return; }

                if (!dressed && TextureChoices.Count <= 1) ShowFacts(new DetailFact("Textures", "None"));
                else ShowFacts();
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelBrowser.Draw failed: " + ex.Message);
                Whynot = "This model couldn't be opened.";
            }

            ApplyCompanions(_frame);
        }
    }
}
