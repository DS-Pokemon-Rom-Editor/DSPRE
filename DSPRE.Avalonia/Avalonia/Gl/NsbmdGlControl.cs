using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DSPRE.ROMFiles;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.OpenGL;
using global::Avalonia.OpenGL.Controls;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Avalonia <see cref="OpenGlControlBase"/> that renders an <see cref="NsbmdRenderModel"/>
    /// (per-material triangle parts + decoded textures) with a perspective + orbit
    /// camera. Vertices are interleaved pos.xyz / uv.st / col.rgb. Textured parts sample
    /// their material texture (with an alpha-test for transparent texels); untextured
    /// parts use the flat material colour. Until a model is supplied it shows a self-test
    /// cube. Normals/lighting are a later slice.
    /// </summary>
    public class NsbmdGlControl : OpenGlControlBase
    {
        private struct GpuPart { public int Vbo; public int VertexCount; public int TextureId; public float Alpha; public int MaterialKey; public int NodeIndex; public int CullMode; public bool Fog; public bool TexAlpha; public float[] TexMatrix; }

        private GlFunctions _f;
        private int _program, _vao, _mvpLoc, _texLoc, _hasTexLoc, _alphaLoc, _texMtxLoc, _matColorLoc, _texAlphaLoc;
        private int _viewLoc, _fogOnLoc, _fogColorLoc, _fogLoc, _fogClipLoc, _fogTableLoc;
        private int _spotOnLoc, _spotCenterLoc, _spotScaleLoc, _spotTrisLoc, _spotTriCountLoc,
                    _spotTexLoc, _spotSizeLoc, _camPosLoc, _camDirLoc, _spotRightLoc, _spotUpLoc;
        private int _tintLoc, _tileOriginLoc, _tileSizeLoc, _collLoc;
        private string _error;

        // Per-tile permission tint of the map textures (mesh overlay mode).
        private int _collTex;
        private bool _tintOn;
        private float _tintStrength = 0.5f;
        private float _tileOx, _tileOz, _tileSx, _tileSz;   // tile grid in normalized space
        private byte[] _collRgb;                            // 32*32*3, pending upload
        private bool _collDirty;

        private NsbmdRenderModel _model;
        private readonly List<GpuPart> _parts = new List<GpuPart>();
        private bool _uploadPending;

        // Optional translucent overlay (e.g. the map permission grid), 8 floats/vertex.
        private float[] _overlayMesh;
        private int _overlayVbo, _overlayCount;
        private bool _overlayDirty;

        // Optional marker layer (e.g. event markers), 8 floats/vertex. Drawn on top of
        // everything with the depth test disabled so markers stay visible through geometry.
        private float[] _markerMesh;
        private int _markerVbo, _markerCount;
        /// <summary>Scene triangles to tint, per material so each keeps its own texture and cut-out.</summary>
        public sealed class HighlightBatch { public int MaterialKey; public float[] Mesh; public float R, G, B; }
        private IReadOnlyList<HighlightBatch> _highlight;
        private readonly List<(int Vbo, int Count, int MaterialKey, float R, float G, float B)> _highlightGpu = new List<(int, int, int, float, float, float)>();
        private bool _highlightDirty;
        private bool _markerDirty;

        // Debug gizmo lines (cell boundaries / geometry extents), 8 floats/vertex. Comes from the model.
        private float[] _gizmoMesh;
        private int _gizmoVbo, _gizmoCount;
        private bool _gizmoDirty;
        private bool _showGizmos;
        public bool ShowGizmos { get => _showGizmos; set { _showGizmos = value; RequestNextFrameRendering(); } }

        // Textured/flat toggle: every vertex already carries its material's diffuse colour
        // (see NsbmdGeometry), so turning this off just skips binding the texture and falls back
        // to that flat per-material colour instead of reloading the model.
        private bool _showTextures = true;
        public bool ShowTextures { get => _showTextures; set { _showTextures = value; RequestNextFrameRendering(); } }

        // ── Terrain animation ──────────────────────────────────────────────────────────── Per-material
        // texture matrices, keyed the same way as the model's materials.
        private Dictionary<int, float[]> _texMatrices;
        private static readonly float[] IdentityTexMatrix = { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };

        /// <summary>Supplies this frame's texture transforms. Pass null to stop animating.</summary>
        public void SetTextureMatrices(Dictionary<int, float[]> byMaterialKey)
        {
            _texMatrices = byMaterialKey;
            RequestNextFrameRendering();
        }

        // Texture swapping (a building's sign flashing). The model carries every texture a material can
        // switch to; this says which one to show, and the ids are uploaded the first time each is asked for.
        private Dictionary<int, string> _texSwaps;
        private readonly Dictionary<(int key, string name), int> _swapTexIds = new Dictionary<(int, string), int>();

        // Buildings whose parts move: their triangles are rebuilt each frame and re-uploaded in place.
        private Dictionary<int, float[]> _movedParts;

        /// <summary>Replaces the triangles of some materials for this frame. Pass null to stop.</summary>
        public void SetMovedParts(Dictionary<int, float[]> byMaterialKey)
        {
            _movedParts = byMaterialKey;
            _movedPartsDirty = true;
            RequestNextFrameRendering();
        }
        private bool _movedPartsDirty;

        // Materials that fade in and out. The renderer already has a per-part alpha, so this just
        // replaces it for the frame.
        private Dictionary<int, float> _fadedMaterials;

        /// <summary>Supplies this frame's material fades, keyed by material. Pass null to stop fading.</summary>
        // Parts an animation hides outright. Drawing them fully see-through looks the same on a plain
        // background but not over anything else, so they are skipped instead.
        private HashSet<int> _hiddenNodes;

        // What a material-colour animation recolours a surface to, as three values from zero to one.
        private Dictionary<int, (float r, float g, float b)> _matColours;

        /// <summary>Recolours some materials this frame, or null to leave every colour alone.</summary>
        public void SetMaterialColours(Dictionary<int, (float r, float g, float b)> byMaterialKey)
        {
            _matColours = byMaterialKey;
            RequestNextFrameRendering();
        }

        /// <summary>NSBMD nodes not to draw at all this frame, or null to draw everything.</summary>
        public void SetHiddenNodes(HashSet<int> byNodeIndex)
        {
            _hiddenNodes = byNodeIndex;
            RequestNextFrameRendering();
        }

        public void SetMaterialFades(Dictionary<int, float> byMaterialKey)
        {
            _fadedMaterials = byMaterialKey;
            RequestNextFrameRendering();
        }

        /// <summary>Supplies this frame's texture swaps, keyed by material. Pass null to stop swapping.</summary>
        public void SetTextureSwaps(Dictionary<int, string> byMaterialKey)
        {
            _texSwaps = byMaterialKey;
            RequestNextFrameRendering();
        }

        /// <summary>The uploaded id for a swapped-in texture, uploading it the first time it is needed.</summary>
        private int SwapTexture(int materialKey, string name)
        {
            if (_swapTexIds.TryGetValue((materialKey, name), out int hit)) return hit;
            int id = 0;
            if (_model != null
                && _model.SwappableTextures.TryGetValue(materialKey, out Dictionary<string, NsbmdTextureData> byName)
                && byName.TryGetValue(name, out NsbmdTextureData tex))
                id = UploadTexture(tex);
            _swapTexIds[(materialKey, name)] = id;
            return id;
        }

        // One-shot framebuffer capture: callback receives raw RGBA (bottom-up) + pixel width/height.
        private Action<byte[], int, int> _captureCb;
        /// <summary>Grabs the next rendered frame's pixels (for a debug screenshot). The callback runs on the
        /// UI thread with the raw RGBA buffer (origin bottom-left) and its pixel dimensions, or null on failure.</summary>
        public void CaptureFrame(Action<byte[], int, int> onCaptured)
        {
            _captureCb = onCaptured;
            RequestNextFrameRendering();
        }

        // ── Translate gizmo (move-tool) ──────────────────────────────────────────────
        // A Unity-style 3-axis move handle drawn at a target point (normalized space) when edit
        // mode is on. Axis dragging is orchestrated by the view via WorldToScreen / HitTestGizmoAxis.
        private bool _editMode;
        public bool EditMode { get => _editMode; set { _editMode = value; RequestNextFrameRendering(); } }
        private bool _gizmoTargetVisible;
        private float _gtx, _gty, _gtz;            // gizmo target in normalized render space
        private int _editVbo; private bool _haveEditVbo;
        private float[] _lastMvp;                    // cached each frame for picking
        private float _lastLogW = 1f, _lastLogH = 1f;

        public void SetGizmoTarget(float x, float y, float z)
        { _gtx = x; _gty = y; _gtz = z; _gizmoTargetVisible = true; RequestNextFrameRendering(); }
        public void ClearGizmoTarget() { _gizmoTargetVisible = false; RequestNextFrameRendering(); }

        /// <summary>On-screen length of one gizmo axis (kept ~constant size regardless of zoom).</summary>
        public float GizmoLength => _distance * 0.14f;
        public static (float x, float y, float z) AxisDir(int axis)
            => axis == 0 ? (1f, 0f, 0f) : axis == 1 ? (0f, 1f, 0f) : (0f, 0f, 1f);

        /// <summary>Projects a normalized-space point to logical control pixels. False if behind camera.</summary>
        public bool WorldToScreen(float x, float y, float z, out float sx, out float sy)
        {
            sx = sy = 0f;
            if (_lastMvp == null) return false;
            float[] m = _lastMvp;
            float cx = m[0] * x + m[4] * y + m[8] * z + m[12];
            float cy = m[1] * x + m[5] * y + m[9] * z + m[13];
            float cw = m[3] * x + m[7] * y + m[11] * z + m[15];
            if (cw <= 1e-5f) return false;
            sx = (cx / cw * 0.5f + 0.5f) * _lastLogW;
            sy = (1f - (cy / cw * 0.5f + 0.5f)) * _lastLogH;
            return true;
        }

        public bool ScreenToRay(float px, float py,
                                out float ox, out float oy, out float oz,
                                out float dx, out float dy, out float dz)
        {
            ox = oy = oz = dx = dy = dz = 0f;
            if (_lastMvp == null || _lastLogW <= 0 || _lastLogH <= 0) return false;

            float[] inv = Mat4.Invert(_lastMvp);
            if (inv == null) return false;

            float nx = px / _lastLogW * 2f - 1f;
            float ny = 1f - py / _lastLogH * 2f;

            if (!Undo(inv, nx, ny, -1f, out float ax, out float ay, out float az)) return false;
            if (!Undo(inv, nx, ny, 1f, out float bx, out float by, out float bz)) return false;

            ox = ax; oy = ay; oz = az;
            dx = bx - ax; dy = by - ay; dz = bz - az;

            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-9f) return false;
            dx /= len; dy /= len; dz /= len;
            return true;
        }

        private static bool Undo(float[] inv, float x, float y, float z,
                                 out float wx, out float wy, out float wz)
        {
            wx = wy = wz = 0f;
            float cx = inv[0] * x + inv[4] * y + inv[8] * z + inv[12];
            float cy = inv[1] * x + inv[5] * y + inv[9] * z + inv[13];
            float cz = inv[2] * x + inv[6] * y + inv[10] * z + inv[14];
            float cw = inv[3] * x + inv[7] * y + inv[11] * z + inv[15];
            if (Math.Abs(cw) < 1e-9f) return false;
            wx = cx / cw; wy = cy / cw; wz = cz / cw;
            return true;
        }

        /// <summary>Which gizmo axis (0=X,1=Y,2=Z) is under the given screen point, or -1.</summary>
        public int HitTestGizmoAxis(float px, float py, float threshold = 9f)
        {
            if (!_editMode || !_gizmoTargetVisible) return -1;
            if (!WorldToScreen(_gtx, _gty, _gtz, out float ox, out float oy)) return -1;
            float len = GizmoLength;
            int best = -1; float bestD = threshold;
            for (int a = 0; a < 3; a++)
            {
                (float dx, float dy, float dz) = AxisDir(a);
                if (!WorldToScreen(_gtx + dx * len, _gty + dy * len, _gtz + dz * len, out float tx, out float ty)) continue;
                float d = DistToSegment(px, py, ox, oy, tx, ty);
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }

        private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay; float wx = px - ax, wy = py - ay;
            float c1 = vx * wx + vy * wy; if (c1 <= 0) return (float)Math.Sqrt(wx * wx + wy * wy);
            float c2 = vx * vx + vy * vy; if (c2 <= c1) { float ex = px - bx, ey = py - by; return (float)Math.Sqrt(ex * ex + ey * ey); }
            float t = c1 / c2; float dx = px - (ax + t * vx), dy = py - (ay + t * vy);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Converts a screen drag (dx,dy px) into a movement along the given gizmo axis,
        /// in normalized-space units. Uses the axis's on-screen projection at the target.</summary>
        public float ScreenDragToAxis(int axis, float dxScreen, float dyScreen)
        {
            if (!WorldToScreen(_gtx, _gty, _gtz, out float ox, out float oy)) return 0f;
            float len = GizmoLength;
            (float ax, float ay, float az) = AxisDir(axis);
            if (!WorldToScreen(_gtx + ax * len, _gty + ay * len, _gtz + az * len, out float tx, out float ty)) return 0f;
            float sxv = tx - ox, syv = ty - oy;
            float denom = sxv * sxv + syv * syv; if (denom < 1e-4f) return 0f;
            float t = (dxScreen * sxv + dyScreen * syv) / denom;   // fraction of one len-unit
            return t * len;
        }

        // Optional textured billboard sprites (e.g. overworld sprites). Each carries a centre
        // in normalized space + half extents; the quad is rebuilt each frame to face the camera.
        private IReadOnlyList<SpriteInstance> _sprites;
        private readonly List<GpuSprite> _gpuSprites = new List<GpuSprite>();
        private int _spriteVbo;
        private bool _spritesDirty;

        // ── Camera ───────────────────────────────────────────────────────────────────
        private float _yaw = 30f, _pitch = 20f, _distance = 4f;
        private bool _orthographic;

        /// <summary>Flat 2D view: no perspective, so tiles keep their true proportions at any zoom.</summary>
        public bool Orthographic
        {
            get => _orthographic;
            set { if (_orthographic != value) { _orthographic = value; RequestNextFrameRendering(); } }
        }
        private float _targetX, _targetY, _targetZ;   // pivot the orbit looks at (for panning)
        public float Yaw { get => _yaw; set { _yaw = value; RequestNextFrameRendering(); } }
        public float Pitch { get => _pitch; set { _pitch = Math.Max(-89f, Math.Min(89f, value)); RequestNextFrameRendering(); } }
        public float Distance { get => _distance; set { _distance = Math.Max(0.2f, value); RequestNextFrameRendering(); } }

        private float _roll;

        /// <summary>Turns the view about the line of sight, in degrees, the way the Distortion World camera rolls.</summary>
        public float Roll { get => _roll; set { if (_roll != value) { _roll = value; RequestNextFrameRendering(); } } }

        private float _fovDegrees = DefaultFovDegrees;

        /// <summary>The everyday editor view, wide enough to see a whole map at a sensible distance.</summary>
        public const float DefaultFovDegrees = 45f;

        /// <summary>How wide the view is, top to bottom, in degrees. </summary>
        public float VerticalFieldOfViewDegrees
        {
            get => _fovDegrees;
            set { float v = Math.Max(1f, Math.Min(120f, value)); if (_fovDegrees != v) { _fovDegrees = v; RequestNextFrameRendering(); } }
        }

        /// <summary>Pans the camera pivot across the ground plane by a screen-space delta.</summary>
        public void PanByScreen(float dx, float dy)
        {
            float yaw = _yaw * (float)Math.PI / 180f;
            float rx = (float)Math.Cos(yaw), rz = (float)Math.Sin(yaw);   // screen-right on ground
            float fx = -(float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);  // screen-up on ground
            float k = _distance * 0.0015f;
            _targetX += (-dx * rx + dy * fx) * k;
            _targetZ += (-dx * rz + dy * fz) * k;
            RequestNextFrameRendering();
        }

        private (float Offset, float Step, (float r, float g, float b) Colour, float Near, float Far, float PerUnit, float[] Table)? _fog;

        /// <summary>
        /// Field fog as G3X_SetFog takes it, over the materials that allow it. <paramref name="near"/> and
        /// <paramref name="far"/> are the game camera's clip planes in game units, and
        /// <paramref name="unitsPerScene"/> turns this view's distances into game units. Null clears it.
        /// </summary>
        public void SetFog(FieldWeather.Fog fog, float near, float far, float unitsPerScene)
        {
            if (fog == null) { _fog = null; RequestNextFrameRendering(); return; }
            // Fog alpha below full lets the layer under the 3D show through, which in the field is the black
            // backdrop, so the fog colour is scaled towards black.
            float a = Math.Clamp(fog.Alpha, 0, 31) / 31f;
            float[] table = new float[32];
            for (int i = 0; i < 32; i++) table[i] = fog.Density(i);
            _fog = (fog.Offset, 0x400 >> fog.Slope,
                    ((fog.Colour & 31) / 31f * a, ((fog.Colour >> 5) & 31) / 31f * a, ((fog.Colour >> 10) & 31) / 31f * a),
                    near, far, unitsPerScene, table);
            RequestNextFrameRendering();
        }

        /// <summary>
        /// A 256 by 192 picture laid over the whole view the way the DS mixes a 2D layer with the 3D one:
        /// out = (layer * eva + under * evb) / 16, each factor capped at 16. See-through pixels are skipped.
        /// </summary>
        public sealed class ScreenLayer
        {
            public byte[] Rgba;
            public int Eva = 16, Evb;

            /// <summary>Drawn before the 3D, so it shows only where nothing is in front.</summary>
            public bool Behind;
        }

        private IReadOnlyList<ScreenLayer> _screenLayers;
        private bool _screenDirty;
        private readonly List<int> _screenTex = new();
        private int _screenVbo;

        /// <summary>Sets the 2D layers drawn with the view, back to front, or null for none.</summary>
        public void SetScreenLayers(IReadOnlyList<ScreenLayer> layers)
        {
            _screenLayers = layers;
            _screenDirty = true;
            RequestNextFrameRendering();
        }

        private static readonly float[] ScreenQuad =
        {
            -1, -1, 0, 0, 1, 1, 1, 1,   1, -1, 0, 1, 1, 1, 1, 1,   1, 1, 0, 1, 0, 1, 1, 1,
            -1, -1, 0, 0, 1, 1, 1, 1,   1, 1, 0, 1, 0, 1, 1, 1,   -1, 1, 0, 0, 0, 1, 1, 1,
        };

        private void RenderScreenLayers(bool behind, int stride)
        {
            IReadOnlyList<ScreenLayer> layers = _screenLayers;
            if (layers == null || layers.Count == 0) return;

            if (_screenDirty)
            {
                while (_screenTex.Count < layers.Count) { int[] t = new int[1]; _f.GenTextures(1, t); _screenTex.Add(t[0]); }
                for (int i = 0; i < layers.Count; i++)
                {
                    if (layers[i]?.Rgba == null) continue;
                    _f.BindTexture(GlFunctions.GL_TEXTURE_2D, _screenTex[i]);
                    _f.TexImage2D(GlFunctions.GL_TEXTURE_2D, 0, GlFunctions.GL_RGBA, 256, 192, 0, GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, layers[i].Rgba);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MIN_FILTER, GlFunctions.GL_NEAREST);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MAG_FILTER, GlFunctions.GL_NEAREST);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_S, GlFunctions.GL_CLAMP_TO_EDGE);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_T, GlFunctions.GL_CLAMP_TO_EDGE);
                }
                _screenDirty = false;
            }

            if (_screenVbo == 0)
            {
                int[] a = new int[1]; _f.GenBuffers(1, a); _screenVbo = a[0];
                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _screenVbo);
                GCHandle h = GCHandle.Alloc(ScreenQuad, GCHandleType.Pinned);
                try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(ScreenQuad.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                finally { h.Free(); }
            }

            _f.BindVertexArray(_vao);
            _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _screenVbo);
            _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
            _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
            _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));

            float[] identity = Mat4.Identity();
            _f.UniformMatrix4fv(_mvpLoc, 1, false, identity);
            if (_viewLoc >= 0) _f.UniformMatrix4fv(_viewLoc, 1, false, identity);
            if (_texMtxLoc >= 0) _f.UniformMatrix3fv(_texMtxLoc, 1, false, IdentityTexMatrix);
            if (_fogOnLoc >= 0) _f.Uniform1i(_fogOnLoc, 0);
            if (_spotOnLoc >= 0) _f.Uniform1i(_spotOnLoc, 0);
            _f.Uniform1f(_tintLoc, 0f);
            _f.Uniform1i(_texLoc, 0);
            _f.Uniform1i(_hasTexLoc, 1);
            _f.ActiveTexture(GlFunctions.GL_TEXTURE0);

            _f.Disable(GlFunctions.GL_DEPTH_TEST);
            _f.DepthMask(false);
            _f.Enable(GlFunctions.GL_BLEND);
            // The layer arrives already scaled by eva and carries evb as its alpha. The picture stays opaque,
            // or the panel behind the view would show through.
            _f.BlendFuncSeparate(GlFunctions.GL_ONE, GlFunctions.GL_SRC_ALPHA, GlFunctions.GL_ZERO, GlFunctions.GL_ONE);

            for (int i = 0; i < layers.Count; i++)
            {
                ScreenLayer l = layers[i];
                if (l?.Rgba == null || l.Behind != behind) continue;
                float eva = Math.Clamp(l.Eva, 0, 16) / 16f, evb = Math.Clamp(l.Evb, 0, 16) / 16f;
                if (_matColorLoc >= 0) _f.Uniform3f(_matColorLoc, eva, eva, eva);
                _f.Uniform1f(_alphaLoc, evb);
                _f.BindTexture(GlFunctions.GL_TEXTURE_2D, _screenTex[i]);
                _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, 6);
            }

            _f.Disable(GlFunctions.GL_BLEND);
            _f.DepthMask(true);
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            if (_matColorLoc >= 0) _f.Uniform3f(_matColorLoc, 1f, 1f, 1f);
            _f.Uniform1f(_alphaLoc, 1f);
            _f.Uniform1i(_hasTexLoc, 0);
        }

        /// <summary>
        /// The HGSS Flash light: the upright field effect model at <see cref="Center"/> in this view's space,
        /// its triangles and texture coordinates in model units, which the scale turns into this view's units.
        /// </summary>
        public sealed class LightSpot
        {
            public float[] Center;
            public float ScaleX, ScaleY;
            public float[] Triangles;            // x, y, u, v for each corner, three corners a triangle
            public byte[] Rgba;
            public int Width, Height;
        }

        // The shader's uSpotTris array holds three corners for each of this many triangles.
        private const int MaxSpotTriangles = 32;
        private readonly float[] _spotTris = new float[MaxSpotTriangles * 3 * 4];

        private LightSpot _spot;
        private int _spotTex;
        private bool _spotDirty;

        public void SetLightSpot(LightSpot spot)
        {
            _spot = spot;
            _spotDirty = true;
            RequestNextFrameRendering();
        }

        /// <summary>Where the camera sits in this view's space.</summary>
        public (float x, float y, float z) EyePosition()
        {
            float[] view = Mat4.Multiply(Mat4.Multiply(Mat4.RotateZ(_roll * (float)Math.PI / 180f), Mat4.OrbitView(_distance, _yaw, _pitch)),
                                     Mat4.Translate(-_targetX, -_targetY, -_targetZ));
            float[] inv = Mat4.Invert(view);
            return inv == null ? (_targetX, _targetY, _targetZ) : (inv[12], inv[13], inv[14]);
        }

        private void ApplySpot(float[] view)
        {
            LightSpot spot = _spot;
            if (_spotOnLoc < 0) return;
            if (spot == null || spot.Triangles == null) { _f.Uniform1i(_spotOnLoc, 0); return; }
            float[] inv = Mat4.Invert(view);
            if (inv == null) { _f.Uniform1i(_spotOnLoc, 0); return; }
            // The model draws its front face only, so from behind it lights nothing.
            float camZ = inv[14];
            if (!_orthographic && camZ <= spot.Center[2]) { _f.Uniform1i(_spotOnLoc, 0); return; }

            if (_spotDirty)
            {
                if (_spotTex == 0) { int[] t = new int[1]; _f.GenTextures(1, t); _spotTex = t[0]; }
                _f.ActiveTexture(GlFunctions.GL_TEXTURE2);
                _f.BindTexture(GlFunctions.GL_TEXTURE_2D, _spotTex);
                _f.TexImage2D(GlFunctions.GL_TEXTURE_2D, 0, GlFunctions.GL_RGBA, spot.Width, spot.Height, 0, GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, spot.Rgba);
                _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MIN_FILTER, GlFunctions.GL_NEAREST);
                _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MAG_FILTER, GlFunctions.GL_NEAREST);
                _f.ActiveTexture(GlFunctions.GL_TEXTURE0);
                _spotDirty = false;
            }

            _f.ActiveTexture(GlFunctions.GL_TEXTURE2);
            _f.BindTexture(GlFunctions.GL_TEXTURE_2D, _spotTex);
            _f.ActiveTexture(GlFunctions.GL_TEXTURE0);
            _f.Uniform1i(_spotTexLoc, 2);
            _f.Uniform1i(_spotOnLoc, 1);
            _f.Uniform3f(_spotCenterLoc, spot.Center[0], spot.Center[1], spot.Center[2]);
            _f.Uniform2f(_spotScaleLoc, spot.ScaleX, spot.ScaleY);
            Array.Clear(_spotTris);
            Array.Copy(spot.Triangles, _spotTris, Math.Min(_spotTris.Length, spot.Triangles.Length));
            _f.Uniform4fv(_spotTrisLoc, MaxSpotTriangles * 3, _spotTris);
            _f.Uniform1i(_spotTriCountLoc, Math.Min(MaxSpotTriangles, spot.Triangles.Length / 12));
            _f.Uniform2f(_spotSizeLoc, spot.Width, spot.Height);
            _f.Uniform3f(_camPosLoc, inv[12], inv[13], inv[14]);
            // The view looks down its own -z, which is the third row of the view matrix.
            _f.Uniform4f(_camDirLoc, -view[2], -view[6], -view[10], _orthographic ? 1f : 0f);
            // The first two rows of the view matrix are the camera's right and up in the world.
            _f.Uniform3f(_spotRightLoc, view[0], view[4], view[8]);
            _f.Uniform3f(_spotUpLoc, view[1], view[5], view[9]);
        }

        /// <summary>What shows where nothing is drawn.</summary>
        public (float r, float g, float b) ClearColour { get; set; } = (0.12f, 0.12f, 0.14f);

        /// <summary>Recentres the camera pivot.</summary>
        public void ResetView() { _targetX = _targetY = _targetZ = 0f; RequestNextFrameRendering(); }

        /// <summary>Moves the camera pivot onto a point so the view centres on it.</summary>
        public void LookAt(float x, float y, float z)
        {
            _targetX = x; _targetY = y; _targetZ = z;
            RequestNextFrameRendering();
        }

        // ── Mouse-driven camera input (honours the user's camera preferences) ─────────────
        // All host views route their right-drag / left-drag / wheel gestures through these so the
        // behaviour (speed + axis inversion) is consistent and configurable in Settings.
        private static DspreSettings Cam => SettingsManager.Settings;

        /// <summary>Orbit (rotate) the camera from a mouse drag, in raw screen-pixel deltas.</summary>
        public void OrbitByDrag(float screenDx, float screenDy)
        {
            DspreSettings c = Cam;
            float spd = (c?.camOrbitSpeed ?? 1f) * 0.5f;
            Yaw   += screenDx * spd * ((c?.camInvertOrbitX ?? false) ? -1f : 1f);
            Pitch += screenDy * spd * ((c?.camInvertOrbitY ?? false) ? -1f : 1f);
        }

        /// <summary>Pan (slide) the camera pivot from a mouse drag, in raw screen-pixel deltas. The default
        /// direction grabs the world (the scene follows the cursor); invert flags flip each axis.</summary>
        public void PanByDrag(float screenDx, float screenDy)
        {
            DspreSettings c = Cam;
            float spd = c?.camPanSpeed ?? 1f;
            PanByScreen(screenDx * spd * ((c?.camInvertPanX ?? false) ? -1f : 1f),
                        screenDy * spd * ((c?.camInvertPanY ?? false) ? -1f : 1f));
        }

        /// <summary>Zoom from a mouse-wheel notch (raw wheel delta-Y).</summary>
        public void ZoomByWheel(float wheelDeltaY)
        {
            DspreSettings c = Cam;
            Distance -= wheelDeltaY * 0.4f * (c?.camZoomSpeed ?? 1f) * ((c?.camInvertZoom ?? false) ? -1f : 1f);
        }

        /// <summary>Sets the orbit camera to a fixed orientation (degrees), e.g. a top-down or side view.</summary>
        public void SetOrientation(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Math.Max(-89f, Math.Min(89f, pitch));
            RequestNextFrameRendering();
        }

        public string LastError => _error;
        public event EventHandler ErrorChanged;

        public NsbmdGlControl() => _model = CubeModel();

        public void ShowTestCube() { SetModel(CubeModel()); }

        public void SetModel(NsbmdRenderModel model)
        {
            _model = model;
            _uploadPending = true;
            _targetX = _targetY = _targetZ = 0f;   // recentre when the scene changes
            _gizmoMesh = model?.GizmoMesh;
            _gizmoCount = model?.GizmoVertexCount ?? 0;
            _gizmoDirty = true;
            RequestNextFrameRendering();
        }

        /// <summary>Sets a translucent overlay mesh (8 floats/vertex: pos,uv,col), or null to clear.</summary>
        public void SetOverlay(float[] mesh, int vertexCount)
        {
            _overlayMesh = mesh;
            _overlayCount = vertexCount;
            _overlayDirty = true;
            RequestNextFrameRendering();
        }

        /// <summary>Enables/updates the per-tile permission tint of the map textures. <paramref name="rgb"/> is a
        /// 32×32 row-major (col=x, row=z) RGB grid of collision colours; the tile grid is given in normalized space
        /// (origin + tile size). Pass on=false to disable.</summary>
        public void SetTileTint(bool on, float strength, float originX, float originZ, float tileX, float tileZ, byte[] rgb)
        {
            _tintOn = on && rgb != null && rgb.Length >= 32 * 32 * 4;
            _tintStrength = strength;
            _tileOx = originX; _tileOz = originZ; _tileSx = tileX; _tileSz = tileZ;
            if (_tintOn) { _collRgb = rgb; _collDirty = true; }
            RequestNextFrameRendering();
        }

        /// <summary>Tints the visible parts of scene triangles (8 floats/vertex, the scene's own positions and uvs); null to clear.</summary>
        public void SetHighlight(IReadOnlyList<HighlightBatch> batches)
        {
            _highlight = batches;
            _highlightDirty = true;
            RequestNextFrameRendering();
        }

        private void FreeHighlight()
        {
            foreach ((int Vbo, int Count, int MaterialKey, float R, float G, float B) h in _highlightGpu) if (h.Vbo != 0) _f?.DeleteBuffers(1, new[] { h.Vbo });
            _highlightGpu.Clear();
        }

        /// <summary>Sets a marker mesh (8 floats/vertex: pos,uv,col) drawn on top of everything
        /// with the depth test disabled (e.g. event markers), or null to clear.</summary>
        public void SetMarkers(float[] mesh, int vertexCount)
        {
            _markerMesh = mesh;
            _markerCount = vertexCount;
            _markerDirty = true;
            RequestNextFrameRendering();
        }

        /// <summary>A camera-facing textured billboard (e.g. an overworld sprite).</summary>
        public sealed class SpriteInstance
        {
            public float Cx, Cy, Cz;     // centre in normalized render space
            public float HalfW, HalfH;   // half extents in normalized units
            public byte[] Rgba;          // top-row-first RGBA pixels
            public int Width, Height;

            public float RollDegrees;
        }

        private struct GpuSprite { public int Tex; public float Cx, Cy, Cz, HalfW, HalfH, Roll; }

        /// <summary>Sets the textured billboard sprites (or null to clear).</summary>
        private bool _spritesSeeThrough = true;

        /// <summary>Whether people show through walls. </summary>
        public bool SpritesSeeThroughGeometry
        {
            get => _spritesSeeThrough;
            set { if (_spritesSeeThrough != value) { _spritesSeeThrough = value; RequestNextFrameRendering(); } }
        }

        public void SetSprites(IReadOnlyList<SpriteInstance> sprites)
        {
            _sprites = sprites;
            _spritesDirty = true;
            RequestNextFrameRendering();
        }

        // ── GL lifecycle ───────────────────────────────────────────────────────────────
        private static bool _glLogged;

        protected override void OnOpenGlInit(GlInterface gl)
        {
            try
            {
                _f = new GlFunctions(gl);
                if (!_glLogged)
                {
                    _glLogged = true;
                    AppLogger.Info($"OpenGL {GlVersion.Type} {GlVersion.Major}.{GlVersion.Minor}: {gl.GetString(GlConsts.GL_RENDERER)} ({gl.GetString(GlConsts.GL_VERSION)})");
                }
                bool es = GlVersion.Type == GlProfileType.OpenGLES;
                string header = es ? "#version 300 es\nprecision highp float;\n" : "#version 330 core\n";

                string vs = header +
                    "layout(location=0) in vec3 aPos;\n" +
                    "layout(location=1) in vec2 aUv;\n" +
                    "layout(location=2) in vec3 aColor;\n" +
                    "uniform mat4 uMvp;\n" +
                    // Terrain animation (NSBTA) scrolls a material's texture coordinates.
                    // Identity for every material the animation does not target.
                    "uniform mat3 uTexMtx;\n" +
                    "uniform mat4 uView;\n" +
                    "out vec2 vUv;\nout vec3 vColor;\nout vec2 vWorld;\nout float vEye;\nout vec3 vPos;\n" +
                    "void main(){ vUv = (uTexMtx * vec3(aUv, 1.0)).xy; vColor = aColor; vWorld = aPos.xz; vEye = -(uView * vec4(aPos, 1.0)).z; vPos = aPos; gl_Position = uMvp * vec4(aPos, 1.0); }\n";
                string fs = header +
                    "uniform sampler2D uTex;\nuniform int uHasTex;\nuniform int uTexAlpha;\nuniform float uAlpha;\n" +
                    // Per-tile permission tint (uTint>0): sample a 32x32 collision-colour texture by the fragment's
                    // world-tile and mix it into the surface AFTER the alpha discard, so the collision colour follows
                    // the real texture shape (trees/lamps tinted on their pixels; transparent texels stay clear).
                    "uniform float uTint;\nuniform vec2 uTileOrigin;\nuniform vec2 uTileSize;\nuniform sampler2D uColl;\n" +
                    // A material-colour animation (NSBMA) recolours a surface over time. White leaves it alone.
                    "uniform vec3 uMatColor;\n" +
                    // DS fog: the 15-bit depth the Z-buffer holds, 0x3FFF + 0x4000 z/w under the game camera's own clip
                    // planes, read through the weather's 32-entry density table, entries (0x400 >> slope) apart.
                    "uniform int uFogOn;\nuniform vec3 uFogColor;\nuniform vec4 uFog;\nuniform vec3 uFogClip;\nuniform float uFogTable[32];\n" +
                    "in vec2 vUv;\nin vec3 vColor;\nin vec2 vWorld;\nin float vEye;\nin vec3 vPos;\nout vec4 fragColor;\n" +
                    "uniform int uSpotOn;\nuniform vec3 uSpotCenter;\nuniform vec2 uSpotScale;\nuniform vec4 uSpotTris[" + MaxSpotTriangles * 3 + "];\nuniform int uSpotTriCount;\n" +
                    "uniform sampler2D uSpotTex;\nuniform vec2 uSpotSize;\n" +
                    "uniform vec3 uCamPos;\nuniform vec4 uCamDir;\nuniform vec3 uSpotRight;\nuniform vec3 uSpotUp;\n" +
                    "float spotAlpha(){\n" +
                    "  vec3 o = uCamDir.w > 0.5 ? vPos - uCamDir.xyz * 1000.0 : uCamPos;\n" +
                    "  vec3 d = vPos - o;\n" +
                    // dun_spot is a billboard: its plane faces the camera through the spot's centre.
                    "  vec3 n = uCamDir.xyz;\n" +
                    "  float dn = dot(d, n);\n" +
                    "  if (abs(dn) < 1e-6) return -1.0;\n" +
                    "  float t = dot(uSpotCenter - o, n) / dn;\n" +
                    "  if (t <= 0.0 || t >= 1.0) return -1.0;\n" +
                    "  vec3 q3 = o + d * t - uSpotCenter;\n" +
                    "  vec2 l = vec2(dot(q3, uSpotRight), dot(q3, uSpotUp)) / uSpotScale;\n" +
                    "  for (int i = 0; i < " + MaxSpotTriangles + "; i++) {\n" +
                    "    if (i >= uSpotTriCount) break;\n" +
                    "    vec4 p0 = uSpotTris[3 * i], p1 = uSpotTris[3 * i + 1], p2 = uSpotTris[3 * i + 2];\n" +
                    "    vec2 e1 = p1.xy - p0.xy, e2 = p2.xy - p0.xy, q = l - p0.xy;\n" +
                    "    float det = e1.x * e2.y - e1.y * e2.x;\n" +
                    "    if (abs(det) < 1e-9) continue;\n" +
                    "    float b1 = (q.x * e2.y - q.y * e2.x) / det, b2 = (e1.x * q.y - e1.y * q.x) / det;\n" +
                    "    if (b1 < -1e-5 || b2 < -1e-5 || b1 + b2 > 1.0 + 1e-5) continue;\n" +
                    "    vec2 uv = (p0.zw + (p1.zw - p0.zw) * b1 + (p2.zw - p0.zw) * b2) * uSpotSize;\n" +
                    "    ivec2 tx = ivec2(clamp(floor(uv), vec2(0.0), uSpotSize - 1.0));\n" +
                    // A5I3 keeps five bits of alpha, 31 of them opaque, which the decoder stores shifted by three.
                    "    float a = floor(texelFetch(uSpotTex, tx, 0).a * 255.0 / 8.0 + 0.5) / 31.0;\n" +
                    "    return a > 0.0 ? a : -1.0;\n" +
                    "  }\n" +
                    "  return -1.0;\n" +
                    "}\n" +
                    "vec3 fogged(vec3 c){\n" +
                    "  float s = uSpotOn == 1 ? spotAlpha() : -1.0;\n" +
                    "  if (s >= 0.0) return c * (1.0 - s);\n" +
                    "  if (uFogOn == 0) return c;\n" +
                    "  float z = vEye * uFog.w, n = uFogClip.x, f = uFogClip.y;\n" +
                    "  float ndc = uFogClip.z > 0.5 ? (2.0 * z - (f + n)) / (f - n) : (f + n) / (f - n) - 2.0 * f * n / ((f - n) * max(z, 0.001));\n" +
                    "  float depth = 16383.0 + 16384.0 * clamp(ndc, -1.0, 1.0);\n" +
                    "  float at = clamp((depth - uFog.x) / uFog.y, 0.0, 31.0);\n" +
                    "  int i0 = int(floor(at)); int i1 = min(i0 + 1, 31);\n" +
                    "  float d = mix(uFogTable[i0], uFogTable[i1], at - float(i0));\n" +
                    "  float k = d >= 127.0 ? 1.0 : d / 128.0;\n" +
                    "  return mix(c, uFogColor, k);\n" +
                    "}\n" +
                    "vec3 tintRgb(vec3 c){\n" +
                    "  if (uTint <= 0.0) return c;\n" +
                    "  vec2 tc = (vWorld - uTileOrigin) / uTileSize;\n" +
                    "  if (tc.x < 0.0 || tc.y < 0.0 || tc.x >= 32.0 || tc.y >= 32.0) return c;\n" +
                    "  vec2 cuv = (floor(tc) + 0.5) / 32.0;\n" +
                    "  vec4 t = texture(uColl, cuv);\n" +
                    "  return mix(c, t.rgb, uTint * t.a);\n" +
                    "}\n" +
                    "void main(){\n" +
                    "  if (uHasTex == 1) { vec4 t = texture(uTex, vUv);\n" +
                    "    if (uTexAlpha == 1) { if (t.a < 0.02) discard; fragColor = vec4(fogged(tintRgb(t.rgb) * uMatColor), uAlpha * t.a); }\n" +
                    "    else { if (t.a < 0.5) discard; fragColor = vec4(fogged(tintRgb(t.rgb) * uMatColor), uAlpha); } }\n" +
                    "  else { fragColor = vec4(fogged(tintRgb(vColor) * uMatColor), uAlpha); }\n" +
                    "}\n";

                int v = _f.CompileShaderOrThrow(GlFunctions.GL_VERTEX_SHADER, vs);
                int f = _f.CompileShaderOrThrow(GlFunctions.GL_FRAGMENT_SHADER, fs);
                _program = _f.LinkProgramOrThrow(v, f);
                _mvpLoc = _f.GetUniformLocation(_program, "uMvp");
                _texMtxLoc = _f.GetUniformLocation(_program, "uTexMtx");
                _matColorLoc = _f.GetUniformLocation(_program, "uMatColor");
                _texLoc = _f.GetUniformLocation(_program, "uTex");
                _hasTexLoc = _f.GetUniformLocation(_program, "uHasTex");
                _alphaLoc = _f.GetUniformLocation(_program, "uAlpha");
                _texAlphaLoc = _f.GetUniformLocation(_program, "uTexAlpha");
                _tintLoc = _f.GetUniformLocation(_program, "uTint");
                _tileOriginLoc = _f.GetUniformLocation(_program, "uTileOrigin");
                _tileSizeLoc = _f.GetUniformLocation(_program, "uTileSize");
                _collLoc = _f.GetUniformLocation(_program, "uColl");
                _viewLoc = _f.GetUniformLocation(_program, "uView");
                _fogOnLoc = _f.GetUniformLocation(_program, "uFogOn");
                _fogColorLoc = _f.GetUniformLocation(_program, "uFogColor");
                _fogLoc = _f.GetUniformLocation(_program, "uFog");
                _fogClipLoc = _f.GetUniformLocation(_program, "uFogClip");
                _fogTableLoc = _f.GetUniformLocation(_program, "uFogTable");
                _spotOnLoc = _f.GetUniformLocation(_program, "uSpotOn");
                _spotCenterLoc = _f.GetUniformLocation(_program, "uSpotCenter");
                _spotScaleLoc = _f.GetUniformLocation(_program, "uSpotScale");
                _spotTrisLoc = _f.GetUniformLocation(_program, "uSpotTris");
                _spotTriCountLoc = _f.GetUniformLocation(_program, "uSpotTriCount");
                _spotTexLoc = _f.GetUniformLocation(_program, "uSpotTex");
                _spotSizeLoc = _f.GetUniformLocation(_program, "uSpotSize");
                _camPosLoc = _f.GetUniformLocation(_program, "uCamPos");
                _camDirLoc = _f.GetUniformLocation(_program, "uCamDir");
                _spotRightLoc = _f.GetUniformLocation(_program, "uSpotRight");
                _spotUpLoc = _f.GetUniformLocation(_program, "uSpotUp");

                int[] arr = new int[1];
                _f.GenVertexArrays(1, arr); _vao = arr[0];
                // Deinit threw away every GPU object, which happens whenever the control leaves the visual
                // tree, so flag all the still-held CPU meshes for re-upload, not just the model.
                _uploadPending = true;
                _markerDirty = true;
                _spritesDirty = true;
                _overlayDirty = true;
                _gizmoDirty = true;
                _collDirty = true;
                SetError(null);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                AppLogger.Error("NsbmdGlControl init failed: " + ex.Message);
            }
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            try
            {
                FreeGpuParts();
                FreeGpuSprites();
                if (_collTex != 0) { _f?.DeleteTextures(1, new[] { _collTex }); _collTex = 0; _collDirty = true; }
                if (_overlayVbo != 0) _f?.DeleteBuffers(1, new[] { _overlayVbo });
                if (_markerVbo != 0) _f?.DeleteBuffers(1, new[] { _markerVbo });
                FreeHighlight();
                if (_spriteVbo != 0) _f?.DeleteBuffers(1, new[] { _spriteVbo });
                if (_gizmoVbo != 0) _f?.DeleteBuffers(1, new[] { _gizmoVbo });
                if (_haveEditVbo && _editVbo != 0) _f?.DeleteBuffers(1, new[] { _editVbo });
                if (_vao != 0) _f?.DeleteVertexArrays(1, new[] { _vao });
            }
            catch { }
            _f = null; _program = _vao = _overlayVbo = _markerVbo = _spriteVbo = _gizmoVbo = 0;
            _highlightDirty = true;
            _haveEditVbo = false; _editVbo = 0;
        }

        private void FreeGpuParts()
        {
            if (_f == null) return;
            foreach (GpuPart p in _parts)
            {
                if (p.Vbo != 0) _f.DeleteBuffers(1, new[] { p.Vbo });
                if (p.TextureId != 0) _f.DeleteTextures(1, new[] { p.TextureId });
            }
            _parts.Clear();

            // Swapped-in textures belong to the model that just went away.
            foreach (int id in _swapTexIds.Values)
                if (id != 0) _f.DeleteTextures(1, new[] { id });
            _swapTexIds.Clear();
        }

        // Sprite GPU textures are cached by their pixel-buffer reference (OverworldSprites.Get returns the
        // SAME cached array for a given sprite), so re-positioning sprites during a drag only rebuilds the
        // lightweight GpuSprite list; it does NOT re-upload textures (that was the move-gizmo lag).
        private readonly Dictionary<byte[], int> _spriteTexCache = new Dictionary<byte[], int>();

        private void FreeGpuSprites()
        {
            if (_f != null)
                foreach (KeyValuePair<byte[], int> kv in _spriteTexCache)
                    if (kv.Value != 0) _f.DeleteTextures(1, new[] { kv.Value });
            _spriteTexCache.Clear();
            _gpuSprites.Clear();
        }

        private void UploadSprites()
        {
            _gpuSprites.Clear();
            if (_sprites != null)
                foreach (SpriteInstance s in _sprites)
                {
                    if (s.Rgba == null || s.Width <= 0 || s.Height <= 0) continue;
                    if (!_spriteTexCache.TryGetValue(s.Rgba, out int id))
                    {
                        int[] arr = new int[1];
                        _f.GenTextures(1, arr); id = arr[0];
                        _f.BindTexture(GlFunctions.GL_TEXTURE_2D, id);
                        _f.TexImage2D(GlFunctions.GL_TEXTURE_2D, 0, GlFunctions.GL_RGBA, s.Width, s.Height, 0,
                            GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, s.Rgba);
                        _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MIN_FILTER, GlFunctions.GL_NEAREST);
                        _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MAG_FILTER, GlFunctions.GL_NEAREST);
                        _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_S, GlFunctions.GL_CLAMP_TO_EDGE);
                        _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_T, GlFunctions.GL_CLAMP_TO_EDGE);
                        _spriteTexCache[s.Rgba] = id;
                    }
                    _gpuSprites.Add(new GpuSprite { Tex = id, Cx = s.Cx, Cy = s.Cy, Cz = s.Cz, HalfW = s.HalfW, HalfH = s.HalfH, Roll = s.RollDegrees });
                }
            _spritesDirty = false;
        }

        private void Upload()
        {
            FreeGpuParts();
            if (_model == null) return;

            foreach (NsbmdMeshPart part in _model.Parts)
            {
                if (part.VertexCount == 0) continue;
                int[] arr = new int[1];
                _f.GenBuffers(1, arr); int vbo = arr[0];
                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, vbo);
                GCHandle h = GCHandle.Alloc(part.Vertices, GCHandleType.Pinned);
                try
                {
                    _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(part.Vertices.Length * sizeof(float)),
                        h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW);
                }
                finally { h.Free(); }

                int texId = 0;
                NsbmdTextureData tex = null;
                if (_model.Textures != null && _model.Textures.TryGetValue(part.MaterialIndex, out tex) && tex?.Rgba != null)
                    texId = UploadTexture(tex);

                _parts.Add(new GpuPart { Vbo = vbo, VertexCount = part.VertexCount, TextureId = texId,
                    Alpha = part.Alpha, MaterialKey = part.MaterialIndex, NodeIndex = part.NodeIndex,
                    CullMode = part.CullMode, Fog = part.Fog, TexAlpha = HasPartialAlpha(tex?.Rgba), TexMatrix = part.TexMatrix });
            }
            _uploadPending = false;
        }

        /// <summary>Re-uploads the triangles of the parts a joint animation has moved. </summary>
        private void UploadMovedParts()
        {
            _movedPartsDirty = false;
            if (_movedParts == null || _movedParts.Count == 0) return;

            for (int i = 0; i < _parts.Count; i++)
            {
                GpuPart part = _parts[i];
                if (part.Vbo == 0) continue;
                if (!_movedParts.TryGetValue(part.MaterialKey, out float[] verts) || verts == null) continue;

                int count = verts.Length / 8;
                if (count == 0) continue;

                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, part.Vbo);
                GCHandle h = GCHandle.Alloc(verts, GCHandleType.Pinned);
                try
                {
                    _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(verts.Length * sizeof(float)),
                        h.AddrOfPinnedObject(), GlFunctions.GL_DYNAMIC_DRAW);
                }
                finally { h.Free(); }

                part.VertexCount = count;
                _parts[i] = part;
            }
        }

        private int UploadTexture(NsbmdTextureData tex)
        {
            int[] arr = new int[1];
            _f.GenTextures(1, arr); int id = arr[0];
            _f.BindTexture(GlFunctions.GL_TEXTURE_2D, id);
            _f.TexImage2D(GlFunctions.GL_TEXTURE_2D, 0, GlFunctions.GL_RGBA, tex.Width, tex.Height, 0,
                GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, tex.Rgba);
            _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MIN_FILTER, GlFunctions.GL_NEAREST);
            _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MAG_FILTER, GlFunctions.GL_NEAREST);
            _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_S, WrapGl(tex.WrapS));
            _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_T, WrapGl(tex.WrapT));
            return id;
        }

        /// <summary>
        /// Whether a texture has see-through-but-not-clear texels (A3I5 and A5I3, like New Bark's wind streaks). Those are
        /// blended with their own alpha as the DS does; cutting them at half alpha made faint ones vanish.
        /// </summary>
        private static bool HasPartialAlpha(byte[] rgba)
        {
            if (rgba == null) return false;
            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] != 0 && rgba[i] != 255) return true;
            return false;
        }

        private static int WrapGl(int w) => w == 2 ? GlFunctions.GL_MIRRORED_REPEAT : w == 1 ? GlFunctions.GL_REPEAT : GlFunctions.GL_CLAMP_TO_EDGE;

        // Field texture animations run on their own clock at the field's 30 steps a second.
        private readonly System.Diagnostics.Stopwatch _fieldClock = System.Diagnostics.Stopwatch.StartNew();
        private Dictionary<int, string> _fieldFrame;
        public bool PlayFieldAnimations { get; set; } = true;

        private Dictionary<int, float[]> _groundMatrices;

        /// <summary>
        /// The scene's buildings played on the field clock (windmills, signs, wind lines), for a view that shows the
        /// field rather than stepping animations itself. Null leaves the building state to the Set* calls.
        /// </summary>
        public SceneBuildingAnimator BuildingAnimator
        {
            get => _buildingAnimator;
            set
            {
                _buildingAnimator = value;
                _buildingTick = -1;
                if (value == null) { _texMatrices = null; _texSwaps = null; _fadedMaterials = null; _movedParts = null; _movedPartsDirty = true; }
                RequestNextFrameRendering();
            }
        }
        private SceneBuildingAnimator _buildingAnimator;
        private long _buildingTick = -1;

        private void StepBuildings()
        {
            SceneBuildingAnimator animator = _buildingAnimator;
            if (animator == null || !PlayFieldAnimations || !animator.HasAnything) return;
            long tick = (long)(_fieldClock.Elapsed.TotalSeconds * 30);
            if (tick != _buildingTick)
            {
                _buildingTick = tick;
                SceneBuildingAnimator.Frame f = animator.At((int)(tick % int.MaxValue));
                _texMatrices = f.TextureMatrices;
                _texSwaps = f.TextureSwaps;
                _fadedMaterials = f.MaterialFades;
                _movedParts = f.MovedParts;
                _movedPartsDirty = true;
            }
            RequestNextFrameRendering();
        }

        private void StepFieldAnimations()
        {
            StepBuildings();
            List<(int MaterialKey, TextureSrtAnimation Anim, int Index)> scrolls = _model?.GroundScrolls;
            if (!PlayFieldAnimations || scrolls == null || scrolls.Count == 0) _groundMatrices = null;
            else
            {
                long tick = (long)(_fieldClock.Elapsed.TotalSeconds * 30);
                _groundMatrices ??= new Dictionary<int, float[]>();
                foreach ((int key, TextureSrtAnimation anim, int index) in scrolls)
                    _groundMatrices[key] = anim.Evaluate(index, (int)(tick % Math.Max(1, anim.FrameCount))).ToMatrix3();
            }

            List<(int MaterialKey, List<(string Swap, int Frames)> Sequence)> anims = _model?.FieldAnimations;
            if (!PlayFieldAnimations || anims == null || anims.Count == 0) { _fieldFrame = null; return; }
            long step = (long)(_fieldClock.Elapsed.TotalSeconds * 30);
            _fieldFrame ??= new Dictionary<int, string>();
            foreach ((int key, List<(string Swap, int Frames)> sequence) in anims)
            {
                int total = 0;
                foreach ((string Swap, int Frames) s in sequence) total += s.Frames;
                long at = total > 0 ? step % total : 0;
                foreach ((string Swap, int Frames) s in sequence)
                {
                    if (at < s.Frames) { _fieldFrame[key] = s.Swap; break; }
                    at -= s.Frames;
                }
            }
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            StepFieldAnimations();
            if (_f == null || _program == 0) return;
            if (_uploadPending) Upload();
            if (_movedPartsDirty) UploadMovedParts();

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            int pw = Math.Max(1, (int)(Bounds.Width * scaling));
            int ph = Math.Max(1, (int)(Bounds.Height * scaling));

            _f.Viewport(0, 0, pw, ph);
            _f.ClearColor(ClearColour.r, ClearColour.g, ClearColour.b, 1f);
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.Clear(GlFunctions.GL_COLOR_BUFFER_BIT | GlFunctions.GL_DEPTH_BUFFER_BIT);

            if (_parts.Count == 0) return;

            float aspect = ph == 0 ? 1f : (float)pw / ph;
            // The flat view frames the same amount as the perspective one does at the target, so
            // switching between them keeps the same zoom instead of jumping.
            float halfFov = _fovDegrees * 0.5f * (float)Math.PI / 180f;
            float[] proj = _orthographic
                ? Mat4.Ortho(_distance * (float)Math.Tan(halfFov), aspect, -1000f, 1000f)
                : Mat4.Perspective(_fovDegrees * (float)Math.PI / 180f, aspect, 0.05f, 1000f);
            float[] view = Mat4.Multiply(Mat4.Multiply(Mat4.RotateZ(_roll * (float)Math.PI / 180f), Mat4.OrbitView(_distance, _yaw, _pitch)),
                                     Mat4.Translate(-_targetX, -_targetY, -_targetZ));
            float[] mvp = Mat4.Multiply(proj, view);
            _lastMvp = mvp; _lastLogW = (float)Math.Max(1.0, Bounds.Width); _lastLogH = (float)Math.Max(1.0, Bounds.Height);

            _f.UseProgram(_program);
            RenderScreenLayers(behind: true, stride: 8 * sizeof(float));
            _f.UniformMatrix4fv(_mvpLoc, 1, false, mvp);
            if (_viewLoc >= 0) _f.UniformMatrix4fv(_viewLoc, 1, false, view);
            _f.Uniform1i(_texLoc, 0);
            _f.Uniform1f(_alphaLoc, 1f);
            if (_fogOnLoc >= 0) _f.Uniform1i(_fogOnLoc, 0);
            if (_fog is { } fog && _fogLoc >= 0)
            {
                _f.Uniform3f(_fogColorLoc, fog.Colour.r, fog.Colour.g, fog.Colour.b);
                _f.Uniform4f(_fogLoc, fog.Offset, fog.Step, 0f, fog.PerUnit);
                _f.Uniform3f(_fogClipLoc, fog.Near, fog.Far, _orthographic ? 1f : 0f);
                if (_fogTableLoc >= 0) _f.Uniform1fv(_fogTableLoc, 32, fog.Table);
            }
            ApplySpot(view);

            // Per-tile permission tint of the map textures (mesh overlay mode): upload the 32×32 collision-colour
            // texture to unit 1 and hand the shader the tile grid. uTint>0 mixes it into each opaque texel.
            if (_tintOn)
            {
                if (_collTex == 0) { int[] ct = new int[1]; _f.GenTextures(1, ct); _collTex = ct[0]; _collDirty = true; }
                _f.ActiveTexture(GlFunctions.GL_TEXTURE1);
                _f.BindTexture(GlFunctions.GL_TEXTURE_2D, _collTex);
                if (_collDirty && _collRgb != null)
                {
                    _f.TexImage2D(GlFunctions.GL_TEXTURE_2D, 0, GlFunctions.GL_RGBA, 32, 32, 0, GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, _collRgb);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MIN_FILTER, GlFunctions.GL_NEAREST);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MAG_FILTER, GlFunctions.GL_NEAREST);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_S, GlFunctions.GL_CLAMP_TO_EDGE);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_T, GlFunctions.GL_CLAMP_TO_EDGE);
                    _collDirty = false;
                }
                _f.Uniform1i(_collLoc, 1);
                _f.Uniform1f(_tintLoc, _tintStrength);
                _f.Uniform2f(_tileOriginLoc, _tileOx, _tileOz);
                _f.Uniform2f(_tileSizeLoc, _tileSx, _tileSz);
            }
            else _f.Uniform1f(_tintLoc, 0f);

            _f.ActiveTexture(GlFunctions.GL_TEXTURE0);
            _f.BindVertexArray(_vao);

            int stride = 8 * sizeof(float);
            // Translucent textures go last, so opaque geometry behind them is already there to blend over.
            for (int pass = 0; pass < 2; pass++)
            foreach (GpuPart part in _parts)
            {
                if (part.TexAlpha != (pass == 1)) continue;
                if (_hiddenNodes != null && _hiddenNodes.Contains(part.NodeIndex)) continue;
                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, part.Vbo);
                _f.EnableVertexAttribArray(0);
                _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
                _f.EnableVertexAttribArray(1);
                _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
                _f.EnableVertexAttribArray(2);
                _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));

                int texId = part.TextureId;
                string swapName = null;
                if ((_fieldFrame != null && _fieldFrame.TryGetValue(part.MaterialKey, out swapName))
                    || (_texSwaps != null && _texSwaps.TryGetValue(part.MaterialKey, out swapName)))
                if (!string.IsNullOrEmpty(swapName))
                {
                    int swapped = SwapTexture(part.MaterialKey, swapName);
                    if (swapped != 0) texId = swapped;
                }

                if (texId != 0 && _showTextures)
                {
                    _f.BindTexture(GlFunctions.GL_TEXTURE_2D, texId);
                    _f.Uniform1i(_hasTexLoc, 1);
                }
                else _f.Uniform1i(_hasTexLoc, 0);

                // Real per-material translucency (ported from WinForms PR #209): materials like the
                // "h_kage" building drop-shadow plane or puddle overlays carry their own NSBMD alpha
                // instead of always being fully opaque (or, previously, skipped and not drawn at all).
                float alpha = part.Alpha;
                if (_fadedMaterials != null && _fadedMaterials.TryGetValue(part.MaterialKey, out float faded))
                    alpha = faded;
                // Faded all the way out is not drawn: without blending it would still cover what is behind.
                if (alpha <= 0.001f) continue;
                bool texAlpha = part.TexAlpha && texId != 0 && _showTextures;
                bool blend = alpha < 0.999f || texAlpha;
                if (blend)
                {
                    _f.Enable(GlFunctions.GL_BLEND);
                    BlendOver();
                }
                if (texAlpha)
                {
                    // Overlays like New Bark's wind lie on the ground they cover; pull them forward so the ground
                    // drawn first does not hide them.
                    _f.DepthMask(false);
                    _f.Enable(GlFunctions.GL_POLYGON_OFFSET_FILL);
                    _f.PolygonOffset(-1f, -4f);
                }
                if (_texAlphaLoc >= 0) _f.Uniform1i(_texAlphaLoc, texAlpha ? 1 : 0);
                _f.Uniform1f(_alphaLoc, alpha);

                // A texture animation replaces the material's own texture matrix while it plays, as on the DS.
                float[] texMtx = part.TexMatrix ?? IdentityTexMatrix;
                if (_texMatrices != null && _texMatrices.TryGetValue(part.MaterialKey, out float[] m) && m != null && m.Length == 9)
                    texMtx = m;
                else if (_groundMatrices != null && _groundMatrices.TryGetValue(part.MaterialKey, out float[] g) && g != null && g.Length == 9)
                    texMtx = g;
                if (_texMtxLoc >= 0) _f.UniformMatrix3fv(_texMtxLoc, 1, false, texMtx);

                if (_matColorLoc >= 0)
                {
                        (float, float, float) c = _matColours != null && _matColours.TryGetValue(part.MaterialKey, out (float r, float g, float b) got)
                        ? got : (1f, 1f, 1f);
                    _f.Uniform3f(_matColorLoc, c.Item1, c.Item2, c.Item3);
                }

                // Only the faces the DS would have drawn.
                bool cull = part.CullMode == NsbmdCull.Front || part.CullMode == NsbmdCull.Back;
                if (cull)
                {
                    _f.Enable(GlFunctions.GL_CULL_FACE);
                    _f.CullFace(part.CullMode == NsbmdCull.Front ? GlFunctions.GL_FRONT : GlFunctions.GL_BACK);
                }

                if (_fogOnLoc >= 0) _f.Uniform1i(_fogOnLoc, _fog != null && part.Fog ? 1 : 0);

                if (part.CullMode != NsbmdCull.Nothing)
                    _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, part.VertexCount);

                if (cull) _f.Disable(GlFunctions.GL_CULL_FACE);
                if (blend) _f.Disable(GlFunctions.GL_BLEND);
                if (texAlpha)
                {
                    _f.Disable(GlFunctions.GL_POLYGON_OFFSET_FILL);
                    _f.DepthMask(true);
                }
            }
            if (_texAlphaLoc >= 0) _f.Uniform1i(_texAlphaLoc, 0);
            if (_texMtxLoc >= 0) _f.UniformMatrix3fv(_texMtxLoc, 1, false, IdentityTexMatrix);
            if (_matColorLoc >= 0) _f.Uniform3f(_matColorLoc, 1f, 1f, 1f);
            if (_fogOnLoc >= 0) _f.Uniform1i(_fogOnLoc, 0);
            _f.Uniform1f(_alphaLoc, 1f);  // don't affect the overlay/marker/gizmo passes below
            _f.Uniform1f(_tintLoc, 0f);   // don't tint the overlay/marker/gizmo passes

            RenderHighlight(stride);
            RenderOverlay(stride);
            RenderSprites(stride);
            if (_spotOnLoc >= 0) _f.Uniform1i(_spotOnLoc, 0);
            RenderMarkers(stride);
            if (_showGizmos) RenderGizmos(stride);
            if (_editMode && _gizmoTargetVisible) RenderEditGizmo(stride);
            RenderScreenLayers(behind: false, stride);

            if ((_fieldFrame != null && _fieldFrame.Count > 0) || (_groundMatrices != null && _groundMatrices.Count > 0)) RequestNextFrameRendering();

            if (_captureCb != null)
            {
                Action<byte[], int, int> cb = _captureCb; _captureCb = null;
                byte[] px = null;
                try { px = new byte[pw * ph * 4]; _f.ReadPixels(0, 0, pw, ph, GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, px); }
                catch { px = null; }
                int cw = pw, ch = ph;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => cb(px, cw, ch));
            }
        }

        /// <summary>Draws the 3-axis translate handle (X red, Y green, Z blue) at the target, as
        /// camera-facing thin quads with a square grab handle at each tip. Depth test off.</summary>
        // Blends colour only. The view is composited over the window by its alpha, so a translucent part that
        // lowered the stored alpha let the panel behind show through, as grey shapes in an otherwise black scene.
        private void BlendOver() =>
            _f.BlendFuncSeparate(GlFunctions.GL_SRC_ALPHA, GlFunctions.GL_ONE_MINUS_SRC_ALPHA, GlFunctions.GL_ZERO, GlFunctions.GL_ONE);

        private void RenderEditGizmo(int stride)
        {
            // Camera basis (world space) from the orbit rotation, for billboarding the axis lines.
            float[] rot = Mat4.Multiply(Mat4.RotateX(_pitch * (float)Math.PI / 180f), Mat4.RotateY(_yaw * (float)Math.PI / 180f));
            (float x, float y, float z) fwd = (x: -rot[2], y: -rot[6], z: -rot[10]);   // camera forward in world space
            float len = GizmoLength, hw = len * 0.03f, hh = len * 0.10f;
            List<float> v = new List<float>(192);

            for (int a = 0; a < 3; a++)
            {
                (float dx, float dy, float dz) = AxisDir(a);
                // perpendicular to the axis and the view direction → keeps the line edge-on to camera.
                float px = dy * fwd.z - dz * fwd.y, py = dz * fwd.x - dx * fwd.z, pz = dx * fwd.y - dy * fwd.x;
                float pl = (float)Math.Sqrt(px * px + py * py + pz * pz);
                if (pl < 1e-4f) { px = 0; py = 1; pz = 0; pl = 1; }
                px /= pl; py /= pl; pz /= pl;
                float r = a == 0 ? 1f : 0.15f, g = a == 1 ? 1f : 0.15f, b = a == 2 ? 1f : 0.2f;
                if (a == 2) { r = 0.25f; g = 0.45f; b = 1f; }
                float ex = _gtx + dx * len, ey = _gty + dy * len, ez = _gtz + dz * len;
                // Shaft quad.
                AddQuad(v, _gtx + px * hw, _gty + py * hw, _gtz + pz * hw,
                            _gtx - px * hw, _gty - py * hw, _gtz - pz * hw,
                            ex - px * hw, ey - py * hw, ez - pz * hw,
                            ex + px * hw, ey + py * hw, ez + pz * hw, r, g, b);
                // Tip grab handle (a fatter billboarded square so it's easy to click).
                float ux = py * fwd.z - pz * fwd.y, uy = pz * fwd.x - px * fwd.z, uz = px * fwd.y - py * fwd.x;
                AddQuad(v, ex + px * hh, ey + py * hh, ez + pz * hh,
                            ex + ux * hh, ey + uy * hh, ez + uz * hh,
                            ex - px * hh, ey - py * hh, ez - pz * hh,
                            ex - ux * hh, ey - uy * hh, ez - uz * hh, r, g, b);
            }

            float[] data = v.ToArray();
            if (!_haveEditVbo) { int[] arr = new int[1]; _f.GenBuffers(1, arr); _editVbo = arr[0]; _haveEditVbo = true; }
            _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _editVbo);
            GCHandle hnd = GCHandle.Alloc(data, GCHandleType.Pinned);
            try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(data.Length * sizeof(float)), hnd.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
            finally { hnd.Free(); }

            _f.Disable(GlFunctions.GL_DEPTH_TEST);
            _f.Uniform1i(_hasTexLoc, 0);
            _f.Uniform1f(_alphaLoc, 1f);
            _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
            _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
            _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));
            _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, data.Length / 8);
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
        }

        private static void AddQuad(List<float> v, float x0, float y0, float z0, float x1, float y1, float z1,
            float x2, float y2, float z2, float x3, float y3, float z3, float r, float g, float b)
        {
            void P(float x, float y, float z) { v.Add(x); v.Add(y); v.Add(z); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(b); }
            P(x0, y0, z0); P(x1, y1, z1); P(x2, y2, z2);
            P(x0, y0, z0); P(x2, y2, z2); P(x3, y3, z3);
        }

        private void RenderGizmos(int stride)
        {
            if (_gizmoDirty)
            {
                if (_gizmoVbo != 0) { _f.DeleteBuffers(1, new[] { _gizmoVbo }); _gizmoVbo = 0; }
                if (_gizmoMesh != null && _gizmoCount > 0)
                {
                    int[] arr = new int[1]; _f.GenBuffers(1, arr); _gizmoVbo = arr[0];
                    _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _gizmoVbo);
                    GCHandle h = GCHandle.Alloc(_gizmoMesh, GCHandleType.Pinned);
                    try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(_gizmoMesh.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                    finally { h.Free(); }
                }
                _gizmoDirty = false;
            }
            if (_gizmoVbo == 0 || _gizmoCount == 0) return;

            _f.Disable(GlFunctions.GL_DEPTH_TEST);   // gizmos always visible
            _f.Uniform1i(_hasTexLoc, 0);
            _f.Uniform1f(_alphaLoc, 1f);
            _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _gizmoVbo);
            _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
            _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
            _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));
            _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, _gizmoCount);
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
        }

        private void RenderOverlay(int stride)
        {
            if (_overlayDirty)
            {
                if (_overlayVbo != 0) { _f.DeleteBuffers(1, new[] { _overlayVbo }); _overlayVbo = 0; }
                if (_overlayMesh != null && _overlayCount > 0)
                {
                    int[] arr = new int[1]; _f.GenBuffers(1, arr); _overlayVbo = arr[0];
                    _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _overlayVbo);
                    GCHandle h = GCHandle.Alloc(_overlayMesh, GCHandleType.Pinned);
                    try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(_overlayMesh.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                    finally { h.Free(); }
                }
                _overlayDirty = false;
            }
            if (_overlayVbo == 0 || _overlayCount == 0) return;

            _f.Enable(GlFunctions.GL_BLEND);
            // Translucent COLOUR tint over the tile's texture (keeps the permission colour, not a darkening shadow).
            BlendOver();
            // Depth-test ON (write OFF): trees/rocks/buildings in front occlude the tint, and their transparent
            // texels were discarded in the map pass, so the tinted ground shows through them; decorations stay clean.
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.DepthMask(false);
            _f.Uniform1i(_hasTexLoc, 0);
            _f.Uniform1f(_alphaLoc, 0.5f);

            _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _overlayVbo);
            _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
            _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
            _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));
            _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, _overlayCount);

            _f.DepthMask(true);
            _f.Disable(GlFunctions.GL_BLEND);
            _f.Uniform1f(_alphaLoc, 1f);
        }

        private void RenderHighlight(int stride)
        {
            if (_highlightDirty)
            {
                FreeHighlight();
                if (_highlight != null)
                    foreach (HighlightBatch batch in _highlight)
                    {
                        if (batch.Mesh == null || batch.Mesh.Length < 24) continue;
                        int[] arr = new int[1]; _f.GenBuffers(1, arr);
                        _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, arr[0]);
                        GCHandle h = GCHandle.Alloc(batch.Mesh, GCHandleType.Pinned);
                        try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(batch.Mesh.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                        finally { h.Free(); }
                        _highlightGpu.Add((arr[0], batch.Mesh.Length / 8, batch.MaterialKey, batch.R, batch.G, batch.B));
                    }
                _highlightDirty = false;
            }
            if (_highlightGpu.Count == 0) return;

            // Same triangles as the scene, so an equal depth passes and only the visible surface is tinted;
            // the material's own texture keeps leaves leaf-shaped instead of tinting the whole quad.
            _f.Enable(GlFunctions.GL_BLEND);
            BlendOver();
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.DepthFunc(GlFunctions.GL_LEQUAL);
            _f.DepthMask(false);
            _f.Disable(GlFunctions.GL_CULL_FACE);
            _f.Uniform1f(_alphaLoc, 0.6f);
            if (_texMtxLoc >= 0) _f.UniformMatrix3fv(_texMtxLoc, 1, false, IdentityTexMatrix);

            foreach ((int Vbo, int Count, int MaterialKey, float R, float G, float B) h in _highlightGpu)
            {
                int texId = 0;
                foreach (GpuPart part in _parts) if (part.MaterialKey == h.MaterialKey) { texId = part.TextureId; break; }
                if (texId != 0 && _showTextures) { _f.BindTexture(GlFunctions.GL_TEXTURE_2D, texId); _f.Uniform1i(_hasTexLoc, 1); }
                else _f.Uniform1i(_hasTexLoc, 0);
                if (_matColorLoc >= 0) _f.Uniform3f(_matColorLoc, h.R, h.G, h.B);

                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, h.Vbo);
                _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
                _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
                _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));
                _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, h.Count);
            }

            if (_matColorLoc >= 0) _f.Uniform3f(_matColorLoc, 1f, 1f, 1f);
            _f.DepthMask(true);
            _f.DepthFunc(GlFunctions.GL_LESS);
            _f.Disable(GlFunctions.GL_BLEND);
            _f.Uniform1f(_alphaLoc, 1f);
        }

        private void RenderMarkers(int stride)
        {
            if (_markerDirty)
            {
                if (_markerVbo != 0) { _f.DeleteBuffers(1, new[] { _markerVbo }); _markerVbo = 0; }
                if (_markerMesh != null && _markerCount > 0)
                {
                    int[] arr = new int[1]; _f.GenBuffers(1, arr); _markerVbo = arr[0];
                    _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _markerVbo);
                    GCHandle h = GCHandle.Alloc(_markerMesh, GCHandleType.Pinned);
                    try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(_markerMesh.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                    finally { h.Free(); }
                }
                _markerDirty = false;
            }
            if (_markerVbo == 0 || _markerCount == 0) return;

            _f.Enable(GlFunctions.GL_BLEND);
            BlendOver();
            _f.Disable(GlFunctions.GL_DEPTH_TEST);   // markers always visible, even through geometry
            _f.Uniform1i(_hasTexLoc, 0);
            _f.Uniform1f(_alphaLoc, 0.92f);

            _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _markerVbo);
            _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
            _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
            _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));
            _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, _markerCount);

            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.Disable(GlFunctions.GL_BLEND);
            _f.Uniform1f(_alphaLoc, 1f);
        }

        private void RenderSprites(int stride)
        {
            if (_spritesDirty) UploadSprites();
            if (_gpuSprites.Count == 0) return;
            if (_spriteVbo == 0) { int[] a = new int[1]; _f.GenBuffers(1, a); _spriteVbo = a[0]; }

            // Full camera-facing billboard: both "right" and "up" rotate with the camera (yaw + pitch),
            // so the sprite always faces the viewer head-on instead of only staying upright.
            float yaw = _yaw * (float)Math.PI / 180f;
            float pitch = _pitch * (float)Math.PI / 180f;
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            float cp = (float)Math.Cos(pitch), sp = (float)Math.Sin(pitch);
            float rx = cy, rz = sy;
            float ux = sy * sp, uy = cp, uz = -cy * sp;
            // Towards the camera, which is right cross up.
            float tx = -sy * cp, ty = sp, tz = cy * cp;

            _f.Enable(GlFunctions.GL_BLEND);
            BlendOver();
            _f.DepthMask(false);                 // sit in the scene but don't write depth
            // In the editor a sprite shows through geometry on purpose: an NPC behind a tall counter or
            // wall prop would otherwise be hidden, and you need to see every event you have placed.
            if (_spritesSeeThrough) _f.Disable(GlFunctions.GL_DEPTH_TEST);
            else _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.Uniform1f(_alphaLoc, 1f);
            _f.Uniform1i(_texLoc, 0);
            _f.ActiveTexture(GlFunctions.GL_TEXTURE0);

            // Without depth writes the last sprite drawn wins, so the farthest go first.
            List<GpuSprite> order = new List<GpuSprite>(_gpuSprites);
            order.Sort((a, b) => (a.Cx * tx + a.Cy * ty + a.Cz * tz).CompareTo(b.Cx * tx + b.Cy * ty + b.Cz * tz));

            float[] buf = new float[6 * 8];
            foreach (GpuSprite s in order)
            {
                float turn = s.Roll * (float)(Math.PI / 180.0);
                float ct = (float)Math.Cos(turn), st = (float)Math.Sin(turn);

                float r3x = rx, r3y = 0f, r3z = rz;
                float nrx = r3x * ct + ux * st, nry = r3y * ct + uy * st, nrz = r3z * ct + uz * st;
                float nux = ux * ct - r3x * st, nuy = uy * ct - r3y * st, nuz = uz * ct - r3z * st;

                float ax = nrx * s.HalfW, ay = nry * s.HalfW, az = nrz * s.HalfW;
                float bx = nux * (s.HalfH * 2f), by = nuy * (s.HalfH * 2f), bz = nuz * (s.HalfH * 2f);
                float footX = s.Cx - bx * 0.5f, footY = s.Cy - by * 0.5f, footZ = s.Cz - bz * 0.5f;
                float blx = footX - ax, bly = footY - ay, blz = footZ - az;
                float brx = footX + ax, bry = footY + ay, brz = footZ + az;
                float trx = footX + ax + bx, try_ = footY + ay + by, trz = footZ + az + bz;
                float tlx = footX - ax + bx, tly = footY - ay + by, tlz = footZ - az + bz;
                int i = 0;
                void V(float x, float y, float z, float u, float w)
                { buf[i++] = x; buf[i++] = y; buf[i++] = z; buf[i++] = u; buf[i++] = w; buf[i++] = 1f; buf[i++] = 1f; buf[i++] = 1f; }
                V(blx, bly, blz, 0, 1); V(brx, bry, brz, 1, 1); V(trx, try_, trz, 1, 0);
                V(blx, bly, blz, 0, 1); V(trx, try_, trz, 1, 0); V(tlx, tly, tlz, 0, 0);

                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _spriteVbo);
                GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
                try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(buf.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                finally { h.Free(); }

                _f.EnableVertexAttribArray(0); _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
                _f.EnableVertexAttribArray(1); _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
                _f.EnableVertexAttribArray(2); _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));

                _f.BindTexture(GlFunctions.GL_TEXTURE_2D, s.Tex);
                _f.Uniform1i(_hasTexLoc, 1);
                _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, 6);
            }

            _f.Uniform1i(_hasTexLoc, 0);
            _f.DepthMask(true);
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.Disable(GlFunctions.GL_BLEND);
        }

        private void SetError(string err)
        {
            if (_error == err) return;
            _error = err;
            ErrorChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Self-test cube as a render model (one untextured part) ─────────────────────
        private static NsbmdRenderModel CubeModel()
        {
            float s = 0.8f;
            (float, float, float)[] col =
            { (0.9f,0.3f,0.3f),(0.3f,0.9f,0.3f),(0.3f,0.3f,0.9f),(0.9f,0.9f,0.3f),(0.9f,0.3f,0.9f),(0.3f,0.9f,0.9f) };
            (float, float, float)[,] faces =
            {
                {(-s,-s, s),( s,-s, s),( s, s, s),(-s,-s, s),( s, s, s),(-s, s, s)},
                {( s,-s,-s),(-s,-s,-s),(-s, s,-s),( s,-s,-s),(-s, s,-s),( s, s,-s)},
                {( s,-s, s),( s,-s,-s),( s, s,-s),( s,-s, s),( s, s,-s),( s, s, s)},
                {(-s,-s,-s),(-s,-s, s),(-s, s, s),(-s,-s,-s),(-s, s, s),(-s, s,-s)},
                {(-s, s, s),( s, s, s),( s, s,-s),(-s, s, s),( s, s,-s),(-s, s,-s)},
                {(-s,-s,-s),( s,-s,-s),( s,-s, s),(-s,-s,-s),( s,-s, s),(-s,-s, s)},
            };

            float[] data = new float[6 * 6 * 8];
            int idx = 0;
            for (int face = 0; face < 6; face++)
                for (int vtx = 0; vtx < 6; vtx++)
                {
                    (float, float, float) p = faces[face, vtx];
                    data[idx++] = p.Item1; data[idx++] = p.Item2; data[idx++] = p.Item3;
                    data[idx++] = 0f; data[idx++] = 0f; // uv
                    data[idx++] = col[face].Item1; data[idx++] = col[face].Item2; data[idx++] = col[face].Item3;
                }

            NsbmdRenderModel model = new NsbmdRenderModel { TotalVertices = 36 };
            model.Parts.Add(new NsbmdMeshPart { MaterialIndex = -1, Vertices = data, VertexCount = 36 });
            return model;
        }
    }
}
