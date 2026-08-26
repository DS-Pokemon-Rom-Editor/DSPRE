using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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
        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // ─── Shared lists ─────────────────────────────────────────────────────────
        public ObservableCollection<string> PokemonNames { get; } = new();

        private Window _owner;
        public void SetOwner(Window owner) => _owner = owner;

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
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
            => await SaveAllAsync();

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
            // Update icon. hg-engine doesn't keep icons in personal.narc at all; each species' icon is
            // a source PNG in the checkout (data/graphics/sprites/<name>/icon.png), so load that
            // directly rather than through the vanilla NCGR/NCLR/ARM9-palette-table pipeline, which
            // relies on a hardcoded byte offset that's meaningless against hg-engine's recompiled ARM9
            // (see HgEnginePokemonIcons for the full story).
            MonIconBitmap = null;
            if (id > 0 && HgEngineProject.IsActive && HgEnginePokemonIcons.TryGetIconPath(id, out string iconPath))
            {
                try { MonIconBitmap = ImageConverter.LoadHgeIconFirstFrame(iconPath); }
                catch { MonIconBitmap = null; }
            }
            else if (id > 0 && !HgEngineProject.IsActive)
            {
                try
                {
                    var gdiImg = DSPRE.DSUtils.GetPokePicRaw(id, 40, 40);
                    MonIconBitmap = gdiImg != null ? ImageConverter.ToAvaloniaBitmap(gdiImg) : null;
                }
                catch { MonIconBitmap = null; }
            }

            // Update title
            string name = (id >= 0 && id < PokemonNames.Count) ? PokemonNames[id] : $"#{id}";
            SetBaseTitle($"Pokémon Editor - #{id} {name}");

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
            // TODO: SpriteVM is NOT saved here. ImportSprite marks it dirty, but PokemonSpriteEditorViewModel.SaveChanges() is a no-op and nothing writes _replacementSprites back to the NARC, so a dirty sprite import makes SaveAll()/SaveAllAsync() report failure via !HasUnsavedChanges. Implementing sprite persistence (encoding the replacement PNG back into the battle-sprite NARC entry) is a separate feature; until then, the parent's save contract honestly surfaces the missing path rather than silently reporting success.
        }

        public async Task<bool> SaveAllAsync()
        {
            if (PersonalVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)PersonalVM).SaveChangesAsync())
                return false;
            if (LearnsetVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)LearnsetVM).SaveChangesAsync())
                return false;
            if (EvolutionsVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)EvolutionsVM).SaveChangesAsync())
                return false;
            if (BattleDisplayVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)BattleDisplayVM).SaveChangesAsync())
                return false;

            // TODO: SpriteVM is NOT saved here (see SaveAll). Its HasUnsavedChanges is included in this VM's dirty aggregation, so an unsaved sprite import makes SaveAllAsync return false via !HasUnsavedChanges.
            return !HasUnsavedChanges;
        }

        /// <summary>hg-engine-only: mints a brand new base species ("fakemon"; a new form of an
        /// existing species is a separate, much higher-risk operation not supported here yet) and jumps
        /// straight to editing it.</summary>
        public async Task AddNewFakemonAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            string name = await DialogHelper.PromptText("New species' display name:", "Add New Pokémon", owner: owner);
            if (name == null) return;

            if (!HgEngineSpeciesExpansion.TryAddFakemon(name, out int newSpeciesId, out string error))
            {
                await DialogHelper.ShowError($"Could not add the species:\n{error}", "Add New Pokémon", owner);
                return;
            }

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> {
                RomInfo.DirNames.personalPokeData, RomInfo.DirNames.learnsets, RomInfo.DirNames.evolutions });

            // In-place update (ListSync), not Clear+Add: Clear briefly empties the collection, which
            // resets the FusionAutoCompleteBox's bound SelectedIndex out from under the assignment below.
            string[] refreshed = RomInfo.GetPokemonNames();
            var decorated = new string[refreshed.Length];
            for (int i = 0; i < refreshed.Length; i++) decorated[i] = $"{i:D3} {refreshed[i]}";
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, decorated);
            OnPropertyChanged(nameof(MaxMonIndex));
            AppEvents.RaiseNamesChanged();

            SelectedMonIndex = newSpeciesId;
        }

        private async System.Threading.Tasks.Task ConfirmDiscardAsync(int pendingIndex)
        {
            var yes = await DialogHelper.AskYesNo(
                "There are unsaved changes. Switch Pokémon and discard them?",
                "Unsaved Changes", _owner);
            if (!yes) return;
            DiscardChanges();
            _selectedMonIndex = pendingIndex;
            OnPropertyChanged(nameof(SelectedMonIndex));
            LoadMon(pendingIndex);
        }
    }
}
