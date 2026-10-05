using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Plays the buildings of a built scene the way the field does: sliding and swapped textures, colour fades, moving
    /// joints (windmills) and whole-model motion. Each building names its own animations, so an animation is matched
    /// only against that building's own materials. Doors and time-of-day animations wait for something to start them,
    /// so they are counted but not played. Shared by the map's animated preview and the header's field view.
    /// </summary>
    public sealed class SceneBuildingAnimator
    {
        /// <summary>One frame's state, keyed by scene material, ready for the GL control; null where nothing applies.</summary>
        public sealed class Frame
        {
            public Dictionary<int, float[]> TextureMatrices;
            public Dictionary<int, string> TextureSwaps;
            public Dictionary<int, float> MaterialFades;
            public Dictionary<int, float[]> MovedParts;
        }

        private readonly NsbmdRenderModel _scene;
        private readonly Dictionary<int, (TextureSrtAnimation anim, int material, int mode)> _scrolled = new();
        private readonly Dictionary<int, (TexturePatternAnimation anim, int material)> _swapped = new();
        private readonly Dictionary<int, (MaterialColourAnimation anim, int material)> _faded = new();
        private readonly List<(NsbmdRenderModel.BuildingMaterials building, JointAnimation anim)> _jointed = new();
        private readonly List<(NsbmdRenderModel.BuildingMaterials building, BuildingAnimationSet.WholeModelMotion motion)> _moving = new();
        private readonly Dictionary<(int key, int x, int y, int z), Dictionary<int, float[]>> _movedCache = new();

        public int MovingBuildings { get; private set; }
        public int JointBuildings { get; private set; }
        public int ColourBuildings { get; private set; }
        public int DoorBuildings { get; private set; }
        public int TimeBuildings { get; private set; }
        public int ScrolledSurfaces => _scrolled.Count;

        /// <summary>Whether any building in the scene animates by itself.</summary>
        public bool HasAnything => _scrolled.Count > 0 || _swapped.Count > 0 || _faded.Count > 0 || _jointed.Count > 0 || _moving.Count > 0;

        /// <param name="taken">Scene materials something else already drives (the terrain), left alone here.</param>
        public SceneBuildingAnimator(NsbmdRenderModel scene, bool indoor, FieldTimeZone timeOfDay, ICollection<int> taken = null)
        {
            _scene = scene;
            if (scene?.Buildings == null) return;
            foreach (NsbmdRenderModel.BuildingMaterials b in scene.Buildings)
            {
                bool moves = false, counted = false;
                (bool door, bool time) = BuildingAnimationSet.WaitsFor(b.ModelId, indoor);
                if (door) DoorBuildings++;
                if (time) TimeBuildings++;
                foreach (TextureSrtAnimation anim in BuildingAnimationSet.ScrollingFor(b.ModelId, indoor, timeOfDay))
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (_scrolled.ContainsKey(k) || taken?.Contains(k) == true) continue;
                        if (!scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = anim.IndexOf(name);
                        if (m < 0 || anim.IsStatic(m)) continue;
                        _scrolled[k] = (anim, m, b.Model?.texMtxMode ?? 0);
                        moves = true;
                    }
                foreach (TexturePatternAnimation anim in BuildingAnimationSet.PatternsFor(b.ModelId, indoor, timeOfDay))
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (_swapped.ContainsKey(k)) continue;
                        if (!scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = anim.IndexOf(name);
                        if (m < 0 || anim.IsStatic(m)) continue;
                        _swapped[k] = (anim, m);
                        moves = true;
                    }
                if (moves) MovingBuildings++;
                foreach (MaterialColourAnimation fade in BuildingAnimationSet.FadesFor(b.ModelId, indoor, timeOfDay))
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (_faded.ContainsKey(k)) continue;
                        if (!scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = fade.IndexOf(name);
                        if (m < 0 || fade.IsStatic(m)) continue;
                        _faded[k] = (fade, m);
                        moves = true;
                        counted = true;
                    }
                if (counted) ColourBuildings++;
                foreach (JointAnimation joint in BuildingAnimationSet.JointsFor(b.ModelId, indoor, timeOfDay))
                {
                    _jointed.Add((b, joint));
                    JointBuildings++;
                    moves = true;
                }
                BuildingAnimationSet.WholeModelMotion motion = BuildingAnimationSet.MotionFor(b.ModelId);
                if (motion != null)
                {
                    _moving.Add((b, motion));
                    if (!moves) MovingBuildings++;
                }
            }
        }

        /// <summary>The buildings' state at a field frame (30 a second).</summary>
        public Frame At(int frame)
        {
            Frame f = new();
            if (_swapped.Count > 0)
            {
                Dictionary<int, string> swaps = new(_swapped.Count);
                foreach (KeyValuePair<int, (TexturePatternAnimation anim, int material)> kv in _swapped)
                {
                    TexturePatternAnimation.Swap swap = kv.Value.anim.Evaluate(kv.Value.material, frame);
                    if (swap.IsSet) swaps[kv.Key] = swap.TextureName;
                }
                if (swaps.Count > 0) f.TextureSwaps = swaps;
            }
            if (_scrolled.Count > 0)
            {
                Dictionary<int, float[]> mats = new(_scrolled.Count);
                foreach (KeyValuePair<int, (TextureSrtAnimation anim, int material, int mode)> kv in _scrolled)
                    mats[kv.Key] = kv.Value.anim.Evaluate(kv.Value.material, frame).ToMatrix3(kv.Value.mode);
                f.TextureMatrices = mats;
            }
            if (_faded.Count > 0)
            {
                Dictionary<int, float> fades = new(_faded.Count);
                foreach (KeyValuePair<int, (MaterialColourAnimation anim, int material)> kv in _faded)
                {
                    float? v = kv.Value.anim.Evaluate(kv.Value.material, frame);
                    if (v.HasValue) fades[kv.Key] = v.Value;
                }
                if (fades.Count > 0) f.MaterialFades = fades;
            }
            if (_jointed.Count > 0 || _moving.Count > 0)
            {
                Dictionary<int, float[]> moved = new();
                foreach ((NsbmdRenderModel.BuildingMaterials building, JointAnimation anim) in _jointed)
                {
                    int at = frame % Math.Max(1, anim.FrameCount);
                    Dictionary<int, float[]> rebuilt = NsbmdGeometry.RebuildBuilding(_scene, building,
                        (objectId, part) => anim.MatrixFor(objectId, at, part, building.Model?.modelScale ?? 1f));
                    foreach (KeyValuePair<int, float[]> kv in rebuilt) moved[kv.Key] = kv.Value;
                }
                float unit = NsbmdGeometry.TileSize / 16f;
                foreach ((NsbmdRenderModel.BuildingMaterials building, BuildingAnimationSet.WholeModelMotion motion) in _moving)
                {
                    (float ox, float oy, float oz) = motion.At(frame);
                    if (ox == 0f && oy == 0f && oz == 0f) continue;
                    (int, int, int, int) key = (building.FirstKey, (int)Math.Round(ox * 64f), (int)Math.Round(oy * 64f), (int)Math.Round(oz * 64f));
                    if (!_movedCache.TryGetValue(key, out Dictionary<int, float[]> parts))
                    {
                        float[] elsewhere = Mat4.Multiply(Mat4.Translate(ox * unit, oy * unit, oz * unit), building.Transform);
                        parts = NsbmdGeometry.RebuildBuilding(_scene, building, null, elsewhere);
                        _movedCache[key] = parts;
                    }
                    foreach (KeyValuePair<int, float[]> kv in parts) moved[kv.Key] = kv.Value;
                }
                if (moved.Count > 0) f.MovedParts = moved;
            }
            return f;
        }
    }
}
