// Vectors.cs, lightweight value types replacing WPF/OpenTK geometry types.
// TODO (Avalonia migration - step 33): Replace with OpenTK 4.x equivalents when
//      the renderer rewrite is complete.
namespace MKDS_Course_Editor.Export3DTools
{
    /// <summary>Replaces System.Windows.Media.Media3D.Vector3D.</summary>
    public struct Vector3D
    {
        public float X, Y, Z;
        public Vector3D(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Replaces System.Windows.Media.Media3D.Point3D.</summary>
    public struct Point3D
    {
        public float X, Y, Z;
        public Point3D(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Replaces System.Windows.Point (2-D texture coordinate).</summary>
    public struct Point2D
    {
        public float X, Y;
        public Point2D(float x, float y) { X = x; Y = y; }
    }

    /// <summary>Replaces OpenTK.Vector3.</summary>
    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Replaces OpenTK.Vector2.</summary>
    public struct Vector2
    {
        public float X, Y;
        public Vector2(float x, float y) { X = x; Y = y; }
    }
}
