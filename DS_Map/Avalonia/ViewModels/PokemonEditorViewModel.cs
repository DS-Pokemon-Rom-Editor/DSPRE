using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Top-level ViewModel for the unified Pokémon editor window.
    /// Owns Personal Data, Learnset, and Evolutions sub-ViewModels and keeps them in sync.
    /// </summary>
    public class PokemonEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }
        // ─── Shared lists ─────────────────────────────────────────────────────────
        public ObservableCollection<string> PokemonNames { get; } = new();

        // ─── Sub-ViewModels ───────────────────────────────────────────────────────
        public PersonalDataEditorViewModel  PersonalVM  { get; }
        public LearnsetEditorViewModel      LearnsetVM  { get; }
        public EvolutionsEditorViewModel    EvolutionsVM { get; }
        public PokemonSpriteEditorViewModel SpriteVM    { get; }
        public BattleDisplayEditorViewModel BattleDisplayVM { get; }

        // ─── Shared header
        private Bitmap _monIconBitmap;
        public  Bitmap MonIconBitmap
        {
            get => _monIconBitmap;
            private set => Set(ref _monIconBitmap, value);
        }

        private string _baseTitle = "Pokémon Editor";
        public string Title => (HasUnsavedChanges ? "● " : "") + _baseTitle;
        private void SetBaseTitle(string t) { _baseTitle = t; OnPropertyChanged(nameof(Title)); }

        // ─── Pokémon selector (shared) ────────────────────────────────────────────
        public int MaxMonIndex => PokemonNames.Count > 0 ? PokemonNames.Count - 1 : 0;

        private int _selectedMonIndex = 1;
        public int SelectedMonIndex
        {
            get => _selectedMonIndex;
            set
            {
                if (value == _selectedMonIndex || value < 0 || value >= PokemonNames.Count) return;
                if (HasUnsavedChanges) { _ = ConfirmDiscardAsync(value); return; }
                _selectedMonIndex = value;
                OnPropertyChanged();
                LoadMon(value);
            }
        }

        // ─── Dirty (delegates to all sub-VMs) ────────────────────────────────────
        public bool HasUnsavedChanges =>
            PersonalVM.HasUnsavedChanges ||
            LearnsetVM.HasUnsavedChanges ||
            EvolutionsVM.HasUnsavedChanges ||
            SpriteVM.HasUnsavedChanges ||
            BattleDisplayVM.HasUnsavedChanges;

        public string UnsavedChangesDescription =>
            $"Pokémon Editor (#{_selectedMonIndex} {(PokemonNames.Count > _selectedMonIndex ? PokemonNames[_selectedMonIndex] : "")})";

        public void SaveChanges() => SaveAll();
        public void DiscardChanges()
        {
            PersonalVM.DiscardChanges();
            LearnsetVM.DiscardChanges();
            EvolutionsVM.DiscardChanges();
            SpriteVM.DiscardChanges();
            BattleDisplayVM.DiscardChanges();
        }

        // ─── Undo / redo (routes to the visible tab) ──────────────────────────────
        // Tab order in the view: 0 = Personal Data, 1 = Learnset, 2 = Evolutions, 3 = Sprites.
        // Only the tabs whose sub-VM implements ISupportsUndo participate; the rest report nothing.
        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { if (Set(ref _selectedTabIndex, value)) RaiseUndoState(); }
        }

        private ISupportsUndo ActiveUndo => _selectedTabIndex switch
        {
            0 => PersonalVM,
            2 => EvolutionsVM,
            _ => null,
        };
        public bool CanUndo => ActiveUndo?.CanUndo ?? false;
        public bool CanRedo => ActiveUndo?.CanRedo ?? false;
        public void Undo() => ActiveUndo?.Undo();
        public void Redo() => ActiveUndo?.Redo();
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        // ─── Design-time constructor ──────────────────────────────────────────────
        public PokemonEditorViewModel()
        {
            if (!Design.IsDesignMode) return;

            _baseTitle = "Pokémon Editor (Preview)";
            for (int i = 0; i < 10; i++) PokemonNames.Add($"{i:D3} Pokémon {i}");

            PersonalVM   = new PersonalDataEditorViewModel();
            LearnsetVM   = new LearnsetEditorViewModel();
            EvolutionsVM = new EvolutionsEditorViewModel();
            SpriteVM     = new PokemonSpriteEditorViewModel();
            BattleDisplayVM = new BattleDisplayEditorViewModel();
        }

        // ─── Runtime constructor ──────────────────────────────────────────────────
        public PokemonEditorViewModel(string[] pokemonNames, string[] moveNames, int initialMon)
        {
            for (int i = 0; i < pokemonNames.Length; i++) PokemonNames.Add($"{i:D3} {pokemonNames[i]}");

            PersonalVM   = new PersonalDataEditorViewModel(pokemonNames);
            LearnsetVM   = new LearnsetEditorViewModel(moveNames);
            EvolutionsVM = new EvolutionsEditorViewModel(pokemonNames);
            SpriteVM     = new PokemonSpriteEditorViewModel(true);
            BattleDisplayVM = new BattleDisplayEditorViewModel(SpriteVM);

            // Propagate dirty change notifications so the window title can reflect unsaved state
            void OnChildDirty() { OnPropertyChanged(nameof(HasUnsavedChanges)); OnPropertyChanged(nameof(Title)); }
            PersonalVM.PropertyChanged   += (_, e) => { if (e.PropertyName == nameof(PersonalVM.HasUnsavedChanges))    OnChildDirty(); };
            LearnsetVM.PropertyChanged   += (_, e) => { if (e.PropertyName == nameof(LearnsetVM.HasUnsavedChanges))    OnChildDirty(); };
            EvolutionsVM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(EvolutionsVM.HasUnsavedChanges)) OnChildDirty(); };
            SpriteVM.PropertyChanged     += (_, e) => { if (e.PropertyName == nameof(SpriteVM.HasUnsavedChanges))     OnChildDirty(); };
            BattleDisplayVM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(BattleDisplayVM.HasUnsavedChanges)) OnChildDirty(); };

            // Bubble the active tab's undo availability up to the window toolbar / Ctrl+Z.
            void OnChildUndoState(object _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ISupportsUndo.CanUndo) || e.PropertyName == nameof(ISupportsUndo.CanRedo))
                    RaiseUndoState();
            }
            PersonalVM.PropertyChanged   += OnChildUndoState;
            EvolutionsVM.PropertyChanged += OnChildUndoState;

            _selectedMonIndex = initialMon;
            LoadMon(initialMon);
        }

        // ─── Load (sync all sub-VMs) ──────────────────────────────────────────────
        private void LoadMon(int id)
        {
            // Update icon
            try
            {
                var gdiImg = id > 0 ? DSPRE.DSUtils.GetPokePic(id, 40, 40) : null;
                MonIconBitmap = gdiImg != null ? ImageConverter.ToAvaloniaBitmap(gdiImg) : null;
            }
            catch { MonIconBitmap = null; }

            // Update title
            string name = (id >= 0 && id < PokemonNames.Count) ? PokemonNames[id] : $"#{id}";
            SetBaseTitle($"Pokémon Editor — #{id} {name}");

            // Sync all child VMs
            PersonalVM.LoadMon(id);
            LearnsetVM.LoadMon(id);
            EvolutionsVM.LoadMon(id);
            SpriteVM.LoadMon(id);
            BattleDisplayVM.LoadMon(id);
        }

        /// <summary>Releases app-wide event subscriptions held by child VMs; call when the window closes.</summary>
        public void Detach() { EvolutionsVM?.Detach(); PersonalVM?.Detach(); }

        // ─── Save all ─────────────────────────────────────────────────────────────
        public void SaveAll()
        {
            if (PersonalVM.HasUnsavedChanges)   ((IEditorWithUnsavedChanges)PersonalVM).SaveChanges();
            if (LearnsetVM.HasUnsavedChanges)   LearnsetVM.SaveChanges();
            if (EvolutionsVM.HasUnsavedChanges) EvolutionsVM.SaveChanges();
            if (BattleDisplayVM.HasUnsavedChanges) BattleDisplayVM.SaveChanges();
            // Sprites are saved on import (written back to NARC immediately); nothing extra to do here.
        }

        private async System.Threading.Tasks.Task ConfirmDiscardAsync(int pendingIndex)
        {
            var yes = await DialogHelper.AskYesNo(
                "There are unsaved changes. Switch Pokémon and discard them?",
                "Unsaved Changes");
            if (!yes) return;
            DiscardChanges();
            _selectedMonIndex = pendingIndex;
            OnPropertyChanged(nameof(SelectedMonIndex));
            LoadMon(pendingIndex);
        }
    }
}
