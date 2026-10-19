namespace MKDS_Course_Editor.Export3DTools
{
    using System;
    using System.Collections.Generic;

    public class OBJWriter
    {
        private List<Vector3> _normals = new List<Vector3>();
        private List<Vector2> _texCoords = new List<Vector2>();
        private List<Vector3> _vertices = new List<Vector3>();

        public void AddTriangle(Vector3[] Vertice)
        {
            this._vertices.AddRange(Vertice);
        }

        public void AddTriangle(Vector3[] Vertice, Vector2 TexCoord)
        {
            this._vertices.AddRange(Vertice);
            this._texCoords.Add(TexCoord);
        }

        public void AddTriangle(Vector3[] Vertice, Vector3 Normal)
        {
            this._vertices.AddRange(Vertice);
            this._normals.Add(Normal);
        }

        public void AddTriangle(Vector3[] Vertice, Vector2 TexCoord, Vector3 Normal)
        {
            this._vertices.AddRange(Vertice);
            this._texCoords.Add(TexCoord);
            this._normals.Add(Normal);
        }
    }
}

