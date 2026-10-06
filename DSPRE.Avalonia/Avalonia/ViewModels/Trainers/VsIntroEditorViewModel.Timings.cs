using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DSPRE.ROMFiles;
using Field = DSPRE.ROMFiles.VsIntroTimingAddon.Field;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>One per-class intro timing; 0 keeps the game's own value.</summary>
    public sealed class TimingRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly Action<TimingRow> _changed;
        private int _value;
        private bool _visible;

        public TimingRow(Field field, string label, string tip, Action<TimingRow> changed)
        {
            Field = field; Label = label; Tip = tip; _changed = changed;
        }

        public Field Field { get; }
        public string Label { get; }
        public string Tip { get; }
        private int _style;
        public string GameValue => $"Game: {VsIntroTimingAddon.GameValue(Field, _style)}";

        public decimal Value
        {
            get => _value;
            set { int v = (int)value; if (v == _value) return; _value = v; Raise(nameof(Value)); _changed(this); }
        }

        public bool Visible { get => _visible; set { if (_visible == value) return; _visible = value; Raise(nameof(Visible)); } }

        internal void Show(int value, int style) { _value = value; _style = style; Raise(nameof(Value)); Raise(nameof(GameValue)); }
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>DSPRE's per-class intro timings, on top of the class metadata patch.</summary>
    public partial class VsIntroEditorViewModel
    {
        private VsIntroTimingAddon _timing;
        private string _timingWhy;

        public ObservableCollection<TimingRow> TimingRows { get; } = new();
        public bool HasTimings => _timing != null;
        public bool CanInstallTimings => _timing == null && _timingWhy == null && IsClassRecords;
        public string TimingNote => _timing != null ? "" : _timingWhy ?? "Add the timing table to give classes their own intro speeds.";
        public bool ShowTimingSection => IsClassRecords && Record != null;
        public bool ShowTimingRows => HasTimings && TimingRows.Any(r => r.Visible);

        // Which timings each style's code reads: the flash every hooked intro opens with, and the style's own moves.
        private static Field[] TimingFieldsFor(int style) => style switch
        {
            1 => new[] { Field.FlashCount, Field.GymSlide, Field.VsShrink, Field.VsGap },
            2 => new[] { Field.FlashCount, Field.VsShrink, Field.VsGap },
            3 => new[] { Field.FlashCount },
            4 => new[] { Field.FlashCount, Field.DoorsClose, Field.DoorsHold, Field.DoorsOpen },
            6 => new[] { Field.FlashCount, Field.EmblemFlight },
            0 or (>= 7 and <= 12) => new[] { Field.FlashCount },
            _ => Array.Empty<Field>(),
        };

        private void LoadTimings()
        {
            _timing = null; _timingWhy = null;
            if (!IsClassRecords) return;
            try
            {
                _timingWhy = VsIntroTimingAddon.WhyNot();
                if (_timingWhy == null) _timing = VsIntroTimingAddon.Load();
            }
            catch (Exception e) { _timingWhy = "The intro timings couldn't be read: " + e.Message; }
        }

        private void ReadyTimings()
        {
            TimingRows.Clear();
            void Add(Field f, string label, string tip) => TimingRows.Add(new TimingRow(f, label, tip, OnTimingChanged));
            Add(Field.FlashCount, "Flashes", "How many times the screen flashes before the intro");
            Add(Field.GymSlide, "Face slide", "Frames the leader's face takes to slide in");
            Add(Field.VsShrink, "VS shrink", "Frames each VS copy takes to shrink to size");
            Add(Field.VsGap, "VS gap", "Frames between one VS copy and the next");
            Add(Field.EmblemFlight, "Emblem flight", "Frames each emblem takes to fly in");
            Add(Field.DoorsClose, "Doors close", "Frames the doors take to slide shut");
            Add(Field.DoorsHold, "Doors hold", "Frames the doors stay shut");
            Add(Field.DoorsOpen, "Doors open", "Frames the doors take to slide open");
            ShowTimings();
        }

        private void ShowTimings()
        {
            Field[] used = Record == null ? Array.Empty<Field>() : TimingFieldsFor(Record.VsStyle);
            foreach (TimingRow row in TimingRows)
            {
                row.Visible = used.Contains(row.Field);
                row.Show(_timing?.Get(_recordIndex, row.Field) ?? 0, Record?.VsStyle ?? 0);
            }
            Raise(nameof(HasTimings), nameof(CanInstallTimings), nameof(TimingNote), nameof(ShowTimingSection), nameof(ShowTimingRows));
        }

        private void OnTimingChanged(TimingRow row)
        {
            if (_suppress || _timing == null || Record == null) return;
            _timing.Set(_recordIndex, row.Field, (int)row.Value);
            _undo?.Record();
            Raise(nameof(HasUnsavedChanges));
            if (!Animating) RenderPreview();
        }

        /// <summary>The class's timing for the preview, or the game's own value.</summary>
        private int TimingOf(Field field)
        {
            int v = _timing?.Get(_recordIndex, field) ?? 0;
            return v > 0 ? v : VsIntroTimingAddon.GameValue(field, Record?.VsStyle ?? 0);
        }

        public async Task InstallTimingsAsync()
        {
            try
            {
                _timing = VsIntroTimingAddon.Install();
                StatusText = "Timing table added. Save the ROM to keep it.";
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError("The timing table couldn't be added:\n" + e.Message, Title);
                return;
            }
            ShowTimings();
            StartUndo();
        }

        private void RestoreTimings(byte[] state)
        {
            if (_timing == null || state.Length == 0) return;
            _timing.Restore(state);
            ShowTimings();
        }

        private async Task<bool> SaveTimingsAsync()
        {
            if (_timing == null || !_timing.HasChanges) return true;
            try { _timing.Save(); return true; }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError("The intro timings were not saved:\n" + e.Message, Title);
                return false;
            }
        }
    }
}
