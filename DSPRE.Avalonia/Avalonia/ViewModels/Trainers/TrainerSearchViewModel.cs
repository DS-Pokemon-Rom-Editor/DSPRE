using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public class TrainerSearchResult
    {
        public int Index { get; }
        public string Display { get; }
        public TrainerSearchResult(int index, string display) { Index = index; Display = display; }
        public override string ToString() => Display;
    }

    /// <summary>
    /// Finds trainers by name, class, AI flags, battle type, party, held item and custom moves.
    /// </summary>
    public class TrainerSearchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); if (_autoSearch) Search(); return true; }

        /// <summary>What a trainer is, read once when the search opens.</summary>
        private sealed record Facts(int Class, int PartySize, bool Double, bool CustomMoves, int AiBits, int[] Species, int[] Items);

        private string[] _names = Array.Empty<string>();
        private Facts[] _facts;

        public ObservableCollection<string> Operators { get; } = new ObservableCollection<string> { "Contains", "Does not contain", "Is exactly", "Is not" };
        public ObservableCollection<TrainerSearchResult> Results { get; } = new ObservableCollection<TrainerSearchResult>();
        public List<string> ClassChoices { get; } = new List<string> { "Any class" };
        public List<string> AiChoices { get; } = new List<string> { "Any AI" };
        public List<string> SpeciesChoices { get; } = new List<string> { "Any Pokémon" };
        public List<string> ItemChoices { get; } = new List<string> { "Any held item" };
        public List<string> BattleChoices { get; } = new List<string> { "Single or double", "Single", "Double" };
        public List<string> MovesChoices { get; } = new List<string> { "Any moves", "Custom moves", "Level-up moves" };

        private int _operatorIndex; public int OperatorIndex { get => _operatorIndex; set => Set(ref _operatorIndex, value); }
        private string _searchText = ""; public string SearchText { get => _searchText; set => Set(ref _searchText, value ?? ""); }
        private bool _caseSensitive; public bool CaseSensitive { get => _caseSensitive; set => Set(ref _caseSensitive, value); }
        private int _classIndex; public int ClassIndex { get => _classIndex; set => Set(ref _classIndex, value); }
        private int _aiIndex; public int AiIndex { get => _aiIndex; set => Set(ref _aiIndex, value); }
        private int _speciesIndex; public int SpeciesIndex { get => _speciesIndex; set => Set(ref _speciesIndex, value); }
        private int _itemIndex; public int ItemIndex { get => _itemIndex; set => Set(ref _itemIndex, value); }
        private int _battleIndex; public int BattleIndex { get => _battleIndex; set => Set(ref _battleIndex, value); }
        private int _movesIndex; public int MovesIndex { get => _movesIndex; set => Set(ref _movesIndex, value); }
        private decimal _minParty = 0; public decimal MinParty { get => _minParty; set => Set(ref _minParty, value); }
        private decimal _maxParty = 6; public decimal MaxParty { get => _maxParty; set => Set(ref _maxParty, value); }

        private bool _autoSearch = true;
        public bool AutoSearch { get => _autoSearch; set { _autoSearch = value; OnPropertyChanged(); } }

        /// <summary>The filters beyond the name; they need every trainer read, which hg-engine projects keep in source.</summary>
        public bool HasFacts => _facts != null;
        private string _status = "";
        public string StatusText { get => _status; private set { _status = value; OnPropertyChanged(); } }

        private TrainerSearchResult _selectedResult;
        public TrainerSearchResult SelectedResult { get => _selectedResult; set { _selectedResult = value; OnPropertyChanged(); } }

        public int ResultIndex { get; private set; } = -1;
        public bool Confirmed { get; private set; }

        public TrainerSearchViewModel() { }

        public TrainerSearchViewModel(IEnumerable<string> names, IEnumerable<string> classNames, IEnumerable<string> aiNames,
                                      IEnumerable<string> speciesNames, IEnumerable<string> itemNames)
        {
            _names = names.ToArray();
            ClassChoices.AddRange(classNames);
            AiChoices.AddRange(aiNames);
            SpeciesChoices.AddRange(speciesNames.Skip(1));
            ItemChoices.AddRange(itemNames.Skip(1));
            Reset();
        }

        /// <summary>Reads every trainer's class, party and flags for the filters.</summary>
        public async Task LoadFactsAsync()
        {
            if (HgEngine.HgEngineProject.IsActive) { StatusText = "hg-engine projects search by name only."; return; }
            StatusText = "Reading trainers…";
            int count = _names.Length;
            _facts = await Task.Run(() =>
            {
                Facts[] facts = new Facts[count];
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        TrainerFile t = TrainerRecords.Load(i);
                        List<int> species = new List<int>(), items = new List<int>();
                        for (int m = 0; m < t.trp.partyCount; m++)
                        {
                            PartyPokemon mon = t.party[m];
                            if (mon?.pokeID is ushort s) species.Add(s);
                            if (mon?.heldItem is ushort h) items.Add(h);
                        }
                        int ai = 0;
                        for (int b = 0; b < t.trp.AI.Length && b < 31; b++) if (t.trp.AI[b]) ai |= 1 << b;
                        facts[i] = new Facts(t.trp.trainerClass, t.trp.partyCount, t.trp.doubleBattle, t.trp.chooseMoves, ai, species.ToArray(), items.ToArray());
                    }
                    catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { facts[i] = null; }
                }
                return facts;
            });
            StatusText = "";
            OnPropertyChanged(nameof(HasFacts));
            Search();
        }

        public void Reset()
        {
            bool auto = _autoSearch;
            _autoSearch = false;
            SearchText = ""; OperatorIndex = 0; ClassIndex = 0; AiIndex = 0; SpeciesIndex = 0; ItemIndex = 0;
            BattleIndex = 0; MovesIndex = 0; MinParty = 0; MaxParty = 6;
            _autoSearch = auto;
            Search();
        }

        public void Search()
        {
            Results.Clear();
            Func<string, bool> contains = _caseSensitive
                ? s => s.Contains(_searchText, StringComparison.Ordinal)
                : s => SearchMatch.Contains(s, _searchText);
            Func<string, bool> equals = _caseSensitive
                ? s => s.Equals(_searchText, StringComparison.Ordinal)
                : s => SearchMatch.Fold(s) == SearchMatch.Fold(_searchText);
            for (int i = 0; i < _names.Length; i++)
            {
                string s = _names[i];
                bool nameOk = _searchText.Length == 0 || _operatorIndex switch
                {
                    0 => contains(s),
                    1 => !contains(s),
                    2 => equals(s),
                    3 => !equals(s),
                    _ => false,
                };
                if (nameOk && FactsMatch(i)) Results.Add(new TrainerSearchResult(i, s));
            }
            if (Results.Count > 0) SelectedResult = Results[0];
        }

        private bool FactsMatch(int i)
        {
            if (_facts == null) return true;
            Facts f = i < _facts.Length ? _facts[i] : null;
            if (f == null) return false;
            if (_classIndex > 0 && f.Class != _classIndex - 1) return false;
            if (_aiIndex > 0 && (f.AiBits & (1 << (_aiIndex - 1))) == 0) return false;
            if (_speciesIndex > 0 && !f.Species.Contains(_speciesIndex)) return false;
            if (_itemIndex > 0 && !f.Items.Contains(_itemIndex)) return false;
            if (_battleIndex == 1 && f.Double || _battleIndex == 2 && !f.Double) return false;
            if (_movesIndex == 1 && !f.CustomMoves || _movesIndex == 2 && f.CustomMoves) return false;
            return f.PartySize >= _minParty && f.PartySize <= _maxParty;
        }

        public bool GoTo()
        {
            if (_selectedResult == null) return false;
            ResultIndex = _selectedResult.Index;
            Confirmed = true;
            return true;
        }
    }
}
