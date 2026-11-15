using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public sealed class HeadbuttEncRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public string Name { get; }
        public ObservableCollection<string> Species { get; }
        private readonly HeadbuttEncounter _e;
        private readonly Action _changed;
        public HeadbuttEncRow(string name, HeadbuttEncounter e, ObservableCollection<string> species, Action changed)
        { Name = name; _e = e; Species = species; _changed = changed; }
        // Out-of-range values show clamped so the boxes never coerce, and stay until another is picked.
        private const int MaxShownLevel = 100;
        // hg-engine keeps a form in the top bits of the species word, as Safari does.
        private const int FormShift = DSPRE.HgEngine.HgEngineTrainerSource.FormShift;
        private int BaseSpecies => _e.PokemonID & DSPRE.HgEngine.HgEngineTrainerSource.SpeciesMask;
        public int SpeciesIndex { get => BaseSpecies < Species.Count ? BaseSpecies : -1; set { if (value < 0 || value == SpeciesIndex) return; _e.PokemonID = (ushort)(value | ((_e.PokemonID >> FormShift) << FormShift)); On(nameof(SpeciesIndex)); _changed(); } }
        public decimal Form { get => _e.PokemonID >> FormShift; set { if ((int)value == (int)Form) return; _e.PokemonID = (ushort)(BaseSpecies | (Math.Clamp((int)value, 0, 31) << FormShift)); On(nameof(Form)); _changed(); } }
        public bool ShowForms => DSPRE.HgEngine.HgEngineProject.IsActive;
        internal void Reselect() => On(nameof(SpeciesIndex));
        public decimal MinLevel { get => Math.Min((int)_e.MinLevel, MaxShownLevel); set { if (value == MinLevel) return; _e.MinLevel = (byte)value; On(nameof(MinLevel)); _changed(); } }
        public decimal MaxLevel { get => Math.Min((int)_e.MaxLevel, MaxShownLevel); set { if (value == MaxLevel) return; _e.MaxLevel = (byte)value; On(nameof(MaxLevel)); _changed(); } }
    }

    public sealed class HeadbuttTreeRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public string Name { get; }
        private readonly HeadbuttTree _t;
        private readonly Action _changed;
        public HeadbuttTreeRow(string name, HeadbuttTree t, Action changed) { Name = name; _t = t; _changed = changed; }
        internal HeadbuttTree Tree => _t;
        public decimal GlobalX { get => _t.GlobalX; set { if (_t.GlobalX == value) return; _t.GlobalX = (ushort)value; OnAll(); } }
        public decimal GlobalY { get => _t.GlobalY; set { if (_t.GlobalY == value) return; _t.GlobalY = (ushort)value; OnAll(); } }
        // Matrix cell and in-map tile (globalX = matrixX*32 + mapX), same as the 3D map view.
        public decimal MatrixX { get => _t.MatrixX; set { if (_t.MatrixX == value) return; _t.MatrixX = (ushort)value; OnAll(); } }
        public decimal MatrixY { get => _t.MatrixY; set { if (_t.MatrixY == value) return; _t.MatrixY = (ushort)value; OnAll(); } }
        public decimal MapX { get => _t.MapX; set { if (_t.MapX == value) return; _t.MapX = (ushort)value; OnAll(); } }
        public decimal MapY { get => _t.MapY; set { if (_t.MapY == value) return; _t.MapY = (ushort)value; OnAll(); } }
        public void RaiseAll() => OnAll();
        private void OnAll()
        {
            On(nameof(GlobalX)); On(nameof(GlobalY)); On(nameof(MatrixX)); On(nameof(MatrixY)); On(nameof(MapX)); On(nameof(MapY));
            _changed();
        }
    }
}
