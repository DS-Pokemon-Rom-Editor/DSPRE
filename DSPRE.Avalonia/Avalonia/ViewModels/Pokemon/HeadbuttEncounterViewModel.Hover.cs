using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class HeadbuttEncounterViewModel
    {
        // A tree group is one tree: its six slots are the tiles the player can headbutt it from.

        public IReadOnlyList<NsbmdGlControl.HighlightBatch> Highlight { get; private set; }

        private sealed class TreeBox
        {
            public bool Special;
            public int Group;
            public float MinX, MaxX, MinZ, MaxZ, Ground, Top;   // normalized render space; grows to the model found
        }
        private readonly List<TreeBox> _treeBoxes = new List<TreeBox>();

        private void BuildTreeBoxes(NsbmdRenderModel m)
        {
            _treeBoxes.Clear();
            if (m == null || _file == null) return;
            void Add(IList<HeadbuttTreeGroup> groups, bool special)
            {
                for (int g = 0; g < groups.Count; g++)
                {
                    TreeBox box = null;
                    foreach (HeadbuttTree t in groups[g].Trees)
                    {
                        if (t.IsUnused || !m.TryCellPlacement(t.MatrixX, t.MatrixY, out NsbmdRenderModel.CellPlacement p)) continue;
                        float tw = p.Width / MapTiles, th = p.Height / MapTiles;
                        float x0 = p.OriginX + t.MapX * tw, z0 = p.OriginZ + t.MapY * th;
                        float ground = m.SurfaceY(x0 + tw / 2, z0 + th / 2);
                        (float ax, float ay, float az) = m.ToNormalized(x0, ground, z0);
                        (float bx, float _, float bz) = m.ToNormalized(x0 + tw, ground, z0 + th);
                        if (box == null) box = new TreeBox { Special = special, Group = g, MinX = ax, MaxX = bx, MinZ = az, MaxZ = bz, Ground = ay, Top = ay };
                        else
                        {
                            box.MinX = Math.Min(box.MinX, ax); box.MaxX = Math.Max(box.MaxX, bx);
                            box.MinZ = Math.Min(box.MinZ, az); box.MaxZ = Math.Max(box.MaxZ, bz);
                            box.Ground = Math.Min(box.Ground, ay);
                        }
                    }
                    if (box != null) _treeBoxes.Add(box);
                }
            }
            Add(_file.NormalTreeGroups, false);
            Add(_file.SpecialTreeGroups, true);
        }

        // Raised scene triangles grouped into connected pieces, built once per scene.
        private NsbmdRenderModel _piecesOf;
        private int[] _triPart, _triStart, _triPiece;
        private float[] _triCx, _triCz;
        private float[] _pieceMinX, _pieceMaxX, _pieceMinZ, _pieceMaxZ;

        private void BuildPieces(NsbmdRenderModel m)
        {
            if (ReferenceEquals(_piecesOf, m)) return;
            _piecesOf = m;
            float tileN = (m.CellStrideX / MapTiles) * m.Scale;
            float lift = tileN * 0.2f;
            List<int> part = new List<int>(); List<int> start = new List<int>(); List<float> cxs = new List<float>(); List<float> czs = new List<float>();
            List<(long, long, long)> keys = new List<(long, long, long)>();
            for (int pi = 0; pi < m.Parts.Count; pi++)
            {
                float[] a = m.Parts[pi].Vertices;
                int n = Math.Min(m.Parts[pi].VertexCount, a.Length / 8) / 3 * 3;
                for (int v = 0; v < n; v += 3)
                {
                    int i = v * 8;
                    float cx = (a[i] + a[i + 8] + a[i + 16]) / 3f, cz = (a[i + 2] + a[i + 10] + a[i + 18]) / 3f;
                    float top = Math.Max(a[i + 1], Math.Max(a[i + 9], a[i + 17]));
                    // Ground under and around trees stays out, so pieces never join through it.
                    float groundN = (m.SurfaceY(cx / m.Scale + m.Cx, cz / m.Scale + m.Cz) - m.Cy) * m.Scale;
                    if (top < groundN + lift) continue;
                    part.Add(pi); start.Add(i); cxs.Add(cx); czs.Add(cz);
                }
            }
            int count = part.Count;
            int[] parent = new int[count];
            for (int t = 0; t < count; t++) parent[t] = t;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            Dictionary<(long, long, long), int> owner = new Dictionary<(long, long, long), int>();
            for (int t = 0; t < count; t++)
            {
                float[] a = m.Parts[part[t]].Vertices;
                for (int k = 0; k < 3; k++)
                {
                    int o = start[t] + k * 8;
                    (long, long, long) key = ((long)Math.Round(a[o] * 4096), (long)Math.Round(a[o + 1] * 4096), (long)Math.Round(a[o + 2] * 4096));
                    if (owner.TryGetValue(key, out int other)) { int ra = Find(t), rb = Find(other); if (ra != rb) parent[ra] = rb; }
                    else owner[key] = t;
                }
            }
            _triPart = part.ToArray(); _triStart = start.ToArray(); _triCx = cxs.ToArray(); _triCz = czs.ToArray();
            _triPiece = new int[count];
            Dictionary<int, int> ids = new Dictionary<int, int>();
            for (int t = 0; t < count; t++)
            {
                int r = Find(t);
                if (!ids.TryGetValue(r, out int id)) ids[r] = id = ids.Count;
                _triPiece[t] = id;
            }
            _pieceMinX = new float[ids.Count]; _pieceMaxX = new float[ids.Count]; _pieceMinZ = new float[ids.Count]; _pieceMaxZ = new float[ids.Count];
            for (int q = 0; q < ids.Count; q++) { _pieceMinX[q] = _pieceMinZ[q] = float.MaxValue; _pieceMaxX[q] = _pieceMaxZ[q] = float.MinValue; }
            for (int t = 0; t < count; t++)
            {
                float[] a = m.Parts[part[t]].Vertices;
                int q = _triPiece[t];
                for (int k = 0; k < 3; k++)
                {
                    int o = start[t] + k * 8;
                    _pieceMinX[q] = Math.Min(_pieceMinX[q], a[o]); _pieceMaxX[q] = Math.Max(_pieceMaxX[q], a[o]);
                    _pieceMinZ[q] = Math.Min(_pieceMinZ[q], a[o + 2]); _pieceMaxZ[q] = Math.Max(_pieceMaxZ[q], a[o + 2]);
                }
            }
        }

        // Tints each tree's model, the selected group strongly and the rest faintly.
        private void BuildTreeHighlight(NsbmdRenderModel m)
        {
            Highlight = null;
            BuildTreeBoxes(m);
            if (m == null || _treeBoxes.Count == 0) return;
            BuildPieces(m);

            float tileN = (m.CellStrideX / MapTiles) * m.Scale;
            float maxSpan = tileN * 4f;   // bigger pieces are cliffs, fences or rows of buildings; thinner ones are edges
            Dictionary<int, TreeBox> pieceOwner = new Dictionary<int, TreeBox>();
            // The selected group claims first, so a piece two groups share shows as selected.
            // A tree's listed tiles can be just its bottom row, so the box widens up to a tile to match.
            foreach (TreeBox box in _treeBoxes.OrderByDescending(b => b.Special == _specialGroupActive && b.Group == _selGroup))
                foreach (float margin in new[] { 0f, tileN * 0.5f, tileN })
                {
                    bool found = false;
                    for (int t = 0; t < _triPiece.Length; t++)
                    {
                        if (_triCx[t] < box.MinX - margin || _triCx[t] > box.MaxX + margin
                            || _triCz[t] < box.MinZ - margin || _triCz[t] > box.MaxZ + margin) continue;
                        int q = _triPiece[t];
                        if (_pieceMaxX[q] - _pieceMinX[q] > maxSpan || _pieceMaxZ[q] - _pieceMinZ[q] > maxSpan
                            || _pieceMaxX[q] - _pieceMinX[q] < tileN * 0.5f || _pieceMaxZ[q] - _pieceMinZ[q] < tileN * 0.5f) continue;
                        if (pieceOwner.TryGetValue(q, out TreeBox had)) { found |= had == box; continue; }
                        pieceOwner[q] = box;
                        found = true;
                    }
                    if (found) break;
                }

            Dictionary<(int mat, bool special, bool picked), List<float>> batches = new Dictionary<(int mat, bool special, bool picked), List<float>>();
            for (int t = 0; t < _triPiece.Length; t++)
            {
                if (!pieceOwner.TryGetValue(_triPiece[t], out TreeBox box)) continue;
                NsbmdMeshPart p = m.Parts[_triPart[t]];
                bool picked = box.Special == _specialGroupActive && box.Group == _selGroup;
                (int MaterialIndex, bool Special, bool picked) key = (p.MaterialIndex, box.Special, picked);
                if (!batches.TryGetValue(key, out List<float> v)) batches[key] = v = new List<float>();
                float[] a = p.Vertices;
                for (int k = 0; k < 24; k++) v.Add(a[_triStart[t] + k]);
                for (int k = 0; k < 3; k++)
                {
                    int o = _triStart[t] + k * 8;
                    box.Top = Math.Max(box.Top, a[o + 1]);
                    box.MinX = Math.Min(box.MinX, a[o]); box.MaxX = Math.Max(box.MaxX, a[o]);
                    box.MinZ = Math.Min(box.MinZ, a[o + 2]); box.MaxZ = Math.Max(box.MaxZ, a[o + 2]);
                }
            }
            List<NsbmdGlControl.HighlightBatch> list = new List<NsbmdGlControl.HighlightBatch>();
            foreach (KeyValuePair<(int mat, bool special, bool picked), List<float>> kv in batches)
            {
                (int mat, bool special, bool picked) = kv.Key;
                (float, float, float) col = special
                    ? (picked ? (2.2f, 1.7f, 0.2f) : (1.5f, 1.2f, 0.4f))
                    : (picked ? (0.5f, 2.0f, 0.5f) : (0.6f, 1.45f, 0.6f));
                list.Add(new NsbmdGlControl.HighlightBatch { MaterialKey = mat, Mesh = kv.Value.ToArray(), R = col.Item1, G = col.Item2, B = col.Item3 });
            }
            Highlight = list;
        }

        /// <summary>The tree a ray (normalized render space) hits first, or null.</summary>
        public (bool Special, int Group)? TreeAlong(float ox, float oy, float oz, float dx, float dy, float dz)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null) return null;
            float tileN = (m.CellStrideX / MapTiles) * m.Scale;
            TreeBox best = null;
            float bestT = float.MaxValue;
            foreach (TreeBox b in _treeBoxes)
            {
                float top = Math.Max(b.Top, b.Ground + tileN * 1.5f);
                if (RayHitsBox(ox, oy, oz, dx, dy, dz, b.MinX, b.Ground, b.MinZ, b.MaxX, top, b.MaxZ, out float t) && t < bestT)
                { bestT = t; best = b; }
            }
            return best == null ? null : (best.Special, best.Group);
        }

        private static bool RayHitsBox(float ox, float oy, float oz, float dx, float dy, float dz,
            float x0, float y0, float z0, float x1, float y1, float z1, out float tNear)
        {
            tNear = float.MinValue;
            float tFar = float.MaxValue;
            bool Slab(float o, float d, float lo, float hi, ref float n, ref float f)
            {
                if (Math.Abs(d) < 1e-9f) return o >= lo && o <= hi;
                float a = (lo - o) / d, b = (hi - o) / d;
                if (a > b) (a, b) = (b, a);
                n = Math.Max(n, a); f = Math.Min(f, b);
                return n <= f;
            }
            return Slab(ox, dx, x0, x1, ref tNear, ref tFar) && Slab(oy, dy, y0, y1, ref tNear, ref tFar)
                && Slab(oz, dz, z0, z1, ref tNear, ref tFar) && tFar >= 0;
        }

        public sealed class HoverSlot
        {
            public Bitmap Icon { get; init; }
            public string Name { get; init; }
            public string Levels { get; init; }
            public string Chance { get; init; }
        }

        public sealed class HoverTable
        {
            public string Header { get; init; }
            public List<HoverSlot> Slots { get; init; }
        }

        public sealed class HoverCard
        {
            public string Title { get; init; }
            public string Rule { get; init; }
            public List<HoverTable> Tables { get; init; }
        }

        private readonly Dictionary<int, Bitmap> _iconCache = new Dictionary<int, Bitmap>();

        private Bitmap IconOf(int species)
        {
            if (species <= 0) return null;
            if (_iconCache.TryGetValue(species, out Bitmap icon)) return icon;
            try { icon = ImageConverter.ToAvaloniaBitmap(DSUtils.GetPokePicRaw(species, 32, 32)); }
            catch (Exception ex) { AppLogger.WarnOnce("HeadbuttEncounterViewModel.IconOf", ex); icon = null; }
            return _iconCache[species] = icon;
        }

        private HoverTable TableOf(string header, IList<HeadbuttEncounter> slots, int first)
        {
            // Slots holding the same Pokémon at the same levels add up, as the player sees them.
            List<(ushort id, byte lo, byte hi)> order = new List<(ushort id, byte lo, byte hi)>();
            Dictionary<(ushort, byte, byte), int> chance = new Dictionary<(ushort, byte, byte), int>();
            for (int s = 0; s < HeadbuttRules.SlotsPerTable && first + s < slots.Count; s++)
            {
                HeadbuttEncounter e = slots[first + s];
                if (e.PokemonID == 0) continue;
                (ushort pokemonID, byte minLevel, byte maxLevel) key = (e.PokemonID, e.MinLevel, e.MaxLevel);
                if (!chance.ContainsKey(key)) { chance[key] = 0; order.Add(key); }
                chance[key] += (EncounterSlotOdds.CurrentPercents("Headbutt") ?? HeadbuttRules.SlotChance)[s];
            }
            List<HoverSlot> rows = new List<HoverSlot>();
            foreach ((ushort id, byte lo, byte hi) in order)
                rows.Add(new HoverSlot
                {
                    Icon = IconOf(id),
                    Name = id < Species.Count ? Species[id] : $"#{id}",
                    Levels = lo == hi ? $"Lv {lo}" : $"Lv {lo}-{hi}",
                    Chance = $"{chance[(id, lo, hi)]}%",
                });
            return new HoverTable { Header = header, Slots = rows };
        }

        /// <summary>What headbutting a tree can find, for the view's hover card.</summary>
        public HoverCard CardFor(bool special, int group)
        {
            if (_file == null) return null;
            if (special)
            {
                if (group < 0 || group >= _file.SpecialTreeGroups.Count) return null;
                return new HoverCard
                {
                    Title = $"Special tree {group}",
                    Rule = "Same for every trainer ID.",
                    Tables = new List<HoverTable> { TableOf("Secret", _file.SpecialEncounters, 0) },
                };
            }
            int count = _file.NormalTreeGroups.Count;
            if (group < 0 || group >= count) return null;
            Dictionary<HeadbuttRules.Table, List<int>> byTable = new Dictionary<HeadbuttRules.Table, List<int>>();
            for (int digit = 0; digit <= 9; digit++)
            {
                HeadbuttRules.Table table = HeadbuttRules.NormalTreeTable(group, count, digit);
                if (!byTable.TryGetValue(table, out List<int> digits)) byTable[table] = digits = new List<int>();
                digits.Add(digit);
            }
            string Ends(HeadbuttRules.Table t) => byTable.TryGetValue(t, out List<int> d) ? string.Join(", ", d) : null;
            List<string> parts = new List<string>();
            if (Ends(HeadbuttRules.Table.Common) is string c) parts.Add($"Common: ID key {c}");
            if (Ends(HeadbuttRules.Table.Rare) is string r) parts.Add($"Rare: ID key {r}");
            if (Ends(HeadbuttRules.Table.None) is string n) parts.Add($"Nothing: ID key {n}");
            List<HoverTable> tables = new List<HoverTable>();
            if (byTable.ContainsKey(HeadbuttRules.Table.Common)) tables.Add(TableOf("Common", _file.NormalEncounters, 0));
            if (byTable.ContainsKey(HeadbuttRules.Table.Rare)) tables.Add(TableOf("Rare", _file.NormalEncounters, HeadbuttRules.SlotsPerTable));
            return new HoverCard { Title = $"Normal tree {group}", Rule = string.Join("\n", parts), Tables = tables };
        }

        private static bool TreeRaw(NsbmdRenderModel m, HeadbuttTree t, out float rx, out float rz)
        {
            rx = rz = 0f;
            if (!m.TryCellPlacement(t.MatrixX, t.MatrixY, out NsbmdRenderModel.CellPlacement p)) return false;
            rx = p.OriginX + (t.MapX + 0.5f) / MapTiles * p.Width;
            rz = p.OriginZ + (t.MapY + 0.5f) / MapTiles * p.Height;
            return true;
        }

        private static void AddMarkerQuad(List<float> v, NsbmdRenderModel m, float cx, float cy, float cz, float half, (float r, float g, float b) col)
        {
            (float x, float y, float z) a = m.ToNormalized(cx - half, cy, cz - half);
            (float x, float y, float z) b = m.ToNormalized(cx + half, cy, cz - half);
            (float x, float y, float z) c = m.ToNormalized(cx + half, cy, cz + half);
            (float x, float y, float z) d = m.ToNormalized(cx - half, cy, cz + half);
            void P((float x, float y, float z) q) { v.Add(q.x); v.Add(q.y); v.Add(q.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            P(a); P(b); P(c); P(a); P(c); P(d);
        }
    }
}
