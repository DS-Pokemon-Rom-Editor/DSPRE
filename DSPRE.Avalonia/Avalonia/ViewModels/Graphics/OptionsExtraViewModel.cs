using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using Row = DSPRE.ROMFiles.OptionsMenuPatch.Row;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>
    /// The Options screen's text speeds, locked choices and shown rows. Before the Options patch is in, the game's own
    /// settings show greyed out. The speed names are lines of the Options text and save with the Text editor's writer.
    /// </summary>
    public sealed class OptionsExtraViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable, IScreenPreviewSource
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>Each pace and its label, slowest first.</summary>
        public static readonly (byte Pace, string Label)[] PaceChoices =
        {
            (8, "8 frames a letter"), (7, "7 frames a letter"), (6, "6 frames a letter"), (5, "5 frames a letter"),
            (4, "4 frames a letter"), (3, "3 frames a letter"), (2, "2 frames a letter"), (1, "1 frame a letter"),
            (OptionsMenuPatch.LettersFlag | 2, "2 letters a frame"), (OptionsMenuPatch.LettersFlag | 3, "3 letters a frame"),
            (OptionsMenuPatch.LettersFlag | 4, "4 letters a frame"), (OptionsMenuPatch.Page, "A page at once"),
        };

        private static readonly (Row Row, string Label)[] RowLabels =
            new[] { Row.Sound, Row.Scene, Row.Style, Row.Button, Row.Frame }.Select(r => (r, OptionsMenuPatch.Label(r))).ToArray();

        public string[] Paces { get; } = PaceChoices.Select(p => p.Label).ToArray();

        private readonly PendingArchiveWrites _pending = new();
        private PendingArchiveWrites.Target _settings, _text;
        private readonly Dictionary<Row, string[]> _choices = new();
        private bool _patched;
        private int _speedLine;
        private OptionsMenuPatch.Settings _now;

        public ObservableCollection<SpeedRow> Speeds { get; } = new();
        public ObservableCollection<OptionRow> Rows { get; } = new();

        /// <summary>The Preview tab redraws with the unsaved settings.</summary>
        public event Action Changed;

        public OptionsExtraViewModel()
        {
            Load();
            _pending.Changed += () =>
            {
                Refresh();
                // A row's own edit raises this, so the rows are rebuilt once that edit has finished.
                Dispatcher.UIThread.Post(Rebuild);
                foreach (string n in new[] { nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo), nameof(CanAdd), nameof(Problem), nameof(HasProblem), nameof(TextPlaces) }) Raise(n);
            };
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            TextArchive.Saved += OnTextSaved;
        }

        private void Load()
        {
            _patched = OptionsMenuPatch.IsApplied;
            _speedLine = _patched ? OptionsMenuPatch.SpeedNamesLine() : -1;
            int bank = OptionsMenuPatch.TextBankId;
            _settings = _patched ? new("options-settings", () => Encode(OptionsMenuPatch.Read()), b => OptionsMenuPatch.Write(Decode(b))) : null;
            _text = _patched ? PendingArchiveWrites.Text("options-text", bank, this) : null;
            _choices.Clear();
            if (OptionsMenuPatch.Supports)
                foreach ((Row row, string _) in RowLabels)
                    _choices[row] = OptionsMenuPatch.ChoiceNames(row).ToArray();
            Refresh();
            Rebuild();
            foreach (string n in new[] { nameof(Patched), nameof(OffersPatch), nameof(Unsupported), nameof(CanAdd), nameof(TextPlaces) }) Raise(n);
        }

        public bool Patched => _patched;
        public bool OffersPatch => !Patched && OptionsMenuPatch.Supports && !RomInfo.IsHGE;
        public bool Unsupported => !Patched && !OptionsMenuPatch.Supports;

        // Reads the pending settings and speed names, or the game's own settings without the patch.
        private void Refresh()
        {
            if (!_patched) { _now = OptionsMenuPatch.Supports ? OptionsMenuPatch.GameSettings() : null; return; }
            OptionsMenuPatch.Settings s = Decode(_pending.Read(_settings));
            if (s != null)
            {
                List<string> lines = PendingArchiveWrites.TextLines(_pending.Read(_text));
                for (int i = 0; i < s.Speeds.Count; i++)
                    s.Speeds[i] = s.Speeds[i] with { Name = _speedLine + i < lines.Count ? lines[_speedLine + i] : "" };
            }
            _now = s;
            _shown = s != null && _pending.IsDirty ? new GameTextScreens.OptionsShown(OptionsMenuPatch.Layout(s), OptionsMenuPatch.HgShownMap(s)) : null;
            Changed?.Invoke();
        }

        private GameTextScreens.OptionsShown _shown;

        public IDisposable DrawScope() => _shown == null ? null : GameTextScreens.UseOptions(_shown);

        private OptionsMenuPatch.Settings Current => _now;

        public bool CanAdd => _patched && Current is { } s && s.Speeds.Count < OptionsMenuPatch.MaxSpeeds;
        public string Problem => _patched && Current is { } s ? OptionsMenuPatch.WhyNot(s) : null;
        public bool HasProblem => Problem != null;

        private void Rebuild()
        {
            OptionsMenuPatch.Settings now = Current;
            Speeds.Clear();
            Rows.Clear();
            if (now == null) return;
            for (int i = 0; i < now.Speeds.Count; i++) Speeds.Add(new SpeedRow(this, i, now.Speeds[i], now.Speeds.Count > 1));
            foreach ((Row row, string label) in RowLabels)
                Rows.Add(new OptionRow(this, row, label, _choices[row], now.Locks[(int)row], now.Removed[(int)row], now.Defaults[(int)row]));
            SpeedNames = now.Speeds.Select(p => p.Name).ToArray();
            _speedDefault = Math.Min(now.Defaults[(int)Row.TextSpeed], now.Speeds.Count - 1);
            Raise(nameof(SpeedNames));
            Raise(nameof(SpeedDefault));
        }

        // One undo step: the settings, and the speed names as text lines.
        private void Change(Action<OptionsMenuPatch.Settings> change)
        {
            if (!_patched || Current == null) return;
            OptionsMenuPatch.Settings now = Decode(Encode(Current));
            for (int i = 0; i < now.Speeds.Count; i++) now.Speeds[i] = now.Speeds[i] with { Name = Current.Speeds[i].Name };
            change(now);
            List<string> lines = PendingArchiveWrites.TextLines(_pending.Read(_text));
            for (int i = 0; i < now.Speeds.Count && _speedLine + i < lines.Count; i++) lines[_speedLine + i] = now.Speeds[i].Name;
            _pending.Import(() =>
            {
                _pending.Stage(_settings, Encode(now));
                _pending.Stage(_text, PendingArchiveWrites.TextBytes(lines));
                return null;
            });
        }

        internal void Rename(int i, string name) => Change(s => { if (i < s.Speeds.Count) s.Speeds[i] = s.Speeds[i] with { Name = name ?? "" }; });

        internal void SetPace(int i, int choice) => Change(s =>
        {
            if (i < s.Speeds.Count && choice >= 0 && choice < PaceChoices.Length) s.Speeds[i] = s.Speeds[i] with { Pace = PaceChoices[choice].Pace };
        });

        internal void Remove(int i) => Change(s =>
        {
            if (s.Speeds.Count <= 1 || i >= s.Speeds.Count) return;
            s.Speeds.RemoveAt(i);
            int d = s.Defaults[(int)Row.TextSpeed];
            if (d > i || d >= s.Speeds.Count) s.Defaults[(int)Row.TextSpeed] = Math.Max(0, d - 1);
        });

        public void Add() => Change(s =>
        {
            if (s.Speeds.Count < OptionsMenuPatch.MaxSpeeds) s.Speeds.Add(new OptionsMenuPatch.Speed("NEW", OptionsMenuPatch.Page));
        });

        // A row can only go once it's locked, so unlocking brings it back.
        internal void SetLock(Row row, int choice) => Change(s =>
        {
            if (choice < 0) return;
            s.Locks[(int)row] = choice == 0 ? null : choice - 1;
            if (choice == 0 && OptionsMenuPatch.Lockable(row)) s.Removed[(int)row] = false;
        });

        internal void SetRemoved(Row row, bool removed) => Change(s => s.Removed[(int)row] = removed);

        internal void SetDefault(Row row, int value) => Change(s => { if (value >= 0) s.Defaults[(int)row] = value; });

        /// <summary>The speeds' names, for the new-save speed.</summary>
        public string[] SpeedNames { get; private set; } = Array.Empty<string>();
        private int _speedDefault;
        public int SpeedDefault { get => _speedDefault; set => SetDefault(Row.TextSpeed, value); }

        /// <summary>The text archive and lines with the speed names, for the Text help tip.</summary>
        public string TextPlaces
        {
            get
            {
                if (!_patched || Current is not { } s) return null;
                int bank = OptionsMenuPatch.TextBankId;
                string names = $"Speed names: text archive {bank}, lines {_speedLine} to {_speedLine + s.Speeds.Count - 1}.";
                return OptionsMenuPatch.SpeedDescriptionLine < 0 ? names
                    : names + $" Text speed's description: line {OptionsMenuPatch.SpeedDescriptionLine}.";
            }
        }

        public void OpenText() => AvaloniaEditorLauncher.OpenTextEditorAt(OptionsMenuPatch.TextBankId,
            OptionsMenuPatch.SpeedDescriptionLine >= 0 ? OptionsMenuPatch.SpeedDescriptionLine : Math.Max(0, _speedLine));

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (_patched || !OptionsMenuPatch.IsApplied) return;
            Load();
        }

        // The Text editor saved the Options text: show its names unless this tab has its own unsaved ones.
        private void OnTextSaved(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || id != OptionsMenuPatch.TextBankId || HasUnsavedChanges) return;
            Dispatcher.UIThread.Post(() => { Load(); Raise(nameof(HasUnsavedChanges)); });
        }

        public void Detach()
        {
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            TextArchive.Saved -= OnTextSaved;
        }

        // Leaves out the speed names, which save as text.
        private static byte[] Encode(OptionsMenuPatch.Settings s)
        {
            if (s == null) return null;
            using MemoryStream m = new();
            using BinaryWriter w = new(m, Encoding.UTF8);
            w.Write((byte)s.Speeds.Count);
            foreach (OptionsMenuPatch.Speed p in s.Speeds) w.Write(p.Pace);
            foreach (int? l in s.Locks) w.Write(l is int v ? (byte)v : (byte)0xFF);
            foreach (bool r in s.Removed) w.Write(r);
            foreach (int d in s.Defaults) w.Write((byte)d);
            return m.ToArray();
        }

        private static OptionsMenuPatch.Settings Decode(byte[] b)
        {
            if (b == null || b.Length == 0) return null;
            using BinaryReader r = new(new MemoryStream(b), Encoding.UTF8);
            int count = r.ReadByte();
            List<OptionsMenuPatch.Speed> speeds = Enumerable.Range(0, count).Select(_ => new OptionsMenuPatch.Speed("", r.ReadByte())).ToList();
            int rows = Enum.GetValues<Row>().Length;
            int?[] locks = Enumerable.Range(0, rows).Select(_ => r.ReadByte() is byte v && v != 0xFF ? (int?)v : null).ToArray();
            bool[] removed = Enumerable.Range(0, rows).Select(_ => r.ReadBoolean()).ToArray();
            int[] defaults = Enumerable.Range(0, rows).Select(_ => (int)r.ReadByte()).ToArray();
            return new OptionsMenuPatch.Settings(speeds, locks, removed, defaults);
        }

        public bool HasUnsavedChanges => _pending.IsDirty;
        public string UnsavedChangesDescription => "Options extras";
        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();

        public void SaveChanges()
        {
            if (HasProblem) return;
            _pending.Save();
            Refresh();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges() => _pending.Discard();

        /// <summary>One speed row.</summary>
        public sealed class SpeedRow
        {
            private readonly OptionsExtraViewModel _owner;
            private readonly int _index;
            private readonly OptionsMenuPatch.Speed _speed;

            internal SpeedRow(OptionsExtraViewModel owner, int index, OptionsMenuPatch.Speed speed, bool canRemove) =>
                (_owner, _index, _speed, CanRemove) = (owner, index, speed, canRemove);

            public string Number => (_index + 1).ToString();
            public string[] Paces => _owner.Paces;
            public string Name { get => _speed.Name; set => _owner.Rename(_index, value); }
            public int PaceIndex { get => Array.FindIndex(PaceChoices, p => p.Pace == _speed.Pace); set => _owner.SetPace(_index, value); }
            public bool CanRemove { get; }
            public void Remove() => _owner.Remove(_index);
        }

        /// <summary>One option row: its lock and whether it shows.</summary>
        public sealed class OptionRow
        {
            private readonly OptionsExtraViewModel _owner;
            private readonly Row _row;
            private readonly int _lock;
            private readonly bool _removed;

            internal OptionRow(OptionsExtraViewModel owner, Row row, string label, string[] choices, int? locked, bool removed, int fresh)
            {
                (_owner, _row, Label, Names, _lock, _removed, _default) = (owner, row, label, choices, locked is int v ? v + 1 : 0, removed, fresh);
                Lockable = OptionsMenuPatch.Lockable(row);
                Choices = Lockable ? new[] { "Any" }.Concat(choices).ToArray() : null;
            }

            private readonly int _default;
            public string Label { get; }
            public string[] Names { get; }
            public string[] Choices { get; }
            public bool Lockable { get; }
            // A locked row starts on its locked choice.
            public int Default { get => _lock > 0 ? _lock - 1 : _default; set => _owner.SetDefault(_row, value); }
            public bool CanSetDefault => _lock == 0;
            public int Lock { get => _lock; set => _owner.SetLock(_row, value); }
            public bool Remove { get => _removed; set => _owner.SetRemoved(_row, value); }
            public bool CanRemove => !Lockable || _lock > 0;
            public string RemoveTip => CanRemove ? "Takes the row off the screen" : "Lock a choice first";
        }
    }
}
