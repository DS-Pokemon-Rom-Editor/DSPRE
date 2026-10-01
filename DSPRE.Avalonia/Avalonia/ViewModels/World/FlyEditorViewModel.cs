using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.World
{
    // ── Observable row: all columns for both game families ──────────────────
    public class FlyRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // Shared header combo indices (index into Headers list)
        private int _headerIdGameOver;
        public int HeaderIdGameOver { get => _headerIdGameOver; set => Set(ref _headerIdGameOver, value); }

        private int _headerIdFly;
        public int HeaderIdFly { get => _headerIdFly; set => Set(ref _headerIdFly, value); }

        // DP/Plat + HGSS: local spawn coords
        private ushort _localX;
        public ushort LocalX { get => _localX; set => Set(ref _localX, value); }

        private ushort _localY;
        public ushort LocalY { get => _localY; set => Set(ref _localY, value); }

        // DP/Plat + HGSS: global fly coords
        private ushort _globalX;
        public ushort GlobalX { get => _globalX; set => Set(ref _globalX, value); }

        private ushort _globalY;
        public ushort GlobalY { get => _globalY; set => Set(ref _globalY, value); }

        // DP/Plat unlock columns
        private bool _isTeleportPos;
        public bool IsTeleportPos { get => _isTeleportPos; set => Set(ref _isTeleportPos, value); }

        private bool _unlockOnMapEntry;
        public bool UnlockOnMapEntry { get => _unlockOnMapEntry; set => Set(ref _unlockOnMapEntry, value); }

        private ushort _unlockId;
        public ushort UnlockId { get => _unlockId; set => Set(ref _unlockId, value); }

        // HGSS-only unlock header
        private int _headerIdUnlockWarp;
        public int HeaderIdUnlockWarp { get => _headerIdUnlockWarp; set => Set(ref _headerIdUnlockWarp, value); }

        private ushort _globalXUnlock;
        public ushort GlobalXUnlock { get => _globalXUnlock; set => Set(ref _globalXUnlock, value); }

        private ushort _globalYUnlock;
        public ushort GlobalYUnlock { get => _globalYUnlock; set => Set(ref _globalYUnlock, value); }

        // HGSS-only flag columns
        private byte _flagIdx;
        public byte FlagIdx { get => _flagIdx; set => Set(ref _flagIdx, value); }

        private bool _isBlackoutSpawn;
        public bool IsBlackoutSpawn { get => _isBlackoutSpawn; set => Set(ref _isBlackoutSpawn, value); }

        private bool _isFlyPoint;
        public bool IsFlyPoint { get => _isFlyPoint; set => Set(ref _isFlyPoint, value); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class FlyEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Fly / Warp Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() { LoadRows(); SetClean(); }

        // ── Observable state ─────────────────────────────────────────────────
        public ObservableCollection<FlyRow>   Rows    { get; } = new();
        public ObservableCollection<string>   Headers { get; } = new();

        private string _title = "Fly / Warp Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // Column-visibility helpers (bound to DataGrid column widths / IsVisible)
        private bool DesignTimeIsHgss = true; // set to true if you want HGSS preview

        public bool IsHgss => Design.IsDesignMode ? DesignTimeIsHgss : (gameFamily == GameFamilies.HGSS);
        public bool IsDpOrPlat => Design.IsDesignMode ? !DesignTimeIsHgss : (gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat);

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // ── Constructor ───────────────────────────────────────────────────────
        public FlyEditorViewModel(List<string> headerNames)
        {
            // The names the rest of the app shows, so a row reads New Bark Town and not T20R0201.
            var friendly = HeaderLabels.Friendly();
            if (friendly.Count == headerNames.Count)
                foreach (var h in friendly) Headers.Add(h);
            else
                foreach (var h in headerNames) Headers.Add(h.TrimEnd('\0'));
            LoadRows();
        }

        // Parameterless constructor is only for design time. Use the constructor with headerNames.
        public FlyEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                // Dummy headers
                for (int i = 0; i < 10; i++) Headers.Add($"Header {i}");

                // Add 3 dummy rows
                for (int i = 0; i < 3; i++)
                {
                    var row = new FlyRow();
                    // Set properties to show something in the DataGrid
                    row.HeaderIdGameOver = i % Headers.Count;
                    row.HeaderIdFly = i % Headers.Count;
                    row.LocalX = (ushort)(i * 10);
                    row.LocalY = (ushort)(i * 10);
                    row.GlobalX = (ushort)(i * 100);
                    row.GlobalY = (ushort)(i * 100);

                    // For DP/Plat columns
                    row.IsTeleportPos = i % 2 == 0;
                    row.UnlockOnMapEntry = i % 2 == 1;
                    row.UnlockId = (ushort)i;

                    // For HGSS columns (won't be visible unless you force IsHgss true)
                    row.FlagIdx = (byte)i;
                    row.IsBlackoutSpawn = i % 2 == 0;
                    row.IsFlyPoint = i % 2 == 1;
                    row.HeaderIdUnlockWarp = i % Headers.Count;
                    row.GlobalXUnlock = (ushort)(i * 50);
                    row.GlobalYUnlock = (ushort)(i * 50);

                    Rows.Add(row);
                }

                StatusText = "Design-time preview (dummy data)";
                Title = "Fly / Warp Editor (Preview)";
                return;
            }
            throw new InvalidOperationException("Parameterless constructor only for design time.");
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public async Task SaveCommand()
        {
            try
            {
                WriteRows();
                SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Could not save: {ex.Message}", "Save Error");
            }
        }



        // ── Private helpers ───────────────────────────────────────────────────
        private void SetDirty()  { _dirty = true;  Title = "● Fly / Warp Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean()  { _dirty = false; Title = "Fly / Warp Editor";  OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private void LoadRows()
        {
            Rows.Clear();
            try
            {
                foreach (var r in FlyTable.Read())
                {
                    var row = new FlyRow
                    {
                        HeaderIdGameOver = r.HeaderIdGameOver, LocalX = r.LocalX, LocalY = r.LocalY,
                        HeaderIdFly = r.HeaderIdFly, GlobalX = r.GlobalX, GlobalY = r.GlobalY,
                        IsTeleportPos = r.IsTeleportPos, UnlockOnMapEntry = r.UnlockOnMapEntry, UnlockId = r.UnlockId,
                        FlagIdx = r.FlagIdx, IsBlackoutSpawn = r.IsBlackoutSpawn, IsFlyPoint = r.IsFlyPoint,
                        HeaderIdUnlockWarp = r.HeaderIdUnlockWarp, GlobalXUnlock = r.GlobalXUnlock, GlobalYUnlock = r.GlobalYUnlock,
                    };
                    row.PropertyChanged += (_, __) => SetDirty();
                    Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FlyEditorViewModel.LoadRows: {ex.Message}");
                StatusText = $"Error loading: {ex.Message}";
            }
        }

        private void WriteRows()
        {
            FlyTable.Write(Rows.Select(row => new FlyTable.Row
            {
                HeaderIdGameOver = row.HeaderIdGameOver, LocalX = row.LocalX, LocalY = row.LocalY,
                HeaderIdFly = row.HeaderIdFly, GlobalX = row.GlobalX, GlobalY = row.GlobalY,
                IsTeleportPos = row.IsTeleportPos, UnlockOnMapEntry = row.UnlockOnMapEntry, UnlockId = row.UnlockId,
                FlagIdx = row.FlagIdx, IsBlackoutSpawn = row.IsBlackoutSpawn, IsFlyPoint = row.IsFlyPoint,
                HeaderIdUnlockWarp = row.HeaderIdUnlockWarp, GlobalXUnlock = row.GlobalXUnlock, GlobalYUnlock = row.GlobalYUnlock,
            }).ToList());
            FlyTable.RaiseSaved();
        }

    }
}
