using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.Models;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapEditorViewModel
    {
        /// <summary>Area data ids of every header that shows the map, the map's own area first.</summary>
        public List<byte> AreasUsingMap(int mapIndex, out int headers)
        {
            List<byte> areas = new List<byte>();
            if (AreaForMap(mapIndex) is byte own) areas.Add(own);
            List<ushort> ids = HeadersUsingMap(mapIndex).Select(h => h.header).Distinct().ToList();
            headers = ids.Count;
            foreach (ushort h in ids)
            {
                try
                {
                    MapHeader header = MapHeader.GetMapHeader(h);
                    if (header != null && !areas.Contains(header.AreaDataID)) areas.Add(header.AreaDataID);
                }
                catch (Exception caught) when (AppLogger.Noted("MapEditorViewModel.AreasUsingMap", caught)) { }   // a header that can't be read is skipped
            }
            return areas;
        }

        public (byte[,] collisions, byte[,] types) PermissionsNow()
            => _map == null ? (null, null) : ((byte[,])_map.Collisions.Clone(), (byte[,])_map.Types.Clone());

        /// <summary>An event an import left on a blocked or water square.</summary>
        public sealed class EventClash
        {
            public string Kind { get; init; }
            public int Index { get; init; }
            public int File { get; init; }
            public int X { get; init; }
            public int Y { get; init; }
            public bool Water { get; init; }
            /// <summary>Why a warp, arrival or edge no longer works, null for other events.</summary>
            public string WarpTrouble { get; init; }
            /// <summary>For a map edge: the squares the row is about.</summary>
            public List<(int x, int y)> Squares { get; init; }
            public string Who => File < 0 ? Kind : $"{Kind} {Index}";
            public string FileText => File < 0 ? "" : File.ToString();
            public string Where => Squares != null ? Plural.Of(Squares.Count, "square") : $"{X},{Y}";
            public string Why => WarpTrouble ?? (Water ? "Water" : "Blocked");
        }

        // Event files changed here, written with the map on Save.
        private readonly Dictionary<int, EventFile> _eventsToSave = new Dictionary<int, EventFile>();

        // Each move made to those files, to redo on a copy another editor saved meanwhile.
        private readonly record struct EventMove(string Kind, int Index, short FromX, short FromY, short ToX, short ToY);
        private readonly Dictionary<int, List<EventMove>> _eventMoves = new Dictionary<int, List<EventMove>>();

        private void MoveEvent(int file, EventFile events, string kind, int index, Event e, short x, short y)
        {
            if (!_eventMoves.TryGetValue(file, out List<EventMove> moves)) _eventMoves[file] = moves = new List<EventMove>();
            moves.Add(new EventMove(kind, index, e.XMapPosition, e.YMapPosition, x, y));
            e.XMapPosition = x;
            e.YMapPosition = y;
            _eventsToSave[file] = events;
        }

        private void ClearPendingEvents()
        {
            _eventsToSave.Clear();
            _eventMoves.Clear();
        }

        private void SavePendingEvents()
        {
            foreach ((int file, EventFile events) in _eventsToSave.ToList())
            {
                events.SaveToFileDefaultDir(file, showSuccessMessage: false);
                AppEvents.RaiseEventFileSaved(this, file);
            }
            ClearPendingEvents();
        }

        private static Event EventAt(EventFile events, string kind, int index)
        {
            IList<Event> list = kind switch
            {
                "Overworld" => events.Overworlds.Cast<Event>().ToList(),
                "Warp" => events.Warps.Cast<Event>().ToList(),
                "Sign/item" => events.Spawnables.Cast<Event>().ToList(),
                _ => events.Triggers.Cast<Event>().ToList(),
            };
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        private void OnEventFileSavedElsewhere(object sender, int file)
        {
            if (ReferenceEquals(sender, this) || !_eventsToSave.ContainsKey(file)) return;
            EventFile fresh;
            try { fresh = new EventFile(file); }
            catch (Exception ex) { AppLogger.Error("Reloading event file after another save failed: " + ex.Message); return; }
            int dropped = 0;
            if (_eventMoves.TryGetValue(file, out List<EventMove> moves))
                foreach (EventMove m in moves)
                {
                    Event e = EventAt(fresh, m.Kind, m.Index);
                    if (e == null || e.XMapPosition != m.FromX || e.YMapPosition != m.FromY) { dropped++; continue; }
                    e.XMapPosition = m.ToX;
                    e.YMapPosition = m.ToY;
                }
            _eventsToSave[file] = fresh;
            StatusText = dropped == 0
                ? $"Event file {file} was saved elsewhere. The events moved here still move on Save."
                : $"Event file {file} was saved elsewhere. {Plural.Of(dropped, "event move")} made here no longer match and were dropped.";
        }

        private HashSet<byte> WaterTypes()
            => new HashSet<byte>(TilePermissions.BehavioursFor(GameFamily).Where(t => t.Surf).Select(t => t.Value));

        private EventFile EventsOf(int file) => _eventsToSave.TryGetValue(file, out EventFile had) ? had : new EventFile(file);

        private static IEnumerable<(string kind, int index, Event e)> AllEvents(EventFile events)
        {
            for (int i = 0; i < events.Overworlds.Count; i++) yield return ("Overworld", i, events.Overworlds[i]);
            for (int i = 0; i < events.Warps.Count; i++) yield return ("Warp", i, events.Warps[i]);
            for (int i = 0; i < events.Spawnables.Count; i++) yield return ("Sign/item", i, events.Spawnables[i]);
            for (int i = 0; i < events.Triggers.Count; i++) yield return ("Trigger", i, events.Triggers[i]);
        }

        // Moves warps under or in front of a building along with it. Written with the map on Save.
        private int MoveWarpsWith(int mapIndex, (double x0, double z0, double x1, double z1) was, int dx, int dz)
        {
            int moved = 0, n = MapFile.MapSize;
            HashSet<(int file, int x, int y)> seen = new HashSet<(int file, int x, int y)>();
            try
            {
                foreach ((ushort h, int mx, int my) in HeadersUsingMap(mapIndex))
                {
                    MapHeader header;
                    try { header = MapHeader.GetMapHeader(h); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.MoveWarpsWith", ex); continue; }
                    if (header == null || !seen.Add((header.EventFileID, mx, my))) continue;
                    EventFile events;
                    try { events = EventsOf(header.EventFileID); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.MoveWarpsWith", ex); continue; }
                    for (int i = 0; i < events.Warps.Count; i++)
                    {
                        Warp w = events.Warps[i];
                        if (w.XMatrixPosition != mx || w.YMatrixPosition != my) continue;
                        double cx = w.XMapPosition + 0.5, cz = w.YMapPosition + 0.5;
                        if (cx < was.x0 || cx > was.x1 || cz < was.z0 || cz > was.z1 + 1) continue;
                        MoveEvent(header.EventFileID, events, "Warp", i, w,
                                  (short)Math.Clamp(w.XMapPosition + dx, 0, n - 1),
                                  (short)Math.Clamp(w.YMapPosition + dz, 0, n - 1));
                        moved++;
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("Moving warps with a building failed: " + ex.Message); }
            return moved;
        }

        // What an import broke: events newly on blocked or water squares, warps, arrivals, edges.
        public List<EventClash> EventsOnUnwalkableSquares((byte[,] collisions, byte[,] types) was)
        {
            List<EventClash> found = new List<EventClash>();
            if (_map == null || _selectedMapIndex < 0) return found;
            HashSet<byte> waterTypes = WaterTypes();
            MapWalkCheck check = new MapWalkCheck(_map.Collisions, _map.Types, was, waterTypes);
            try
            {
                HashSet<(int file, int x, int y)> seen = new HashSet<(int file, int x, int y)>();
                foreach ((ushort h, int x, int y) in HeadersUsingMap(_selectedMapIndex))
                {
                    MapHeader header;
                    try { header = MapHeader.GetMapHeader(h); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.EventsOnUnwalkableSquares", ex); continue; }
                    if (header == null || !seen.Add((header.EventFileID, x, y))) continue;
                    EventFile events;
                    try { events = EventsOf(header.EventFileID); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.EventsOnUnwalkableSquares", ex); continue; }
                    ArrivalsOf(h, header, x, y, events, check, found);
                    foreach ((string kind, int index, Event e) in AllEvents(events))
                    {
                        if (e.XMatrixPosition != x || e.YMatrixPosition != y) continue;
                        int ex = e.XMapPosition, ey = e.YMapPosition;
                        if (ex < 0 || ey < 0 || ex >= MapFile.MapSize || ey >= MapFile.MapSize) continue;
                        bool blocked = (_map.Collisions[ey, ex] & 0x80) != 0 && (was.collisions == null || (was.collisions[ey, ex] & 0x80) == 0);
                        bool water = waterTypes.Contains(_map.Types[ey, ex]) && (was.types == null || !waterTypes.Contains(was.types[ey, ex]));
                        if (kind == "Warp" && check.WarpTrouble(ex, ey, WarpSquares(events, x, y), BehaviourName) is string trouble)
                        {
                            found.Add(new EventClash { Kind = kind, Index = index, File = header.EventFileID, X = ex, Y = ey, WarpTrouble = trouble });
                            continue;
                        }
                        if (blocked || water)
                            found.Add(new EventClash { Kind = kind, Index = index, File = header.EventFileID, X = ex, Y = ey, Water = !blocked });
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("Import event check failed: " + ex.Message); }
            return found;
        }

        // An import can cut off Fly and blackout arrivals, and entries from neighbouring maps.
        private void ArrivalsOf(ushort h, MapHeader header, int mx, int my, EventFile events, MapWalkCheck check, List<EventClash> found)
        {
            int n = MapFile.MapSize;
            HashSet<(int x, int y)> warps = WarpSquares(events, mx, my);
            try
            {
                foreach (SpawnPoints.Point point in RomInfo.FlyTableUnverified ? Enumerable.Empty<SpawnPoints.Point>() : SpawnPoints.Read().Where(pt => pt.Header == h))
                {
                    int px = point.Global ? point.X - mx * n : point.X, pz = point.Global ? point.Z - my * n : point.Z;
                    if (px < 0 || pz < 0 || px >= n || pz >= n) continue;
                    string trouble = !check.IsOpen(px, pz)
                        ? (check.KnowsBefore && !check.WasOpen(px, pz) ? null : "now on a blocked or water square")
                        : check.DeadEnd(px, pz, warps);
                    if (trouble != null)
                        found.Add(new EventClash { Kind = point.Kind, File = -1, X = px, Y = pz, WarpTrouble = trouble + "; change it in the Fly / warp editor" });
                }
            }
            catch (Exception ex) { AppLogger.Error("Import spawn point check failed: " + ex.Message); }

            if (!check.KnowsBefore) return;
            try
            {
                GameMatrix matrix = new GameMatrix(header.MatrixID);
                foreach ((int dx, int dy, string side) in new[] { (0, -1, "North"), (0, 1, "South"), (-1, 0, "West"), (1, 0, "East") })
                {
                    int nx = mx + dx, ny = my + dy;
                    if (nx < 0 || ny < 0 || nx >= matrix.Width || ny >= matrix.Height) continue;
                    int other = matrix.Maps[ny, nx];
                    if (other == 0xFFFF || other == _selectedMapIndex) continue;
                    MapFile beside;
                    try { beside = new MapFile(other, GameFamily, false, false); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.ArrivalsOf", ex); continue; }
                    (List<(int x, int y)> closed, List<(int x, int y)> crossing) = check.EdgeSquares(side, beside.Collisions, beside.Types);
                    string leadsNowhere = crossing.Count == 0 ? null : check.WalkedInFrom(crossing, side, warps);
                    if (closed.Count == 0 && leadsNowhere != null)
                        found.Add(new EventClash
                        {
                            Kind = $"{side} edge to map {other}", File = -1, Squares = crossing,
                            WarpTrouble = $"walking in from map {other}, {leadsNowhere}; paint a way through",
                        });
                    if (closed.Count > 0)
                        found.Add(new EventClash
                        {
                            Kind = $"{side} edge to map {other}", File = -1, Squares = closed,
                            WarpTrouble = $"{Plural.Of(closed.Count, "square")} that led to map {other} {(closed.Count == 1 ? "is" : "are")} now closed: "
                                        + string.Join(" ", closed.Take(8).Select(c => $"{c.x},{c.y}")) + (closed.Count > 8 ? " ..." : "")
                                        + (leadsNowhere != null ? $"; even reopened, {leadsNowhere}" : ""),
                        });
                }
            }
            catch (Exception ex) { AppLogger.Error("Import edge check failed: " + ex.Message); }
        }

        private static HashSet<(int x, int y)> WarpSquares(EventFile events, int mx, int my)
            => new HashSet<(int, int)>(events.Warps.Where(w => w.XMatrixPosition == mx && w.YMatrixPosition == my)
                                                   .Select(w => ((int)w.XMapPosition, (int)w.YMapPosition)));

        private string BehaviourName(byte value) => TilePermissions.BehaviourLabel(value, GameFamily);
    }
}
