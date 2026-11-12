using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        // One a/1/6/9 record per form. Every form is staged, so undo and Discard cover them all.
        private string _athlonWhyNot;
        private int _athlonSpecies;
        private byte[][] _athlon, _athlonLoaded;

        public bool ShowPokeathlon => RomInfo.GameFamily == GameFamilies.HGSS && !HgEngineProject.IsActive && BetaEditors.Enabled;
        public bool PokeathlonEditable => ShowPokeathlon && _athlonWhyNot == null && _athlon != null && _athlon.Length > 0;
        public string PokeathlonNote => !ShowPokeathlon ? "" : _athlonWhyNot ?? (_athlon == null || _athlon.Length == 0 ? "This Pokémon has no Pokéathlon stats." : "");
        public bool HasPokeathlonNote => PokeathlonNote.Length > 0;

        public ObservableCollection<string> PokeathlonFormNames { get; } = new();
        public bool HasPokeathlonFormChoice => PokeathlonFormNames.Count > 1;
        public ObservableCollection<PokeathlonStatRow> PokeathlonStats { get; } = new();
        public int[] StarOptions { get; } = { 0, 1, 2, 3, 4 };
        public string[] AthlonSizeOptions { get; } = { "Circle 1", "Circle 2", "Circle 3" };
        public string[] AthlonLiftOptions { get; } = { "8 px (1)", "8 px (2)", "16 px" };
        public string[] AthlonOffsetOptions { get; } = { "3 px", "4 px", "5 px" };

        public bool AthlonRaised
        {
            get => AthlonRaisedIndex == 1;
            set => AthlonRaisedIndex = value ? 1 : 0;
        }

        private int _athlonFormIndex;
        public int PokeathlonFormIndex
        {
            get => _athlonFormIndex;
            set { if (_athlon != null && value >= 0 && value < _athlon.Length && Set(ref _athlonFormIndex, value)) ShowPokeathlonForm(); }
        }

        private PokeathlonPerformance AthlonRecord =>
            _athlon != null && _athlonFormIndex < _athlon.Length
                ? new PokeathlonPerformance(PokeathlonPerformance.MemberOf(_athlonSpecies, _athlonFormIndex), _athlon[_athlonFormIndex]) : null;

        private void StageAthlon(Action<PokeathlonPerformance> change)
        {
            PokeathlonPerformance record = AthlonRecord;
            if (_loading || record == null) return;
            change(record);
            byte[] next = record.ToBytes();
            if (next.AsSpan().SequenceEqual(_athlon[_athlonFormIndex])) return;
            _athlon[_athlonFormIndex] = next;
            ShowPokeathlonForm();
            SetDirty();
        }

        public int AthlonRaisedIndex
        {
            get => AthlonRecord == null ? -1 : AthlonRecord.Raised == 0 ? 0 : 1;
            set { if (value is >= 0 and <= 1 && value != AthlonRaisedIndex) StageAthlon(r => r.Raised = (byte)value); }
        }
        public int AthlonHitboxIndex
        {
            get => (AthlonRecord?.Hitbox ?? 0) - 1;
            set { if (value is >= 0 and <= 2) StageAthlon(r => r.Hitbox = (byte)(value + 1)); }
        }
        public int AthlonLiftIndex
        {
            get => (AthlonRecord?.Lift ?? 0) - 1;
            set { if (value is >= 0 and <= 2) StageAthlon(r => r.Lift = (byte)(value + 1)); }
        }
        public int AthlonOffsetIndex
        {
            get => (AthlonRecord?.Offset ?? 0) - 1;
            set { if (value is >= 0 and <= 2) StageAthlon(r => r.Offset = (byte)(value + 1)); }
        }

        public string PokeathlonProblem => AthlonRecord?.Problem() ?? "";
        public bool HasPokeathlonProblem => PokeathlonProblem.Length > 0;

        /// <summary>One stat's base, minimum and maximum as star picker indices.</summary>
        public class PokeathlonStatRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly PersonalDataEditorViewModel _owner;
            private readonly int _stat;

            public PokeathlonStatRow(PersonalDataEditorViewModel owner, int stat) { _owner = owner; _stat = stat; }

            public string Name => PokeathlonPerformance.Stats[_stat];
            public int[] StarOptions => _owner.StarOptions;

            public int BaseIndex
            {
                get => _owner.AthlonRecord?.Base(_stat) ?? -1;
                set { if (value is >= 0 and <= PokeathlonPerformance.HighestStar) _owner.StageAthlon(r => r.SetBase(_stat, (byte)value)); }
            }
            public int MinIndex
            {
                get => _owner.AthlonRecord?.Min(_stat) ?? -1;
                set { if (value is >= 0 and <= PokeathlonPerformance.HighestStar) _owner.StageAthlon(r => r.SetMin(_stat, (byte)value)); }
            }
            public int MaxIndex
            {
                get => _owner.AthlonRecord?.Max(_stat) ?? -1;
                set { if (value is >= 0 and <= PokeathlonPerformance.HighestStar) _owner.StageAthlon(r => r.SetMax(_stat, (byte)value)); }
            }

            public void Refresh()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BaseIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxIndex)));
            }
        }

        private void ShowPokeathlonForm()
        {
            foreach (PokeathlonStatRow row in PokeathlonStats) row.Refresh();
            foreach (string name in new[] { nameof(PokeathlonFormIndex), nameof(AthlonRaisedIndex), nameof(AthlonRaised), nameof(AthlonHitboxIndex), nameof(AthlonLiftIndex),
                                         nameof(AthlonOffsetIndex), nameof(PokeathlonProblem), nameof(HasPokeathlonProblem),
                                         nameof(PokeathlonEditable), nameof(PokeathlonNote), nameof(HasPokeathlonNote),
                                         nameof(HasPokeathlonFormChoice) })
                OnPropertyChanged(name);
        }

        // Form personal files follow the name list in personalExtraFiles order.
        internal static (int species, int form) AthlonSpeciesOf(int personalId)
        {
            if (personalId >= 1 && personalId <= PokeathlonPerformance.LastSpecies) return (personalId, 0);
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = DSPRE.Resources.PokeDatabase.PersonalData.ExtraFiles;
            int k = personalId - GetPokemonNames().Length;
            if (k < 0 || k >= extras.Length) return (0, 0);
            int form = Array.IndexOf(PokeathlonPerformance.FormNamesOf(extras[k].MonId), extras[k].Description);
            return form < 0 ? (0, 0) : (extras[k].MonId, form);
        }

        private void LoadPokeathlon()
        {
            _athlon = _athlonLoaded = null;
            _athlonWhyNot = null;
            PokeathlonFormNames.Clear();
            _athlonFormIndex = 0;
            if (PokeathlonStats.Count == 0)
                for (int s = 0; s < PokeathlonPerformance.StatCount; s++) PokeathlonStats.Add(new PokeathlonStatRow(this, s));

            if (ShowPokeathlon)
            {
                (int species, int form) = AthlonSpeciesOf(_currentId);
                _athlonSpecies = species;
                try
                {
                    _athlonWhyNot = PokeathlonPerformance.WhyNot();
                    if (_athlonWhyNot == null && species > 0)
                    {
                        string[] names = PokeathlonPerformance.FormNamesOf(species);
                        List<byte[]> records = new List<byte[]>();
                        for (int f = 0; f < PokeathlonPerformance.FormsOf(species); f++)
                        {
                            PokeathlonPerformance record = PokeathlonPerformance.Read(PokeathlonPerformance.MemberOf(species, f));
                            if (record == null) { _athlonWhyNot = $"Pokéathlon record {PokeathlonPerformance.MemberOf(species, f)} is missing."; break; }
                            records.Add(record.ToBytes());
                            PokeathlonFormNames.Add(f < names.Length ? names[f] : $"Form {f}");
                        }
                        if (_athlonWhyNot == null)
                        {
                            _athlon = records.ToArray();
                            _athlonLoaded = records.Select(r => (byte[])r.Clone()).ToArray();
                            _athlonFormIndex = Math.Min(form, _athlon.Length - 1);
                        }
                        else PokeathlonFormNames.Clear();
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
                {
                    _athlonWhyNot = e.Message;
                    PokeathlonFormNames.Clear();
                }
            }
            ShowPokeathlonForm();
        }

        private string PokeathlonSaveProblem()
        {
            if (_athlon == null) return null;
            for (int f = 0; f < _athlon.Length; f++)
            {
                string problem = new PokeathlonPerformance(0, _athlon[f]).Problem();
                if (problem != null) return PokeathlonFormNames.Count > 1 ? $"{PokeathlonFormNames[f]}: {problem}" : problem;
            }
            return null;
        }

        private string SavePokeathlon()
        {
            if (_athlon == null || _athlonLoaded == null) return null;
            try
            {
                for (int f = 0; f < _athlon.Length; f++)
                {
                    if (_athlon[f].AsSpan().SequenceEqual(_athlonLoaded[f])) continue;
                    new PokeathlonPerformance(PokeathlonPerformance.MemberOf(_athlonSpecies, f), _athlon[f]).Write();
                    _athlonLoaded[f] = (byte[])_athlon[f].Clone();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
            {
                return e.Message;
            }
            return null;
        }
    }
}
