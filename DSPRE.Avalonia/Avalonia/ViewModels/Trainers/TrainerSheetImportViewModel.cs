using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>Each option change is tried on fresh copies of the frames, so the window shows what Apply will do.</summary>
    public sealed class TrainerSheetImportViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public enum Jobs { Drawings, Animation }

        public sealed class Target
        {
            public string Label;
            public Func<ISheetFrames> Open;
        }

        public sealed class Leftover : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public int Part, Frame;
            public Bitmap Thumbnail { get; init; }
            public string Label { get; init; }
            public string Uses { get; init; }
            public bool HasUses => !string.IsNullOrEmpty(Uses);
            private bool _keep = true;
            public bool Keep
            {
                get => _keep;
                set { if (_keep == value) return; _keep = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keep))); Changed?.Invoke(); }
            }
            public Action Changed;
        }

        public sealed class StepRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            public int Number { get; init; }
            public Bitmap Thumbnail { get; init; }
            private string _shows = "";
            public string Shows { get => _shows; set { _shows = value; Raise(); } }
            private decimal _hold = 8, _x, _y;
            public decimal Hold { get => _hold; set { _hold = value; Raise(); Changed?.Invoke(); } }
            public decimal X { get => _x; set { _x = value; Raise(); Changed?.Invoke(); } }
            public decimal Y { get => _y; set { _y = value; Raise(); Changed?.Invoke(); } }
            public Action Changed;
        }

        public sealed record AnimationChoice(int Index, string Label)
        {
            public override string ToString() => Label;
        }

        private readonly IReadOnlyList<Target> _targets;
        private readonly IReadOnlyList<uint[]> _palettes;
        private readonly TrainerSpriteSheet.Read _sheet;

        public Jobs Job { get; }
        public bool IsDrawings => Job == Jobs.Drawings;
        public bool IsAnimation => Job == Jobs.Animation;
        public string Title => IsDrawings ? "Import frames sheet" : "Import animation sheet";
        public string SheetName { get; }

        public ObservableCollection<IBrush> SheetSwatches { get; } = new();
        public ObservableCollection<IBrush> SpriteSwatches { get; } = new();
        public ObservableCollection<Leftover> Leftovers { get; } = new();
        public ObservableCollection<StepRow> Steps { get; } = new();
        // HeartGold's two back sprite sets have different drawings, so an animation goes into one.
        public ObservableCollection<string> Sets { get; } = new();
        public ObservableCollection<AnimationChoice> Animations { get; } = new();

        public bool HasLeftovers => Leftovers.Count > 0;
        public bool HasSetChoice => IsAnimation && Sets.Count > 1;

        private int _set;
        public int SelectedSet
        {
            get => _set;
            set { if (value < 0 || value >= _targets.Count || _set == value) return; _set = value; OnPropertyChanged(); FillStepsFromSprite(); Retry(); }
        }

        private bool _useSheetColours;
        public bool UseSheetColours
        {
            get => _useSheetColours;
            set { if (_useSheetColours == value) return; _useSheetColours = value; OnPropertyChanged(); OnPropertyChanged(nameof(UseSpriteColours)); Retry(); }
        }
        public bool UseSpriteColours { get => !_useSheetColours; set => UseSheetColours = !value; }

        public bool CanUseSheetColours { get; }

        private AnimationChoice _animation;
        public AnimationChoice Animation
        {
            get => _animation;
            set { if (value == null || _animation == value) return; _animation = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowsPosition)); FillStepsFromSprite(); Retry(); }
        }

        private bool _showsPosition;
        public bool ShowsPosition { get => _showsPosition; private set { _showsPosition = value; OnPropertyChanged(); } }

        private string _outcome = "";
        public string Outcome { get => _outcome; private set { _outcome = value; OnPropertyChanged(); } }

        private string _problem;
        public string Problem { get => _problem; private set { _problem = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProblem)); OnPropertyChanged(nameof(CanApply)); } }
        public bool HasProblem => _problem != null;
        public bool CanApply => _problem == null && _result != null;

        public sealed class Result
        {
            public List<ISheetFrames> Frames = new();
            public uint[][] Palettes;
        }
        private Result _result;
        public Result Outcomes => _result;

        private readonly TrainerSpriteSheet.StepsFile _stepsFile;
        private bool _filling;

        public TrainerSheetImportViewModel(Jobs job, IReadOnlyList<Target> targets, IReadOnlyList<uint[]> palettes,
            TrainerSpriteSheet.Read sheet, string sheetName, TrainerSpriteSheet.StepsFile steps, IReadOnlyList<AnimationChoice> animations, int set = 0)
        {
            Job = job; _targets = targets; _palettes = palettes; _sheet = sheet; SheetName = sheetName; _stepsFile = steps;

            foreach (uint[] p in palettes) foreach (uint c in p.Take(16)) SpriteSwatches.Add(Brush(c));
            int used = UsedColours();
            for (int i = 0; i < Math.Min(sheet.Colours.Count, Math.Max(used, 1)); i++) SheetSwatches.Add(Brush(sheet.Colours[i]));

            if (sheet.Indexed)
            {
                CanUseSheetColours = used <= palettes.Count * 16;
            }
            else
            {
                // Without an indexed PNG there is no telling which palette a colour belongs to.
                CanUseSheetColours = palettes.Count == 1 && used <= 16;
            }
            // An exported sheet carries the sprite's own palette.
            _useSheetColours = CanUseSheetColours;

            if (IsAnimation)
            {
                if (targets.Count > 1) foreach (Target t in targets) Sets.Add(t.Label);
                int named = steps?.Set == null ? -1 : targets.ToList().FindIndex(t => t.Label == steps.Set);
                _set = Math.Clamp(named >= 0 ? named : set, 0, targets.Count - 1);
                foreach (AnimationChoice a in animations) Animations.Add(a);
                _animation = Animations.FirstOrDefault(a => a.Index == steps?.Animation) ?? Animations.FirstOrDefault(a => a.Index == 1) ?? Animations.FirstOrDefault();
                FillStepsFromSprite();
            }
            else BuildLeftovers();
            Retry();
        }

        // ── drawings job ───────────────────────────────────────────────────────

        private int SheetFrames(int row, int frameCount)
        {
            int last = -1;
            for (int c = 0; c < _sheet.Columns; c++) if (!TrainerSpriteSheet.Read.IsEmpty(_sheet.CellAt(row, c))) last = c;
            return Math.Max(Math.Min(_sheet.Columns, frameCount), last + 1);
        }

        private void BuildLeftovers()
        {
            for (int p = 0; p < _targets.Count && p < _sheet.Rows; p++)
            {
                ISheetFrames frames = _targets[p].Open();
                if (!frames.CanChangeFrameCount) continue;
                for (int f = SheetFrames(p, frames.FrameCount); f < frames.FrameCount; f++)
                {
                    List<(int Sequence, int Step)> uses = (frames as TrainerSpriteFrames)?.UsesOf(f).ToList() ?? new();
                    Leftovers.Add(new Leftover
                    {
                        Part = p, Frame = f,
                        Thumbnail = Render(frames.Draw(f)),
                        Label = (_targets.Count > 1 ? _targets[p].Label + ", frame " : "Frame ") + f + (frames.IsBlank(f) ? " (blank)" : ""),
                        Uses = uses.Count == 0 ? "" : "Shown in " + string.Join(", ", uses.GroupBy(u => u.Sequence)
                            .Select(g => $"animation {g.Key} step{(g.Count() > 1 ? "s" : "")} {string.Join(", ", g.Select(u => u.Step))}")),
                        Changed = Retry,
                    });
                }
            }
            OnPropertyChanged(nameof(HasLeftovers));
        }

        private string ApplyDrawings(Result result)
        {
            if (_sheet.Rows < _targets.Count)
                return _targets.Count == 2 ? $"This sprite has two sets, {_targets[0].Label.ToLowerInvariant()} and {_targets[1].Label.ToLowerInvariant()}; the sheet needs a row for each." : "The sheet has no rows.";
            if (_sheet.Rows > _targets.Count) return $"The sheet has {_sheet.Rows} rows; this sprite takes {_targets.Count}.";

            List<string> lines = new List<string>();
            for (int p = 0; p < _targets.Count; p++)
            {
                ISheetFrames frames = _targets[p].Open();
                int count = SheetFrames(p, frames.FrameCount), before = frames.FrameCount;
                if (count > before && !frames.CanChangeFrameCount) return $"The sheet has {count} frames; this sprite always has {before}.";

                for (int f = 0; f < count; f++)
                {
                    if (f >= frames.FrameCount) ((TrainerSpriteFrames)frames).AddFrame();
                    int[] canvas = Convert(_sheet.CellAt(p, f), frames.PalettesOf(f), $"Frame {f}", out string why);
                    if (canvas == null) return why;
                    why = frames.SetDrawing(f, canvas);
                    if (why != null) return why;
                }
                List<int> remove = Leftovers.Where(l => l.Part == p && !l.Keep).Select(l => l.Frame).ToList();
                if (remove.Count > 0) ((TrainerSpriteFrames)frames).RemoveFrames(remove);

                string who = _targets.Count > 1 ? _targets[p].Label + ": " : "";
                string change = count > before ? $", {count - before} added" : remove.Count > 0 ? $", {remove.Count} removed" : "";
                int redrawn = Math.Min(count, before);
                lines.Add($"{who}{redrawn} frame{(redrawn == 1 ? "" : "s")} redrawn{change}.");
                result.Frames.Add(frames);
            }
            Outcome = string.Join(" ", lines);
            return null;
        }

        // ── animation job ─────────────────────────────────────────────────────

        private void FillStepsFromSprite()
        {
            _filling = true;
            Steps.Clear();
            TrainerSpriteFrames frames = _targets[_set].Open() as TrainerSpriteFrames;
            IReadOnlyList<TrainerSpriteFrames.Step> current = frames?.StepsOf(_animation?.Index ?? 1) ?? Array.Empty<TrainerSpriteFrames.Step>();
            ShowsPosition = frames != null && _animation != null && frames.SequenceShifts(_animation.Index);

            int count = _stepsFile?.Steps.Count > 0 ? Math.Min(_stepsFile.Steps.Count, _sheet.Cells.Count) : LastDrawnCell() + 1;
            for (int i = 0; i < count; i++)
            {
                TrainerSpriteSheet.StepJson fromFile = _stepsFile != null && i < _stepsFile.Steps.Count ? _stepsFile.Steps[i] : null;
                TrainerSpriteFrames.Step fromSprite = i < current.Count ? current[i] : null;
                Steps.Add(new StepRow
                {
                    Number = i,
                    Thumbnail = RenderSheetCell(_sheet.Cells[i]),
                    Hold = fromFile?.Hold ?? fromSprite?.Hold ?? 8,
                    X = fromFile?.X ?? fromSprite?.X ?? 0,
                    Y = fromFile?.Y ?? fromSprite?.Y ?? 0,
                    Changed = Retry,
                });
            }
            _filling = false;
        }

        private int LastDrawnCell()
        {
            for (int i = _sheet.Cells.Count - 1; i >= 0; i--) if (!TrainerSpriteSheet.Read.IsEmpty(_sheet.Cells[i])) return i;
            return 0;
        }

        private string ApplyAnimation(Result result)
        {
            if (Steps.Count == 0) return "The sheet has no steps.";
            if (_animation == null) return "Pick an animation.";
            string[] shows = new string[Steps.Count];
            int added = 0;
            for (int p = 0; p < _targets.Count; p++)
            {
                if (p != _set) { result.Frames.Add(null); continue; }
                TrainerSpriteFrames frames = (TrainerSpriteFrames)_targets[p].Open();

                List<(int Frame, int[] Canvas)> known = new List<(int Frame, int[] Canvas)>();
                for (int f = 0; f < frames.FrameCount; f++) known.Add((f, frames.Draw(f)));
                List<TrainerSpriteFrames.Step> steps = new List<TrainerSpriteFrames.Step>();
                for (int i = 0; i < Steps.Count; i++)
                {
                    int[] canvas = Convert(_sheet.Cells[i], frames.PalettesOf(0), $"Step {i}", out string why);
                    if (canvas == null) return why;
                    int frame = known.FirstOrDefault(k => k.Canvas.AsSpan().SequenceEqual(canvas), (-1, null)).Frame;
                    if (frame < 0)
                    {
                        frame = frames.AddFrame();
                        why = frames.SetDrawing(frame, canvas);
                        if (why != null) return why;
                        known.Add((frame, frames.Draw(frame)));
                        added++;
                        shows[i] = $"New frame {frame}";
                    }
                    else shows[i] = $"Frame {frame}";
                    StepRow row = Steps[i];
                    steps.Add(new TrainerSpriteFrames.Step(frame, (int)row.Hold, (int)row.X, (int)row.Y));
                }
                string error = frames.SetSequence(_animation.Index, steps);
                if (error != null) return error;
                result.Frames.Add(frames);
            }
            for (int i = 0; i < Steps.Count; i++) if (shows[i] != null) Steps[i].Shows = shows[i];
            Outcome = $"{Steps.Count} step{(Steps.Count == 1 ? "" : "s")}, {(added == 0 ? "all showing frames already drawn" : $"{added} new frame{(added == 1 ? "" : "s")}")}.";
            return null;
        }

        // ── shared ────────────────────────────────────────────────────────────

        private void Retry()
        {
            if (_filling) return;
            Result result = new Result();
            _approximated = 0;
            string why;
            try
            {
                why = IsDrawings ? ApplyDrawings(result) : ApplyAnimation(result);
            }
            catch (Exception e) { why = e.Message; }
            if (why == null && _useSheetColours) result.Palettes = TrainerSpriteSheet.SheetPalettes(_sheet, _palettes);
            if (why == null && !_useSheetColours && _approximated > 0)
                Outcome += $" {_approximated} pixel{(_approximated == 1 ? "" : "s")} had no exact colour and took the nearest.";
            _result = why == null ? result : null;
            Problem = why;
            OnPropertyChanged(nameof(CanApply));
        }

        private int _approximated;

        private int[] Convert(int[] cell, int[] under, string where, out string why)
        {
            why = null;
            if (_useSheetColours) return TrainerSpriteSheet.WithSheetColours(cell, under, _palettes.Count, where, out why);
            return TrainerSpriteSheet.WithSpriteColours(cell, _sheet.Colours, under, _palettes, ref _approximated);
        }

        private int UsedColours()
        {
            int max = 0;
            foreach (int[] cell in _sheet.Cells) foreach (int v in cell) if (v > max) max = v;
            return max + 1;
        }

        private static IBrush Brush(uint argb) => new SolidColorBrush(Color.FromUInt32(0xFF000000 | argb));

        private Bitmap Render(int[] canvas)
        {
            int n = TrainerSpriteSheet.CellSize;
            RawImage raw = new DSPRE.RawImage(n, n);
            for (int i = 0; i < canvas.Length; i++)
            {
                int v = canvas[i];
                if ((v & 0xF) == 0) continue;
                uint c = _palettes[Math.Min(v >> 4, _palettes.Count - 1)][v & 0xF];
                raw.SetPixel(i % n, i / n, (byte)(c >> 16), (byte)(c >> 8), (byte)c, 255);
            }
            return ImageConverter.ToAvaloniaBitmap(raw);
        }

        private Bitmap RenderSheetCell(int[] cell)
        {
            int n = TrainerSpriteSheet.CellSize;
            RawImage raw = new DSPRE.RawImage(n, n);
            for (int i = 0; i < cell.Length; i++)
            {
                if (cell[i] == 0) continue;
                uint c = _sheet.Colours[cell[i]];
                raw.SetPixel(i % n, i / n, (byte)(c >> 16), (byte)(c >> 8), (byte)c, 255);
            }
            return ImageConverter.ToAvaloniaBitmap(raw);
        }
    }
}
