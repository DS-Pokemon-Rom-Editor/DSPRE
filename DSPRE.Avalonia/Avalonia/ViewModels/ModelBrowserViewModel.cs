using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// The game's models and the pictures painted on them, in their own list.
    ///
    /// Deliberately not a tab of the flat graphics window. A model has shape as well as colour, what comes
    /// out of it is not a picture, and putting one back means converting between formats. The same rule
    /// holds though: anything that cannot be shown or saved says why, in words.
    /// </summary>
    public sealed class ModelBrowserViewModel : INotifyPropertyChanged
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
            /// <summary>What the file calls itself, which is the name GameFreak gave it.</summary>
            public string Name { get; init; }
            public string Label => Name == null
                ? $"{Index,5}  {Archive.Title}"
                : $"{Index,5}  {Name}";
            public string Search { get; init; }
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
            public ModelAssets.Group? Only { get; init; }   // null means everything
            public int Count { get; init; }
            public string What { get; init; }
            public string Header => $"{Title} ({Count})";
        }

        private readonly List<Item> _everything = new();

        public ModelBrowserViewModel() => Reload();

        public void Reload()
        {
            _everything.Clear();
            Tabs.Clear();
            foreach (var g in Enum.GetValues<ModelAssets.Group>())
            {
                int inGroup = 0;
                foreach (var a in ModelAssets.All.Where(x => x.In == g))
                {
                    int n;
                    try { n = ModelAssets.Count(a); } catch { n = 0; }
                    if (n == 0) continue;
                    inGroup += ModelAssets.Units(a, n).Count;
                    foreach (var u in ModelAssets.Units(a, n))
                        _everything.Add(new Item
                        {
                            Archive = a, In = g, Index = u.First, Unit = u,
                            Name = u.Name == a.Title ? null : u.Name,
                            Search = (a.Title + " " + u.First + " " + a.What + " " + u.Name).ToLowerInvariant(),
                        });
                }
                if (inGroup > 0)
                    Tabs.Add(new CategoryTab { Title = FriendlyGroup(g), Only = g, Count = inGroup,
                                               What = string.Join("  ", ModelAssets.All
                                                   .Where(x => x.In == g).Select(x => x.Title)) });
            }

            if (Tabs.Count > 0)
                Tabs.Insert(0, new CategoryTab { Title = "Everything", Only = null, Count = _everything.Count,
                                                 What = "Every model, picture set and animation this game has." });
            _selectedTab = Tabs.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedTab));

            ApplyFilter();
        }

        private static string FriendlyGroup(ModelAssets.Group g) => g switch
        {
            ModelAssets.Group.Overworld => "People and objects",
            ModelAssets.Group.Buildings => "Buildings",
            ModelAssets.Group.Maps => "Maps",
            _ => "Other",
        };

        public ObservableCollection<CategoryTab> Tabs { get; } = new();
        public ObservableCollection<Item> Shown { get; } = new();

        private CategoryTab _selectedTab;
        public CategoryTab SelectedTab
        {
            get => _selectedTab;
            set { if (Set(ref _selectedTab, value)) ApplyFilter(); }
        }

        private string _search = "";
        public string Search { get => _search; set { if (Set(ref _search, value)) ApplyFilter(); } }

        private const int ShowAtMost = 3000;

        private void ApplyFilter()
        {
            Shown.Clear();
            string q = (_search ?? "").Trim().ToLowerInvariant();
            IEnumerable<Item> hits = _everything;
            if (_selectedTab?.Only != null) hits = hits.Where(i => i.In == _selectedTab.Only.Value);
            if (!string.IsNullOrEmpty(q)) hits = hits.Where(i => i.Search.Contains(q));
            foreach (var i in hits.Take(ShowAtMost)) Shown.Add(i);
            OnPropertyChanged(nameof(FoundSummary));
        }

        public string FoundSummary
        {
            get
            {
                int total = _everything.Count;
                if (total == 0) return "This game has no 3D data DSPRE can list. Open a ROM first.";
                string q = (_search ?? "").Trim();
                int here = _selectedTab?.Count ?? total;
                string what = _selectedTab?.Only == null ? "in this game" : "under " + _selectedTab.Title;
                if (string.IsNullOrEmpty(q))
                    return Shown.Count < here
                        ? $"{here} models, picture sets and animations {what}. Showing the first {Shown.Count}; type to narrow it down."
                        : $"{here} models, picture sets and animations {what}.";
                return Shown.Count == 0
                    ? $"Nothing {what} matches \"{q}\". Try part of a name, like building or map."
                    : $"{Shown.Count} of {here} {what} match \"{q}\".";
            }
        }

        /// <summary>The files that make up whatever is picked, shown only when there is more than one.</summary>
        public ObservableCollection<Part> Parts { get; } = new();
        public bool HasParts => Parts.Count > 1;

        private int _partIndex = -1;
        public int PartIndex
        {
            get => _partIndex;
            set { if (Set(ref _partIndex, value) && _selected != null) Look(); }
        }

        /// <summary>The file being shown: the piece picked, or the thing itself when it is one file.</summary>
        public int ShowingIndex => _partIndex >= 0 && _partIndex < Parts.Count
            ? Parts[_partIndex].Index : _selected?.Index ?? 0;

        private void BuildParts()
        {
            Parts.Clear();
            if (_selected?.Unit != null && _selected.Unit.Parts.Count > 1)
                foreach (var up in _selected.Unit.Parts)
                    Parts.Add(new Part { Index = up.Index, Name = up.Name });

            _partIndex = -1;
            OnPropertyChanged(nameof(PartIndex));
            _partIndex = Parts.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(PartIndex));
            OnPropertyChanged(nameof(HasParts));
        }

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

        private string _details = "Pick something on the left to see it.";
        public string Details { get => _details; private set => Set(ref _details, value); }

        private string _whynot = "";
        public string Whynot { get => _whynot; private set => Set(ref _whynot, value); }
        public bool HasModel => Model3D != null;
        public bool HasNoModel => Model3D == null && !string.IsNullOrEmpty(_whynot);

        private ModelAssets.Options _options;

        private void Look()
        {
            Model3D = null;
            Whynot = "";
            if (_selected == null)
            {
                Details = "Pick something on the left to see it.";
                RaiseAll();
                return;
            }

            var a = _selected.Archive;
            _options = ModelAssets.WhatCanBeDone(a, ShowingIndex);
            BaseDetails = $"{Named}, number {ShowingIndex}. {ModelAssets.Describe(_options.Kind)} {a.What}";
            Details = BaseDetails;

            if (_options.CanShow)
            {
                BuildTextureChoices(a);
                BuildAnimationChoices(a);
                Draw();
            }
            else
            {
                Whynot = _options.ShowNote ?? "There is nothing here to show.";
                TextureChoices.Clear();
                AnimationChoices.Clear();
                Playing = false;
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
            }

            RaiseAll();
            if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>What this model can be dressed in. Buildings and map scenery take their pictures from
        /// a separate set shared by a whole map, so which set to use is a choice rather than something the
        /// model records. Offering the choice is the only way to see one of these dressed at all.</summary>
        public ObservableCollection<string> TextureChoices { get; } = new();
        public bool HasTextureChoice => TextureChoices.Count > 1;

        private int _textureChoice;
        public int TextureChoice
        {
            get => _textureChoice;
            set
            {
                if (!Set(ref _textureChoice, value) || _selected == null) return;
                Draw();
                RaiseAll();
                if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BuildTextureChoices(ModelAssets.Archive a)
        {
            TextureChoices.Clear();
            bool embedded = ModelAssets.EmbeddedTextures(a, ShowingIndex) != null
                         || ModelAssets.NeighbouringTextures(a, ShowingIndex) != null;
            TextureChoices.Add(embedded ? "Its own pictures" : "No pictures");
            int sets = ModelAssets.TextureSetCount(a);
            for (int i = 0; i < sets; i++) TextureChoices.Add("Picture set " + i);

            // Clear the choice first, then set it. The list was just refilled, so the box has dropped back
            // to nothing selected, and going straight to the same number as before would say nothing.
            _textureChoice = -1;
            OnPropertyChanged(nameof(TextureChoice));
            _textureChoice = 0;
            OnPropertyChanged(nameof(TextureChoice));
            OnPropertyChanged(nameof(HasTextureChoice));
        }

        // ── movement ──────────────────────────────────────────────────────────────────────────────
        //
        // Which movement belongs to which building is not recorded any more than which pictures are, so
        // it is offered as a choice too. An animation that turns out to move nothing is left out of the
        // list rather than sitting there doing nothing when picked.

        public ObservableCollection<string> AnimationChoices { get; } = new();
        public bool HasAnimationChoice => AnimationChoices.Count > 1;

        private readonly List<int> _animationEntries = new();   // what each row above points at

        /// <summary>
        /// What a movement is called, from the name inside the file.
        ///
        /// Every one of these carries a sixteen character name written by whoever built it: door_op,
        /// door_cl, gym01_lift. All 89 in HeartGold and all 32 in Platinum have one. Listing them as
        /// numbers meant picking the right one was guesswork.
        /// </summary>
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

            // The game's own table says which movements belong to this model, so those go first and one
            // of them is what the box starts on. The rest of the archive is still there underneath, for
            // trying somebody else's movement on something.
            var own = ModelAssets.OwnAnimations(a, ShowingIndex);
            int startOn = 0;
            foreach (int code in own)
            {
                var anim = ModelAssets.AnimationFor(a, ShowingIndex, code);
                if (anim == null) continue;
                if (startOn == 0) startOn = AnimationChoices.Count;
                AnimationChoices.Add($"The one it uses: {Called(anim, code)}, {anim.FrameCount} frames");
                _animationEntries.Add(code);
            }

            int n = ModelAssets.AnimationCount(a);
            for (int i = 0; i < n; i++)
            {
                if (own.Contains(i)) continue;
                var anim = ModelAssets.AnimationFor(a, ShowingIndex, i);
                if (anim == null) continue;
                AnimationChoices.Add($"{Called(anim, i)}, {anim.FrameCount} frames");
                _animationEntries.Add(i);
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

        /// <summary>The still model, kept so every frame of a movement is placed the same way it was.</summary>
        private NsbmdRenderModel _placeLike;

        private JointAnimation Chosen()
        {
            if (_selected == null) return null;
            if (_animationIndex <= 0 || _animationIndex >= _animationEntries.Count) return null;
            return ModelAssets.AnimationFor(_selected.Archive, _selected.Index, _animationEntries[_animationIndex]);
        }

        public bool CanPlay => _animationIndex > 0 && _animationIndex < _animationEntries.Count;

        public string PlayHelp => CanPlay
            ? "Run the movement. Buildings mostly move on a clock rather than on their own, so this is a "
            + "way to see what a movement does, not what the game does with it."
            : "Pick a movement first. Still means nothing is moving.";

        /// <summary>Says whether the movement showing is the one the game gives this model or one that
        /// belongs to something else and is only being tried on it.</summary>
        public string MovementSource
        {
            get
            {
                if (_selected == null || _animationIndex <= 0
                    || _animationIndex >= _animationEntries.Count) return "";
                int code = _animationEntries[_animationIndex];
                var own = ModelAssets.OwnAnimations(_selected.Archive, ShowingIndex);
                if (own.Count == 0) return "";
                return own.Contains(code)
                    ? "This is the movement the game gives this model."
                    : "This movement belongs to something else and is only being tried on this model.";
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
        /// <summary>Says when a movement leaves this model exactly where it was. A movement written for
        /// one building can name parts another building also has and still hold every one of them still,
        /// so picking it looks the same as picking nothing. Saying so beats leaving it a mystery.</summary>
        public string MovementNote { get => _movementNote; private set => Set(ref _movementNote, value); }
        public bool HasMovementNote => !string.IsNullOrEmpty(_movementNote);

        /// <summary>Whether a movement changes this model at all, checked once when it is picked rather
        /// than on every frame.</summary>
        private void CheckTheMovementDoesSomething(NSBMD nsbmd, JointAnimation anim)
        {
            MovementNote = "";
            OnPropertyChanged(nameof(HasMovementNote));
            if (anim == null || nsbmd?.models == null || nsbmd.models.Length == 0) return;

            try
            {
                var model = nsbmd.models[0];
                float scale = model.modelScale;
                var still = NsbmdGeometry.BuildModel(model);
                bool differs = false;
                for (int f = 1; f < anim.FrameCount && !differs; f++)
                {
                    var at = NsbmdGeometry.BuildModel(model,
                        (objectId, part) => anim.MatrixFor(objectId, f, part, scale), still);
                    differs = Differs(still, at);
                }
                if (!differs)
                    MovementNote = "This movement leaves every part of this model where it was. It was "
                                 + "written for a different building, and this one has nothing it moves.";
            }
            catch (Exception ex) { AppLogger.Error("ModelBrowser.CheckTheMovement failed: " + ex.Message); }
            OnPropertyChanged(nameof(HasMovementNote));
        }

        private static bool Differs(NsbmdRenderModel a, NsbmdRenderModel b)
        {
            if (a?.Parts == null || b?.Parts == null || a.Parts.Count != b.Parts.Count) return true;
            for (int m = 0; m < a.Parts.Count; m++)
            {
                var va = a.Parts[m].Vertices; var vb = b.Parts[m].Vertices;
                if (va == null || vb == null || va.Length != vb.Length) return true;
                for (int i = 0; i < va.Length; i++)
                    if (Math.Abs(va[i] - vb[i]) > 1e-6f) return true;
            }
            return false;
        }

        /// <summary>Moves on one frame and redraws. Called by the view on a timer.</summary>
        public void Step()
        {
            var anim = Chosen();
            if (anim == null || anim.FrameCount <= 0) { Playing = false; return; }
            _frame = (_frame + 1) % anim.FrameCount;
            Draw();
            if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
        }

        private void Draw()
        {
            var a = _selected.Archive;
            Model3D = null;
            Whynot = "";
            try
            {
                var nsbmd = ModelAssets.LoadModel(a, ShowingIndex);
                if (nsbmd == null || nsbmd.models == null || nsbmd.models.Length == 0)
                {
                    Whynot = "This model would not open.";
                    return;
                }

                var textures = ModelAssets.TexturesFor(a, ShowingIndex, _textureChoice - 1);
                bool dressed = ModelAssets.Dress(nsbmd, textures);

                var anim = Chosen();
                if (_movementJustPicked)
                {
                    _movementJustPicked = false;
                    _placeLike = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                    CheckTheMovementDoesSomething(nsbmd, anim);
                }
                float scale = nsbmd.models[0].modelScale;
                Model3D = anim == null
                    ? NsbmdGeometry.BuildModel(nsbmd.models[0])
                    : NsbmdGeometry.BuildModel(nsbmd.models[0],
                        (objectId, part) => anim.MatrixFor(objectId, _frame % Math.Max(1, anim.FrameCount), part, scale),
                        _placeLike);
                if (Model3D == null) { Whynot = "This model would not open."; return; }

                if (!dressed && TextureChoices.Count > 1)
                    Details = BaseDetails + "  Shown bare. Pick a picture set above to dress it.";
                else if (!dressed)
                    Details = BaseDetails + "  This model has no pictures of its own, and this game keeps "
                            + "none for it to borrow.";
                else Details = BaseDetails;
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelBrowser.Draw failed: " + ex.Message);
                Whynot = "This model would not open.";
            }
        }

        private string BaseDetails = "";

        /// <summary>What to call the thing on screen: the name in the file when it has one.</summary>
        private string Named => _selected?.Name ?? _selected?.Archive.Title ?? "";

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(HasModel));
            OnPropertyChanged(nameof(HasNoModel));
            _cannotImport = _selected == null
                ? "Pick something first."
                : ModelAssets.CannotImportBecause(_selected.Archive, ShowingIndex);

            OnPropertyChanged(nameof(CanSaveModel));
            OnPropertyChanged(nameof(SaveModelHelp));
            OnPropertyChanged(nameof(CanPutFileIn));
            OnPropertyChanged(nameof(PutFileInHelp));
            OnPropertyChanged(nameof(DeepEditorHint));
            OnPropertyChanged(nameof(HasDeepEditor));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlayHelp));
            OnPropertyChanged(nameof(MovementSource));
            OnPropertyChanged(nameof(HasMovementSource));
        }

        public bool CanSaveModel => _options?.CanSaveModel == true;

        public string SaveModelHelp => _selected == null
            ? "Pick something first."
            : _options?.SaveNote ?? "There is nothing here to save as a 3D file.";

        public string DeepEditorName => _selected?.Archive.DeepEditor;
        public bool HasDeepEditor => !string.IsNullOrEmpty(DeepEditorName);
        public string DeepEditorHint => HasDeepEditor
            ? $"The {DeepEditorName} knows this kind of thing properly. Use it when you want more than a look."
            : "";

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        public string SuggestedFileName(string extension)
        {
            if (_selected == null) return "model" + extension;
            string name = _selected.Archive.Title.ToLowerInvariant().Replace(' ', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return $"{name}_{ShowingIndex:D4}{extension}";
        }

        /// <summary>Whether a file can be put in over what is picked, and why not when it cannot.</summary>
        public bool CanPutFileIn => _selected != null && _cannotImport == null;

        public string PutFileInHelp => _selected == null
            ? "Pick something first."
            : _cannotImport
              ?? ("Put a finished NSBMD, NSBTX or animation file in place of this one. It has to be the "
                  + "same kind as what is here now.\n\n" + ModelAssets.CannotConvertAMesh);

        private string _cannotImport = "Pick something first.";

        /// <summary>Puts a file in over the entry showing. Comes back with a reason when it could not.</summary>
        public string PutFileIn(string path)
        {
            if (_selected == null) return "Pick something first.";
            if (_cannotImport != null) return _cannotImport;
            return ModelAssets.ImportRaw(_selected.Archive, ShowingIndex, path);
        }

        /// <summary>Saves the entry exactly as it sits in the ROM.</summary>
        public string SaveFileAsItIs(string path)
        {
            if (_selected == null) return "Pick something first.";
            return ModelAssets.SaveRaw(_selected.Archive, ShowingIndex, path);
        }

        /// <summary>Saves the model as a file other 3D programs open, with its pictures.</summary>
        public string SaveAsThreeD(string path, bool glb)
        {
            if (_selected == null) return "Pick something first.";
            if (!CanSaveModel) return _options?.SaveNote ?? "This is not a model.";

            var narc = new ScriptNarc(_selected.Archive.Dir);
            var model = narc.Get(ShowingIndex);
            var textures = ModelAssets.TexturesFor(_selected.Archive, ShowingIndex, _textureChoice - 1);

            try
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string dir = Path.GetDirectoryName(path);
                string was = Directory.GetCurrentDirectory();
                try
                {
                    if (!string.IsNullOrEmpty(dir)) Directory.SetCurrentDirectory(dir);
                    if (glb) DSPRE.ModelUtils.ModelToGLB(name, model, textures);
                    else DSPRE.ModelUtils.ModelToDAE(name, model, textures);
                }
                finally { Directory.SetCurrentDirectory(was); }
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelBrowser.SaveAsThreeD failed: " + ex.Message);
                return "This model could not be turned into a 3D file. The whole file can still be saved "
                     + "as it is.";
            }
        }
    }
}
