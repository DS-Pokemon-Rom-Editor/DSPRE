using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class EventEditorViewModel
    {
        public void Save()
        {
            if (_file == null || _selectedIndex < 0) return;
            _archive.Save(_selectedIndex, _file.ToByteArray());
            SetClean();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved event file {_selectedIndex}.";
            AppEvents.RaiseEventFileSaved(this, _selectedIndex);
        }

        /// <summary>Imports events: all into an empty file, otherwise the ones choose picks.</summary>
        public async Task ImportAsync(Func<EventFile, Task<EventImportChoice>> choose)
        {
            if (_selectedIndex < 0 || _file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import event file", new[] { filter });
            if (path == null) return;
            EventFile source;
            try { using (FileStream fs = PlainFiles.OpenRead(path)) source = new EventFile(fs); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Event editor");
                return;
            }

            EventImportChoice choice;
            if (EventCount(source) == 0)
            {
                if (EventCount(_file) == 0) { StatusText = "That file has no events."; return; }
                if (!await DialogHelper.AskYesNoAsync("That file has no events. Importing it empties this one.", "Import events", _owner)) return;
                choice = EventImportChoice.All(source, clearFirst: true);
            }
            else if (EventCount(_file) == 0) choice = EventImportChoice.All(source, clearFirst: false);
            else
            {
                choice = await choose(source);
                if (choice == null) return;
            }

            ClearSelection();
            if (choice.ClearFirst) { _file.Overworlds.Clear(); _file.Warps.Clear(); _file.Triggers.Clear(); _file.Spawnables.Clear(); }
            int renumbered = 0;
            foreach (int i in choice.Overworlds)
            {
                Overworld ow = source.Overworlds[i];
                // Scripts and hide flags find an overworld by its id, so two can't share one.
                if (_file.Overworlds.Any(o => o.OwID == ow.OwID)) { ow.OwID = (ushort)FreeOverworldId(); renumbered++; }
                _file.Overworlds.Add(ow);
            }
            foreach (int i in choice.Warps) _file.Warps.Add(source.Warps[i]);
            foreach (int i in choice.Triggers) _file.Triggers.Add(source.Triggers[i]);
            foreach (int i in choice.Spawnables) _file.Spawnables.Add(source.Spawnables[i]);
            RefreshLists(); Dirty(); RefreshMarkers();
            int count = choice.Overworlds.Length + choice.Warps.Length + choice.Triggers.Length + choice.Spawnables.Length;
            SaveNotice.Imported($"{count} events", path);
            if (renumbered > 0) StatusText = $"{Plural.Of(renumbered, "overworld")} got a new ID.";
        }

        private static int EventCount(EventFile f) =>
            (f.Overworlds?.Count ?? 0) + (f.Warps?.Count ?? 0) + (f.Triggers?.Count ?? 0) + (f.Spawnables?.Count ?? 0);

        // The previous events must not stay editable through the shared boxes or the gizmo.
        private void ClearSelection()
        {
            _current = null; _spawn = null; _ow = null; _warp = null; _trig = null;
            _selSpawn = _selOw = _selWarp = _selTrig = -1;
            foreach (string n in new[] { nameof(HasSelectedEvent), nameof(HasSpawn), nameof(HasOw), nameof(HasWarp), nameof(HasTrig),
                                      nameof(SelectedSpawnableIndex), nameof(SelectedOverworldIndex), nameof(SelectedWarpIndex), nameof(SelectedTriggerIndex) })
                OnPropertyChanged(n);
        }

        /// <summary>Cell placements and event positions as text, to check stitching by eye.</summary>
        public string BuildDebugReport()
        {
            StringBuilder sb = new System.Text.StringBuilder();
            NsbmdRenderModel m = Model3D;
            sb.AppendLine("=== DSPRE Event editor 3D debug dump ===");
            sb.AppendLine($"Timestamp:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Event file:  {_selectedIndex}");
            sb.AppendLine($"Matrix id:   {_matrixId}    Area data: {_areaDataId}");
            sb.AppendLine($"Stitch mode: {(_stitchGrid ? "Grid" : "Continuous")}");
            if (_matrix != null) sb.AppendLine($"Matrix size: {_matrix.Width} x {_matrix.Height}");
            sb.AppendLine("Units: 1 matrix cell = 32 tiles; Grid stride = 8.0 raw units (0.25/tile).");
            sb.AppendLine();

            if (m == null) { sb.AppendLine("(No 3D model is currently built.)"); return sb.ToString(); }

            sb.AppendLine("--- Model bounds (raw space) ---");
            sb.AppendLine($"Scale={m.Scale:F4}  Center=({m.Cx:F3}, {m.Cy:F3}, {m.Cz:F3})");
            sb.AppendLine($"Raw bounds  X[{m.RawMinX:F3}, {m.RawMaxX:F3}]  Y[{m.RawMinY:F3}, {m.RawMaxY:F3}]  Z[{m.RawMinZ:F3}, {m.RawMaxZ:F3}]");
            sb.AppendLine($"Map  bounds X[{m.MapMinX:F3}, {m.MapMaxX:F3}]  Z[{m.MapMinZ:F3}, {m.MapMaxZ:F3}]");
            sb.AppendLine($"Representative CellStride X={m.CellStrideX:F3} Z={m.CellStrideZ:F3}");
            sb.AppendLine($"DefaultSurfaceY (fallback) = {m.DefaultSurfaceY:F3}");
            sb.AppendLine($"BDHC cells: {m.CellBdhc?.Count ?? 0}");
            sb.AppendLine();

            sb.AppendLine("--- Per-cell map placement (raw space) ---");
            sb.AppendLine("  cell    map | originX  originZ |  width  height | rightGap bottomGap   (gap<0 = OVERLAP)");
            if (_matrix != null && m.CellPlacements != null)
            {
                for (int cy = 0; cy < _matrix.Height; cy++)
                    for (int cx = 0; cx < _matrix.Width; cx++)
                    {
                        int map; try { map = _matrix.Maps[cy, cx]; } catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.BuildDebugReport", ex); continue; }
                        if (map == GameMatrix.EMPTY) continue;
                        if (!m.TryCellPlacement(cx, cy, out NsbmdRenderModel.CellPlacement p)) { sb.AppendLine($"({cx,2},{cy,2}) {map,5} | (no placement in scene)"); continue; }
                        string rg = m.TryCellPlacement(cx + 1, cy, out NsbmdRenderModel.CellPlacement pr) ? (pr.OriginX - (p.OriginX + p.Width)).ToString("F3") : "  -";
                        string bg = m.TryCellPlacement(cx, cy + 1, out NsbmdRenderModel.CellPlacement pb) ? (pb.OriginZ - (p.OriginZ + p.Height)).ToString("F3") : "  -";
                        sb.AppendLine($"({cx,2},{cy,2}) {map,5} | {p.OriginX,7:F3} {p.OriginZ,7:F3} | {p.Width,6:F3} {p.Height,6:F3} | {rg,8} {bg,9}");
                    }
            }
            sb.AppendLine();

            sb.AppendLine("--- Events (exact computed world position) ---");
            sb.AppendLine("type        idx | matrix  map(x,y) | z(fixed) yHint(raw) |   rawX    rawZ  surfaceY source");
            if (_file != null)
            {
                void Dump(string type, System.Collections.IEnumerable list)
                {
                    int i = 0;
                    foreach (Event e in list)
                    {
                        (float rx, float rz) = EventCellRaw(m, e);
                        float yHint = HeightHint(e);
                        bool bdhc = m.TryBdhcSurfaceY(e.XMatrixPosition, e.YMatrixPosition, rx, rz, yHint, out float sy);
                        if (!bdhc) sy = EventSurfaceY(m, rx, rz, e);
                        sb.AppendLine($"{type,-10} {i,3} | ({e.XMatrixPosition},{e.YMatrixPosition})  ({e.XMapPosition,2},{e.YMapPosition,2}) | {e.ZPosition,8} {yHint,7:F3} | {rx,7:F3} {rz,7:F3} {sy,8:F3} {(bdhc ? "bdhc" : "mesh")}");
                        i++;
                    }
                }
                Dump("overworld", _file.Overworlds);
                Dump("warp", _file.Warps);
                Dump("trigger", _file.Triggers);
                Dump("spawnable", _file.Spawnables);
            }

            return sb.ToString();
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export event file", new[] { filter }, $"event_{_selectedIndex:D4}.ev");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _file.ToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("EventEditorViewModel.ExportAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Event editor"); }
        }
    }
}
