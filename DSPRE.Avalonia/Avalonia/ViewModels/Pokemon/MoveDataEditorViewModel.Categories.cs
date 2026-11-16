using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class MoveDataEditorViewModel
    {
        private MoveCategoryTable _punching, _sound;
        private List<ushort> _savedPunching, _savedSound;
        public string AbilityWhyNot { get; private set; }
        public bool HasAbilityWhyNot => AbilityWhyNot != null;
        public bool HasPunchingList => _punching != null;
        public bool HasSoundList => _sound != null;

        /// <summary>Raised when a list still sits in the overlay, so the view offers the patch.</summary>
        public event Action<MoveCategoryTable.Kind, bool> NeedsExpansion;

        public event Action ListsChanged;

        /// <summary>The moves on a list as held here, unsaved ticks included, lowest id first.</summary>
        public List<(int Id, string Name)> ListedMoves(MoveCategoryTable.Kind kind)
        {
            MoveCategoryTable table = kind == MoveCategoryTable.Kind.Punching ? _punching : _sound;
            List<(int Id, string Name)> rows = new List<(int Id, string Name)>();
            if (table == null) return rows;
            foreach (ushort id in table.Moves.OrderBy(m => m))
                rows.Add((id, id < MoveNames.Count ? MoveNames[id] : $"Move {id}"));
            return rows;
        }

        public bool IsPunching
        {
            get => _punching != null && _punching.Contains(_currentId);
            set => SetListed(_punching, MoveCategoryTable.Kind.Punching, value, nameof(IsPunching), nameof(PunchingNote));
        }

        public bool IsSound
        {
            get => _sound != null && _sound.Contains(_currentId);
            set => SetListed(_sound, MoveCategoryTable.Kind.Sound, value, nameof(IsSound), nameof(SoundNote));
        }

        private void SetListed(MoveCategoryTable table, MoveCategoryTable.Kind kind, bool listed, string flag, string note)
        {
            if (table == null || _loading || _currentFile == null) return;
            if (listed == table.Contains(_currentId)) return;
            // The game's own list has no room to speak of, so every change goes to the moved list.
            if (!table.InExpansion) { OnPropertyChanged(flag); NeedsExpansion?.Invoke(kind, listed); return; }
            if (!table.Set(_currentId, listed))
            {
                OnPropertyChanged(flag);
                StatusText = $"The {MoveCategoryTable.NameOf(kind)} move list is full.";
                return;
            }
            OnPropertyChanged(flag);
            OnPropertyChanged(note);
            SetDirty();
            ListsChanged?.Invoke();
        }

        public string PunchingNote => _punching == null ? null : "Iron Fist boosts these. The trainer AI counts the boost itself.";
        public string SoundNote => _sound == null ? null : "Soundproof blocks these. The trainer AI keeps its own sound list, and the guide shows how to edit it.";

        private void LoadCategories()
        {
            _punching = _sound = null;
            _savedPunching = _savedSound = null;
            AbilityWhyNot = null;
            {
                try
                {
                    if (MoveCategoryTable.WhyNot(MoveCategoryTable.Kind.Punching) is string why) AbilityWhyNot = why;
                    else
                    {
                        _punching = MoveCategoryTable.Load(MoveCategoryTable.Kind.Punching);
                        _sound = MoveCategoryTable.Load(MoveCategoryTable.Kind.Sound);
                        _savedPunching = _punching.Moves.ToList();
                        _savedSound = _sound.Moves.ToList();
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException)
                {
                    _punching = _sound = null;
                    AbilityWhyNot = ex.Message;
                }
            }
            LoadExtraLists();
            RaiseCategories();
        }

        // Shows the ticks as the lists hold them, after a declined or failed expansion.
        public void RefreshCategories() => RaiseCategories();

        private void RaiseCategories()
        {
            foreach (string n in new[] { nameof(IsPunching), nameof(IsSound), nameof(PunchingNote), nameof(SoundNote),
                                         nameof(AbilityWhyNot), nameof(HasAbilityWhyNot), nameof(HasPunchingList), nameof(HasSoundList) })
                OnPropertyChanged(n);
            foreach (ExtraListRow row in ExtraLists) row.Raise();
            ListsChanged?.Invoke();
        }

        private bool CategoriesChanged =>
            (_punching != null && !_punching.Moves.SequenceEqual(_savedPunching)) || (_sound != null && !_sound.Moves.SequenceEqual(_savedSound))
            || ExtraLists.Any(l => l.Changed);

        private void RevertCategories()
        {
            if (_punching != null) { _punching.Moves.Clear(); _punching.Moves.AddRange(_savedPunching); }
            if (_sound != null) { _sound.Moves.Clear(); _sound.Moves.AddRange(_savedSound); }
            foreach (ExtraListRow row in ExtraLists) { row.Moves.Clear(); row.Moves.AddRange(row.Saved); }
            RaiseCategories();
        }

        // On hg-engine this runs inside the move's save session, so all commit together.
        private string WriteCategories()
        {
            if (!CategoriesChanged) return null;
            try
            {
                if (_punching != null && !_punching.Moves.SequenceEqual(_savedPunching)) _punching.Save();
                if (_sound != null && !_sound.Moves.SequenceEqual(_savedSound)) _sound.Save();
                foreach (ExtraListRow row in ExtraLists.Where(l => l.Changed)) HgEngine.HgEngineMoveList.Save(row.Info, row.Moves);
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }

        private sealed record MoveTicks(bool Punching, bool Sound, List<string> Extra);

        private MoveTicks TicksOf(int id) => !CategoriesChanged ? null : new MoveTicks(_punching?.Contains(id) == true,
            _sound?.Contains(id) == true, ExtraLists.Where(l => l.Moves.Contains((ushort)id)).Select(l => l.Info.Symbol).ToList());

        // Adding a move renumbers later ones, so the lists are read again with its ticks.
        private string ReloadWithTicks(int id, MoveTicks ticks)
        {
            LoadCategories();
            if ((ticks.Punching || ticks.Sound) && (_punching == null || _sound == null)) return AbilityWhyNot;
            if (ticks.Punching && !_punching.Contains(id) && !_punching.Set(id, true))
                return $"The {MoveCategoryTable.NameOf(MoveCategoryTable.Kind.Punching)} move list is full.";
            if (ticks.Sound && !_sound.Contains(id) && !_sound.Set(id, true))
                return $"The {MoveCategoryTable.NameOf(MoveCategoryTable.Kind.Sound)} move list is full.";
            foreach (ExtraListRow row in ExtraLists.Where(l => ticks.Extra.Contains(l.Info.Symbol) && !l.Moves.Contains((ushort)id)))
                row.Moves.Add((ushort)id);
            return null;
        }

        private string WriteNewMoveCategories(int id, MoveTicks ticks)
            => ticks == null ? null : ReloadWithTicks(id, ticks) ?? WriteCategories();

        private void MarkCategoriesSaved()
        {
            if (_punching != null) _savedPunching = _punching.Moves.ToList();
            if (_sound != null) _savedSound = _sound.Moves.ToList();
            foreach (ExtraListRow row in ExtraLists) row.Saved = row.Moves.ToList();
        }
    }
}
