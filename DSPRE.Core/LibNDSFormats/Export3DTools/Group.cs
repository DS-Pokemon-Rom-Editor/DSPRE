namespace MKDS_Course_Editor.Export3DTools
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public class Group
    {
        private List<Polygon> _polygonList = new List<Polygon>();

        public void Add(Polygon g)
        {
            this._polygonList.Add(g);
        }

        public IEnumerator<Polygon> GetEnumerator()
        {
            return this._polygonList.GetEnumerator();
        }

        public Polygon this[int i]
        {
            get
            {
                return this._polygonList[i];
            }
            set
            {
                this._polygonList[i] = value;
            }
        }

        public Polygon[] Polygons
        {
            get
            {
                return this._polygonList.ToArray();
            }
        }
    }
}

