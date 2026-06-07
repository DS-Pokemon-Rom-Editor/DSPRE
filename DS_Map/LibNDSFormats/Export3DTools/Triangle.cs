namespace MKDS_Course_Editor.Export3DTools
{
    public class Triangle : Face
    {
        public Vector3D[] Normal   = new Vector3D[3];
        public Point2D[]  TexCoord = new Point2D[3];
        public Point3D[]  Vertex   = new Point3D[3];
    }
}

