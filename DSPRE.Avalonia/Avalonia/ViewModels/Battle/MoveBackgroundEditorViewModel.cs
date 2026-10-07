using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>The move-effect backgrounds: which battle background files each background id draws with.</summary>
    public sealed class MoveBackgroundEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private MoveBackgroundTable _table;
        private int[][] _saved = Array.Empty<int[]>();
        private readonly BattleBgRenderer _renderer = new();

        public ObservableCollection<string> Backgrounds { get; } = new();
        public string[] Screens { get; } = { "Screen", "Reversed screen", "Contest screen" };
        public int ArchiveMax { get; private set; } = 65535;

        private string _status = "";
        public string StatusText { get => _status; private set { _status = value; OnPropertyChanged(); } }

        public bool CanResize => _table?.FromSource == true;

        private int _selected = -1;
        public int SelectedIndex
        {
            get => _selected;
            set { if (value == _selected) return; _selected = value; OnPropertyChanged(); RaiseRow(); }
        }
        public bool HasSelection => _table != null && _selected >= 0 && _selected < _table.Rows.Count;

        private int _screen;
        public int ScreenIndex { get => _screen; set { if (value is >= 0 and <= 2 && value != _screen) { _screen = value; OnPropertyChanged(); RenderPreview(); } } }

        private int Get(int c) => HasSelection ? _table.Rows[_selected][c] : 0;
        private void Put(int c, int v)
        {
            if (!HasSelection || _table.Rows[_selected][c] == v) return;
            _table.Rows[_selected][c] = v;
            RenderPreview();
            Changed();
        }
        public int Drawing { get => Get(0); set => Put(0, value); }
        public int Palette { get => Get(1); set => Put(1, value); }
        public int Screen { get => Get(2); set => Put(2, value); }
        public int ReversedScreen { get => Get(3); set => Put(3, value); }
        public int ContestScreen { get => Get(4); set => Put(4, value); }

        private Bitmap _preview;
        public Bitmap Preview { get => _preview; private set { _preview = value; OnPropertyChanged(); } }

        private void RaiseRow()
        {
            foreach (string n in new[] { nameof(HasSelection), nameof(Drawing), nameof(Palette), nameof(Screen), nameof(ReversedScreen), nameof(ContestScreen) }) OnPropertyChanged(n);
            RenderPreview();
        }

        private void RenderPreview()
        {
            BattleBgRenderer.BgImage img = HasSelection ? _renderer.Build(_table.Rows[_selected], _screen) : null;
            Preview = img == null ? null : ImageConverter.FromRgba(img.Rgba, img.Width, img.Height);
        }

        public MoveBackgroundEditorViewModel() { }

        public MoveBackgroundEditorViewModel(bool load)
        {
            if (load) Load();
        }

        private void Load()
        {
            int keep = _selected;
            if (!MoveBackgroundTable.TryLoad(out _table, out string error)) { _table = null; StatusText = error; return; }
            _saved = _table.Rows.Select(r => r.ToArray()).ToArray();
            ScriptNarc narc = new ScriptNarc(DirNames.battleBg);
            ArchiveMax = narc.Available ? Math.Max(0, narc.Count - 1) : 65535;
            OnPropertyChanged(nameof(ArchiveMax));
            RebuildList();
            _selected = -1;
            SelectedIndex = Backgrounds.Count == 0 ? -1 : Math.Clamp(keep, 0, Backgrounds.Count - 1);
            StatusText = $"{_table.Rows.Count} backgrounds · " + (_table.FromSource ? DSPRE.HgEngine.HgEngineMoveBackgrounds.RelPath : $"overlay {MoveBackgroundTableSite.Overlay}");
            OnPropertyChanged(nameof(CanResize));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
        }

        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(_table.Rows.Count);
            foreach (int[] row in _table.Rows) { w.Write(row.Length); foreach (int v in row) w.Write(v); }
        });

        private void ApplyState(byte[] state)
        {
            int keep = _selected;
            _table.Rows.Clear();
            ByteStateUndo.Unpack(state, r =>
            {
                for (int n = r.ReadInt32(), i = 0; i < n; i++)
                {
                    int[] row = new int[r.ReadInt32()];
                    for (int c = 0; c < row.Length; c++) row[c] = r.ReadInt32();
                    _table.Rows.Add(row);
                }
            });
            if (Backgrounds.Count != _table.Rows.Count) RebuildList();
            _selected = -1;
            SelectedIndex = Backgrounds.Count == 0 ? -1 : Math.Clamp(keep, 0, Backgrounds.Count - 1);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void Changed()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        private void RebuildList()
        {
            Backgrounds.Clear();
            for (int i = 0; i < _table.Rows.Count; i++) Backgrounds.Add($"Background {i}");
        }

        public void AddRow()
        {
            if (!CanResize) return;
            _table.Rows.Add((HasSelection ? _table.Rows[_selected] : _table.Rows.LastOrDefault() ?? new int[MoveBackgroundTable.Columns]).ToArray());
            RebuildList();
            SelectedIndex = _table.Rows.Count - 1;
            Changed();
        }

        // Move scripts name backgrounds by position, so only the last one can go without renumbering the rest.
        public void RemoveRow()
        {
            if (!CanResize || _table.Rows.Count <= 1) return;
            _table.Rows.RemoveAt(_table.Rows.Count - 1);
            RebuildList();
            _selected = -1;
            SelectedIndex = _table.Rows.Count - 1;
            Changed();
        }

        public bool HasUnsavedChanges => _table != null && (_table.Rows.Count != _saved.Length || _table.Rows.Zip(_saved).Any(z => !z.First.SequenceEqual(z.Second)));
        public string UnsavedChangesDescription => "Move Backgrounds";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (!HasUnsavedChanges) return true;
            int members = ArchiveMax + 1;
            string error;
            if (_table.FromSource)
            {
                (bool saved, error) = await HgEngineSave.RunAsync(() =>
                {
                    try { _table.Save(members); return null; }
                    catch (InvalidOperationException e) { return e.Message; }
                });
                if (!saved) { if (error != null) await DialogHelper.ShowError("The move backgrounds were not saved:\n" + error, "Move Backgrounds"); return false; }
            }
            else
            {
                try { _table.Save(members); }
                catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    await DialogHelper.ShowError("The move backgrounds were not saved:\n" + e.Message, "Move Backgrounds");
                    return false;
                }
            }
            _saved = _table.Rows.Select(r => r.ToArray()).ToArray();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges() => Load();
    }
}
