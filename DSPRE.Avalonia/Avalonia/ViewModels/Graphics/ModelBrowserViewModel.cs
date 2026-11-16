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
    /// <summary>The game's models and the pictures painted on them, in their own list.</summary>
    public sealed partial class ModelBrowserViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        /// <summary>Raised when a model has been read and is ready to be handed to the 3D view.</summary>
        public event EventHandler ModelReady;

        public sealed class Item
        {
            public ModelAssets.Archive Archive { get; init; }
            public ModelAssets.Group In { get; init; }
            public int Index { get; init; }
            public ModelAssets.Unit Unit { get; init; }
            /// <summary>The name stored in the file, Game Freak's own.</summary>
            public string Name { get; init; }
            public string Label => Name == null
                ? $"{Index,5}  {Archive.Title}"
                : $"{Index,5}  {Name}";
            public string Search { get; init; }

            /// <summary>An animation that no model is named for.</summary>
            public bool Unclaimed { get; set; }
        }

        /// <summary>One file of the thing picked.</summary>
        public sealed class Part
        {
            public int Index { get; init; }
            public string Name { get; init; }
            public string Label => Name;
        }

        /// <summary>One tab: a kind of 3D thing, and how many of them this game has.</summary>
        public sealed class CategoryTab
        {
            public string Title { get; init; }
            public ModelAssets.Group? Only { get; init; }   // null on Everything and Unclaimed animations
            /// <summary>A tab for animations no model is named for.</summary>
            public bool OnlyUnclaimed { get; init; }
            public int Count { get; init; }
            public string What { get; init; }
            public string Header => $"{Title} ({Count})";
        }

        private readonly List<Item> _everything = new();
        private IReadOnlyList<BuildingModelTextureSet> _buildingTextureSets = Array.Empty<BuildingModelTextureSet>();

        /// <summary>
        /// Reading every archive to see what is in it is real file work, so it does not happen here.
        /// The caller runs <see cref="Scan"/> off the UI thread and then <see cref="Publish"/> on it.
        /// </summary>
        public ModelBrowserViewModel()
        {
            // Reads here run from the scan thread and the render as well, so they all see the pending entries.
            _overlay = ScriptNarc.Overlay(_pending.Read);
            _pending.Changed += () =>
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                PendingChanged?.Invoke();
            };
        }

        // ── imports wait for Save ───────────────────────────────────────────────────────────────────

        private readonly PendingArchiveWrites _pending = new();
        private readonly IDisposable _overlay;

        /// <summary>Raised when an import, undo, redo, discard or save changed the entries.</summary>
        public event Action PendingChanged;

        public bool HasUnsavedChanges => _pending.IsDirty;
        public string UnsavedChangesDescription => "Models";
        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();
        public void SaveChanges()
        {
            if (!DialogHelper.TrySave(_pending.Save, UnsavedChangesDescription)) return;
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = "Saved.";
        }
        public void DiscardChanges() => _pending.Discard();
        public void Dispose() => _overlay.Dispose();

        private Item _selected;
        public Item Selected
        {
            get => _selected;
            set
            {
                if (!Set(ref _selected, value)) return;
                BuildParts();
                Look();
            }
        }

        /// <summary>The model the 3D view should draw, or null when there is not one.</summary>
        public NsbmdRenderModel Model3D { get; private set; }

        private Bitmap _texturePreview;
        public Bitmap TexturePreview
        {
            get => _texturePreview;
            private set
            {
                if (!Set(ref _texturePreview, value)) return;
                OnPropertyChanged(nameof(HasTexturePreview));
                OnPropertyChanged(nameof(HasNoModel));
            }
        }
        public bool HasTexturePreview => TexturePreview != null;

        private string _details = "Pick something on the left to see it.";
        public string Details { get => _details; private set => Set(ref _details, value); }

        public ObservableCollection<DetailFact> Facts { get; } = new();
        private List<DetailFact> _baseFacts = new();

        private void ShowFacts(params DetailFact[] more)
        {
            Details = "";
            Facts.Clear();
            foreach (DetailFact f in _baseFacts.Concat(more)) Facts.Add(f);
        }

        private string _whynot = "";
        public string Whynot { get => _whynot; private set => Set(ref _whynot, value); }
        public bool HasModel => Model3D != null;
        public bool HasNoModel => Model3D == null && TexturePreview == null && !string.IsNullOrEmpty(_whynot);

        private ModelAssets.Options _options;

        private void Look()
        {
            Model3D = null;
            ClearTexturePreview();
            ClearCompanions();
            TextureChoices.Clear();
            _textureSetEntries.Clear();
            AnimationChoices.Clear();
            _animationEntries.Clear();
            Playing = false;
            _frame = 0;
            AnimationDetails = "";
            AnimationOwner = "";
            MovementNote = "";
            Whynot = "";
            if (_selected == null)
            {
                _options = null;
                _baseFacts = new List<DetailFact>();
                Facts.Clear();
                Details = "Pick something on the left to see it.";
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
                OnPropertyChanged(nameof(HasAnimationDetails));
                OnPropertyChanged(nameof(HasMovementNote));
                OnPropertyChanged(nameof(HasCompanionSummary));
                OnPropertyChanged(nameof(CompanionSummary));
                RaiseAll();
                return;
            }

            ModelAssets.Archive a = _selected.Archive;
            _options = ModelAssets.WhatCanBeDone(a, ShowingIndex);
            _baseFacts = new List<DetailFact>
            {
                new DetailFact("Name", Named),
                new DetailFact("Number", ShowingIndex.ToString()),
                new DetailFact("Kind", ModelAssets.KindName(_options.Kind)),
            };
            ShowFacts();

            // An animation picked on its own has no shape to draw, so say what it does instead of
            // leaving an empty box and the word "animation".
            if (IsAnAnimation(_options.Kind))
            {
                AnimationDetails = DescribeAnimation(a, ShowingIndex, _options.Kind);
                AnimationOwner = WhoClaimsIt(a, ShowingIndex, _options.Kind);
            }
            OnPropertyChanged(nameof(AnimationDetails));
            OnPropertyChanged(nameof(AnimationOwner));
            OnPropertyChanged(nameof(HasAnimationDetails));
            OnPropertyChanged(nameof(HasCompanionSummary));
            OnPropertyChanged(nameof(CompanionSummary));

            if (_options.Kind == ModelAssets.Kind.TextureBundle)
            {
                LoadTexturePreview(a, ShowingIndex);
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
            }
            else if (_options.CanShow)
            {
                FindCompanions(a, ShowingIndex, Named);
                BuildTextureChoices(a);
                BuildAnimationChoices(a);
                Draw();
            }
            else
            {
                Whynot = _options.ShowNote ?? "There is nothing here to show.";
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
            }

            RaiseAll();
            if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>What an animation picked on its own does, and which model it was written for.</summary>
        public string AnimationDetails { get; private set; } = "";
        public string AnimationOwner { get; private set; } = "";
        public bool HasAnimationDetails => !string.IsNullOrEmpty(AnimationDetails);

        private static bool IsAnAnimation(ModelAssets.Kind k) =>
            k is ModelAssets.Kind.JointAnimation or ModelAssets.Kind.TextureAnimation
              or ModelAssets.Kind.TextureSwap or ModelAssets.Kind.VisibilityAnimation
              or ModelAssets.Kind.MaterialAnimation;

        /// <summary>What to call the thing on screen: the name in the file when it has one.</summary>
        private string Named => _selected?.Name ?? _selected?.Archive.Title ?? "";

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(HasModel));
            OnPropertyChanged(nameof(HasNoModel));
            OnPropertyChanged(nameof(HasTexturePreview));
            _cannotImport = _selected == null
                ? "Pick something first."
                : ModelAssets.CannotImportBecause(_selected.Archive, ShowingIndex);

            OnPropertyChanged(nameof(CanSaveModel));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SaveModelHelp));
            OnPropertyChanged(nameof(CanPutFileIn));
            OnPropertyChanged(nameof(PutFileInHelp));
            OnPropertyChanged(nameof(OpenDeepEditorLabel));
            OnPropertyChanged(nameof(HasDeepEditor));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlayHelp));
            OnPropertyChanged(nameof(MovementSource));
            OnPropertyChanged(nameof(HasMovementSource));
            OnPropertyChanged(nameof(Frame));
            OnPropertyChanged(nameof(FrameCount));
            OnPropertyChanged(nameof(FrameSummary));
        }

        /// <summary>Whether anything is picked. Saving a file as it is works on whatever is
        /// selected, so with nothing selected the button has nothing to act on.</summary>
        public bool HasSelection => _selected != null;

        public bool CanSaveModel => _options?.CanSaveModel == true;

        public string SaveModelHelp => _selected == null
            ? "Pick something first."
            : _options?.SaveNote ?? "There is nothing here to save as a 3D file.";

        public string DeepEditorName => _selected?.Archive.DeepEditor;
        public bool HasDeepEditor => !string.IsNullOrEmpty(DeepEditorName);
        public string OpenDeepEditorLabel => HasDeepEditor ? $"Open in {DeepEditorName}" : "";

        public void OpenDeepEditor()
        {
            if (HasDeepEditor) AvaloniaEditorLauncher.OpenEditorNamed(DeepEditorName);
        }

        private string _status = "";
        public string StatusText { get => _status; set => Set(ref _status, value); }
    }
}
