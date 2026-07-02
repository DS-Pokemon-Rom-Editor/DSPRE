using System;
using System.Runtime.InteropServices;
using System.Text;
using global::Avalonia.OpenGL;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Minimal modern-GL function set bound through <see cref="GlInterface.GetProcAddress"/>.
    /// Binding by proc-address (rather than relying on a specific Avalonia GlInterface
    /// member surface) keeps this robust across Avalonia/driver variations. Covers the
    /// subset needed for VBO + VAO + shader rendering.
    /// </summary>
    public sealed class GlFunctions
    {
        // ── Delegate signatures ──────────────────────────────────────────────────────
        public delegate void GenBuffersDelegate(int n, int[] buffers);
        public delegate void BindBufferDelegate(int target, int buffer);
        public delegate void BufferDataDelegate(int target, IntPtr size, IntPtr data, int usage);
        public delegate void DeleteBuffersDelegate(int n, int[] buffers);

        public delegate void GenVertexArraysDelegate(int n, int[] arrays);
        public delegate void BindVertexArrayDelegate(int array);
        public delegate void DeleteVertexArraysDelegate(int n, int[] arrays);

        public delegate int CreateShaderDelegate(int type);
        public delegate void ShaderSourceDelegate(int shader, int count, string[] str, int[] length);
        public delegate void CompileShaderDelegate(int shader);
        public delegate void GetShaderivDelegate(int shader, int pname, out int result);
        public delegate void GetShaderInfoLogDelegate(int shader, int maxLength, out int length, StringBuilder infoLog);

        public delegate int CreateProgramDelegate();
        public delegate void AttachShaderDelegate(int program, int shader);
        public delegate void LinkProgramDelegate(int program);
        public delegate void GetProgramivDelegate(int program, int pname, out int result);
        public delegate void UseProgramDelegate(int program);
        public delegate int GetUniformLocationDelegate(int program, string name);
        public delegate int GetAttribLocationDelegate(int program, string name);

        public delegate void EnableVertexAttribArrayDelegate(int index);
        public delegate void VertexAttribPointerDelegate(int index, int size, int type, bool normalized, int stride, IntPtr pointer);
        public delegate void UniformMatrix4fvDelegate(int location, int count, bool transpose, float[] value);
        public delegate void Uniform3fDelegate(int location, float x, float y, float z);

        public delegate void Uniform1iDelegate(int location, int v);
        public delegate void DrawArraysDelegate(int mode, int first, int count);
        public delegate void ClearColorDelegate(float r, float g, float b, float a);
        public delegate void ClearDelegate(int mask);
        public delegate void ViewportDelegate(int x, int y, int w, int h);
        public delegate void EnableDelegate(int cap);
        public delegate void DisableDelegate(int cap);

        public delegate void GenTexturesDelegate(int n, int[] textures);
        public delegate void DeleteTexturesDelegate(int n, int[] textures);
        public delegate void BindTextureDelegate(int target, int texture);
        public delegate void ActiveTextureDelegate(int texture);
        public delegate void TexImage2DDelegate(int target, int level, int internalFormat, int width, int height, int border, int format, int type, byte[] pixels);
        public delegate void TexParameteriDelegate(int target, int pname, int param);
        public delegate void BlendFuncDelegate(int sfactor, int dfactor);
        public delegate void Uniform1fDelegate(int location, float v);
        public delegate void Uniform2fDelegate(int location, float v0, float v1);
        public delegate void DepthMaskDelegate(bool flag);
        public delegate void ReadPixelsDelegate(int x, int y, int w, int h, int format, int type, byte[] data);

        // ── Bound functions ──────────────────────────────────────────────────────────
        public readonly GenBuffersDelegate GenBuffers;
        public readonly BindBufferDelegate BindBuffer;
        public readonly BufferDataDelegate BufferData;
        public readonly DeleteBuffersDelegate DeleteBuffers;
        public readonly GenVertexArraysDelegate GenVertexArrays;
        public readonly BindVertexArrayDelegate BindVertexArray;
        public readonly DeleteVertexArraysDelegate DeleteVertexArrays;
        public readonly CreateShaderDelegate CreateShader;
        public readonly ShaderSourceDelegate ShaderSource;
        public readonly CompileShaderDelegate CompileShader;
        public readonly GetShaderivDelegate GetShaderiv;
        public readonly GetShaderInfoLogDelegate GetShaderInfoLog;
        public readonly CreateProgramDelegate CreateProgram;
        public readonly AttachShaderDelegate AttachShader;
        public readonly LinkProgramDelegate LinkProgram;
        public readonly GetProgramivDelegate GetProgramiv;
        public readonly UseProgramDelegate UseProgram;
        public readonly GetUniformLocationDelegate GetUniformLocation;
        public readonly GetAttribLocationDelegate GetAttribLocation;
        public readonly EnableVertexAttribArrayDelegate EnableVertexAttribArray;
        public readonly VertexAttribPointerDelegate VertexAttribPointer;
        public readonly UniformMatrix4fvDelegate UniformMatrix4fv;
        public readonly Uniform3fDelegate Uniform3f;
        public readonly Uniform1iDelegate Uniform1i;
        public readonly DrawArraysDelegate DrawArrays;
        public readonly ClearColorDelegate ClearColor;
        public readonly ClearDelegate Clear;
        public readonly ViewportDelegate Viewport;
        public readonly EnableDelegate Enable;
        public readonly DisableDelegate Disable;
        public readonly GenTexturesDelegate GenTextures;
        public readonly DeleteTexturesDelegate DeleteTextures;
        public readonly BindTextureDelegate BindTexture;
        public readonly ActiveTextureDelegate ActiveTexture;
        public readonly TexImage2DDelegate TexImage2D;
        public readonly TexParameteriDelegate TexParameteri;
        public readonly BlendFuncDelegate BlendFunc;
        public readonly Uniform1fDelegate Uniform1f;
        public readonly Uniform2fDelegate Uniform2f;
        public readonly DepthMaskDelegate DepthMask;
        public readonly ReadPixelsDelegate ReadPixels;

        // ── GL constants ─────────────────────────────────────────────────────────────
        public const int GL_ARRAY_BUFFER = 0x8892;
        public const int GL_STATIC_DRAW = 0x88E4;
        public const int GL_FRAGMENT_SHADER = 0x8B30;
        public const int GL_VERTEX_SHADER = 0x8B31;
        public const int GL_COMPILE_STATUS = 0x8B81;
        public const int GL_LINK_STATUS = 0x8B82;
        public const int GL_FLOAT = 0x1406;
        public const int GL_TRIANGLES = 0x0004;
        public const int GL_DEPTH_TEST = 0x0B71;
        public const int GL_CULL_FACE = 0x0B44;
        public const int GL_BLEND = 0x0BE2;
        public const int GL_COLOR_BUFFER_BIT = 0x4000;
        public const int GL_DEPTH_BUFFER_BIT = 0x0100;
        public const int GL_TEXTURE_2D = 0x0DE1;
        public const int GL_RGBA = 0x1908;
        public const int GL_UNSIGNED_BYTE = 0x1401;
        public const int GL_TEXTURE_MAG_FILTER = 0x2800;
        public const int GL_TEXTURE_MIN_FILTER = 0x2801;
        public const int GL_NEAREST = 0x2600;
        public const int GL_LINEAR = 0x2601;
        public const int GL_TEXTURE_WRAP_S = 0x2802;
        public const int GL_TEXTURE_WRAP_T = 0x2803;
        public const int GL_CLAMP_TO_EDGE = 0x812F;
        public const int GL_REPEAT = 0x2901;
        public const int GL_MIRRORED_REPEAT = 0x8370;
        public const int GL_TEXTURE0 = 0x84C0;
        public const int GL_TEXTURE1 = 0x84C1;
        public const int GL_SRC_ALPHA = 0x0302;
        public const int GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        public const int GL_ZERO = 0x0000;
        public const int GL_DST_COLOR = 0x0306;

        private T Bind<T>(GlInterface gl, string name) where T : Delegate
        {
            var ptr = gl.GetProcAddress(name);
            if (ptr == IntPtr.Zero) throw new InvalidOperationException($"GL function not available: {name}");
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        public GlFunctions(GlInterface gl)
        {
            GenBuffers = Bind<GenBuffersDelegate>(gl, "glGenBuffers");
            BindBuffer = Bind<BindBufferDelegate>(gl, "glBindBuffer");
            BufferData = Bind<BufferDataDelegate>(gl, "glBufferData");
            DeleteBuffers = Bind<DeleteBuffersDelegate>(gl, "glDeleteBuffers");
            GenVertexArrays = Bind<GenVertexArraysDelegate>(gl, "glGenVertexArrays");
            BindVertexArray = Bind<BindVertexArrayDelegate>(gl, "glBindVertexArray");
            DeleteVertexArrays = Bind<DeleteVertexArraysDelegate>(gl, "glDeleteVertexArrays");
            CreateShader = Bind<CreateShaderDelegate>(gl, "glCreateShader");
            ShaderSource = Bind<ShaderSourceDelegate>(gl, "glShaderSource");
            CompileShader = Bind<CompileShaderDelegate>(gl, "glCompileShader");
            GetShaderiv = Bind<GetShaderivDelegate>(gl, "glGetShaderiv");
            GetShaderInfoLog = Bind<GetShaderInfoLogDelegate>(gl, "glGetShaderInfoLog");
            CreateProgram = Bind<CreateProgramDelegate>(gl, "glCreateProgram");
            AttachShader = Bind<AttachShaderDelegate>(gl, "glAttachShader");
            LinkProgram = Bind<LinkProgramDelegate>(gl, "glLinkProgram");
            GetProgramiv = Bind<GetProgramivDelegate>(gl, "glGetProgramiv");
            UseProgram = Bind<UseProgramDelegate>(gl, "glUseProgram");
            GetUniformLocation = Bind<GetUniformLocationDelegate>(gl, "glGetUniformLocation");
            GetAttribLocation = Bind<GetAttribLocationDelegate>(gl, "glGetAttribLocation");
            EnableVertexAttribArray = Bind<EnableVertexAttribArrayDelegate>(gl, "glEnableVertexAttribArray");
            VertexAttribPointer = Bind<VertexAttribPointerDelegate>(gl, "glVertexAttribPointer");
            UniformMatrix4fv = Bind<UniformMatrix4fvDelegate>(gl, "glUniformMatrix4fv");
            Uniform3f = Bind<Uniform3fDelegate>(gl, "glUniform3f");
            Uniform1i = Bind<Uniform1iDelegate>(gl, "glUniform1i");
            GenTextures = Bind<GenTexturesDelegate>(gl, "glGenTextures");
            DeleteTextures = Bind<DeleteTexturesDelegate>(gl, "glDeleteTextures");
            BindTexture = Bind<BindTextureDelegate>(gl, "glBindTexture");
            ActiveTexture = Bind<ActiveTextureDelegate>(gl, "glActiveTexture");
            TexImage2D = Bind<TexImage2DDelegate>(gl, "glTexImage2D");
            TexParameteri = Bind<TexParameteriDelegate>(gl, "glTexParameteri");
            BlendFunc = Bind<BlendFuncDelegate>(gl, "glBlendFunc");
            Uniform1f = Bind<Uniform1fDelegate>(gl, "glUniform1f");
            Uniform2f = Bind<Uniform2fDelegate>(gl, "glUniform2f");
            DepthMask = Bind<DepthMaskDelegate>(gl, "glDepthMask");
            DrawArrays = Bind<DrawArraysDelegate>(gl, "glDrawArrays");
            ClearColor = Bind<ClearColorDelegate>(gl, "glClearColor");
            Clear = Bind<ClearDelegate>(gl, "glClear");
            Viewport = Bind<ViewportDelegate>(gl, "glViewport");
            Enable = Bind<EnableDelegate>(gl, "glEnable");
            Disable = Bind<DisableDelegate>(gl, "glDisable");
            ReadPixels = Bind<ReadPixelsDelegate>(gl, "glReadPixels");
        }

        public int CompileShaderOrThrow(int type, string source)
        {
            int shader = CreateShader(type);
            ShaderSource(shader, 1, new[] { source }, null);
            CompileShader(shader);
            GetShaderiv(shader, GL_COMPILE_STATUS, out int ok);
            if (ok == 0)
            {
                var sb = new StringBuilder(4096);
                GetShaderInfoLog(shader, sb.Capacity, out _, sb);
                throw new InvalidOperationException($"Shader compile failed: {sb}");
            }
            return shader;
        }

        public int LinkProgramOrThrow(int vs, int fs)
        {
            int program = CreateProgram();
            AttachShader(program, vs);
            AttachShader(program, fs);
            LinkProgram(program);
            GetProgramiv(program, GL_LINK_STATUS, out int ok);
            if (ok == 0) throw new InvalidOperationException("Program link failed.");
            return program;
        }
    }
}
