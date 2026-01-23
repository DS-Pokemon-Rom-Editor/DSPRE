using System;
using OpenTK.Graphics.OpenGL;

namespace Tao.OpenGl
{
    public static class Gl
    {
        public const int GL_LIGHTING = (int)EnableCap.Lighting;
        public const int GL_TEXTURE_2D = (int)EnableCap.Texture2D;
        public const int GL_ALPHA_TEST = (int)EnableCap.AlphaTest;
        public const int GL_BLEND = (int)EnableCap.Blend;
        public const int GL_DEPTH_TEST = (int)EnableCap.DepthTest;
        public const int GL_CULL_FACE = (int)EnableCap.CullFace;
        public const int GL_COLOR_MATERIAL = (int)EnableCap.ColorMaterial;
        public const int GL_NORMALIZE = (int)EnableCap.Normalize;
        public const int GL_POLYGON_OFFSET_FILL = (int)EnableCap.PolygonOffsetFill;

        public const int GL_LIGHT0 = (int)LightName.Light0;
        public const int GL_LIGHT1 = (int)LightName.Light1;
        public const int GL_LIGHT2 = (int)LightName.Light2;
        public const int GL_LIGHT3 = (int)LightName.Light3;
        public const int GL_LIGHT4 = (int)LightName.Light4;
        public const int GL_LIGHT5 = (int)LightName.Light5;
        public const int GL_LIGHT6 = (int)LightName.Light6;
        public const int GL_LIGHT7 = (int)LightName.Light7;

        public const int GL_DIFFUSE = (int)LightParameter.Diffuse;
        public const int GL_AMBIENT = (int)LightParameter.Ambient;
        public const int GL_SPECULAR = (int)LightParameter.Specular;
        public const int GL_EMISSION = 0x1600;
        public const int GL_POSITION = (int)LightParameter.Position;

        public const int GL_MODELVIEW = (int)MatrixMode.Modelview;
        public const int GL_PROJECTION = (int)MatrixMode.Projection;
        public const int GL_TEXTURE = (int)MatrixMode.Texture;
        public const int GL_MODELVIEW_MATRIX = (int)GetPName.ModelviewMatrix;
        public const int GL_PROJECTION_MATRIX = (int)GetPName.ProjectionMatrix;

        public const int GL_TRIANGLES = (int)PrimitiveType.Triangles;
        public const int GL_TRIANGLE_STRIP = (int)PrimitiveType.TriangleStrip;
        public const int GL_QUADS = (int)PrimitiveType.Quads;
        public const int GL_QUAD_STRIP = (int)PrimitiveType.QuadStrip;
        public const int GL_LINES = (int)PrimitiveType.Lines;
        public const int GL_LINE_STRIP = (int)PrimitiveType.LineStrip;
        public const int GL_POLYGON = (int)PrimitiveType.Polygon;

        public const int GL_GREATER = (int)AlphaFunction.Greater;
        public const int GL_LESS = (int)DepthFunction.Less;
        public const int GL_LEQUAL = (int)DepthFunction.Lequal;
        public const int GL_EQUAL = (int)DepthFunction.Equal;
        public const int GL_GEQUAL = (int)DepthFunction.Gequal;
        public const int GL_NOTEQUAL = (int)DepthFunction.Notequal;
        public const int GL_ALWAYS = (int)DepthFunction.Always;
        public const int GL_NEVER = (int)DepthFunction.Never;

        public const int GL_SRC_ALPHA = (int)BlendingFactor.SrcAlpha;
        public const int GL_ONE_MINUS_SRC_ALPHA = (int)BlendingFactor.OneMinusSrcAlpha;
        public const int GL_ONE = (int)BlendingFactor.One;
        public const int GL_ZERO = (int)BlendingFactor.Zero;
        public const int GL_DST_ALPHA = (int)BlendingFactor.DstAlpha;

        public const int GL_FRONT = (int)MaterialFace.Front;
        public const int GL_BACK = (int)MaterialFace.Back;
        public const int GL_FRONT_AND_BACK = (int)MaterialFace.FrontAndBack;

        public const int GL_AMBIENT_AND_DIFFUSE = (int)ColorMaterialParameter.AmbientAndDiffuse;

        public const int GL_SMOOTH = (int)ShadingModel.Smooth;
        public const int GL_FLAT = (int)ShadingModel.Flat;

        public const int GL_FILL = (int)PolygonMode.Fill;
        public const int GL_LINE = (int)PolygonMode.Line;
        public const int GL_POINT = (int)PolygonMode.Point;

        public const int GL_REPEAT = (int)TextureWrapMode.Repeat;
        public const int GL_CLAMP = (int)TextureWrapMode.Clamp;
        public const int GL_CLAMP_TO_EDGE = (int)TextureWrapMode.ClampToEdge;
        public const int GL_MIRRORED_REPEAT = (int)TextureWrapMode.MirroredRepeat;

        public const int GL_TEXTURE_WRAP_S = (int)TextureParameterName.TextureWrapS;
        public const int GL_TEXTURE_WRAP_T = (int)TextureParameterName.TextureWrapT;
        public const int GL_TEXTURE_MIN_FILTER = (int)TextureParameterName.TextureMinFilter;
        public const int GL_TEXTURE_MAG_FILTER = (int)TextureParameterName.TextureMagFilter;

        public const int GL_NEAREST = (int)TextureMinFilter.Nearest;
        public const int GL_LINEAR = (int)TextureMinFilter.Linear;
        public const int GL_NEAREST_MIPMAP_NEAREST = (int)TextureMinFilter.NearestMipmapNearest;
        public const int GL_LINEAR_MIPMAP_LINEAR = (int)TextureMinFilter.LinearMipmapLinear;

        public const int GL_RGBA = (int)PixelFormat.Rgba;
        public const int GL_RGB = (int)PixelFormat.Rgb;
        public const int GL_BGRA = (int)PixelFormat.Bgra;
        public const int GL_BGR = (int)PixelFormat.Bgr;
        public const int GL_LUMINANCE = (int)PixelFormat.Luminance;
        public const int GL_LUMINANCE_ALPHA = (int)PixelFormat.LuminanceAlpha;
        public const int GL_ALPHA = (int)PixelFormat.Alpha;

        public const int GL_UNSIGNED_BYTE = (int)PixelType.UnsignedByte;
        public const int GL_UNSIGNED_SHORT = (int)PixelType.UnsignedShort;
        public const int GL_FLOAT = (int)PixelType.Float;

        public const int GL_COLOR_BUFFER_BIT = (int)ClearBufferMask.ColorBufferBit;
        public const int GL_DEPTH_BUFFER_BIT = (int)ClearBufferMask.DepthBufferBit;
        public const int GL_STENCIL_BUFFER_BIT = (int)ClearBufferMask.StencilBufferBit;

        public const int GL_VIEWPORT = (int)GetPName.Viewport;

        public const int GL_COMPILE = (int)ListMode.Compile;
        public const int GL_COMPILE_AND_EXECUTE = (int)ListMode.CompileAndExecute;

        public const int GL_CW = (int)FrontFaceDirection.Cw;
        public const int GL_CCW = (int)FrontFaceDirection.Ccw;

        public const int GL_TRUE = 1;
        public const int GL_FALSE = 0;

        // Texture generation
        public const int GL_S = (int)TextureCoordName.S;
        public const int GL_T = (int)TextureCoordName.T;
        public const int GL_TEXTURE_GEN_MODE = (int)TextureGenParameter.TextureGenMode;
        public const int GL_SPHERE_MAP = (int)TextureGenMode.SphereMap;
        public const int GL_TEXTURE_GEN_S = (int)EnableCap.TextureGenS;
        public const int GL_TEXTURE_GEN_T = (int)EnableCap.TextureGenT;

        // Texture environment
        public const int GL_TEXTURE_ENV = (int)TextureEnvTarget.TextureEnv;
        public const int GL_TEXTURE_ENV_MODE = (int)TextureEnvParameter.TextureEnvMode;
        public const int GL_MODULATE = (int)TextureEnvMode.Modulate;
        public const int GL_DECAL = (int)TextureEnvMode.Decal;

        // Cull face mode
        public const int GL_NONE = 0;

        // Rescale normal
        public const int GL_RESCALE_NORMAL = (int)EnableCap.RescaleNormal;

        // Texture matrix
        public const int GL_TEXTURE_MATRIX = (int)GetPName.TextureMatrix;

        public static void glEnable(int cap) => GL.Enable((EnableCap)cap);
        public static void glDisable(int cap) => GL.Disable((EnableCap)cap);
        public static int glIsEnabled(int cap) => GL.IsEnabled((EnableCap)cap) ? 1 : 0;

        public static void glClear(int mask) => GL.Clear((ClearBufferMask)mask);
        public static void glClearColor(float r, float g, float b, float a) => GL.ClearColor(r, g, b, a);
        public static void glClearDepth(double depth) => GL.ClearDepth(depth);

        public static void glMatrixMode(int mode) => GL.MatrixMode((MatrixMode)mode);
        public static void glLoadIdentity() => GL.LoadIdentity();
        public static void glPushMatrix() => GL.PushMatrix();
        public static void glPopMatrix() => GL.PopMatrix();

        public static void glLoadMatrixf(float[] m) => GL.LoadMatrix(m);
        public static void glMultMatrixf(float[] m) => GL.MultMatrix(m);
        public static void glGetFloatv(int pname, float[] data) => GL.GetFloat((GetPName)pname, data);
        public static void glGetIntegerv(int pname, int[] data) => GL.GetInteger((GetPName)pname, data);

        public static void glTranslatef(float x, float y, float z) => GL.Translate(x, y, z);
        public static void glTranslated(double x, double y, double z) => GL.Translate(x, y, z);
        public static void glRotatef(float angle, float x, float y, float z) => GL.Rotate(angle, x, y, z);
        public static void glRotated(double angle, double x, double y, double z) => GL.Rotate(angle, x, y, z);
        public static void glScalef(float x, float y, float z) => GL.Scale(x, y, z);
        public static void glScaled(double x, double y, double z) => GL.Scale(x, y, z);

        public static void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar)
            => GL.Ortho(left, right, bottom, top, zNear, zFar);

        public static void glFrustum(double left, double right, double bottom, double top, double zNear, double zFar)
            => GL.Frustum(left, right, bottom, top, zNear, zFar);

        public static void glViewport(int x, int y, int width, int height) => GL.Viewport(x, y, width, height);

        public static void glBegin(int mode) => GL.Begin((PrimitiveType)mode);
        public static void glEnd() => GL.End();

        public static void glVertex2f(float x, float y) => GL.Vertex2(x, y);
        public static void glVertex2d(double x, double y) => GL.Vertex2(x, y);
        public static void glVertex3f(float x, float y, float z) => GL.Vertex3(x, y, z);
        public static void glVertex3d(double x, double y, double z) => GL.Vertex3(x, y, z);
        public static void glVertex3fv(float[] v) => GL.Vertex3(v[0], v[1], v[2]);

        public static void glNormal3f(float x, float y, float z) => GL.Normal3(x, y, z);
        public static void glNormal3d(double x, double y, double z) => GL.Normal3(x, y, z);
        public static void glNormal3fv(float[] v) => GL.Normal3(v[0], v[1], v[2]);

        public static void glTexCoord2f(float s, float t) => GL.TexCoord2(s, t);
        public static void glTexCoord2d(double s, double t) => GL.TexCoord2(s, t);

        public static void glColor3f(float r, float g, float b) => GL.Color3(r, g, b);
        public static void glColor3d(double r, double g, double b) => GL.Color3(r, g, b);
        public static void glColor3ub(byte r, byte g, byte b) => GL.Color3(r, g, b);
        public static void glColor4f(float r, float g, float b, float a) => GL.Color4(r, g, b, a);
        public static void glColor4d(double r, double g, double b, double a) => GL.Color4(r, g, b, a);
        public static void glColor4ub(byte r, byte g, byte b, byte a) => GL.Color4(r, g, b, a);

        public static void glLineWidth(float width) => GL.LineWidth(width);
        public static void glPointSize(float size) => GL.PointSize(size);

        public static void glPolygonMode(int face, int mode) => GL.PolygonMode((MaterialFace)face, (PolygonMode)mode);
        public static void glPolygonOffset(float factor, float units) => GL.PolygonOffset(factor, units);
        public static void glFrontFace(int mode) => GL.FrontFace((FrontFaceDirection)mode);
        public static void glCullFace(int mode) => GL.CullFace((CullFaceMode)mode);

        public static void glShadeModel(int mode) => GL.ShadeModel((ShadingModel)mode);

        public static void glDepthFunc(int func) => GL.DepthFunc((DepthFunction)func);
        public static void glDepthMask(bool flag) => GL.DepthMask(flag);
        public static void glDepthMask(int flag) => GL.DepthMask(flag != 0);

        public static void glAlphaFunc(int func, float refValue) => GL.AlphaFunc((AlphaFunction)func, refValue);

        public static void glBlendFunc(int sfactor, int dfactor)
            => GL.BlendFunc((BlendingFactor)sfactor, (BlendingFactor)dfactor);

        public static void glLightfv(int light, int pname, float[] param)
            => GL.Light((LightName)light, (LightParameter)pname, param);

        public static void glLightf(int light, int pname, float param)
            => GL.Light((LightName)light, (LightParameter)pname, param);

        public static void glMaterialfv(int face, int pname, float[] param)
            => GL.Material((MaterialFace)face, (MaterialParameter)pname, param);

        public static void glMaterialf(int face, int pname, float param)
            => GL.Material((MaterialFace)face, (MaterialParameter)pname, param);

        public static void glColorMaterial(int face, int mode)
            => GL.ColorMaterial((MaterialFace)face, (ColorMaterialParameter)mode);

        public static void glGenTextures(int n, int[] textures) => GL.GenTextures(n, textures);
        public static void glGenTextures(int n, out int texture)
        {
            int[] textures = new int[n];
            GL.GenTextures(n, textures);
            texture = textures[0];
        }

        public static void glDeleteTextures(int n, int[] textures) => GL.DeleteTextures(n, textures);
        public static void glDeleteTextures(int n, ref int texture)
        {
            int[] textures = new int[] { texture };
            GL.DeleteTextures(n, textures);
        }

        public static void glBindTexture(int target, int texture) => GL.BindTexture((TextureTarget)target, texture);

        public static void glTexParameteri(int target, int pname, int param)
            => GL.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);

        public static void glTexParameterf(int target, int pname, float param)
            => GL.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);

        public static void glTexImage2D(int target, int level, int internalformat, int width, int height,
            int border, int format, int type, IntPtr pixels)
            => GL.TexImage2D((TextureTarget)target, level, (PixelInternalFormat)internalformat,
                width, height, border, (PixelFormat)format, (PixelType)type, pixels);

        public static void glTexImage2D(int target, int level, int internalformat, int width, int height,
            int border, int format, int type, byte[] pixels)
            => GL.TexImage2D((TextureTarget)target, level, (PixelInternalFormat)internalformat,
                width, height, border, (PixelFormat)format, (PixelType)type, pixels);

        public static void glTexSubImage2D(int target, int level, int xoffset, int yoffset,
            int width, int height, int format, int type, IntPtr pixels)
            => GL.TexSubImage2D((TextureTarget)target, level, xoffset, yoffset, width, height,
                (PixelFormat)format, (PixelType)type, pixels);

        public static void glPixelStorei(int pname, int param)
            => GL.PixelStore((PixelStoreParameter)pname, param);

        // Texture generation functions
        public static void glTexGeni(int coord, int pname, int param)
            => GL.TexGen((TextureCoordName)coord, (TextureGenParameter)pname, param);

        public static void glTexEnvi(int target, int pname, int param)
            => GL.TexEnv((TextureEnvTarget)target, (TextureEnvParameter)pname, param);

        public static int glGenLists(int range) => GL.GenLists(range);
        public static void glNewList(int list, int mode) => GL.NewList(list, (ListMode)mode);
        public static void glEndList() => GL.EndList();
        public static void glCallList(int list) => GL.CallList(list);
        public static void glDeleteLists(int list, int range) => GL.DeleteLists(list, range);

        public static void glFlush() => GL.Flush();
        public static void glFinish() => GL.Finish();

        public static int glGetError() => (int)GL.GetError();

        public static void glReadPixels(int x, int y, int width, int height, int format, int type, IntPtr data)
        {
            GL.ReadPixels(x, y, width, height, (PixelFormat)format, (PixelType)type, data);
        }

        public static void gluPerspective(double fovy, double aspect, double zNear, double zFar)
        {
            double ymax = zNear * Math.Tan(fovy * Math.PI / 360.0);
            double ymin = -ymax;
            double xmin = ymin * aspect;
            double xmax = ymax * aspect;
            GL.Frustum(xmin, xmax, ymin, ymax, zNear, zFar);
        }

        public static void gluLookAt(double eyeX, double eyeY, double eyeZ,
            double centerX, double centerY, double centerZ,
            double upX, double upY, double upZ)
        {
            OpenTK.Mathematics.Vector3 eye = new OpenTK.Mathematics.Vector3((float)eyeX, (float)eyeY, (float)eyeZ);
            OpenTK.Mathematics.Vector3 target = new OpenTK.Mathematics.Vector3((float)centerX, (float)centerY, (float)centerZ);
            OpenTK.Mathematics.Vector3 up = new OpenTK.Mathematics.Vector3((float)upX, (float)upY, (float)upZ);

            OpenTK.Mathematics.Vector3 z = OpenTK.Mathematics.Vector3.Normalize(eye - target);
            OpenTK.Mathematics.Vector3 x = OpenTK.Mathematics.Vector3.Normalize(OpenTK.Mathematics.Vector3.Cross(up, z));
            OpenTK.Mathematics.Vector3 y = OpenTK.Mathematics.Vector3.Normalize(OpenTK.Mathematics.Vector3.Cross(z, x));

            float[] m = new float[16];
            m[0] = x.X; m[4] = x.Y; m[8] = x.Z; m[12] = 0;
            m[1] = y.X; m[5] = y.Y; m[9] = y.Z; m[13] = 0;
            m[2] = z.X; m[6] = z.Y; m[10] = z.Z; m[14] = 0;
            m[3] = 0; m[7] = 0; m[11] = 0; m[15] = 1;

            GL.MultMatrix(m);
            GL.Translate(-eyeX, -eyeY, -eyeZ);
        }
    }

    public static class Glu
    {
        public static void gluPerspective(double fovy, double aspect, double zNear, double zFar)
            => Gl.gluPerspective(fovy, aspect, zNear, zFar);

        public static void gluLookAt(double eyeX, double eyeY, double eyeZ,
            double centerX, double centerY, double centerZ,
            double upX, double upY, double upZ)
            => Gl.gluLookAt(eyeX, eyeY, eyeZ, centerX, centerY, centerZ, upX, upY, upZ);
    }
}

namespace Tao.Platform.Windows
{
    public static class Winmm
    {
        public static uint timeGetTime()
        {
            return (uint)Environment.TickCount;
        }
    }
}
