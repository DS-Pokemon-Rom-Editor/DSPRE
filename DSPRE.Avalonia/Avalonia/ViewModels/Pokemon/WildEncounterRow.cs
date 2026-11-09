using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public class WildEncounterRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly Func<int, IImage> _iconLoader;

        public WildEncounterRow() : this(Array.Empty<string>(), null) { }
        public WildEncounterRow(IEnumerable<string> pokemonNames, Func<int, IImage> iconLoader)
        {
            PokemonNames = pokemonNames ?? Array.Empty<string>();
            _iconLoader = iconLoader;
        }

        public IEnumerable<string> PokemonNames { get; }

        private string _label;
        public string Label { get => _label; set { if (_label == value) return; _label = value; OnPropertyChanged(); } }
        public string RateLabel { get; set; }

        private string _note, _noteTip;
        public string Note { get => _note; set { if (_note == value) return; _note = value; OnPropertyChanged(); } }
        public string NoteTip { get => _noteTip; set { if (_noteTip == value) return; _noteTip = value; OnPropertyChanged(); } }

        public sealed class Replacement
        {
            public IImage Icon { get; init; }
            public string Tip { get; init; }
        }

        private string _warning;
        /// <summary>Why this slot gives a glitch Pokémon when the game uses it, or null.</summary>
        public string Warning { get => _warning; set { if (_warning == value) return; _warning = value; OnPropertyChanged(); } }

        private IReadOnlyList<Replacement> _replacements = Array.Empty<Replacement>();
        public IReadOnlyList<Replacement> Replacements { get => _replacements; set { _replacements = value ?? Array.Empty<Replacement>(); OnPropertyChanged(); } }

        private int _pokemonIndex;
        public int PokemonIndex
        {
            get => _pokemonIndex;
            set
            {
                if (_pokemonIndex == value) return;
                _pokemonIndex = value;
                OnPropertyChanged();
                UpdateIcon();
            }
        }

        /// <summary>True on an hg-engine folder, whose species words also carry a form.</summary>
        public bool ShowForm { get; set; }

        private int _form;
        public int Form
        {
            get => _form;
            set { if (_form != value) { _form = value; OnPropertyChanged(); } }
        }

        /// <summary>The slot's stored value: the species, with hg-engine's form above it when the project has forms.</summary>
        public int PackedSpecies
        {
            get => ShowForm ? _pokemonIndex | (_form << DSPRE.HgEngine.HgEngineTrainerSource.FormShift) : _pokemonIndex;
            set
            {
                PokemonIndex = ShowForm ? value & DSPRE.HgEngine.HgEngineTrainerSource.SpeciesMask : value;
                Form = ShowForm ? value >> DSPRE.HgEngine.HgEngineTrainerSource.FormShift : 0;
            }
        }

        private IImage _pokemonIcon;
        public IImage PokemonIcon => _pokemonIcon;

        private void UpdateIcon()
        {
            IImage icon = _iconLoader?.Invoke(_pokemonIndex);
            if (ReferenceEquals(_pokemonIcon, icon)) return;
            _pokemonIcon = icon;
            OnPropertyChanged(nameof(PokemonIcon));
        }

        private int _level;
        public int Level
        {
            get => _level;
            set { if (_level != value) { _level = value; OnPropertyChanged(); } }
        }

        private int _minLevel;
        public int MinLevel
        {
            get => _minLevel;
            set { if (_minLevel != value) { _minLevel = value; OnPropertyChanged(); } }
        }

        private int _maxLevel;
        public int MaxLevel
        {
            get => _maxLevel;
            set { if (_maxLevel != value) { _maxLevel = value; OnPropertyChanged(); } }
        }
    }
}
