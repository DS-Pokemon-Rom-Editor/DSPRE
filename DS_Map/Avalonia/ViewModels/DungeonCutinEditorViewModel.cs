using Avalonia.Controls;
using DSPRE.Avalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels
{
    // One row of the real HGSS DUNGEON_CUTIN_DATA struct (dungeon_cutin_def.h): ZoneID, WipeType,
    // Graphic[4][3] (Morning/Noon/Evening/Night x Palette/Tiles/Screen), Name.
    public class DungeonCutinRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly IReadOnlyList<string> _headers;
        public DungeonCutinRow(IReadOnlyList<string> headers) { _headers = headers; }

        /// <summary>1-based position in the table, for the row list display only.</summary>
        public int RowNumber { get; set; }
        public string HeaderName => (_headers != null && _headerIndex >= 0 && _headerIndex < _headers.Count) ? _headers[_headerIndex] : "?";
        public string RowLabel => $"{RowNumber:00}: {HeaderName}";

        // Combo index into the Headers list (Zone == header in this engine generation).
        private int _headerIndex;
        public int HeaderIndex
        {
            get => _headerIndex;
            set { if (Set(ref _headerIndex, value)) { OnPropertyChanged(nameof(HeaderName)); OnPropertyChanged(nameof(RowLabel)); } }
        }

        // Unused in practice (always 0, never read by dungeon_cutin.c), but left editable so an
        // import doesn't discard whatever value a ROM actually has here.
        private int _wipeType;
        public int WipeType { get => _wipeType; set => Set(ref _wipeType, value); }

        private int _morningPaletteId;
        public int MorningPaletteId { get => _morningPaletteId; set => Set(ref _morningPaletteId, value); }
        private int _morningTilesId;
        public int MorningTilesId { get => _morningTilesId; set => Set(ref _morningTilesId, value); }
        private int _morningScreenId;
        public int MorningScreenId { get => _morningScreenId; set => Set(ref _morningScreenId, value); }

        private int _noonPaletteId;
        public int NoonPaletteId { get => _noonPaletteId; set => Set(ref _noonPaletteId, value); }
        private int _noonTilesId;
        public int NoonTilesId { get => _noonTilesId; set => Set(ref _noonTilesId, value); }
        private int _noonScreenId;
        public int NoonScreenId { get => _noonScreenId; set => Set(ref _noonScreenId, value); }

        private int _eveningPaletteId;
        public int EveningPaletteId { get => _eveningPaletteId; set => Set(ref _eveningPaletteId, value); }
        private int _eveningTilesId;
        public int EveningTilesId { get => _eveningTilesId; set => Set(ref _eveningTilesId, value); }
        private int _eveningScreenId;
        public int EveningScreenId { get => _eveningScreenId; set => Set(ref _eveningScreenId, value); }

        private int _nightPaletteId;
        public int NightPaletteId { get => _nightPaletteId; set => Set(ref _nightPaletteId, value); }
        private int _nightTilesId;
        public int NightTilesId { get => _nightTilesId; set => Set(ref _nightTilesId, value); }
        private int _nightScreenId;
        public int NightScreenId { get => _nightScreenId; set => Set(ref _nightScreenId, value); }

        // Message/text-archive ID for the location name, not a raw group counter. Entries for the
        // same real-world location, e.g. the two Dark Cave entrances, share this value.
        private int _nameMessageId;
        public int NameMessageId { get => _nameMessageId; set => Set(ref _nameMessageId, value); }
    }

    public enum DungeonCutinTimezone { Morning, Noon, Evening, Night }

    public class DungeonCutinEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly Data.DungeonCutinGraphics _graphics = new();

        public const int RowCount = 25;      // DUNGEON_CUTIN_NUM in the real source
        private const int FieldsPerRow = 15;  // sizeof(DUNGEON_CUTIN_DATA) / 4

        private static uint ResolveTableOffset()
        {
            RomInfo.SetDungeonCutinTableOffsetToRAMAddress();
            uint ramAddress = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.dungeonCutinTableOffsetToRAMAddress, 4), 0);
            return ramAddress - ARM9.address;
        }

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Dungeon Cutin Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() => LoadRows();

        // ── Observable state ─────────────────────────────────────────────────
        public ObservableCollection<DungeonCutinRow> Rows { get; } = new();
        public ObservableCollection<string> Headers { get; } = new();

        private string _title = "Dungeon Cutin Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // ── Graphics (timezone thumbnails for the selected row) ────────────────
        public bool GraphicsAvailable => _graphics.Available;

        private DungeonCutinRow _selectedRow;
        public DungeonCutinRow SelectedRow
        {
            get => _selectedRow;
            set { if (Set(ref _selectedRow, value)) RefreshPreviews(); }
        }

        private AvaBitmap _morningPreview, _noonPreview, _eveningPreview, _nightPreview;
        public AvaBitmap MorningPreview { get => _morningPreview; private set => Set(ref _morningPreview, value); }
        public AvaBitmap NoonPreview { get => _noonPreview; private set => Set(ref _noonPreview, value); }
        public AvaBitmap EveningPreview { get => _eveningPreview; private set => Set(ref _eveningPreview, value); }
        public AvaBitmap NightPreview { get => _nightPreview; private set => Set(ref _nightPreview, value); }

        private void RefreshPreviews()
        {
            if (_selectedRow == null || !GraphicsAvailable)
            {
                MorningPreview = NoonPreview = EveningPreview = NightPreview = null;
                return;
            }
            MorningPreview = ImageConverter.ToAvaloniaBitmap(_graphics.Composite(_selectedRow.MorningPaletteId, _selectedRow.MorningTilesId, _selectedRow.MorningScreenId));
            NoonPreview = ImageConverter.ToAvaloniaBitmap(_graphics.Composite(_selectedRow.NoonPaletteId, _selectedRow.NoonTilesId, _selectedRow.NoonScreenId));
            EveningPreview = ImageConverter.ToAvaloniaBitmap(_graphics.Composite(_selectedRow.EveningPaletteId, _selectedRow.EveningTilesId, _selectedRow.EveningScreenId));
            NightPreview = ImageConverter.ToAvaloniaBitmap(_graphics.Composite(_selectedRow.NightPaletteId, _selectedRow.NightTilesId, _selectedRow.NightScreenId));
        }

        private static (int pal, int tiles, int scr) GetIds(DungeonCutinRow r, DungeonCutinTimezone tz) => tz switch
        {
            DungeonCutinTimezone.Morning => (r.MorningPaletteId, r.MorningTilesId, r.MorningScreenId),
            DungeonCutinTimezone.Noon => (r.NoonPaletteId, r.NoonTilesId, r.NoonScreenId),
            DungeonCutinTimezone.Evening => (r.EveningPaletteId, r.EveningTilesId, r.EveningScreenId),
            _ => (r.NightPaletteId, r.NightTilesId, r.NightScreenId),
        };

        private static void SetIds(DungeonCutinRow r, DungeonCutinTimezone tz, int pal, int tiles, int scr)
        {
            switch (tz)
            {
                case DungeonCutinTimezone.Morning: r.MorningPaletteId = pal; r.MorningTilesId = tiles; r.MorningScreenId = scr; break;
                case DungeonCutinTimezone.Noon: r.NoonPaletteId = pal; r.NoonTilesId = tiles; r.NoonScreenId = scr; break;
                case DungeonCutinTimezone.Evening: r.EveningPaletteId = pal; r.EveningTilesId = tiles; r.EveningScreenId = scr; break;
                case DungeonCutinTimezone.Night: r.NightPaletteId = pal; r.NightTilesId = tiles; r.NightScreenId = scr; break;
            }
        }

        /// <summary>Composites + PNG-saves the given timezone's currently assigned art. Returns an error, or null on success.</summary>
        public string ExportTimezoneImage(DungeonCutinTimezone tz, string pngPath)
        {
            if (SelectedRow == null) return "No row selected.";
            var (pal, tiles, scr) = GetIds(SelectedRow, tz);
            var raw = _graphics.Composite(pal, tiles, scr);
            if (raw == null) return "Could not decode this slot's graphics.";
            try { ImageConverter.ToAvaloniaBitmap(raw).Save(pngPath); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Encodes the PNG as a brand-new palette+tiles+screen triple (never overwriting the slot the
        /// row currently points at, since other rows/timezones may share it) and points this
        /// row+timezone at the new triple. Returns an error, or null on success.
        /// </summary>
        public string ImportTimezoneImage(DungeonCutinTimezone tz, string pngPath)
        {
            if (SelectedRow == null) return "No row selected.";
            DSPRE.RawImage raw;
            try
            {
                using var stream = File.OpenRead(pngPath);
                raw = ImageConverter.DecodeRawImage(stream);
            }
            catch (Exception ex) { return ex.Message; }
            if (raw == null) return "Could not read this PNG.";

            if (!_graphics.Import(raw, out int pal, out int tiles, out int scr, out string error))
                return error;

            SetIds(SelectedRow, tz, pal, tiles, scr);
            RefreshPreviews();
            return null;
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public DungeonCutinEditorViewModel(List<string> headerNames)
        {
            foreach (var h in headerNames)
                Headers.Add(h.TrimEnd('\0'));
            LoadRows();
        }

        // Parameterless constructor is only for design time.
        public DungeonCutinEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                for (int i = 0; i < 10; i++) Headers.Add($"Header {i}");
                for (int i = 0; i < 3; i++)
                {
                    var row = new DungeonCutinRow(Headers) { RowNumber = i + 1, HeaderIndex = i, NameMessageId = 100 + i };
                    Rows.Add(row);
                }
                SelectedRow = Rows.Count > 0 ? Rows[0] : null;
                StatusText = "Design-time preview (dummy data)";
                Title = "Dungeon Cutin Editor (Preview)";
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
                await DialogHelper.ShowInfo("Dungeon Cutin table saved successfully.", "Save");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Could not save: {ex.Message}", "Save Error");
            }
        }

        private static readonly string[] CsvHeader =
        {
            "ZoneID", "WipeType",
            "MorningPalette", "MorningTiles", "MorningScreen",
            "NoonPalette", "NoonTiles", "NoonScreen",
            "EveningPalette", "EveningTiles", "EveningScreen",
            "NightPalette", "NightTiles", "NightScreen",
            "NameMessageId"
        };

        public async Task<string> ExportCsvAsync(string path)
        {
            try
            {
                var lines = new List<string> { string.Join(",", CsvHeader) };
                foreach (var row in Rows)
                {
                    lines.Add(string.Join(",", new[]
                    {
                        row.HeaderIndex, row.WipeType,
                        row.MorningPaletteId, row.MorningTilesId, row.MorningScreenId,
                        row.NoonPaletteId, row.NoonTilesId, row.NoonScreenId,
                        row.EveningPaletteId, row.EveningTilesId, row.EveningScreenId,
                        row.NightPaletteId, row.NightTilesId, row.NightScreenId,
                        row.NameMessageId
                    }.Select(v => v.ToString(CultureInfo.InvariantCulture))));
                }
                await File.WriteAllLinesAsync(path, lines);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> ImportCsvAsync(string path)
        {
            try
            {
                string[] lines = await File.ReadAllLinesAsync(path);
                var dataLines = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                if (dataLines.Count != RowCount)
                    return $"Expected exactly {RowCount} rows, found {dataLines.Count}. " +
                        "This is a fixed-size ARM9-embedded table with no room to grow or shrink; " +
                        "import was rejected to avoid corrupting adjacent ARM9 data.";

                var parsedRows = new List<int[]>(RowCount);
                for (int i = 0; i < dataLines.Count; i++)
                {
                    string[] parts = dataLines[i].Split(',');
                    if (parts.Length != FieldsPerRow)
                        return $"Row {i + 1}: expected {FieldsPerRow} columns, found {parts.Length}.";

                    var values = new int[FieldsPerRow];
                    for (int c = 0; c < FieldsPerRow; c++)
                    {
                        if (!int.TryParse(parts[c], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[c]))
                            return $"Row {i + 1}, column {c + 1} ('{parts[c]}') is not a valid integer.";
                    }
                    parsedRows.Add(values);
                }

                Rows.Clear();
                int rowNum = 0;
                foreach (var v in parsedRows)
                {
                    rowNum++;
                    var row = new DungeonCutinRow(Headers)
                    {
                        RowNumber = rowNum,
                        HeaderIndex = v[0],
                        WipeType = v[1],
                        MorningPaletteId = v[2],
                        MorningTilesId = v[3],
                        MorningScreenId = v[4],
                        NoonPaletteId = v[5],
                        NoonTilesId = v[6],
                        NoonScreenId = v[7],
                        EveningPaletteId = v[8],
                        EveningTilesId = v[9],
                        EveningScreenId = v[10],
                        NightPaletteId = v[11],
                        NightTilesId = v[12],
                        NightScreenId = v[13],
                        NameMessageId = v[14],
                    };
                    row.PropertyChanged += (_, __) => { SetDirty(); if (ReferenceEquals(row, SelectedRow)) RefreshPreviews(); };
                    Rows.Add(row);
                }
                SetDirty();
                SelectedRow = Rows.Count > 0 ? Rows[0] : null;
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private void SetDirty() { _dirty = true; Title = "● Dungeon Cutin Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { _dirty = false; Title = "Dungeon Cutin Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private void LoadRows()
        {
            Rows.Clear();
            try
            {
                using var reader = new ARM9.Reader(ResolveTableOffset());
                for (int i = 0; i < RowCount; i++)
                {
                    var row = new DungeonCutinRow(Headers)
                    {
                        RowNumber = i + 1,
                        HeaderIndex = reader.ReadInt32(),
                        WipeType = reader.ReadInt32(),
                        MorningPaletteId = reader.ReadInt32(),
                        MorningTilesId = reader.ReadInt32(),
                        MorningScreenId = reader.ReadInt32(),
                        NoonPaletteId = reader.ReadInt32(),
                        NoonTilesId = reader.ReadInt32(),
                        NoonScreenId = reader.ReadInt32(),
                        EveningPaletteId = reader.ReadInt32(),
                        EveningTilesId = reader.ReadInt32(),
                        EveningScreenId = reader.ReadInt32(),
                        NightPaletteId = reader.ReadInt32(),
                        NightTilesId = reader.ReadInt32(),
                        NightScreenId = reader.ReadInt32(),
                        NameMessageId = reader.ReadInt32(),
                    };
                    row.PropertyChanged += (_, __) => { SetDirty(); if (ReferenceEquals(row, SelectedRow)) RefreshPreviews(); };
                    Rows.Add(row);
                }
                SetClean();
                SelectedRow = Rows.Count > 0 ? Rows[0] : null;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"DungeonCutinEditorViewModel.LoadRows: {ex.Message}");
                StatusText = $"Error loading: {ex.Message}";
            }
        }

        private void WriteRows()
        {
            using var writer = new ARM9.Writer(ResolveTableOffset());
            foreach (var row in Rows)
            {
                writer.Write(row.HeaderIndex);
                writer.Write(row.WipeType);
                writer.Write(row.MorningPaletteId);
                writer.Write(row.MorningTilesId);
                writer.Write(row.MorningScreenId);
                writer.Write(row.NoonPaletteId);
                writer.Write(row.NoonTilesId);
                writer.Write(row.NoonScreenId);
                writer.Write(row.EveningPaletteId);
                writer.Write(row.EveningTilesId);
                writer.Write(row.EveningScreenId);
                writer.Write(row.NightPaletteId);
                writer.Write(row.NightTilesId);
                writer.Write(row.NightScreenId);
                writer.Write(row.NameMessageId);
            }
        }
    }
}
