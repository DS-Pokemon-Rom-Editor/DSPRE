namespace MKDS_Course_Editor.Export3DTools
{
    using System.Collections.Generic;

    public class QuadStrip : Face
    {
        public List<Vector3D> Normal   = new List<Vector3D>();
        public List<Point2D>  TexCoord = new List<Point2D>();
        public List<Point3D>  Vertex   = new List<Point3D>();
    }
}

