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
        private readonly MoveTextScreens _screens = new();
        private int _previewTab;
        /// <summary>0 summary, 1 battle, 2 relearner, 3 the bag's TM pocket.</summary>
        public int PreviewTab
        {
            get => _previewTab;
            set { if (Set(ref _previewTab, value)) { OnPropertyChanged(nameof(IsBagTab)); RefreshPreview(); } }
        }
        public bool IsBagTab => _previewTab == 3;
        public global::Avalonia.Media.Imaging.Bitmap Preview { get; private set; }

        private void RefreshPreview()
        {
            if (_descBank == null) return;
            MoveTextScreens.Screen screen = (MoveTextScreens.Screen)Math.Clamp(_previewTab, 0, 3);
            byte[] rgba = _screens.Render(screen, screen == MoveTextScreens.Screen.Bag ? _bagText : _description, SystemFont);
            Preview = rgba == null ? null : DSPRE.Avalonia.ImageConverter.FromRgba(rgba, 256, 192);
            OnPropertyChanged(nameof(Preview));
        }

        private sealed record UndoState(byte[] Move, string Description, string Bag, bool Punching, bool Sound, bool[] Extra);
        private byte[] Snapshot() => DSPRE.Avalonia.UndoJson.Take(new UndoState(_currentFile.ToByteArray(), _description, _bagText, IsPunching, IsSound,
            ExtraLists.Select(l => l.IsListed).ToArray()));

        /// <summary>One of hg-engine's own move lists, ticked for the move shown.</summary>
        public sealed class ExtraListRow : INotifyPropertyChanged
        {
            private readonly MoveDataEditorViewModel _owner;
            public HgEngine.HgEngineMoveList.Info Info { get; }
            public List<ushort> Moves { get; }
            public List<ushort> Saved { get; set; }
            public string Label => Info.Label;
            public string Tip => Info.Tip;

            public ExtraListRow(MoveDataEditorViewModel owner, HgEngine.HgEngineMoveList.Info info, List<ushort> moves)
            {
                _owner = owner; Info = info; Moves = moves; Saved = moves.ToList();
            }

            public bool IsListed
            {
                get => Moves.Contains((ushort)_owner._currentId);
                set
                {
                    if (_owner._loading || _owner._currentFile == null || value == IsListed) return;
                    if (value) Moves.Add((ushort)_owner._currentId); else Moves.Remove((ushort)_owner._currentId);
                    Raise();
                    _owner.SetDirty();
                }
            }

            public bool Changed => !Moves.SequenceEqual(Saved);
            public void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsListed)));
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ObservableCollection<ExtraListRow> ExtraLists { get; } = new ObservableCollection<ExtraListRow>();
        public bool HasExtraLists => ExtraLists.Count > 0;

        private void LoadExtraLists()
        {
            ExtraLists.Clear();
            if (!HgEngine.HgEngineProject.IsActive) return;
            foreach (HgEngine.HgEngineMoveList.Info info in HgEngine.HgEngineMoveList.Extra)
            {
                if (HgEngine.HgEngineMoveList.WhyNot(info) != null) continue;
                try { ExtraLists.Add(new ExtraListRow(this, info, HgEngine.HgEngineMoveList.Load(info))); }
                catch (InvalidDataException ex) { AppLogger.Warn($"Move list {info.Symbol}: {ex.Message}"); }
            }
            OnPropertyChanged(nameof(HasExtraLists));
        }
    }
}
