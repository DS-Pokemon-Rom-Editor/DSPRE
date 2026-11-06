using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
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

    /// <summary>One fly spot on the HGSS town map, with the Fly row it sends the player to.</summary>
    public class FlySpotRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public int Index { get; set; }
        public string Name { get; set; }
        public int NameMap { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        private byte _x;
        public byte X { get => _x; set { if (Set(ref _x, value)) OnPropertyChanged(nameof(Left)); } }
        private byte _y;
        public byte Y { get => _y; set { if (Set(ref _y, value)) OnPropertyChanged(nameof(Top)); } }
        private int _warpMap;
        public int WarpMap { get => _warpMap; set => Set(ref _warpMap, value); }
        private byte _flag;
        public byte Flag { get => _flag; set => Set(ref _flag, value); }

        // The picture is the region map at twice its size.
        public const int Scale = 2;
        public double Left => X * 8 * Scale;
        public double Top => (Y + HgssFlySpots.RowsDown) * 8 * Scale;
        public double BoxWidth => Math.Max(1, Width) * 8 * Scale;
        public double BoxHeight => Math.Max(1, Height) * 8 * Scale;

        /// <summary>The town's area on the town map, in spot tiles, or null when the town has none.</summary>
        public (int X, int Y, int Width, int Height)? Area { get; set; }
        public bool HasArea => Area != null;
        public double AreaLeft => (Area?.X ?? 0) * 8 * Scale;
        public double AreaTop => ((Area?.Y ?? 0) + HgssFlySpots.RowsDown) * 8 * Scale;
        public double AreaWidth => Math.Max(1, Area?.Width ?? 1) * 8 * Scale;
        public double AreaHeight => Math.Max(1, Area?.Height ?? 1) * 8 * Scale;

        private int _flyRow = -1;
        /// <summary>The Fly row this spot uses, or -1 when no row has its warp map.</summary>
        public int FlyRow { get => _flyRow; set => Set(ref _flyRow, value); }
        private string _warning = "";
        public string Warning { get => _warning; set { if (Set(ref _warning, value)) OnPropertyChanged(nameof(HasWarning)); } }
        public bool HasWarning => !string.IsNullOrEmpty(Warning);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class FlyEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Fly and warp points";
        void IEditorWithUnsavedChanges.SaveChanges() => DialogHelper.Watch(SaveCommandAsync());
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommandAsync();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() { LoadRows(); SetClean(); }

        // ── Undo / redo ──────────────────────────────────────────────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private sealed class UndoState
        {
            public List<FlyRow> Rows { get; set; }
            public List<FlySpotRow> Spots { get; set; }
        }

        private byte[] TakeState() => UndoJson.Take(new UndoState { Rows = Rows.ToList(), Spots = Spots.ToList() });

        private void ApplyState(byte[] state)
        {
            UndoState read = UndoJson.Read<UndoState>(state);
            _applying = true;
            for (int i = 0; i < Rows.Count && i < read.Rows.Count; i++) UndoJson.CopyInto(read.Rows[i], Rows[i]);
            for (int i = 0; i < Spots.Count && i < (read.Spots?.Count ?? 0); i++)
            {
                (Spots[i].X, Spots[i].Y, Spots[i].WarpMap, Spots[i].Flag) = (read.Spots[i].X, read.Spots[i].Y, read.Spots[i].WarpMap, read.Spots[i].Flag);
            }
            _applying = false;
            CheckSpots();
            if (_undo.IsDirty) SetDirty(); else SetClean();
        }

        private bool _applying;

        // ── Observable state ─────────────────────────────────────────────────
        public ObservableCollection<FlyRow>   Rows    { get; } = new();
        public ObservableCollection<string>   Headers { get; } = new();
        public ObservableCollection<FlySpotRow> Spots { get; } = new();
        /// <summary>Each Fly row by its fly map and flag, for linking a spot.</summary>
        public ObservableCollection<string> FlyRowChoices { get; } = new();
        public global::Avalonia.Media.Imaging.Bitmap TownMap { get; private set; }
        public bool HasSpots => Spots.Count > 0;

        private string _title = "Fly / warp editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // Column-visibility helpers (bound to DataGrid column widths / IsVisible)
        private bool _designTimeIsHgss = true; // set to true if you want HGSS preview

        public bool IsHgss => Design.IsDesignMode ? _designTimeIsHgss : (GameFamily == GameFamilies.HGSS);
        public bool IsDpOrPlat => Design.IsDesignMode ? !_designTimeIsHgss : (GameFamily == GameFamilies.DP || GameFamily == GameFamilies.Plat);

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // ── Constructor ───────────────────────────────────────────────────────
        public FlyEditorViewModel(List<string> headerNames)
        {
            // The names the rest of the app shows, so a row reads New Bark Town and not T20R0201.
            IReadOnlyList<string> friendly = HeaderLabels.Friendly();
            if (friendly.Count == headerNames.Count)
                foreach (string h in friendly) Headers.Add(h);
            else
                foreach (string h in headerNames) Headers.Add(h.TrimEnd('\0'));
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
                    FlyRow row = new FlyRow();
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
                Title = "Fly / warp editor (Preview)";
                return;
            }
            throw new InvalidOperationException("Parameterless constructor only for design time.");
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public async Task SaveCommandAsync()
        {
            try
            {
                WriteRows();
                if (Spots.Count == HgssFlySpots.Count)
                {
                    List<HgssFlySpots.Spot> spots = HgssFlySpots.Read();
                    for (int i = 0; i < spots.Count; i++)
                        (spots[i].X, spots[i].Y, spots[i].WarpMap, spots[i].Flag) = (Spots[i].X, Spots[i].Y, Spots[i].WarpMap, Spots[i].Flag);
                    HgssFlySpots.Write(spots);
                }
                _undo?.MarkSaved();
                SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't save:\n{ex.Message}", "Fly / warp editor");
            }
        }



        // ── Private helpers ───────────────────────────────────────────────────
        private void SetDirty()  { _dirty = true;  OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean()  { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private void Edited()
        {
            if (_applying) return;
            CheckSpots();
            _undo?.Record(_step);
            if (_undo == null || _undo.IsDirty) SetDirty(); else SetClean();
        }

        private void LoadRows()
        {
            Rows.Clear();
            try
            {
                foreach (FlyTable.Row r in FlyTable.Read())
                {
                    FlyRow row = new FlyRow
                    {
                        HeaderIdGameOver = r.HeaderIdGameOver, LocalX = r.LocalX, LocalY = r.LocalY,
                        HeaderIdFly = r.HeaderIdFly, GlobalX = r.GlobalX, GlobalY = r.GlobalY,
                        IsTeleportPos = r.IsTeleportPos, UnlockOnMapEntry = r.UnlockOnMapEntry, UnlockId = r.UnlockId,
                        FlagIdx = r.FlagIdx, IsBlackoutSpawn = r.IsBlackoutSpawn, IsFlyPoint = r.IsFlyPoint,
                        HeaderIdUnlockWarp = r.HeaderIdUnlockWarp, GlobalXUnlock = r.GlobalXUnlock, GlobalYUnlock = r.GlobalYUnlock,
                    };
                    row.PropertyChanged += (_, __) => Edited();
                    Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FlyEditorViewModel.LoadRows: {ex.Message}");
                StatusText = $"Couldn't load: {ex.Message}";
            }
            LoadSpots();
            _undo = new ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

        // The town map's spots (HGSS only). A spot sends the player to the first Fly row with its warp map and shows once its flag
        // is set, so a spot is checked against that row.
        private void LoadSpots()
        {
            Spots.Clear();
            if (GameFamily != GameFamilies.HGSS) return;
            List<HgssFlySpots.Spot> spots = null;
            try { spots = HgssFlySpots.Read(); }
            catch (Exception ex) { AppLogger.Error($"FlyEditorViewModel.LoadSpots: {ex.Message}"); }
            if (spots == null) { OnPropertyChanged(nameof(HasSpots)); return; }
            for (int i = 0; i < spots.Count; i++)
            {
                HgssFlySpots.Spot s = spots[i];
                FlySpotRow row = new FlySpotRow
                {
                    Index = i, NameMap = s.NameMap, Name = s.NameMap < Headers.Count ? Headers[s.NameMap] : $"Map {s.NameMap}",
                    Width = s.Width, Height = s.Height, X = s.X, Y = s.Y, WarpMap = s.WarpMap, Flag = s.Flag,
                };
                row.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(FlySpotRow.X) or nameof(FlySpotRow.Y) or nameof(FlySpotRow.WarpMap) or nameof(FlySpotRow.Flag)) Edited(); };
                Spots.Add(row);
            }
            LoadWorld();
            foreach (FlySpotRow row in Spots)
            {
                HgssFlySpots.Location? area = _locations?.FirstOrDefault(l => l.Map == spots[row.Index].NameMap);
                if (area is HgssFlySpots.Location l && l.Width > 0) row.Area = (l.X, l.Y - HgssFlySpots.RowsDown, l.Width, l.Height);
            }
            NitroBgCodec.BgImage map = PokegearPictures.WholeRegionMap();
            TownMap = map == null ? null : ImageConverter.FromRgba(map.Rgba, map.Width, map.Height);
            OnPropertyChanged(nameof(TownMap));
            OnPropertyChanged(nameof(HasSpots));
            FlyRowChoices.Clear();
            for (int i = 0; i < Rows.Count; i++)
                FlyRowChoices.Add($"{i}: {(Rows[i].HeaderIdFly < Headers.Count ? Headers[Rows[i].HeaderIdFly] : Rows[i].HeaderIdFly.ToString())}, flag {Rows[i].FlagIdx}");
            CheckSpots();
        }

        /// <summary>Links a spot to a Fly row: the spot takes that row's map and flag.</summary>
        public void LinkSpot(FlySpotRow spot, int row)
        {
            if (spot == null || row < 0 || row >= Rows.Count) return;
            BeginStep();
            spot.WarpMap = Rows[row].HeaderIdFly;
            spot.Flag = Rows[row].FlagIdx;
            EndStep();
        }

        // Edits between these are one undo step, however long a drag takes.
        private object _step;
        public void BeginStep() => _step = new object();
        public void EndStep() => _step = null;

        /// <summary>Moves a spot to a town map tile, clamped to the map.</summary>
        public void MoveSpot(FlySpotRow spot, int x, int y)
        {
            if (spot == null) return;
            spot.X = (byte)Math.Clamp(x, 0, HgssFlySpots.MapColumns - Math.Max(1, spot.Width));
            spot.Y = (byte)Math.Clamp(y, 0, HgssFlySpots.MapRows - HgssFlySpots.RowsDown - Math.Max(1, spot.Height));
        }

        private List<HgssFlySpots.Location> _locations;
        private GameMatrix _world;

        // Spot tiles are cells of the main world matrix (pokeheartgold FieldSystem_InitPokegearArgs).
        private void LoadWorld()
        {
            try { _locations = HgssFlySpots.ReadLocations(); }
            catch (Exception ex) { AppLogger.WarnOnce("FlyEditorViewModel.Locations", ex); }
            try { _world = new GameMatrix(0); }
            catch (Exception ex) { AppLogger.WarnOnce("FlyEditorViewModel.World", ex); }
        }

        // Empty cells don't count: Victory Road's spot sits on them in the retail game.
        private string OffWorld(FlySpotRow spot, int nameMap)
        {
            if (_world?.Headers == null) return null;
            bool own = false, other = false;
            for (int r = spot.Y; r < spot.Y + Math.Max(1, spot.Height) && r < _world.Height; r++)
                for (int c = spot.X; c < spot.X + Math.Max(1, spot.Width) && c < _world.Width; c++)
                {
                    ushort map = _world.Headers[r, c];
                    if (map == spot.WarpMap || map == nameMap) own = true;
                    else if (map != 0) other = true;
                }
            return other && !own ? "Covers another map in the main matrix." : null;
        }

        private static string OffArea(FlySpotRow spot)
        {
            if (spot.Area is not (int ax, int ay, int aw, int ah)) return null;
            bool overlaps = spot.X < ax + aw && ax < spot.X + Math.Max(1, spot.Width) && spot.Y < ay + ah && ay < spot.Y + Math.Max(1, spot.Height);
            return overlaps ? null : "Outside its town's area on the town map.";
        }

        private void CheckSpots()
        {
            if (Spots.Count == 0) return;
            foreach (FlySpotRow spot in Spots)
            {
                int row = -1;
                for (int i = 0; i < Rows.Count && row < 0; i++) if (Rows[i].HeaderIdFly == spot.WarpMap) row = i;
                spot.FlyRow = row;
                string link = row < 0 ? "No Fly row has this map, so flying here does nothing."
                    : Rows[row].FlagIdx != spot.Flag ? $"Shows on flag {spot.Flag}, but its Fly row sets flag {Rows[row].FlagIdx}." : null;
                spot.Warning = string.Join("\n", new[] { link, OffWorld(spot, spot.NameMap), OffArea(spot) }.Where(w => w != null));
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
