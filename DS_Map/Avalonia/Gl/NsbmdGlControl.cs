using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        private struct GpuPart { public int Vbo; public int VertexCount; public int TextureId; }

        private GlFunctions _f;
        private int _program, _vao, _mvpLoc, _texLoc, _hasTexLoc, _alphaLoc;
        private int _tintLoc, _tileOriginLoc, _tileSizeLoc, _collLoc;
        private string _error;

        // Per-tile permission tint of the map textures (mesh overlay mode). A 32×32 collision-colour texture is
        // sampled in the fragment shader; only the tint texture is re-uploaded on paint (cheap).
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
        private bool _markerDirty;

        // Debug gizmo lines (cell boundaries / geometry extents), 8 floats/vertex. Comes from the model.
        private float[] _gizmoMesh;
        private int _gizmoVbo, _gizmoCount;
        private bool _gizmoDirty;
        private bool _showGizmos;
        public bool ShowGizmos { get => _showGizmos; set { _showGizmos = value; RequestNextFrameRendering(); } }

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
            var m = _lastMvp;
            float cx = m[0] * x + m[4] * y + m[8] * z + m[12];
            float cy = m[1] * x + m[5] * y + m[9] * z + m[13];
            float cw = m[3] * x + m[7] * y + m[11] * z + m[15];
            if (cw <= 1e-5f) return false;
            sx = (cx / cw * 0.5f + 0.5f) * _lastLogW;
            sy = (1f - (cy / cw * 0.5f + 0.5f)) * _lastLogH;
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
                var (dx, dy, dz) = AxisDir(a);
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
            var (ax, ay, az) = AxisDir(axis);
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
        private float _targetX, _targetY, _targetZ;   // pivot the orbit looks at (for panning)
        public float Yaw { get => _yaw; set { _yaw = value; RequestNextFrameRendering(); } }
        public float Pitch { get => _pitch; set { _pitch = Math.Max(-89f, Math.Min(89f, value)); RequestNextFrameRendering(); } }
        public float Distance { get => _distance; set { _distance = Math.Max(0.2f, value); RequestNextFrameRendering(); } }

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

        /// <summary>Recentres the camera pivot.</summary>
        public void ResetView() { _targetX = _targetY = _targetZ = 0f; RequestNextFrameRendering(); }

        // ── Mouse-driven camera input (honours the user's camera preferences) ─────────────
        // All host views route their right-drag / left-drag / wheel gestures through these so the
        // behaviour (speed + axis inversion) is consistent and configurable in Settings.
        private static DspreSettings Cam => SettingsManager.Settings;

        /// <summary>Orbit (rotate) the camera from a mouse drag, in raw screen-pixel deltas.</summary>
        public void OrbitByDrag(float screenDx, float screenDy)
        {
            var c = Cam;
            float spd = (c?.camOrbitSpeed ?? 1f) * 0.5f;
            Yaw   += screenDx * spd * ((c?.camInvertOrbitX ?? false) ? -1f : 1f);
            Pitch += screenDy * spd * ((c?.camInvertOrbitY ?? false) ? -1f : 1f);
        }

        /// <summary>Pan (slide) the camera pivot from a mouse drag, in raw screen-pixel deltas. The default
        /// direction grabs the world (the scene follows the cursor); invert flags flip each axis.</summary>
        public void PanByDrag(float screenDx, float screenDy)
        {
            var c = Cam;
            float spd = c?.camPanSpeed ?? 1f;
            PanByScreen(screenDx * spd * ((c?.camInvertPanX ?? false) ? -1f : 1f),
                        screenDy * spd * ((c?.camInvertPanY ?? false) ? -1f : 1f));
        }

        /// <summary>Zoom from a mouse-wheel notch (raw wheel delta-Y).</summary>
        public void ZoomByWheel(float wheelDeltaY)
        {
            var c = Cam;
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

        /// <summary>Sets a marker mesh (8 floats/vertex: pos,uv,col) drawn on top of everything
        /// with the depth test disabled — e.g. event markers — or null to clear.</summary>
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
        }

        private struct GpuSprite { public int Tex; public float Cx, Cy, Cz, HalfW, HalfH; }

        /// <summary>Sets the textured billboard sprites (or null to clear).</summary>
        public void SetSprites(IReadOnlyList<SpriteInstance> sprites)
        {
            _sprites = sprites;
            _spritesDirty = true;
            RequestNextFrameRendering();
        }

        // ── GL lifecycle ───────────────────────────────────────────────────────────────
        protected override void OnOpenGlInit(GlInterface gl)
        {
            try
            {
                _f = new GlFunctions(gl);
                bool es = GlVersion.Type == GlProfileType.OpenGLES;
                string header = es ? "#version 300 es\nprecision highp float;\n" : "#version 330 core\n";

                string vs = header +
                    "layout(location=0) in vec3 aPos;\n" +
                    "layout(location=1) in vec2 aUv;\n" +
                    "layout(location=2) in vec3 aColor;\n" +
                    "uniform mat4 uMvp;\n" +
                    "out vec2 vUv;\nout vec3 vColor;\nout vec2 vWorld;\n" +
                    "void main(){ vUv = aUv; vColor = aColor; vWorld = aPos.xz; gl_Position = uMvp * vec4(aPos, 1.0); }\n";
                string fs = header +
                    "uniform sampler2D uTex;\nuniform int uHasTex;\nuniform float uAlpha;\n" +
                    // Per-tile permission tint (uTint>0): sample a 32x32 collision-colour texture by the fragment's
                    // world-tile and mix it into the surface AFTER the alpha discard, so the collision colour follows
                    // the real texture shape (trees/lamps tinted on their pixels; transparent texels stay clear).
                    "uniform float uTint;\nuniform vec2 uTileOrigin;\nuniform vec2 uTileSize;\nuniform sampler2D uColl;\n" +
                    "in vec2 vUv;\nin vec3 vColor;\nin vec2 vWorld;\nout vec4 fragColor;\n" +
                    "vec3 tintRgb(vec3 c){\n" +
                    "  if (uTint <= 0.0) return c;\n" +
                    "  vec2 tc = (vWorld - uTileOrigin) / uTileSize;\n" +
                    "  vec2 cuv = (clamp(floor(tc), 0.0, 31.0) + 0.5) / 32.0;\n" +
                    "  return mix(c, texture(uColl, cuv).rgb, uTint);\n" +
                    "}\n" +
                    "void main(){\n" +
                    "  if (uHasTex == 1) { vec4 t = texture(uTex, vUv); if (t.a < 0.5) discard; fragColor = vec4(tintRgb(t.rgb), uAlpha); }\n" +
                    "  else { fragColor = vec4(tintRgb(vColor), uAlpha); }\n" +
                    "}\n";

                int v = _f.CompileShaderOrThrow(GlFunctions.GL_VERTEX_SHADER, vs);
                int f = _f.CompileShaderOrThrow(GlFunctions.GL_FRAGMENT_SHADER, fs);
                _program = _f.LinkProgramOrThrow(v, f);
                _mvpLoc = _f.GetUniformLocation(_program, "uMvp");
                _texLoc = _f.GetUniformLocation(_program, "uTex");
                _hasTexLoc = _f.GetUniformLocation(_program, "uHasTex");
                _alphaLoc = _f.GetUniformLocation(_program, "uAlpha");
                _tintLoc = _f.GetUniformLocation(_program, "uTint");
                _tileOriginLoc = _f.GetUniformLocation(_program, "uTileOrigin");
                _tileSizeLoc = _f.GetUniformLocation(_program, "uTileSize");
                _collLoc = _f.GetUniformLocation(_program, "uColl");

                var arr = new int[1];
                _f.GenVertexArrays(1, arr); _vao = arr[0];
                _uploadPending = true;
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
                if (_spriteVbo != 0) _f?.DeleteBuffers(1, new[] { _spriteVbo });
                if (_gizmoVbo != 0) _f?.DeleteBuffers(1, new[] { _gizmoVbo });
                if (_haveEditVbo && _editVbo != 0) _f?.DeleteBuffers(1, new[] { _editVbo });
                if (_vao != 0) _f?.DeleteVertexArrays(1, new[] { _vao });
            }
            catch { }
            _f = null; _program = _vao = _overlayVbo = _markerVbo = _spriteVbo = _gizmoVbo = 0;
            _haveEditVbo = false; _editVbo = 0;
        }

        private void FreeGpuParts()
        {
            if (_f == null) return;
            foreach (var p in _parts)
            {
                if (p.Vbo != 0) _f.DeleteBuffers(1, new[] { p.Vbo });
                if (p.TextureId != 0) _f.DeleteTextures(1, new[] { p.TextureId });
            }
            _parts.Clear();
        }

        // Sprite GPU textures are cached by their pixel-buffer reference (OverworldSprites.Get returns the
        // SAME cached array for a given sprite), so re-positioning sprites during a drag only rebuilds the
        // lightweight GpuSprite list — it does NOT re-upload textures (that was the move-gizmo lag).
        private readonly Dictionary<byte[], int> _spriteTexCache = new Dictionary<byte[], int>();

        private void FreeGpuSprites()
        {
            if (_f != null)
                foreach (var kv in _spriteTexCache)
                    if (kv.Value != 0) _f.DeleteTextures(1, new[] { kv.Value });
            _spriteTexCache.Clear();
            _gpuSprites.Clear();
        }

        private void UploadSprites()
        {
            _gpuSprites.Clear();
            if (_sprites != null)
                foreach (var s in _sprites)
                {
                    if (s.Rgba == null || s.Width <= 0 || s.Height <= 0) continue;
                    if (!_spriteTexCache.TryGetValue(s.Rgba, out int id))
                    {
                        var arr = new int[1];
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
                    _gpuSprites.Add(new GpuSprite { Tex = id, Cx = s.Cx, Cy = s.Cy, Cz = s.Cz, HalfW = s.HalfW, HalfH = s.HalfH });
                }
            _spritesDirty = false;
        }

        private void Upload()
        {
            FreeGpuParts();
            if (_model == null) return;

            foreach (var part in _model.Parts)
            {
                if (part.VertexCount == 0) continue;
                var arr = new int[1];
                _f.GenBuffers(1, arr); int vbo = arr[0];
                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, vbo);
                var h = GCHandle.Alloc(part.Vertices, GCHandleType.Pinned);
                try
                {
                    _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(part.Vertices.Length * sizeof(float)),
                        h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW);
                }
                finally { h.Free(); }

                int texId = 0;
                if (_model.Textures != null && _model.Textures.TryGetValue(part.MaterialIndex, out var tex) && tex?.Rgba != null)
                    texId = UploadTexture(tex);

                _parts.Add(new GpuPart { Vbo = vbo, VertexCount = part.VertexCount, TextureId = texId });
            }
            _uploadPending = false;
        }

        private int UploadTexture(NsbmdTextureData tex)
        {
            var arr = new int[1];
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

        private static int WrapGl(int w) => w == 2 ? GlFunctions.GL_MIRRORED_REPEAT : w == 1 ? GlFunctions.GL_REPEAT : GlFunctions.GL_CLAMP_TO_EDGE;

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (_f == null || _program == 0) return;
            if (_uploadPending) Upload();

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            int pw = Math.Max(1, (int)(Bounds.Width * scaling));
            int ph = Math.Max(1, (int)(Bounds.Height * scaling));

            _f.Viewport(0, 0, pw, ph);
            _f.ClearColor(0.12f, 0.12f, 0.14f, 1f);
            _f.Enable(GlFunctions.GL_DEPTH_TEST);
            _f.Clear(GlFunctions.GL_COLOR_BUFFER_BIT | GlFunctions.GL_DEPTH_BUFFER_BIT);

            if (_parts.Count == 0) return;

            float aspect = ph == 0 ? 1f : (float)pw / ph;
            var proj = Mat4.Perspective(45f * (float)Math.PI / 180f, aspect, 0.05f, 1000f);
            var view = Mat4.Multiply(Mat4.OrbitView(_distance, _yaw, _pitch), Mat4.Translate(-_targetX, -_targetY, -_targetZ));
            var mvp = Mat4.Multiply(proj, view);
            _lastMvp = mvp; _lastLogW = (float)Math.Max(1.0, Bounds.Width); _lastLogH = (float)Math.Max(1.0, Bounds.Height);

            _f.UseProgram(_program);
            _f.UniformMatrix4fv(_mvpLoc, 1, false, mvp);
            _f.Uniform1i(_texLoc, 0);
            _f.Uniform1f(_alphaLoc, 1f);

            // Per-tile permission tint of the map textures (mesh overlay mode): upload the 32×32 collision-colour
            // texture to unit 1 and hand the shader the tile grid. uTint>0 mixes it into each opaque texel.
            if (_tintOn)
            {
                if (_collTex == 0) { var ct = new int[1]; _f.GenTextures(1, ct); _collTex = ct[0]; _collDirty = true; }
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
            foreach (var part in _parts)
            {
                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, part.Vbo);
                _f.EnableVertexAttribArray(0);
                _f.VertexAttribPointer(0, 3, GlFunctions.GL_FLOAT, false, stride, IntPtr.Zero);
                _f.EnableVertexAttribArray(1);
                _f.VertexAttribPointer(1, 2, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(3 * sizeof(float)));
                _f.EnableVertexAttribArray(2);
                _f.VertexAttribPointer(2, 3, GlFunctions.GL_FLOAT, false, stride, (IntPtr)(5 * sizeof(float)));

                if (part.TextureId != 0)
                {
                    _f.BindTexture(GlFunctions.GL_TEXTURE_2D, part.TextureId);
                    _f.Uniform1i(_hasTexLoc, 1);
                }
                else _f.Uniform1i(_hasTexLoc, 0);

                _f.DrawArrays(GlFunctions.GL_TRIANGLES, 0, part.VertexCount);
            }
            _f.Uniform1f(_tintLoc, 0f);   // don't tint the overlay/marker/gizmo passes

            RenderOverlay(stride);
            RenderSprites(stride);
            RenderMarkers(stride);
            if (_showGizmos) RenderGizmos(stride);
            if (_editMode && _gizmoTargetVisible) RenderEditGizmo(stride);

            if (_captureCb != null)
            {
                var cb = _captureCb; _captureCb = null;
                byte[] px = null;
                try { px = new byte[pw * ph * 4]; _f.ReadPixels(0, 0, pw, ph, GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, px); }
                catch { px = null; }
                int cw = pw, ch = ph;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => cb(px, cw, ch));
            }
        }

        /// <summary>Draws the 3-axis translate handle (X red, Y green, Z blue) at the target, as
        /// camera-facing thin quads with a square grab handle at each tip. Depth test off.</summary>
        private void RenderEditGizmo(int stride)
        {
            // Camera basis (world space) from the orbit rotation, for billboarding the axis lines.
            var rot = Mat4.Multiply(Mat4.RotateX(_pitch * (float)Math.PI / 180f), Mat4.RotateY(_yaw * (float)Math.PI / 180f));
            var fwd = (x: -rot[2], y: -rot[6], z: -rot[10]);   // camera forward in world space
            float len = GizmoLength, hw = len * 0.03f, hh = len * 0.10f;
            var v = new List<float>(192);

            for (int a = 0; a < 3; a++)
            {
                var (dx, dy, dz) = AxisDir(a);
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

            var data = v.ToArray();
            if (!_haveEditVbo) { var arr = new int[1]; _f.GenBuffers(1, arr); _editVbo = arr[0]; _haveEditVbo = true; }
            _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _editVbo);
            var hnd = GCHandle.Alloc(data, GCHandleType.Pinned);
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
                    var arr = new int[1]; _f.GenBuffers(1, arr); _gizmoVbo = arr[0];
                    _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _gizmoVbo);
                    var h = GCHandle.Alloc(_gizmoMesh, GCHandleType.Pinned);
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
                    var arr = new int[1]; _f.GenBuffers(1, arr); _overlayVbo = arr[0];
                    _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _overlayVbo);
                    var h = GCHandle.Alloc(_overlayMesh, GCHandleType.Pinned);
                    try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(_overlayMesh.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                    finally { h.Free(); }
                }
                _overlayDirty = false;
            }
            if (_overlayVbo == 0 || _overlayCount == 0) return;

            _f.Enable(GlFunctions.GL_BLEND);
            // Translucent COLOUR tint over the tile's texture (keeps the permission colour, not a darkening shadow).
            _f.BlendFunc(GlFunctions.GL_SRC_ALPHA, GlFunctions.GL_ONE_MINUS_SRC_ALPHA);
            // Depth-test ON (write OFF): trees/rocks/buildings in front occlude the tint, and their transparent
            // texels were discarded in the map pass, so the tinted ground shows through them — decorations stay clean.
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

        private void RenderMarkers(int stride)
        {
            if (_markerDirty)
            {
                if (_markerVbo != 0) { _f.DeleteBuffers(1, new[] { _markerVbo }); _markerVbo = 0; }
                if (_markerMesh != null && _markerCount > 0)
                {
                    var arr = new int[1]; _f.GenBuffers(1, arr); _markerVbo = arr[0];
                    _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _markerVbo);
                    var h = GCHandle.Alloc(_markerMesh, GCHandleType.Pinned);
                    try { _f.BufferData(GlFunctions.GL_ARRAY_BUFFER, (IntPtr)(_markerMesh.Length * sizeof(float)), h.AddrOfPinnedObject(), GlFunctions.GL_STATIC_DRAW); }
                    finally { h.Free(); }
                }
                _markerDirty = false;
            }
            if (_markerVbo == 0 || _markerCount == 0) return;

            _f.Enable(GlFunctions.GL_BLEND);
            _f.BlendFunc(GlFunctions.GL_SRC_ALPHA, GlFunctions.GL_ONE_MINUS_SRC_ALPHA);
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
            if (_spriteVbo == 0) { var a = new int[1]; _f.GenBuffers(1, a); _spriteVbo = a[0]; }

            // Upright billboard: horizontal "right" faces the camera, "up" stays world-vertical.
            float yaw = _yaw * (float)Math.PI / 180f;
            float rx = (float)Math.Cos(yaw), rz = (float)Math.Sin(yaw);

            _f.Enable(GlFunctions.GL_BLEND);
            _f.BlendFunc(GlFunctions.GL_SRC_ALPHA, GlFunctions.GL_ONE_MINUS_SRC_ALPHA);
            _f.DepthMask(false);                 // sit in the scene but don't write depth
            _f.Uniform1f(_alphaLoc, 1f);
            _f.Uniform1i(_texLoc, 0);
            _f.ActiveTexture(GlFunctions.GL_TEXTURE0);

            var buf = new float[6 * 8];
            foreach (var s in _gpuSprites)
            {
                float ax = rx * s.HalfW, az = rz * s.HalfW;  // right * halfW
                float uy = s.HalfH;                           // up * halfH
                // corners: BL, BR, TR, TL  with uv (0,1)(1,1)(1,0)(0,0)
                float blx = s.Cx - ax, bly = s.Cy - uy, blz = s.Cz - az;
                float brx = s.Cx + ax, bry = s.Cy - uy, brz = s.Cz + az;
                float trx = s.Cx + ax, try_ = s.Cy + uy, trz = s.Cz + az;
                float tlx = s.Cx - ax, tly = s.Cy + uy, tlz = s.Cz - az;
                int i = 0;
                void V(float x, float y, float z, float u, float w)
                { buf[i++] = x; buf[i++] = y; buf[i++] = z; buf[i++] = u; buf[i++] = w; buf[i++] = 1f; buf[i++] = 1f; buf[i++] = 1f; }
                V(blx, bly, blz, 0, 1); V(brx, bry, brz, 1, 1); V(trx, try_, trz, 1, 0);
                V(blx, bly, blz, 0, 1); V(trx, try_, trz, 1, 0); V(tlx, tly, tlz, 0, 0);

                _f.BindBuffer(GlFunctions.GL_ARRAY_BUFFER, _spriteVbo);
                var h = GCHandle.Alloc(buf, GCHandleType.Pinned);
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

            var data = new float[6 * 6 * 8];
            int idx = 0;
            for (int face = 0; face < 6; face++)
                for (int vtx = 0; vtx < 6; vtx++)
                {
                    var p = faces[face, vtx];
                    data[idx++] = p.Item1; data[idx++] = p.Item2; data[idx++] = p.Item3;
                    data[idx++] = 0f; data[idx++] = 0f; // uv
                    data[idx++] = col[face].Item1; data[idx++] = col[face].Item2; data[idx++] = col[face].Item3;
                }

            var model = new NsbmdRenderModel { TotalVertices = 36 };
            model.Parts.Add(new NsbmdMeshPart { MaterialIndex = -1, Vertices = data, VertexCount = 36 });
            return model;
        }
    }
}
