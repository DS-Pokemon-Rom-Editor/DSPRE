namespace MKDS_Course_Editor.Export3DTools
{
    public class Quad : Face
    {
        public Vector3D[] Normal   = new Vector3D[4];
        public Point2D[]  TexCoord = new Point2D[4];
        public Point3D[]  Vertex   = new Point3D[4];
    }
}

