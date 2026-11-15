using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class CellAnimationEditorViewModel
    {
        public ObservableCollection<CellPieceRow> Pieces { get; } = new();

        private int _pieceCell;
        /// <summary>Which drawing's pieces the panel is moving.</summary>
        public int PieceCell
        {
            get => _pieceCell;
            set
            {
                if (!Set(ref _pieceCell, value)) return;
                OnPropertyChanged(nameof(PieceHeading));
                FillPieces();
            }
        }

        public bool CanMovePieces => _layout != null && _layout.Cells.Count > 0;

        public string PieceHeading
        {
            get
            {
                if (_layout == null) return "Pieces";
                NcerFile.Cell cell = _pieceCell >= 0 && _pieceCell < _layout.Cells.Count ? _layout.Cells[_pieceCell] : null;
                int n = cell?.Pieces.Count ?? 0;
                return $"Drawing {_pieceCell}: {n} piece{(n == 1 ? "" : "s")}";
            }
        }

        private void FillPieces()
        {
            _fillingPieces = true;
            foreach (CellPieceRow row in Pieces) row.Changed = null;
            Pieces.Clear();

            NcerFile.Cell cell = _layout != null && _pieceCell >= 0 && _pieceCell < _layout.Cells.Count
                     ? _layout.Cells[_pieceCell] : null;
            if (cell != null)
                for (int i = 0; i < cell.Pieces.Count; i++)
                {
                    NcerFile.Piece p = cell.Pieces[i];
                    CellPieceRow row = new CellPieceRow { Number = i, Size = $"{p.Width}x{p.Height}" };
                    row.Quietly(p.X, p.Y);
                    row.Changed = PieceMoved;
                    Pieces.Add(row);
                }

            _fillingPieces = false;
            _lastMoveCell = -1;
            _lastMovePiece = -1;
            OnPropertyChanged(nameof(CanMovePieces));
            OnPropertyChanged(nameof(PieceHeading));
        }

        private void PieceMoved(CellPieceRow row)
        {
            if (_loading || _fillingPieces || _layout == null) return;

            // Snapshot first, but only recorded if the move goes through.
            Step before = Now();
            string trouble = _layout.Move(_pieceCell, row.Number, row.Across, row.Down);
            if (trouble != null)
            {
                StatusText = trouble;
                NcerFile.Piece p = _layout.PieceAt(_pieceCell, row.Number);
                if (p != null)
                {
                    _fillingPieces = true;
                    row.Quietly(p.X, p.Y);
                    _fillingPieces = false;
                }
                return;
            }

            // Keystrokes on one piece collapse into a single step; a move of anything else starts a new one.
            bool sameAsLast = _lastMoveCell == _pieceCell && _lastMovePiece == row.Number && _history.CanUndo;
            Remember(before, sameAsLast);
            _lastMoveCell = _pieceCell;
            _lastMovePiece = row.Number;
            _lastEditFrame = -1;
            _layoutTouched = true;

            // The preview draws from the cells as read, so they are read again from the patched bytes.
            _banks = DsBgScreen.ReadCells(_layout.Write());
            Dirty = true;
            Draw();
            OnPropertyChanged(nameof(PieceHeading));
            StatusText = $"Moved piece {row.Number + 1} to {row.Across}, {row.Down}.";
        }
    }
}
