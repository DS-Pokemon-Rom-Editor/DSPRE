using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>The Battle display tab: a species' icon palette and battle sprite positions.</summary>
    public partial class BattleDisplayEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        /// <summary>True on DP, Platinum and HGSS, whose battle sprite offsets are known.</summary>
        public bool IsAvailable => GameFamily == GameFamilies.DP
                                || GameFamily == GameFamilies.Plat
                                || GameFamily == GameFamilies.HGSS;
        public string UnavailableText =>
            "The Battle display tab works in Diamond, Pearl, Platinum, HeartGold and SoulSilver.";

        // The 3 party-icon palettes a mon can use (the icons themselves are edited in the icon NARC).
        public ObservableCollection<string> PartyPalettes { get; } =
            new ObservableCollection<string> { "Palette 0", "Palette 1", "Palette 2" };

        private int _currentId = -1;
        private bool _loading;

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Battle display of {RomInfo.PokemonName(_currentId)}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); _src?.Invalidate(); _formHeightNarc?.Invalidate(); _animNarc?.Invalidate(); if (_currentId >= 0) LoadMon(_currentId); }
        private void SetDirty()
        {
            if (_loading) return;
            _undo?.Record();
            // An imported icon picture is not part of undo; it stays unsaved until Save or Discard.
            bool dirty = (_undo?.IsDirty ?? true) || _pendingIconGraphic != null;
            if (_dirty == dirty) return;
            _dirty = dirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public BattleDisplayEditorViewModel() { }

        /// <summary>Takes the Sprite tab's view model, so the preview shows its sprites.</summary>
        public BattleDisplayEditorViewModel(PokemonSpriteEditorViewModel sprites)
        {
            _sprites = sprites;
            _picturesSaved = AppEvents.WhenSaved(() => _gfx?.ForgetPictures(), SendOutGraphics.PictureArchives);
            if (_sprites != null)
                _sprites.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != null && e.PropertyName.StartsWith("Battle", StringComparison.Ordinal))
                    {
                        if (e.PropertyName == nameof(PokemonSpriteEditorViewModel.BattleFrameCount)) OnPropertyChanged(nameof(MaxFrameIndex));
                        RaiseSprites();
                    }
                    else if (e.PropertyName is nameof(PokemonSpriteEditorViewModel.IsAlternateForms)
                             or nameof(PokemonSpriteEditorViewModel.SelectedFormIndex))
                        OnSpriteFormChanged();
                };

            // Polled well above the game rate; OnAnimTimer decides from the stopwatch how many ticks are due.
            _animTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(5)
            };
            _animTimer.Tick += (_, _) => OnAnimTimer();
            _sound = true;

            if (IsAvailable)
            {
                ApplyArena();
                try { LoadBallOptions(); } catch (Exception ex) { AppLogger.Warn("Battle display: ball options: " + ex.Message); }
            }
        }

        /// <summary>Stops the preview and the save listener.</summary>
        public void Detach() { StopPlayback(); _picturesSaved?.Dispose(); }

        // A trainer or ball saved in another editor shows from the next frame on.
        private readonly IDisposable _picturesSaved;

        public void LoadMon(int id)
        {
            _loading = true;
            // The last Pokémon's history must never apply to this one.
            _undo = null;
            RaiseUndo();
            _currentId = id;
            OnPropertyChanged(nameof(GaugeNameText));
            OnPropertyChanged(nameof(GaugeNameImage));
            RenderGauges();
            _pendingIconGraphic = null;
            try
            {
                int pal = 0;
                if (IsAvailable && id >= 0)
                {
                    // monIconPalTableAddress is a vanilla-only ARM9 offset; hg-engine uses IconPaletteTable.c instead.
                    if (!(HgEngineProject.IsActive && HgEngineIconPalette.TryGetPaletteId(id, out pal)))
                        pal = DSPRE.DSUtils.GetMonIconPaletteId(IconIdFor(id));
                }
                _partyPaletteIndex = (pal >= 0 && pal < PartyPalettes.Count) ? pal : 0;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.LoadMon", ex); _partyPaletteIndex = 0; }
            _savedPartyPaletteIndex = _partyPaletteIndex;
            OnPropertyChanged(nameof(PartyPaletteIndex));
            RefreshPreview();
            LoadFormOptions(id);
            LoadSpriteData(id);
            _formMode = _sprites != null && _sprites.IsAlternateForms;
            _formIndex = _sprites != null ? _sprites.SelectedFormIndex : -1;
            LoadFormHeights();
            StopPlayback();
            LoadAnim(id);
            RefreshMusic();
            OnPropertyChanged(nameof(FormMode)); OnPropertyChanged(nameof(ShowBaseHeights)); OnPropertyChanged(nameof(CanPlay));
            RaiseLayout();
            RaiseSprites();
            SetClean();
            _loading = false;
            _undo = new ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

        public void Save()
        {
            if (!IsAvailable || _currentId < 0) return;
            if (HgEngineProject.IsActive) { DialogHelper.Watch(SaveHgEngineAsync()); return; }
            AfterWrite(WriteAll());
        }

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (HgEngineProject.IsActive) await SaveHgEngineAsync();
            else Save();
            return !HasUnsavedChanges;
        }

        private async System.Threading.Tasks.Task SaveHgEngineAsync()
        {
            if (!IsAvailable || _currentId < 0) return;
            int species = _currentId;
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(WriteAll);
            if (species != _currentId || (!saved && error == null)) return;
            AfterWrite(saved ? null : error);
        }

        // Writes every part of the tab. Returns why it stopped, or null.
        private string WriteAll()
        {
            try
            {
                // Personal Data also writes this table; an untouched value here would undo its save.
                if (_partyPaletteIndex != _savedPartyPaletteIndex)
                {
                    if (HgEngineProject.IsActive)
                    {
                        if (!HgEngineIconPalette.TrySetPaletteId(_currentId, _partyPaletteIndex, out string paletteError))
                            return paletteError;
                    }
                    else
                        DSPRE.DSUtils.SetMonIconPaletteId(IconIdFor(_currentId), (byte)_partyPaletteIndex);
                }
                if (_pendingIconGraphic != null)
                    DSPRE.DSUtils.SetMonIconGraphic(IconIdFor(_currentId), _partyPaletteIndex, _pendingIconGraphic);
                SaveSpriteData(); SaveFormHeights(); SaveAnim(); SaveFrames();
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("BattleDisplayEditorViewModel.WriteAll", ex))
            {
                return ex.Message;
            }
        }

        private void AfterWrite(string error)
        {
            if (error != null)
            {
                AppLogger.Error("Battle Display save failed: " + error);
                _ = DSPRE.Avalonia.DialogHelper.ShowErrorAsync("Couldn't save the Battle display tab: " + error, "Pokémon editor");
                return;
            }
            _savedPartyPaletteIndex = _partyPaletteIndex;
            _pendingIconGraphic = null;
            _undo?.MarkSaved();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            RefreshPreview();   // Re-read from disk, not the staged import.
        }
    }

    /// <summary>A trainer or ball the send-out preview draws over the battle scene.</summary>
    public sealed class SceneSprite : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private Bitmap _image; public Bitmap Image { get => _image; set { if (_image != value) { _image = value; Raise(nameof(Image)); } } }
        private double _left; public double Left { get => _left; set { if (_left != value) { _left = value; Raise(nameof(Left)); } } }
        private double _top; public double Top { get => _top; set { if (_top != value) { _top = value; Raise(nameof(Top)); } } }
        private bool _visible; public bool Visible { get => _visible; set { if (_visible != value) { _visible = value; Raise(nameof(Visible)); } } }
        private double _rotation; public double Rotation { get => _rotation; set { if (_rotation != value) { _rotation = value; Raise(nameof(Rotation)); } } }
        private double _flash; public double Flash { get => _flash; set { if (_flash != value) { _flash = value; Raise(nameof(Flash)); } } }
    }

    /// <summary>One DP pattern step: a sprite frame (0 or 1) and how many ticks it shows.</summary>
    public sealed class AnimPatternStep : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _frame; public int Frame { get => _frame; set { if (_frame != value) { _frame = value; Raise(nameof(Frame)); } } }
        private int _wait = 1; public int Wait { get => _wait; set { if (_wait != value) { _wait = value; Raise(nameof(Wait)); } } }
    }

    /// <summary>One back step (pokeanm prg_anm_b): a program-animation number and a wait.</summary>
    public sealed class AnimProgStep : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _number; public int Number { get => _number; set { if (_number != value) { _number = value; Raise(nameof(Number)); } } }
        private int _wait; public int Wait { get => _wait; set { if (_wait != value) { _wait = value; Raise(nameof(Wait)); } } }
    }

    /// <summary>One frame-run slot: frame, duration and pixel shift. FrameNo -1 ends it.</summary>
    public sealed class SpriteFrameEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _frameNo = -1; public int FrameNo { get => _frameNo; set { if (_frameNo != value) { _frameNo = value; Raise(nameof(FrameNo)); } } }
        private int _duration; public int Duration { get => _duration; set { if (_duration != value) { _duration = value; Raise(nameof(Duration)); } } }
        private int _horizontalShift; public int HorizontalShift { get => _horizontalShift; set { if (_horizontalShift != value) { _horizontalShift = value; Raise(nameof(HorizontalShift)); } } }
        private int _verticalShift; public int VerticalShift { get => _verticalShift; set { if (_verticalShift != value) { _verticalShift = value; Raise(nameof(VerticalShift)); } } }
    }

    /// <summary>One row of an animation script: an opcode and its argument words.</summary>
    public sealed class ProgramCmdRow : INotifyPropertyChanged
    {
        private static readonly PokemonAnimOp[] _ops =
            (PokemonAnimOp[])System.Enum.GetValues(typeof(PokemonAnimOp));
        public System.Collections.Generic.IReadOnlyList<PokemonAnimOp> Ops => _ops;

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private PokemonAnimOp _op;
        public PokemonAnimOp Op { get => _op; set { if (_op != value) { _op = value; Raise(nameof(Op)); Raise(nameof(ArgHint)); } } }
        private string _argsText = "";
        public string ArgsText { get => _argsText; set { if (_argsText != value) { _argsText = value; Raise(nameof(ArgsText)); } } }
        public string ArgHint
        {
            get
            {
                string[] names = PokeAnimScript.ArgNames(Op);
                if (names.Length > 0) return string.Join(", ", names);
                int n = PokeAnimScript.ArgsFor(Op);
                return n == 0 ? "No values" : Plural.Of(n, "value");
            }
        }
    }
}
