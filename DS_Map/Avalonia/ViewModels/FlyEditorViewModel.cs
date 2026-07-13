using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels
{
    // ── Observable row — all columns for both game families ──────────────────
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

        // ── ARM9 offsets ─────────────────────────────────────────────────────
        private const uint DPjpOffset  = 0xF41D0, DPusOffset  = 0xF2224, DPfrOffset = 0xF2264;
        private const uint DPdeOffset  = 0xF2234, DPitOffset  = 0xF21D8, DPspOffset = 0xF2270;
        private const uint PTjpOffset  = 0xE8E88, PTusOffset  = 0xE97B4, PTfrOffset = 0xE983C;
        private const uint PTdeOffset  = 0xE980C, PTitOffset  = 0xE97D0, PTspOffset = 0xE9848;
        private const uint HGSSjpOffset = 0xF9630, HGSSusOffset = 0xF9E80, HGSSfrOffset = 0xF9E64;
        private const uint HGSSdeOffset = 0xF9E34, HGSSitOffset = 0xF9DF8, HGSSspOffset = 0xF9E68;

        private static uint FlyTableOffset
        {
            get
            {
                switch (gameFamily)
                {
                    case GameFamilies.DP:
                        switch (gameLanguage)
                        {
                            case GameLanguages.Japanese: return DPjpOffset;
                            case GameLanguages.French:   return DPfrOffset;
                            case GameLanguages.German:   return DPdeOffset;
                            case GameLanguages.Italian:  return DPitOffset;
                            case GameLanguages.Spanish:  return DPspOffset;
                            default: return DPusOffset;
                        }
                    case GameFamilies.Plat:
                        switch (gameLanguage)
                        {
                            case GameLanguages.Japanese: return PTjpOffset;
                            case GameLanguages.French:   return PTfrOffset;
                            case GameLanguages.German:   return PTdeOffset;
                            case GameLanguages.Italian:  return PTitOffset;
                            case GameLanguages.Spanish:  return PTspOffset;
                            default: return PTusOffset;
                        }
                    case GameFamilies.HGSS:
                        switch (gameLanguage)
                        {
                            case GameLanguages.Japanese: return HGSSjpOffset;
                            case GameLanguages.French:   return HGSSfrOffset;
                            case GameLanguages.German:   return HGSSdeOffset;
                            case GameLanguages.Italian:  return HGSSitOffset;
                            case GameLanguages.Spanish:  return HGSSspOffset;
                            default: return HGSSusOffset;
                        }
                    default: return DPusOffset;
                }
            }
        }

        private static int TableSize => gameFamily switch
        {
            GameFamilies.DP   => 20,
            GameFamilies.Plat => 20,
            GameFamilies.HGSS => 30,
            _                 => 20,
        };

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
        public void DiscardChanges() => SetClean();

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
            foreach (var h in headerNames)
                Headers.Add(h.TrimEnd('\0'));
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

                StatusText = "Design‑time preview (dummy data)";
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
                await DialogHelper.ShowInfo("Fly table saved successfully.", "Save");
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
                using var reader = new ARM9.Reader(FlyTableOffset);
                for (int i = 0; i < TableSize; i++)
                {
                    var row = new FlyRow();
                    if (IsHgss)
                    {
                        row.FlagIdx         = ReadByte(reader);
                        byte flags          = ReadByte(reader);
                        row.IsBlackoutSpawn = (flags & 0x01) != 0;
                        row.IsFlyPoint      = (flags & 0x02) != 0;
                        row.HeaderIdGameOver = ReadUInt16(reader);
                        row.LocalX           = ReadByte(reader);
                        row.LocalY           = ReadByte(reader);
                        row.HeaderIdFly      = ReadUInt16(reader);
                        row.GlobalX          = ReadUInt16(reader);
                        row.GlobalY          = ReadUInt16(reader);
                        row.HeaderIdUnlockWarp = ReadUInt16(reader);
                        row.GlobalXUnlock    = ReadUInt16(reader);
                        row.GlobalYUnlock    = ReadUInt16(reader);
                    }
                    else
                    {
                        row.HeaderIdGameOver  = ReadUInt16(reader);
                        row.LocalX            = ReadUInt16(reader);
                        row.LocalY            = ReadUInt16(reader);
                        row.HeaderIdFly       = ReadUInt16(reader);
                        row.GlobalX           = ReadUInt16(reader);
                        row.GlobalY           = ReadUInt16(reader);
                        row.IsTeleportPos     = ReadByte(reader) != 0;
                        row.UnlockOnMapEntry  = ReadByte(reader) != 0;
                        row.UnlockId          = ReadUInt16(reader);
                    }
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
            using var writer = new ARM9.Writer(FlyTableOffset);
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                if (IsHgss)
                {
                    writer.Write(row.FlagIdx);
                    byte flags = (byte)((row.IsBlackoutSpawn ? 0x01 : 0x00) | (row.IsFlyPoint ? 0x02 : 0x00));
                    writer.Write(flags);
                    writer.Write((ushort)row.HeaderIdGameOver);
                    writer.Write((byte)row.LocalX);
                    writer.Write((byte)row.LocalY);
                    writer.Write((ushort)row.HeaderIdFly);
                    writer.Write(row.GlobalX);
                    writer.Write(row.GlobalY);
                    writer.Write((ushort)row.HeaderIdUnlockWarp);
                    writer.Write(row.GlobalXUnlock);
                    writer.Write(row.GlobalYUnlock);
                }
                else
                {
                    writer.Write((ushort)row.HeaderIdGameOver);
                    writer.Write(row.LocalX);
                    writer.Write(row.LocalY);
                    writer.Write((ushort)row.HeaderIdFly);
                    writer.Write(row.GlobalX);
                    writer.Write(row.GlobalY);
                    writer.Write(row.IsTeleportPos     ? (byte)1 : (byte)0);
                    writer.Write(row.UnlockOnMapEntry  ? (byte)1 : (byte)0);
                    writer.Write(row.UnlockId);
                }
            }
        }

        // synchronous wrappers (ARM9.Reader inherits BinaryReader)
        private static byte   ReadByte(BinaryReader r)   => r.ReadByte();
        private static ushort ReadUInt16(BinaryReader r)  => r.ReadUInt16();
    }
}
