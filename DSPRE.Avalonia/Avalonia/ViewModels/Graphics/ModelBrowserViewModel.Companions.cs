using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>
    /// The things that change a model without changing its shape: a picture sliding across it, a picture
    /// being swapped for another, a colour fading, parts being hidden. The game keeps each of these in
    /// its own file beside the model, and none of them says which model it is for, so each is offered as
    /// a choice with how sure we are about it.
    /// </summary>
    public sealed partial class ModelBrowserViewModel
    {
        /// <summary>One animation that could go with the model being shown.</summary>
        public sealed class Companion
        {
            public int Index { get; init; }
            public string Label { get; init; }
            public ModelAssets.Kind Kind { get; init; }
            public ModelAssets.Match Sureness { get; init; }
            /// <summary>Which archive it came out of, since it is not always the model's own.</summary>
            public RomInfo.DirNames Where { get; init; }
        }

        public ObservableCollection<string> SlideChoices { get; } = new();
        public ObservableCollection<string> SwapChoices { get; } = new();
        public ObservableCollection<string> ColourChoices { get; } = new();
        public ObservableCollection<string> ShowingChoices { get; } = new();

        private readonly List<Companion> _slides = new(), _swaps = new(), _colours = new(), _showings = new();

        private TextureSrtAnimation _slide;
        private TexturePatternAnimation _swap;
        private MaterialColourAnimation _colour;
        private VisibilityAnimation _showing;

        private int _slideChoice, _swapChoice, _colourChoice, _showingChoice;

        public int SlideChoice
        {
            get => _slideChoice;
            set
            {
                // Replacing the list under a ComboBox makes it write -1 back; that means None, not nothing.
                int picked = Math.Max(0, value);
                if (!Set(ref _slideChoice, picked)) { if (value < 0) OnPropertyChanged(); return; }
                _slide = LoadSlide(picked);
                OnPropertyChanged(nameof(SlideChoiceName));
                Redraw();
            }
        }
        public int SwapChoice
        {
            get => _swapChoice;
            set
            {
                // Replacing the list under a ComboBox makes it write -1 back; that means None, not nothing.
                int picked = Math.Max(0, value);
                if (!Set(ref _swapChoice, picked)) { if (value < 0) OnPropertyChanged(); return; }
                _swap = LoadSwap(picked);
                OnPropertyChanged(nameof(SwapChoiceName));
                Redraw();
            }
        }
        public int ColourChoice
        {
            get => _colourChoice;
            set
            {
                // Replacing the list under a ComboBox makes it write -1 back; that means None, not nothing.
                int picked = Math.Max(0, value);
                if (!Set(ref _colourChoice, picked)) { if (value < 0) OnPropertyChanged(); return; }
                _colour = LoadColour(picked);
                OnPropertyChanged(nameof(ColourChoiceName));
                Redraw();
            }
        }
        public int ShowingChoice
        {
            get => _showingChoice;
            set
            {
                // Replacing the list under a ComboBox makes it write -1 back; that means None, not nothing.
                int picked = Math.Max(0, value);
                if (!Set(ref _showingChoice, picked)) { if (value < 0) OnPropertyChanged(); return; }
                _showing = LoadShowing(picked);
                OnPropertyChanged(nameof(ShowingChoiceName));
                Redraw();
            }
        }


        /// <summary>
        /// The chosen line as text. Bound instead of the number because replacing a ComboBox's list
        /// clears its SelectedIndex, which left every picker looking empty until it was opened.
        /// </summary>
        public string SlideChoiceName
        {
            get => LineAt(SlideChoices, _slideChoice);
            set => SlideChoice = LineFor(SlideChoices, value);
        }
        public string SwapChoiceName
        {
            get => LineAt(SwapChoices, _swapChoice);
            set => SwapChoice = LineFor(SwapChoices, value);
        }
        public string ColourChoiceName
        {
            get => LineAt(ColourChoices, _colourChoice);
            set => ColourChoice = LineFor(ColourChoices, value);
        }
        public string ShowingChoiceName
        {
            get => LineAt(ShowingChoices, _showingChoice);
            set => ShowingChoice = LineFor(ShowingChoices, value);
        }

        private static string LineAt(ObservableCollection<string> from, int at) =>
            at >= 0 && at < from.Count ? from[at] : (from.Count > 0 ? from[0] : null);

        private static int LineFor(ObservableCollection<string> from, string what)
        {
            int at = what == null ? -1 : from.IndexOf(what);
            return at < 0 ? 0 : at;
        }

        public bool HasSlideChoice => SlideChoices.Count > 1;
        public bool HasSwapChoice => SwapChoices.Count > 1;
        public bool HasColourChoice => ColourChoices.Count > 1;
        public bool HasShowingChoice => ShowingChoices.Count > 1;

        private string _companionNote = "";
        /// <summary>
        /// Says when a chosen animation drives none of this model's surfaces. It happens often: an
        /// animation names the surfaces it drives, and plenty of them name surfaces that live on a model
        /// in some archive this list does not carry. Without this the picker just looks broken.
        /// </summary>
        public string CompanionNote { get => _companionNote; private set => Set(ref _companionNote, value); }
        public bool HasCompanionNote => !string.IsNullOrEmpty(_companionNote);

        /// <summary>What the 3D view should do to each material this frame, or null when nothing does.</summary>
        public Dictionary<int, float[]> TextureMatrices { get; private set; }
        public Dictionary<int, string> TextureSwaps { get; private set; }
        public Dictionary<int, float> MaterialFades { get; private set; }
        /// <summary>Model nodes an animation hides this frame, so the view can leave their shapes out.</summary>
        public HashSet<int> HiddenNodes { get; private set; }
        /// <summary>What a colour animation recolours a surface to this frame.</summary>
        public Dictionary<int, (float r, float g, float b)> MaterialColours { get; private set; }

        /// <summary>How long the longest chosen animation runs, so the frame counter has an end.</summary>
        public int CompanionFrames => Math.Max(Math.Max(_slide?.FrameCount ?? 0, _swap?.FrameCount ?? 0),
                                               Math.Max(_colour?.FrameCount ?? 0, _showing?.FrameCount ?? 0));

        /// <summary>
        /// Every animation in this model's archive that could belong to it, best first. Sureness comes
        /// from the names; when no name fits, the game's habit of filing a model's animations right
        /// after it decides, and that is only ever offered as a guess.
        /// </summary>
        private void FindCompanions(ModelAssets.Archive a, int modelIndex, string modelName)
        {
            _slides.Clear(); _swaps.Clear(); _colours.Clear(); _showings.Clear();

            // Buildings keep their animations in an archive of their own, so look there as well as
            // beside the model. In Platinum every sliding and swapping picture is in that one archive
            // and none of them sits next to a model at all.
            foreach (var (dir, sameArchive) in Sources(a))
            {
                var narc = new ScriptNarc(dir);
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] b;
                    try { b = narc.Get(i); } catch { continue; }
                    if (b == null) continue;

                    var kind = ModelAssets.Identify(b);
                    var into = kind switch
                    {
                        ModelAssets.Kind.TextureAnimation => _slides,
                        ModelAssets.Kind.TextureSwap => _swaps,
                        ModelAssets.Kind.MaterialAnimation => _colours,
                        ModelAssets.Kind.VisibilityAnimation => _showings,
                        _ => null,
                    };
                    if (into == null) continue;

                    var names = NamesOf(kind, b);
                    var sure = ModelAssets.MatchFor(names, modelName);
                    // Filing order only means something inside the model's own archive.
                    if (sure == ModelAssets.Match.None && (!sameArchive || Math.Abs(i - modelIndex) > 3))
                        continue;

                    into.Add(new Companion
                    {
                        Index = i,
                        Kind = kind,
                        Sureness = sure,
                        Where = dir,
                        Label = Describe(sure, names.FirstOrDefault(), i),
                    });
                }
            }

            Fill(SlideChoices, _slides, "slide");
            Fill(SwapChoices, _swaps, "swap");
            Fill(ColourChoices, _colours, "colour");
            Fill(ShowingChoices, _showings, "hide");

            _slideChoice = _swapChoice = _colourChoice = _showingChoice = 0;
            _slide = null; _swap = null; _colour = null; _showing = null;

            foreach (var n in new[] { nameof(SlideChoices), nameof(SwapChoices), nameof(ColourChoices),
                                      nameof(ShowingChoices), nameof(HasSlideChoice), nameof(HasSwapChoice),
                                      nameof(HasColourChoice), nameof(HasShowingChoice),
                                      nameof(SlideChoice), nameof(SwapChoice), nameof(ColourChoice),
                                      nameof(ShowingChoice), nameof(SlideChoiceName), nameof(SwapChoiceName),
                                      nameof(ColourChoiceName), nameof(ShowingChoiceName),
                                      nameof(CompanionSummary) })
                OnPropertyChanged(n);
        }

        private void ClearCompanions()
        {
            _slides.Clear(); _swaps.Clear(); _colours.Clear(); _showings.Clear();
            SlideChoices.Clear(); SwapChoices.Clear(); ColourChoices.Clear(); ShowingChoices.Clear();
            _slide = null; _swap = null; _colour = null; _showing = null;
            _slideChoice = _swapChoice = _colourChoice = _showingChoice = 0;
            TextureMatrices = null; TextureSwaps = null; MaterialFades = null;
            HiddenNodes = null; MaterialColours = null;
            CompanionNote = "";
            foreach (var name in new[] { nameof(HasSlideChoice), nameof(HasSwapChoice),
                         nameof(HasColourChoice), nameof(HasShowingChoice), nameof(HasCompanionSummary),
                         nameof(CompanionSummary), nameof(HasCompanionNote) })
                OnPropertyChanged(name);
        }

        /// <summary>The archives worth looking in: the model's own, and the one its animations live in.</summary>
        private static IEnumerable<(RomInfo.DirNames dir, bool sameArchive)> Sources(ModelAssets.Archive a)
        {
            yield return (a.Dir, true);
            if (a.AnimationArchive != null && a.AnimationArchive.Value != a.Dir)
                yield return (a.AnimationArchive.Value, false);
        }

        private static IReadOnlyList<string> NamesOf(ModelAssets.Kind kind, byte[] b)
        {
            try
            {
                switch (kind)
                {
                    case ModelAssets.Kind.TextureAnimation:
                        return TextureSrtAnimation.Load(b)?.MaterialNames ?? Array.Empty<string>();
                    case ModelAssets.Kind.TextureSwap:
                        return TexturePatternAnimation.Load(b)?.MaterialNames ?? Array.Empty<string>();
                    case ModelAssets.Kind.MaterialAnimation:
                        return MaterialColourAnimation.Load(b)?.MaterialNames ?? Array.Empty<string>();
                    case ModelAssets.Kind.VisibilityAnimation:
                        return VisibilityAnimation.Load(b)?.AnimationNames ?? Array.Empty<string>();
                    default: return Array.Empty<string>();
                }
            }
            catch { return Array.Empty<string>(); }
        }

        private static string Describe(ModelAssets.Match sure, string name, int index)
        {
            string called = string.IsNullOrWhiteSpace(name) ? $"number {index}" : name;
            return sure switch
            {
                ModelAssets.Match.Exact => $"Its own: {called}",
                ModelAssets.Match.SameStart => $"Named for it: {called}",
                ModelAssets.Match.SameFamily => $"Same family, a guess: {called}",
                _ => $"Filed beside it, a guess: {called}",
            };
        }

        private static void Fill(ObservableCollection<string> into, List<Companion> from, string what)
        {
            into.Clear();
            into.Add("None");
            foreach (var c in from.OrderByDescending(c => c.Sureness).ThenBy(c => c.Index))
                into.Add(c.Label);
            _ = what;
        }

        /// <summary>Only a model has things attached to it; an animation is one of the things. Nothing
        /// is picked when the window first opens, so there is no kind to ask about yet.</summary>
        public bool HasCompanionSummary => _options != null && _options.Kind == ModelAssets.Kind.Model;

        /// <summary>A line saying what is attached to this model, for the panel beside the view.</summary>
        public string CompanionSummary
        {
            get
            {
                if (!HasCompanionSummary) return "";
                var bits = new List<string>();
                if (_slides.Count > 0) bits.Add($"{_slides.Count} that slide a picture across it");
                if (_swaps.Count > 0) bits.Add($"{_swaps.Count} that swap its pictures");
                if (_colours.Count > 0) bits.Add($"{_colours.Count} that change its colour");
                if (_showings.Count > 0) bits.Add($"{_showings.Count} that hide parts of it");
                if (bits.Count == 0) return "Nothing in this archive animates this model.";
                return "This model has " + string.Join(", ", bits) + ".";
            }
        }

        private TextureSrtAnimation LoadSlide(int choice) =>
            At(_slides, choice, b => TextureSrtAnimation.Load(b));
        private TexturePatternAnimation LoadSwap(int choice) =>
            At(_swaps, choice, b => TexturePatternAnimation.Load(b));
        private MaterialColourAnimation LoadColour(int choice) =>
            At(_colours, choice, b => MaterialColourAnimation.Load(b));
        private VisibilityAnimation LoadShowing(int choice) =>
            At(_showings, choice, b => VisibilityAnimation.Load(b));

        private T At<T>(List<Companion> from, int choice, Func<byte[], T> read) where T : class
        {
            if (choice <= 0 || _selected == null) return null;
            var ordered = from.OrderByDescending(c => c.Sureness).ThenBy(c => c.Index).ToList();
            if (choice - 1 >= ordered.Count) return null;
            try
            {
                var narc = new ScriptNarc(ordered[choice - 1].Where);
                if (!narc.Available) return null;
                var b = narc.Get(ordered[choice - 1].Index);
                return b == null ? null : read(b);
            }
            catch (Exception ex) { AppLogger.Error("ModelBrowser companion failed: " + ex.Message); return null; }
        }

        /// <summary>
        /// What each material should look like this frame. Animations name the materials they drive, and
        /// the built model knows each material's name, so the two are matched by name rather than order.
        /// </summary>
        private void ApplyCompanions(int frame)
        {
            TextureMatrices = null;
            TextureSwaps = null;
            MaterialFades = null;
            HiddenNodes = null;
            MaterialColours = null;
            if (Model3D == null) return;

            if (_slide != null)
            {
                var mats = new Dictionary<int, float[]>();
                foreach (var kv in Model3D.MaterialNameByKey)
                {
                    int m = _slide.IndexOf(kv.Value);
                    if (m >= 0)
                        mats[kv.Key] = _slide.Evaluate(m, frame % Math.Max(1, _slide.FrameCount)).ToMatrix3();
                }
                if (mats.Count > 0) TextureMatrices = mats;
            }

            if (_swap != null)
            {
                var swaps = new Dictionary<int, string>();
                foreach (var kv in Model3D.MaterialNameByKey)
                {
                    int m = _swap.IndexOf(kv.Value);
                    if (m < 0) continue;
                    var s = _swap.Evaluate(m, frame % Math.Max(1, _swap.FrameCount));
                    if (s.IsSet) swaps[kv.Key] = s.TextureName;
                }
                if (swaps.Count > 0) TextureSwaps = swaps;
            }

            var fades = new Dictionary<int, float>();
            var colours = new Dictionary<int, (float r, float g, float b)>();
            if (_colour != null)
                foreach (var kv in Model3D.MaterialNameByKey)
                {
                    int m = _colour.IndexOf(kv.Value);
                    if (m < 0) continue;
                    int at = frame % Math.Max(1, _colour.FrameCount);
                    float? v = _colour.Evaluate(m, at);
                    if (v.HasValue) fades[kv.Key] = v.Value;
                    var c = _colour.ColourAt(m, at);
                    if (c.HasValue) colours[kv.Key] = c.Value;
                }
            MaterialColours = colours.Count > 0 ? colours : null;

            // A hidden part is left out rather than drawn see-through, which is only the same thing
            // when there is nothing behind it.
            HiddenNodes = null;
            if (_showing != null && _showing.AnimationNames.Count > 0)
            {
                int at = frame % Math.Max(1, _showing.FrameCount);
                var gone = new HashSet<int>();
                for (int node = 0; node < _showing.PartCount(0); node++)
                    if (!_showing.Visible(0, node, at)) gone.Add(node);
                if (gone.Count > 0) HiddenNodes = gone;
            }

            if (fades.Count > 0) MaterialFades = fades;

            SayWhatDrivesNothing();
        }

        /// <summary>Names the chosen animations that touch nothing on this model, and what they wanted.</summary>
        private void SayWhatDrivesNothing()
        {
            var idle = new List<string>();
            if (_slide != null && (TextureMatrices == null || TextureMatrices.Count == 0))
                idle.Add(Wanted("The sliding picture", _slide.MaterialNames));
            if (_swap != null && (TextureSwaps == null || TextureSwaps.Count == 0))
                idle.Add(Wanted("The swapped picture", _swap.MaterialNames));
            if (_colour != null && (MaterialFades == null || MaterialFades.Count == 0))
                idle.Add(Wanted("The colour change", _colour.MaterialNames));

            CompanionNote = idle.Count == 0 ? "" : string.Join(" ", idle);
            OnPropertyChanged(nameof(HasCompanionNote));
        }

        private static string Wanted(string what, IReadOnlyList<string> names)
        {
            var real = names.Where(n => !string.IsNullOrWhiteSpace(n)).Take(3).ToList();
            return real.Count == 0
                ? $"{what} names no surface, so it changes nothing here."
                : $"{what} is for {string.Join(", ", real)}, which this model does not have, so it changes "
                  + "nothing here.";
        }


        /// <summary>
        /// What an animation picked on its own actually is: what it drives, how long it runs, and which
        /// model it was written for. Saying "this is an animation" and stopping is no use to anybody.
        /// </summary>
        public string DescribeAnimation(ModelAssets.Archive a, int index, ModelAssets.Kind kind)
        {
            byte[] b;
            try
            {
                var narc = new ScriptNarc(a.Dir);
                if (!narc.Available) return "";
                b = narc.Get(index);
            }
            catch { return ""; }
            if (b == null) return "";

            var bits = new List<string>();
            int frames = 0;
            switch (kind)
            {
                case ModelAssets.Kind.TextureAnimation:
                {
                    var t = TextureSrtAnimation.Load(b);
                    if (t == null) return "This one would not open.";
                    frames = t.FrameCount;
                    int moving = Enumerable.Range(0, t.MaterialNames.Count).Count(i => !t.IsStatic(i));
                    bits.Add(Count(t.MaterialNames.Count, "surface", "surfaces") + " it slides a picture across");
                    if (moving < t.MaterialNames.Count) bits.Add($"{moving} of them actually move");
                    bits.Add("on " + Join(t.MaterialNames));
                    break;
                }
                case ModelAssets.Kind.TextureSwap:
                {
                    var t = TexturePatternAnimation.Load(b);
                    if (t == null) return "This one would not open.";
                    frames = t.FrameCount;
                    bits.Add(Count(t.MaterialNames.Count, "surface", "surfaces") + " it swaps the picture on");
                    bits.Add("on " + Join(t.MaterialNames));
                    break;
                }
                case ModelAssets.Kind.MaterialAnimation:
                {
                    var m = MaterialColourAnimation.Load(b);
                    if (m == null) return "This one would not open.";
                    frames = m.FrameCount;
                    bits.Add(Count(m.MaterialNames.Count, "surface", "surfaces") + " it colours");
                    bits.Add(m.Fades ? "it fades them in and out" : "it holds them steady");
                    bits.Add("on " + Join(m.MaterialNames));
                    break;
                }
                case ModelAssets.Kind.VisibilityAnimation:
                {
                    var v = VisibilityAnimation.Load(b);
                    if (v == null) return "This one would not open.";
                    frames = v.FrameCount;
                    int parts = v.PartCount(0);
                    int changing = v.PartsThatChange(0).Count;
                    bits.Add(Count(parts, "part", "parts") + " it can hide");
                    bits.Add($"{changing} of them are hidden at some point");
                    break;
                }
                case ModelAssets.Kind.JointAnimation:
                {
                    var j = JointAnimation.Load(b);
                    if (j == null) return "This one would not open.";
                    frames = j.FrameCount;
                    bits.Add(Count(j.AnimatedObjects.Count, "part", "parts") + " it moves");
                    break;
                }
                default: return "";
            }

            string howLong = frames <= 0 ? "" :
                $"{frames} frames, about {frames / 30.0:0.#} seconds at the speed the DS runs them. ";
            return howLong + Sentence(bits) + ".";
        }

        private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

        private static string Join(IReadOnlyList<string> names)
        {
            var real = names.Where(n => !string.IsNullOrWhiteSpace(n)).Take(4).ToList();
            if (real.Count == 0) return "surfaces it does not name";
            return string.Join(", ", real) + (names.Count > real.Count ? " and more" : "");
        }

        private static string Sentence(List<string> bits)
        {
            if (bits.Count == 0) return "";
            string first = char.ToUpperInvariant(bits[0][0]) + bits[0].Substring(1);
            return bits.Count == 1 ? first : first + ", " + string.Join(", ", bits.Skip(1));
        }

        /// <summary>
        /// The models this animation could have been written for, best match first, so an animation
        /// picked on its own still says what it goes with.
        /// </summary>
        public string WhoClaimsIt(ModelAssets.Archive a, int index, ModelAssets.Kind kind)
        {
            byte[] b;
            try
            {
                var narc = new ScriptNarc(a.Dir);
                if (!narc.Available) return "";
                b = narc.Get(index);
            }
            catch { return ""; }
            if (b == null) return "";

            var names = NamesOf(kind, b);
            if (names.Count == 0) return "It carries no name, so there is no telling which model it is for.";

            var hits = new List<(string name, ModelAssets.Match sure)>();
            foreach (var other in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(other); } catch { continue; }
                foreach (var u in ModelAssets.Units(other, n))
                {
                    if (string.IsNullOrWhiteSpace(u.Name)) continue;
                    var sure = ModelAssets.MatchFor(names, u.Name);
                    if (sure != ModelAssets.Match.None) hits.Add((u.Name, sure));
                }
            }

            if (hits.Count == 0)
                return "No model in this game is named for it, so nothing here claims it.";

            var best = hits.OrderByDescending(h => h.sure).Take(3).ToList();
            string how = best[0].sure switch
            {
                ModelAssets.Match.Exact => "Written for",
                ModelAssets.Match.SameStart => "Named for",
                _ => "Probably for",
            };
            return how + " " + string.Join(", ", best.Select(h => h.name))
                 + (hits.Count > best.Count ? $" and {hits.Count - best.Count} more." : ".");
        }

        /// <summary>Redraws with whatever is chosen, without moving the frame on.</summary>
        private void Redraw()
        {
            DrawNow();
            OnPropertyChanged(nameof(CompanionSummary));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlayHelp));
            OnPropertyChanged(nameof(CompanionFrames));
        }
    }
}
