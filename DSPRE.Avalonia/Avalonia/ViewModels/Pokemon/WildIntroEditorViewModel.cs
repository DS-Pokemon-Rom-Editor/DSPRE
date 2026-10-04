using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Trainers;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using static DSPRE.ROMFiles.VsIntroTables;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>Which wild Pokémon get their own battle intro, and the music of the intros only wild Pokémon use.</summary>
    public class WildIntroEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private void Raise(params string[] names) { foreach (var n in names) Raise(n); }

        public const string Title = "Wild Pokémon Intro Editor";

        private VsIntroTables _t;
        private VsIntroNames _names = new();
        private bool _suppress;

        public WildIntroEditorViewModel() { }

        /// <summary>File work only, for the busy overlay's thread.</summary>
        public void Load()
        {
            _t = VsIntroTables.Load(Part.Wild);
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives });
            _names = VsIntroNames.Read();
        }

        public void Ready()
        {
            Build();
            StartUndo();
            SelectedSpecies = SpeciesRows.FirstOrDefault();
            SelectedCombo = ComboRows.FirstOrDefault();
        }

        public bool SpeciesEditable => _t?.SpeciesRowCount > 0;
        public string SpeciesNote => SpeciesEditable ? "The first row for a Pokémon wins. Rows can be replaced but not added."
            : "This game picks these Pokémon in its code, so they can't be changed here.";

        private string _status = "";
        public string StatusText { get => _status; set { if (_status == value) return; _status = value; Raise(); } }

        // ── Lists ────────────────────────────────────────────────────────────────────────────────

        public ObservableCollection<IntroRow> SpeciesRows { get; } = new();
        public ObservableCollection<IntroRow> ComboRows { get; } = new();
        public ObservableCollection<string> SpeciesChoices { get; } = new();
        public ObservableCollection<string> ComboChoices { get; } = new();
        public ObservableCollection<string> SequenceChoices { get; } = new();
        private readonly List<int> _comboIds = new(), _sequenceIds = new();

        private IEnumerable<int> WildCombos() => Enumerable.Range(0, _t.ComboCount).Where(c => _t.OwnsMusic(c) || _t.SpeciesUsing(c).Count > 0);

        private void Build()
        {
            SpeciesChoices.Clear();
            for (int i = 0; i < _names.Species.Length; i++) SpeciesChoices.Add(_names.SpeciesName(i));
            _sequenceIds.Clear();
            _sequenceIds.AddRange(_names.SequenceChoices(Enumerable.Range(0, _t.ComboCount).Select(_t.SequenceOf)));
            SequenceChoices.Clear();
            foreach (int id in _sequenceIds) SequenceChoices.Add(_names.Sequence(id));

            _comboIds.Clear();
            _comboIds.AddRange(WildCombos());
            ComboChoices.Clear();
            foreach (int c in _comboIds) ComboChoices.Add(_names.Combo(_t, c));

            SpeciesRows.Clear();
            if (SpeciesEditable)
                for (int i = 0; i < _t.SpeciesRowCount; i++) SpeciesRows.Add(new IntroRow { Class = i });
            else
                foreach (var kv in _t.Music.CodeSpeciesCombos.OrderBy(k => k.Key)) SpeciesRows.Add(new IntroRow { Class = kv.Key, Combo = kv.Value });
            ComboRows.Clear();
            foreach (int c in _comboIds) ComboRows.Add(new IntroRow { Combo = c });
            Rename();
        }

        private (int Species, int Combo) SpeciesOf(IntroRow row) => SpeciesEditable ? _t.SpeciesRow(row.Class) : (row.Class, row.Combo);

        private void Rename()
        {
            foreach (var row in SpeciesRows)
            {
                var (sp, combo) = SpeciesOf(row);
                row.Title = _names.SpeciesName(sp);
                row.Detail = _names.Combo(_t, combo);
            }
            foreach (var row in ComboRows)
            {
                row.Title = _names.Combo(_t, row.Combo);
                row.Detail = _names.Sequence(_t.SequenceOf(row.Combo));
            }
            for (int i = 0; i < _comboIds.Count; i++) if (ComboChoices[i] != _names.Combo(_t, _comboIds[i])) ComboChoices[i] = _names.Combo(_t, _comboIds[i]);
        }

        // ── Species ──────────────────────────────────────────────────────────────────────────────

        private IntroRow _species;
        public IntroRow SelectedSpecies { get => _species; set { if (_species == value) return; _species = value; RaiseSpecies(); } }
        public bool HasSpecies => _species != null;

        public int SpeciesIndex
        {
            get => HasSpecies ? SpeciesOf(_species).Species : -1;
            set
            {
                if (_suppress || !HasSpecies || !SpeciesEditable || value < 0 || value == SpeciesIndex) return;
                _t.SetSpeciesRow(_species.Class, value, SpeciesOf(_species).Combo);
                Edited();
            }
        }

        public int SpeciesComboIndex
        {
            get => HasSpecies ? _comboIds.IndexOf(SpeciesOf(_species).Combo) : -1;
            set
            {
                if (_suppress || !HasSpecies || !SpeciesEditable || value < 0 || value >= _comboIds.Count || value == SpeciesComboIndex) return;
                _t.SetSpeciesRow(_species.Class, SpeciesOf(_species).Species, _comboIds[value]);
                Edited();
            }
        }

        private void RaiseSpecies()
        {
            _suppress = true;
            try { Raise(nameof(HasSpecies), nameof(SpeciesIndex), nameof(SpeciesComboIndex)); }
            finally { _suppress = false; }
        }

        // ── Combos ───────────────────────────────────────────────────────────────────────────────

        private IntroRow _combo;
        public IntroRow SelectedCombo { get => _combo; set { if (_combo == value) return; _combo = value; RaiseCombo(); } }
        private int Combo => _combo?.Combo ?? -1;
        public bool HasCombo => Combo >= 0;
        public string ComboTitle => HasCombo ? _names.Combo(_t, Combo) : "";
        public string ComboKind => HasCombo ? VsIntroNames.Kind(_t, _t.EffectOf(Combo)) : "";
        public bool MusicEditable => HasCombo && _t.OwnsMusic(Combo);
        public bool MusicElsewhere => HasCombo && !_t.OwnsMusic(Combo);
        public int SequenceIndex
        {
            get => HasCombo ? _sequenceIds.IndexOf(_t.SequenceOf(Combo)) : -1;
            set
            {
                if (_suppress || !MusicEditable || value < 0 || value >= _sequenceIds.Count || value == SequenceIndex) return;
                _t.SetSequence(Combo, _sequenceIds[value]);
                Edited();
            }
        }
        public string ComboSpecies
        {
            get
            {
                if (!HasCombo) return "";
                var list = _t.SpeciesUsing(Combo).Select(_names.SpeciesName).ToList();
                return list.Count == 0 ? "" : "Used by " + string.Join(", ", list) + ".";
            }
        }

        private void RaiseCombo()
        {
            _suppress = true;
            try { Raise(nameof(HasCombo), nameof(ComboTitle), nameof(ComboKind), nameof(MusicEditable), nameof(MusicElsewhere), nameof(SequenceIndex), nameof(ComboSpecies)); }
            finally { _suppress = false; }
        }

        // ── Editing, undo and saving ─────────────────────────────────────────────────────────────

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(_t.Snapshot, s => { _t.Restore(s); Refresh(); }, () => Raise(nameof(CanUndo), nameof(CanRedo)));
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void Edited() { _undo?.Record(); Refresh(); }

        private void Refresh()
        {
            Rename();
            RaiseSpecies();
            RaiseCombo();
            Raise(nameof(HasUnsavedChanges));
        }

        public bool HasUnsavedChanges => _t?.HasChanges == true;
        public string UnsavedChangesDescription => "Wild Pokémon intros";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_t == null || !_t.HasChanges) return true;
            // hg-engine's music rows are source text, saved as one hg-engine write like the other editors.
            if (_t.FromSource)
            {
                var (saved, error) = await HgEngineSave.RunAsync(() => { _t.Save(); return null; });
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError("The wild Pokémon intros were not saved:\n" + error, Title);
                    return false;
                }
            }
            else
            {
                try { _t.Save(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    await DialogHelper.ShowError("The wild Pokémon intros were not saved:\n" + e.Message, Title);
                    return false;
                }
            }
            Refresh();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = "Saved. Save the ROM to keep the changes.";
            return true;
        }

        public void DiscardChanges()
        {
            if (_t == null) return;
            try { _t = VsIntroTables.Load(Part.Wild); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException)
            {
                _ = DialogHelper.ShowError("The saved wild Pokémon intros couldn't be read back:\n" + e.Message, Title);
                return;
            }
            int sp = SpeciesRows.IndexOf(_species), co = ComboRows.IndexOf(_combo);
            _species = null; _combo = null;
            Build();
            StartUndo();
            SelectedSpecies = sp >= 0 && sp < SpeciesRows.Count ? SpeciesRows[sp] : SpeciesRows.FirstOrDefault();
            SelectedCombo = co >= 0 && co < ComboRows.Count ? ComboRows[co] : ComboRows.FirstOrDefault();
            Raise(nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo));
        }
    }
}
