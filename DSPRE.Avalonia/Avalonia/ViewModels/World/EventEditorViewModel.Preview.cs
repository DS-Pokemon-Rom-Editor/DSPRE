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
        public EventFile Events => _file;

        public int CameraId
        {
            get
            {
                try { return _headerId < 0 ? 0 : MapHeader.GetMapHeader((ushort)_headerId).CameraAngleID; }
                catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.CameraId", ex); return 0; }
            }
        }

        public int MusicDayId => HeaderValue(h => h.MusicDayID);
        public int MusicNightId => HeaderValue(h => h.MusicNightID);

        private int HeaderValue(Func<MapHeader, int> pick)
        {
            try { return _headerId < 0 ? 0 : pick(MapHeader.GetMapHeader((ushort)_headerId)); }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.HeaderValue", ex); return 0; }
        }

        public IReadOnlyList<ScriptAction> ActionsFor(int movementNumber)
        {
            try
            {
                MapHeader header = _headerId < 0 ? null : MapHeader.GetMapHeader((ushort)_headerId);
                if (header == null) return null;
                return ScriptWalker.ActionsById(new ScriptFile(header.ScriptFileID)?.AllActions)?.Invoke(movementNumber);
            }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.ActionsFor", ex); return null; }
        }

        /// <summary>The scene's area, which picks its terrain animation.</summary>
        public AreaData Area
        {
            get { try { return new AreaData(_areaDataId); } catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.Area", ex); return null; } }
        }

        /// <summary>Where this file's maps are closed off, for the preview to walk against.</summary>
        public MapCollisionGrid Collision
        {
            get
            {
                MapCollisionGrid grid = new MapCollisionGrid();
                if (_matrix == null) return grid;
                foreach ((int x, int y) in (HeaderCells() ?? (ISet<(int x, int y)>)EventCells()))
                {
                    long key = ((long)y << 32) | (uint)x;
                    byte[,] col = null, types = null;
                    try
                    {
                        int map = _matrix.Maps[y, x];
                        if (map != GameMatrix.EMPTY)
                        {
                            MapFile mf = new MapFile(map, GameFamily, false, false);
                            col = mf.Collisions;
                            types = mf.Types;
                        }
                    }
                    catch (Exception caught) when (AppLogger.Noted("EventEditorViewModel.Collision", caught)) { }   // a map that can't be read has no collisions to show
                    _collisionCache[key] = col;
                    grid.Add(x, y, col);
                    grid.AddTypes(x, y, types);
                }
                return grid;
            }
        }

        private string _hoverCoords = "";

        /// <summary>The tile under the pointer, with its cell, since several maps are stitched.</summary>
        public string HoverCoordsText { get => _hoverCoords; private set => Set(ref _hoverCoords, value); }

        public void UpdateHoverCoords(double px, double py,
                                      Func<float, float, float, (bool ok, float sx, float sy)> project)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || project == null) { HoverCoordsText = ""; return; }

            List<FieldTilePicker.CellQuad> cells = HoverCells();
            if (cells.Count == 0) { HoverCoordsText = ""; return; }

            (bool, float, float) ProjectRaw(float rawX, float rawZ)
            {
                (float nx, float ny, float nz) = m.ToNormalized(rawX, m.SurfaceY(rawX, rawZ), rawZ);
                return project(nx, ny, nz);
            }

            if (!FieldTilePicker.TileAtScreen(cells, ProjectRaw, px, py,
                                              out int cellX, out int cellY, out int col, out int row, MapTiles))
            {
                HoverCoordsText = "";
                return;
            }

            // Global is what an event stores; local is that tile within its own map.
            HoverCoordsText = $"Matrix {cellX}, {cellY}    Local {col}, {row}    Global "
                            + $"{cellX * MapTiles + col}, {cellY * MapTiles + row}";
        }

        public void ClearHoverCoords() => HoverCoordsText = "";

        private List<FieldTilePicker.CellQuad> HoverCells()
        {
            List<FieldTilePicker.CellQuad> list = new List<FieldTilePicker.CellQuad>();
            NsbmdRenderModel m = Model3D;
            if (m == null) return list;

            ISet<(int x, int y)> cells = HeaderCells() ?? (ISet<(int x, int y)>)EventCells();
            if (cells == null) return list;

            foreach ((int x, int y) in cells)
            {
                if (m.TryCellPlacement(x, y, out NsbmdRenderModel.CellPlacement p))
                    list.Add(new FieldTilePicker.CellQuad(x, y, p.OriginX, p.OriginZ, p.Width, p.Height));
                else if (m.CellStrideX != 0)
                    list.Add(new FieldTilePicker.CellQuad(x, y,
                        m.CellBaseX + x * m.CellStrideX, m.CellBaseZ + y * m.CellStrideZ,
                        m.CellStrideX, m.CellStrideZ));
            }
            return list;
        }

        public (float x, float y, float z) TileFoot(float tileX, float tileZ)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null) return (0f, 0f, 0f);

            // Whole tiles pick the cell; the fraction is how far across it the walker has got.
            int cellX = (int)Math.Floor(tileX / MapTiles), cellZ = (int)Math.Floor(tileZ / MapTiles);
            float inX = tileX - cellX * MapTiles, inZ = tileZ - cellZ * MapTiles;

            float rawX, rawZ;
            if (m.TryCellPlacement(cellX, cellZ, out NsbmdRenderModel.CellPlacement p))
            {
                rawX = p.OriginX + (inX + 0.5f) / MapTiles * p.Width;
                rawZ = p.OriginZ + (inZ + 0.5f) / MapTiles * p.Height;
            }
            else
            {
                rawX = m.CellBaseX + (cellX + (inX + 0.5f) / MapTiles) * m.CellStrideX;
                rawZ = m.CellBaseZ + (cellZ + (inZ + 0.5f) / MapTiles) * m.CellStrideZ;
            }

            float rawY = m.TryBdhcSurfaceY(cellX, cellZ, rawX, rawZ, 0f, out float y) ? y : m.SurfaceY(rawX, rawZ);
            return m.ToNormalized(rawX, rawY, rawZ);
        }

        /// <summary>A walker for a script this header can run, with its text archive.</summary>
        public ScriptWalker WalkerFor(int scriptNumber)
        {
            try
            {
                // Ids above 2000 live in shared files, counted from their range's start (SetScriptDataSub).
                CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, scriptNumber);

                int scriptArchive, textArchive;
                if (common.Kind == CommonScriptId.Kind.Resolved)
                {
                    scriptArchive = common.ScriptArchiveId;
                    textArchive = common.TextArchiveId;
                }
                else
                {
                    MapHeader header = MapHeader.GetMapHeader((ushort)_headerId);
                    if (header == null) return null;
                    scriptArchive = header.ScriptFileID;
                    textArchive = header.TextArchiveID;
                }

                ScriptFile file = new ScriptFile(scriptArchive);
                TextArchive text = null;
                try { text = new TextArchive(textArchive); } catch (Exception ex) { AppLogger.Warn("Event editor: script text: " + ex.Message); }

                return new ScriptWalker(file.AllScripts, file.AllFunctions,
                    id => text?.Messages != null && id >= 0 && id < text.Messages.Count ? text.Messages[id] : null,
                    ScriptWalker.ActionsById(file.AllActions));
            }
            catch (Exception ex) { AppLogger.Error("Script walker failed: " + ex.Message); return null; }
        }

        private readonly Dictionary<int, TextArchive> _previewArchives = new Dictionary<int, TextArchive>();
        private readonly Dictionary<int, ScriptSource> _previewCommonScripts = new Dictionary<int, ScriptSource>();

        private string PreviewMessage(int archive, int id)
        {
            if (archive < 0) return null;
            try
            {
                if (!_previewArchives.TryGetValue(archive, out TextArchive text))
                    _previewArchives[archive] = text = new TextArchive(archive);
                return text?.Messages != null && id >= 0 && id < text.Messages.Count ? text.Messages[id] : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.PreviewMessage", ex); _previewArchives[archive] = null; return null; }
        }

        public void ConfigureScriptPreview(ViewModels.Battle.AnimatedPreviewViewModel preview)
        {
            if (preview == null) return;
            _previewArchives.Clear();
            _previewCommonScripts.Clear();

            preview.Family = GameFamily;
            preview.ArchiveText = PreviewMessage;
            preview.MenuText = id => PreviewMessage(FieldMenuEntriesArchive, id);
            preview.SharedArchive = which =>
                which >= 0 && which < RomInfo.FieldSharedMessageArchives.Length ? RomInfo.FieldSharedMessageArchives[which] : -1;
            preview.CommonScripts = id =>
            {
                if (_previewCommonScripts.TryGetValue(id, out ScriptSource cached)) return cached;
                ScriptSource source = null;
                try
                {
                    CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, id);
                    if (common.Kind == CommonScriptId.Kind.Resolved)
                    {
                        ScriptFile file = new ScriptFile(common.ScriptArchiveId);
                        int textArchive = common.TextArchiveId;
                        source = new ScriptSource
                        {
                            Scripts = file.AllScripts,
                            Functions = file.AllFunctions,
                            Messages = m => PreviewMessage(textArchive, m),
                            Actions = ScriptWalker.ActionsById(file.AllActions),
                            StartId = common.ManualUserId,
                        };
                    }
                }
                catch (Exception ex) { AppLogger.Error("Shared script for the preview failed: " + ex.Message); }
                _previewCommonScripts[id] = source;
                return source;
            };
        }

        public int WalkerStartId(int scriptNumber)
        {
            CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, scriptNumber);
            return common.Kind == CommonScriptId.Kind.Resolved ? common.ManualUserId : scriptNumber;
        }

        /// <summary>Which file a script id really lives in.</summary>
        public string ScriptHome(int scriptNumber)
        {
            CommonScriptId.Result common = CommonScriptId.Resolve(GameFamily, scriptNumber);
            switch (common.Kind)
            {
                case CommonScriptId.Kind.Resolved:
                    return $"Script {scriptNumber} is script {common.ManualUserId} in shared script file {common.ScriptArchiveId}.";
                default:
                    return null;
            }
        }

        /// <summary>Where an event's feet sit in the scene, the same placement the markers use.</summary>
        public (float x, float y, float z) EventFoot(Event e)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || e == null) return (0f, 0f, 0f);
            (float rx, float rz) = EventCellRaw(m, e);
            return m.ToNormalized(rx, EventSurfaceY(m, rx, rz, e), rz);
        }
    }
}
