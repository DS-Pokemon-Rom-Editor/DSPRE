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
    public class PokemonEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
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
        public PokedexMetricsEditorViewModel PokedexVM  { get; }
        public PokemonSpriteEditorViewModel SpriteVM    { get; }
        public PokewalkerViewModel          PokewalkerVM { get; } = new();
        public bool ShowPokewalkerTab => PokewalkerViewModel.GameHasPokewalker;
        public BattleDisplayEditorViewModel BattleDisplayVM { get; }

        // ─── Shared header
        private Bitmap _monIconBitmap;
        public  Bitmap MonIconBitmap
        {
            get => _monIconBitmap;
            private set => Set(ref _monIconBitmap, value);
        }

        private string _baseTitle = "Pokémon editor";
        public string Title => _baseTitle;
        private void SetBaseTitle(string t) { _baseTitle = t; OnPropertyChanged(nameof(Title)); }

        // ─── Pokémon selector (shared) ────────────────────────────────────────────
        // The pending species' list entry has no data to load, so the number box stops before it.
        public int MaxMonIndex => System.Math.Max(0, PokemonNames.Count - 1 - (_pendingSpecies != null ? 1 : 0));

        private int _selectedMonIndex = 1;
        /// <summary>What importing a cry actually does, and what sort of file it takes.</summary>
        public string CryImportHelp =>
            "Import a WAV as this Pokémon's cry.\n\n"
            + DSPRE.Avalonia.Data.SoundArchive.HowItWorks + "\n\n"
            + DSPRE.Avalonia.Data.CryFiles.AcceptedFormat + "\n\n"
            + "It's compressed as the game compresses its cries, so the sound file stays about the same size. "
            + "That loses a little detail, so import from your own source each time instead of exporting and "
            + "importing the same cry again.\n\n"
            + "The ROM's sound file is written straight away.";

        public int SelectedMonIndex
        {
            get => _selectedMonIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack || value == _selectedMonIndex || value < 0 || value >= PokemonNames.Count) return;
                if (_pendingSpecies != null && value == _pendingListIndex)
                {
                    PutSelectorBack();
                    return;
                }
                if (HasRecordEdits) { _ = ConfirmDiscardAsync(value); return; }
                _selectedMonIndex = value;
                OnPropertyChanged();
                HoldImports();
                LoadMon(value);
            }
        }

        // ─── Dirty (delegates to all sub-VMs) ────────────────────────────────────
        public bool HasUnsavedChanges =>
            _pendingSpecies != null ||
            PersonalVM.HasUnsavedChanges ||
            LearnsetVM.HasUnsavedChanges ||
            EvolutionsVM.HasUnsavedChanges ||
            PokedexVM.HasUnsavedChanges ||
            SpriteVM.HasUnsavedChanges ||
            PokewalkerVM.HasUnsavedChanges ||
            BattleDisplayVM.HasUnsavedChanges;

        // The tabs with changes, each naming its own data, so the list says what Save would write.
        public string UnsavedChangesDescription
        {
            get
            {
                List<string> parts = new List<string>();
                if (_pendingSpecies != null) parts.Add($"New Pokémon {_pendingSpecies.DisplayName}");
                foreach (IEditorWithUnsavedChanges tab in new IEditorWithUnsavedChanges[] { PersonalVM, LearnsetVM, EvolutionsVM, PokedexVM, SpriteVM, PokewalkerVM, BattleDisplayVM })
                    if (tab.HasUnsavedChanges) parts.Add(tab.UnsavedChangesDescription);
                return parts.Count > 0 ? string.Join(", ", parts)
                    : (PokemonNames.Count > _selectedMonIndex && _selectedMonIndex >= 0 ? PokemonNames[_selectedMonIndex] : $"Pokémon {_selectedMonIndex}");
            }
        }

        public void SaveChanges() => SaveAll();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
            => await SaveAllAsync();

        /// <summary>Edits that switching Pokémon would lose. Imported Pokémon are held until Save instead.</summary>
        private bool HasRecordEdits =>
            _pendingSpecies != null ||
            PersonalVM.HasRecordEdits ||
            LearnsetVM.HasUnsavedChanges ||
            EvolutionsVM.HasRecordEdits ||
            PokedexVM.HasUnsavedChanges ||
            SpriteVM.HasUnsavedChanges ||
            PokewalkerVM.HasUnsavedChanges ||
            BattleDisplayVM.HasUnsavedChanges;

        private void HoldImports()
        {
            PersonalVM.HoldForSwitch();
            EvolutionsVM.HoldForSwitch();
        }

        /// <summary>What the switch prompt saves or discards: Save writes everything, Discard keeps the imports.</summary>
        private sealed class RecordEdits : IEditorWithUnsavedChanges
        {
            private readonly PokemonEditorViewModel _owner;
            public RecordEdits(PokemonEditorViewModel owner) { _owner = owner; }
            public bool HasUnsavedChanges => _owner.HasRecordEdits;
            public string UnsavedChangesDescription => _owner.UnsavedChangesDescription;
            public void SaveChanges() => _owner.SaveAll();
            public Task<bool> SaveChangesAsync() => _owner.SaveAllAsync();
            public void DiscardChanges() => _owner.DiscardChanges(keepImports: true);
        }

        public void DiscardChanges() => DiscardChanges(keepImports: false);

        private void DiscardChanges(bool keepImports)
        {
            // Keep the imports and only discard manual edits.
            if (keepImports) { HoldImports(); PersonalVM.DiscardRecordEdits(); } else PersonalVM.DiscardChanges();
            BattleDisplayVM.DiscardChanges();
            // These three only clear their flag on discard, so their edits are put back by reading the mon again.
            if (LearnsetVM.HasUnsavedChanges)   { LearnsetVM.DiscardChanges();   LearnsetVM.LoadMon(_selectedMonIndex); }
            if (keepImports) EvolutionsVM.DiscardRecordEdits();
            else if (EvolutionsVM.HasUnsavedChanges) { EvolutionsVM.DiscardChanges(); EvolutionsVM.LoadMon(_selectedMonIndex); }
            if (PokedexVM.HasUnsavedChanges)    PokedexVM.DiscardChanges();
            if (SpriteVM.HasUnsavedChanges)     { SpriteVM.DiscardChanges();     SpriteVM.LoadMon(_selectedMonIndex); }
            if (PokewalkerVM.HasUnsavedChanges) PokewalkerVM.DiscardChanges();
            DropPendingSpecies();
        }

        // ─── Undo / redo (routes to the visible tab) ──────────────────────────────
        // Tab order in the view: 0 = Personal Data, 1 = Learnset, 2 = Evolutions, 3 = Pokédex, 4 = Sprites, 5 = Pokéwalker,
        // 6 = Battle Display. The Pokéwalker tab is hidden outside HGSS but keeps its place.
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
            1 => LearnsetVM,
            2 => EvolutionsVM,
            3 => PokedexVM,
            4 => SpriteVM,
            5 => PokewalkerVM,
            6 => BattleDisplayVM as ISupportsUndo,
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

            _baseTitle = "Pokémon editor (Preview)";
            for (int i = 0; i < 10; i++) PokemonNames.Add($"{i:D3} Pokémon {i}");

            PersonalVM   = new PersonalDataEditorViewModel();
            LearnsetVM   = new LearnsetEditorViewModel();
            EvolutionsVM = new EvolutionsEditorViewModel();
            PokedexVM    = new PokedexMetricsEditorViewModel();
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
            PokedexVM    = new PokedexMetricsEditorViewModel();
            SpriteVM     = new PokemonSpriteEditorViewModel(true);
            BattleDisplayVM = new BattleDisplayEditorViewModel(SpriteVM);

            // Propagate dirty change notifications so the window title can reflect unsaved state
            void OnChildDirty() => OnPropertyChanged(nameof(HasUnsavedChanges));
            PersonalVM.PropertyChanged   += (_, e) => { if (e.PropertyName == nameof(PersonalVM.HasUnsavedChanges))    OnChildDirty(); };
            LearnsetVM.PropertyChanged   += (_, e) => { if (e.PropertyName == nameof(LearnsetVM.HasUnsavedChanges))    OnChildDirty(); };
            EvolutionsVM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(EvolutionsVM.HasUnsavedChanges)) OnChildDirty(); };
            PokedexVM.PropertyChanged    += (_, e) => { if (e.PropertyName == nameof(PokedexVM.HasUnsavedChanges))    OnChildDirty(); };
            SpriteVM.PropertyChanged     += (_, e) => { if (e.PropertyName == nameof(SpriteVM.HasUnsavedChanges))     OnChildDirty(); };
            PokewalkerVM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PokewalkerVM.HasUnsavedChanges)) OnChildDirty(); };
            BattleDisplayVM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(BattleDisplayVM.HasUnsavedChanges)) OnChildDirty(); };

            // Bubble the active tab's undo availability up to the window toolbar / Ctrl+Z.
            void OnChildUndoState(object _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ISupportsUndo.CanUndo) || e.PropertyName == nameof(ISupportsUndo.CanRedo))
                    RaiseUndoState();
            }
            PersonalVM.PropertyChanged   += OnChildUndoState;
            LearnsetVM.PropertyChanged   += OnChildUndoState;
            EvolutionsVM.PropertyChanged += OnChildUndoState;
            PokedexVM.PropertyChanged    += OnChildUndoState;
            SpriteVM.PropertyChanged     += OnChildUndoState;
            PokewalkerVM.PropertyChanged += OnChildUndoState;
            BattleDisplayVM.PropertyChanged += OnChildUndoState;

            // A form with its own list entry (e.g. Deoxys - Attack) moves the main selector too, so every tab
            // and Save work on that form.
            SpriteVM.FormPseudoIdSelected += pseudoId => { if (pseudoId != _selectedMonIndex) SelectedMonIndex = pseudoId; };

            _selectedMonIndex = initialMon;
            LoadMon(initialMon);
        }

        // ─── Load (sync all sub-VMs) ──────────────────────────────────────────────
        private void LoadMon(int id)
        {
            // hg-engine keeps each icon as data/graphics/sprites/<name>/icon.png, so it's read directly
            // (see HgEnginePokemonIcons).
            MonIconBitmap = null;
            if (id > 0 && HgEngineProject.IsActive && HgEnginePokemonIcons.TryGetIconPath(id, out string iconPath))
            {
                try { MonIconBitmap = ImageConverter.LoadHgeIconFirstFrame(iconPath); }
                catch (System.Exception ex) { AppLogger.WarnOnce("PokemonEditorViewModel.LoadMon", ex); MonIconBitmap = null; }
            }
            else if (id > 0 && !HgEngineProject.IsActive)
            {
                try
                {
                    RawImage gdiImg = DSPRE.DSUtils.GetPokePicRaw(DSPRE.DSUtils.ResolveIconId(id), 40, 40);
                    MonIconBitmap = gdiImg != null ? ImageConverter.ToAvaloniaBitmap(gdiImg) : null;
                }
                catch (System.Exception ex) { AppLogger.WarnOnce("PokemonEditorViewModel.LoadMon", ex); MonIconBitmap = null; }
            }

            string name = (id >= 0 && id < PokemonNames.Count) ? PokemonNames[id] : $"#{id}";
            SetBaseTitle($"Pokémon editor - {name}");

            PersonalVM.LoadMon(id);
            LearnsetVM.LoadMon(id);
            EvolutionsVM.LoadMon(id);
            PokedexVM.LoadMon(id);
            SpriteVM.LoadMon(id);
            PokewalkerVM.LoadMon(id);
            BattleDisplayVM.LoadMon(id);
        }

        /// <summary>Releases app-wide event subscriptions held by child VMs; call when the window closes.</summary>
        public void Detach() { EvolutionsVM?.Detach(); PersonalVM?.Detach(); BattleDisplayVM?.Detach(); }

        public void RefreshNames()
        {
            if (PersonalVM == null) return;
            string[] full = RomInfo.GetPokemonNamesWithForms(RomInfo.GetPersonalFilesCount());
            List<string> decorated = new List<string>(full.Length + 1);
            for (int i = 0; i < full.Length; i++) decorated.Add($"{i:D3} {full[i]}");
            // An unsaved new species keeps its row at the end.
            if (_pendingSpecies != null && _pendingListIndex >= 0 && _pendingListIndex < PokemonNames.Count)
                decorated.Add(PokemonNames[_pendingListIndex]);
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, decorated);
            PersonalVM.RefreshNames(full);
            LearnsetVM?.RefreshNames();
            EvolutionsVM?.RefreshNames(full);
            int id = _selectedMonIndex;
            SetBaseTitle($"Pokémon editor - {(id >= 0 && id < PokemonNames.Count ? PokemonNames[id] : $"#{id}")}");
        }

        // ─── Save all ─────────────────────────────────────────────────────────────
        public void SaveAll()
        {
            // A new species' save can ask about source comments, which needs the async path.
            if (_pendingSpecies != null) { DialogHelper.Watch(SaveAllAsync()); return; }
            if (PersonalVM.HasUnsavedChanges)   ((IEditorWithUnsavedChanges)PersonalVM).SaveChanges();
            if (LearnsetVM.HasUnsavedChanges)   LearnsetVM.SaveChanges();
            if (EvolutionsVM.HasUnsavedChanges) EvolutionsVM.SaveChanges();
            if (PokedexVM.HasUnsavedChanges)    PokedexVM.SaveChanges();
            if (BattleDisplayVM.HasUnsavedChanges) BattleDisplayVM.SaveChanges();
            if (SpriteVM.HasUnsavedChanges) SpriteVM.SaveChanges();
            if (PokewalkerVM.HasUnsavedChanges) PokewalkerVM.SaveChanges();
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
            if (PokedexVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)PokedexVM).SaveChangesAsync())
                return false;
            if (BattleDisplayVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)BattleDisplayVM).SaveChangesAsync())
                return false;
            if (SpriteVM.HasUnsavedChanges)
            {
                SpriteVM.SaveChanges();
                if (SpriteVM.HasUnsavedChanges) return false;
            }
            if (PokewalkerVM.HasUnsavedChanges &&
                !await ((IEditorWithUnsavedChanges)PokewalkerVM).SaveChangesAsync())
                return false;

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
            string name = await DialogHelper.PromptTextAsync("New species' display name:", "Add new Pokémon", owner: owner);
            if (name == null || _pendingSpecies != null) return;

            // Checked now so a name the checkout can't take is refused before it looks added.
            if (!HgEngineSpeciesExpansion.TryPlanFakemon(name, out HgEngineSpeciesExpansion.FakemonPlan plan, out string error))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't add the species:\n{error}", "Add new Pokémon", owner);
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
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
                HgEngineSpeciesExpansion.TryWriteFakemon(name, out written, out string writeError) ? null : writeError);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowErrorAsync($"Couldn't add the species:\n{error}", "Add new Pokémon", _owner);
                return -1;
            }

            HgEngineSpeciesExpansion.FinishFakemon(written);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> {
                RomInfo.DirNames.PersonalPokeData, RomInfo.DirNames.Learnsets, RomInfo.DirNames.Evolutions });
            DropPendingSpecies();

            // In-place update (ListSync), not Clear+Add: Clear briefly empties the collection, which
            // resets the FusionAutoCompleteBox's bound SelectedIndex out from under the selection.
            string[] refreshed = RomInfo.GetPokemonNames();
            string[] decorated = new string[refreshed.Length];
            for (int i = 0; i < refreshed.Length; i++) decorated[i] = $"{i:D3} {refreshed[i]}";
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, decorated);
            OnPropertyChanged(nameof(MaxMonIndex));
            AppEvents.RaiseNamesChanged();

            return written.SpeciesId;
        }

        private void PutSelectorBack()
            => RecordSwitchGuard.SnapBack(() => _selectedMonIndex, v => _selectedMonIndex = v, () => OnPropertyChanged(nameof(SelectedMonIndex)));

        private async System.Threading.Tasks.Task ConfirmDiscardAsync(int pendingIndex)
        {
            // The same Save / Discard / Cancel prompt every other editor shows when switching records.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(new RecordEdits(this), _owner, "Pokémon"))
            {
                PutSelectorBack();
                return;
            }
            _selectedMonIndex = pendingIndex;
            OnPropertyChanged(nameof(SelectedMonIndex));
            HoldImports();
            LoadMon(pendingIndex);
        }
    }
}
