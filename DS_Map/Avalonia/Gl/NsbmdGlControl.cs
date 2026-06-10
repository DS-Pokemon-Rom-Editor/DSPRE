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
        private string _error;

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

        // Optional textured billboard sprites (e.g. overworld sprites). Each carries a centre
        // in normalized space + half extents; the quad is rebuilt each frame to face the camera.
        private IReadOnlyList<SpriteInstance> _sprites;
        private readonly List<GpuSprite> _gpuSprites = new List<GpuSprite>();
        private int _spriteVbo;
        private bool _spritesDirty;

        // ── Camera ───────────────────────────────────────────────────────────────────
        private float _yaw = 30f, _pitch = 20f, _distance = 4f;
        public float Yaw { get => _yaw; set { _yaw = value; RequestNextFrameRendering(); } }
        public float Pitch { get => _pitch; set { _pitch = Math.Max(-89f, Math.Min(89f, value)); RequestNextFrameRendering(); } }
        public float Distance { get => _distance; set { _distance = Math.Max(0.2f, value); RequestNextFrameRendering(); } }

        public string LastError => _error;
        public event EventHandler ErrorChanged;

        public NsbmdGlControl() => _model = CubeModel();

        public void ShowTestCube() { SetModel(CubeModel()); }

        public void SetModel(NsbmdRenderModel model)
        {
            _model = model;
            _uploadPending = true;
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
                    "out vec2 vUv;\nout vec3 vColor;\n" +
                    "void main(){ vUv = aUv; vColor = aColor; gl_Position = uMvp * vec4(aPos, 1.0); }\n";
                string fs = header +
                    "uniform sampler2D uTex;\nuniform int uHasTex;\nuniform float uAlpha;\n" +
                    "in vec2 vUv;\nin vec3 vColor;\nout vec4 fragColor;\n" +
                    "void main(){\n" +
                    "  if (uHasTex == 1) { vec4 t = texture(uTex, vUv); if (t.a < 0.5) discard; fragColor = vec4(t.rgb, uAlpha); }\n" +
                    "  else { fragColor = vec4(vColor, uAlpha); }\n" +
                    "}\n";

                int v = _f.CompileShaderOrThrow(GlFunctions.GL_VERTEX_SHADER, vs);
                int f = _f.CompileShaderOrThrow(GlFunctions.GL_FRAGMENT_SHADER, fs);
                _program = _f.LinkProgramOrThrow(v, f);
                _mvpLoc = _f.GetUniformLocation(_program, "uMvp");
                _texLoc = _f.GetUniformLocation(_program, "uTex");
                _hasTexLoc = _f.GetUniformLocation(_program, "uHasTex");
                _alphaLoc = _f.GetUniformLocation(_program, "uAlpha");

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
                if (_overlayVbo != 0) _f?.DeleteBuffers(1, new[] { _overlayVbo });
                if (_markerVbo != 0) _f?.DeleteBuffers(1, new[] { _markerVbo });
                if (_spriteVbo != 0) _f?.DeleteBuffers(1, new[] { _spriteVbo });
                if (_vao != 0) _f?.DeleteVertexArrays(1, new[] { _vao });
            }
            catch { }
            _f = null; _program = _vao = _overlayVbo = _markerVbo = _spriteVbo = 0;
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

        private void FreeGpuSprites()
        {
            if (_f == null) return;
            foreach (var s in _gpuSprites)
                if (s.Tex != 0) _f.DeleteTextures(1, new[] { s.Tex });
            _gpuSprites.Clear();
        }

        private void UploadSprites()
        {
            FreeGpuSprites();
            if (_sprites != null)
                foreach (var s in _sprites)
                {
                    if (s.Rgba == null || s.Width <= 0 || s.Height <= 0) continue;
                    var arr = new int[1];
                    _f.GenTextures(1, arr); int id = arr[0];
                    _f.BindTexture(GlFunctions.GL_TEXTURE_2D, id);
                    _f.TexImage2D(GlFunctions.GL_TEXTURE_2D, 0, GlFunctions.GL_RGBA, s.Width, s.Height, 0,
                        GlFunctions.GL_RGBA, GlFunctions.GL_UNSIGNED_BYTE, s.Rgba);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MIN_FILTER, GlFunctions.GL_NEAREST);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_MAG_FILTER, GlFunctions.GL_NEAREST);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_S, GlFunctions.GL_CLAMP_TO_EDGE);
                    _f.TexParameteri(GlFunctions.GL_TEXTURE_2D, GlFunctions.GL_TEXTURE_WRAP_T, GlFunctions.GL_CLAMP_TO_EDGE);
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
            var view = Mat4.OrbitView(_distance, _yaw, _pitch);
            var mvp = Mat4.Multiply(proj, view);

            _f.UseProgram(_program);
            _f.UniformMatrix4fv(_mvpLoc, 1, false, mvp);
            _f.Uniform1i(_texLoc, 0);
            _f.Uniform1f(_alphaLoc, 1f);
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

            RenderOverlay(stride);
            RenderSprites(stride);
            RenderMarkers(stride);
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
            _f.BlendFunc(GlFunctions.GL_SRC_ALPHA, GlFunctions.GL_ONE_MINUS_SRC_ALPHA);
            _f.DepthMask(false);                 // don't write depth — overlay shouldn't occlude
            _f.Uniform1i(_hasTexLoc, 0);
            _f.Uniform1f(_alphaLoc, 0.55f);

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
