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

namespace DSPRE.Avalonia.ViewModels.Pokemon
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
        // The pending species' list entry has no data to load, so the number box stops before it.
        public int MaxMonIndex => System.Math.Max(0, PokemonNames.Count - 1 - (_pendingSpecies != null ? 1 : 0));

        private int _selectedMonIndex = 1;
        /// <summary>What putting a cry in actually does, and what sort of file it takes. </summary>
        public string CryImportHelp =>
            "Put a WAV in as this Pokémon's cry.\n\n"
            + DSPRE.Avalonia.Data.SoundArchive.HowItWorks + "\n\n"
            + DSPRE.Avalonia.Data.CryFiles.AcceptedFormat + "\n\n"
            + "It is squeezed down the way the games squeeze their own cries, so it takes about the same "
            + "room rather than making the sound file bigger. That costs a little detail, so put a cry in "
            + "once from your own source rather than exporting and importing the same one over and over.\n\n"
            + "The ROM's sound file is written straight away.";

        public int SelectedMonIndex
        {
            get => _selectedMonIndex;
            set
            {
                if (value == _selectedMonIndex || value < 0 || value >= PokemonNames.Count) return;
                if (_pendingSpecies != null && value == _pendingListIndex)
                {
                    // Put the selector back once the control has finished applying the pick.
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => OnPropertyChanged(nameof(SelectedMonIndex)),
                        global::Avalonia.Threading.DispatcherPriority.Background);
                    return;
                }
                if (HasUnsavedChanges) { _ = ConfirmDiscardAsync(value); return; }
                _selectedMonIndex = value;
                OnPropertyChanged();
                LoadMon(value);
            }
        }

        // ─── Dirty (delegates to all sub-VMs) ────────────────────────────────────
        public bool HasUnsavedChanges =>
            _pendingSpecies != null ||
            PersonalVM.HasUnsavedChanges ||
            LearnsetVM.HasUnsavedChanges ||
            EvolutionsVM.HasUnsavedChanges ||
            SpriteVM.HasUnsavedChanges ||
            BattleDisplayVM.HasUnsavedChanges;

        public string UnsavedChangesDescription =>
            $"Pokémon Editor (#{_selectedMonIndex} {(PokemonNames.Count > _selectedMonIndex ? PokemonNames[_selectedMonIndex] : "")}"
            + (_pendingSpecies != null ? $", new Pokémon {_pendingSpecies.DisplayName}" : "") + ")";

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
            DropPendingSpecies();
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

            // Picking a form in the Sprites tab that has its own main-list entry (e.g. Deoxys - Attack) should
            // move the main selector there too, so Personal Data/Learnset/Evolutions and Save all agree on
            // which form is actually loaded instead of quietly staying on the base species.
            SpriteVM.FormPseudoIdSelected += pseudoId => { if (pseudoId != _selectedMonIndex) SelectedMonIndex = pseudoId; };

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
                    var gdiImg = DSPRE.DSUtils.GetPokePicRaw(DSPRE.DSUtils.ResolveIconId(id), 40, 40);
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
            // A new species' save can ask about source comments, which needs the async path.
            if (_pendingSpecies != null) { _ = SaveAllAsync(); return; }
            if (PersonalVM.HasUnsavedChanges)   ((IEditorWithUnsavedChanges)PersonalVM).SaveChanges();
            if (LearnsetVM.HasUnsavedChanges)   LearnsetVM.SaveChanges();
            if (EvolutionsVM.HasUnsavedChanges) EvolutionsVM.SaveChanges();
            if (BattleDisplayVM.HasUnsavedChanges) BattleDisplayVM.SaveChanges();
            if (SpriteVM.HasUnsavedChanges) SpriteVM.SaveChanges();
            // Announced after the children so the one visible notice names the whole save.
            if (!HasUnsavedChanges) SaveNotice.Saved(UnsavedChangesDescription);
        }

        public async Task<bool> SaveAllAsync()
        {
            // The tabs save before a new species is added: adding one moves every form after it up an id.
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
            if (SpriteVM.HasUnsavedChanges)
            {
                SpriteVM.SaveChanges();
                if (SpriteVM.HasUnsavedChanges) return false;
            }

            int added = -1;
            if (_pendingSpecies != null && (added = await SavePendingSpeciesAsync()) < 0) return false;

            SaveNotice.Saved(UnsavedChangesDescription);
            if (added >= 0 && added < PokemonNames.Count && !HasUnsavedChanges)
            {
                // The same index can now be a different species, so load it even when it is already selected.
                _selectedMonIndex = added;
                OnPropertyChanged(nameof(SelectedMonIndex));
                LoadMon(added);
            }
            return !HasUnsavedChanges;
        }

        // ─── New species (hg-engine) ──────────────────────────────────────────────
        private HgEngineSpeciesExpansion.FakemonPlan _pendingSpecies;
        private int _pendingListIndex = -1;

        public bool HasPendingSpecies => _pendingSpecies != null;
        public bool CanAddSpecies => _pendingSpecies == null;
        public string PendingSpeciesBanner => _pendingSpecies != null ? $"{_pendingSpecies.DisplayName} is created when you save." : null;

        /// <summary>hg-engine-only: adds a brand new base species ("fakemon"; a new form of an existing
        /// species is not supported here). Nothing is written until Save All; Discard drops it.</summary>
        public async Task AddNewFakemonAsync(Window owner)
        {
            if (!HgEngineProject.IsActive || _pendingSpecies != null) return;
            string name = await DialogHelper.PromptText("New species' display name:", "Add New Pokémon", owner: owner);
            if (name == null || _pendingSpecies != null) return;

            // Checked now so a name the checkout can't take is refused before it looks added.
            if (!HgEngineSpeciesExpansion.TryPlanFakemon(name, out var plan, out string error))
            {
                await DialogHelper.ShowError($"Could not add the species:\n{error}", "Add New Pokémon", owner);
                return;
            }

            _pendingSpecies = plan;
            _pendingListIndex = PokemonNames.Count;
            PokemonNames.Add($"{plan.SpeciesId:D3} {plan.DisplayName} (not saved)");
            RaisePendingSpecies();
        }

        private void DropPendingSpecies()
        {
            if (_pendingSpecies == null) return;
            if (_pendingListIndex >= 0 && _pendingListIndex < PokemonNames.Count) PokemonNames.RemoveAt(_pendingListIndex);
            _pendingSpecies = null;
            _pendingListIndex = -1;
            RaisePendingSpecies();
        }

        private void RaisePendingSpecies()
        {
            OnPropertyChanged(nameof(HasPendingSpecies));
            OnPropertyChanged(nameof(CanAddSpecies));
            OnPropertyChanged(nameof(PendingSpeciesBanner));
            OnPropertyChanged(nameof(MaxMonIndex));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(Title));
        }

        /// <summary>Writes the pending species as one source save, then brings names and unpacked data up to
        /// date. Returns its id, or -1 when it was not written and is still pending.</summary>
        private async Task<int> SavePendingSpeciesAsync()
        {
            string name = _pendingSpecies.DisplayName;
            HgEngineSpeciesExpansion.FakemonPlan written = null;
            var (saved, error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
                HgEngineSpeciesExpansion.TryWriteFakemon(name, out written, out string writeError) ? null : writeError);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowError($"Could not add the species:\n{error}", "Add New Pokémon", _owner);
                return -1;
            }

            HgEngineSpeciesExpansion.FinishFakemon(written);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> {
                RomInfo.DirNames.personalPokeData, RomInfo.DirNames.learnsets, RomInfo.DirNames.evolutions });
            DropPendingSpecies();

            // In-place update (ListSync), not Clear+Add: Clear briefly empties the collection, which
            // resets the FusionAutoCompleteBox's bound SelectedIndex out from under the selection.
            string[] refreshed = RomInfo.GetPokemonNames();
            var decorated = new string[refreshed.Length];
            for (int i = 0; i < refreshed.Length; i++) decorated[i] = $"{i:D3} {refreshed[i]}";
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, decorated);
            OnPropertyChanged(nameof(MaxMonIndex));
            AppEvents.RaiseNamesChanged();

            return written.SpeciesId;
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
