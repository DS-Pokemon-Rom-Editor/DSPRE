// 4x4 matrix class.
// Code adapted from kiwi.ds' NSBMD Model Viewer.

using System;
using System.Diagnostics;

namespace LibNDSFormats.NSBMD
{
    /// <summary>
    /// 4x4 matrix class.
    /// </summary>
    internal class MTX44
    {
        #region Fields (1) 

        /// <summary>
        /// Float values of matrix.
        /// </summary>
        private float[] _array = new float[4 * 4];

        #endregion Fields 

        #region Properties (2) 

        // TODO: Index check!

        /// <summary>
        /// 2-dimensional index accessor.
        /// </summary>
        public float this[int x, int y]
        {
            get { return _array[x + y * 4]; }
            set { _array[x + y * 4] = value; }
        }

        public void SetValues(float[] array)
        {
            this._array = array;
        }

        /// <summary>
        /// Index accessor.
        /// </summary>
        public float this[int index]
        {
            get { return _array[index]; }
            set { _array[index] = value; }
        }

        #endregion Properties 

        #region Methods (8) 

        // Public Methods (7) 

        /// <summary>
        /// Get float array.
        /// </summary>
        public float[] Floats
        {
            get
            {
                return _array;
            }
        }

        /// <summary>
        /// Clone this matrix.
        /// </summary>
        /// <returns>Clone of matrix.</returns>
        public MTX44 Clone()
        {
            MTX44 clone = new MTX44();
            for (int i = 0; i < 4 * 4; ++i)
            {
                clone._array[i] = _array[i];
            }
            return clone;
        }

        public void Translate(float x, float y, float z)
        {
            MTX44 b = new MTX44();
            b.LoadIdentity();
            b[12] = x;
            b[13] = y;
            b[14] = z;
            MultMatrix(b).CopyValuesTo(this);
        }

        /// <summary>
        /// Load identity.
        /// </summary>
        public void LoadIdentity()
        {
            Zero();
            this[0, 0] =
                this[1, 1] =
                this[2, 2] =
                this[3, 3] = 1.0f;
        }

        /// <summary>
        /// Multiplicate this matrix with another.
        /// </summary>
        /// <param name="b">Other matrix.</param>
        /// <returns>Multiplication result.</returns>
        public MTX44 MultMatrix(MTX44 b)
        {
            MTX44 m = new MTX44();
            MTX44 a = this;
            int i, j, k;

            for (i = 0; i < 4; i++)
            {
                for (j = 0; j < 4; j++)
                {
                    m._array[(i << 2) + j] = 0.0f;
                    for (k = 0; k < 4; k++)
                        m._array[(i << 2) + j] += a._array[(k << 2) + j] * b._array[(i << 2) + k];
                }
            }

            return m;
        }
        /// <summary>
        /// Multiplicate this matrix with vector.
        /// </summary>
        /// <param name="v">Vector.</param>
        /// <returns>Multiplication result.</returns>
        public float[] MultVector(float[] v)
        {
            /* MTX44 a = this;
             float[] dest = new float[3];
             float x = v[0];
             float y = v[1];
             float z = v[2];
             dest[0] = x*a[(0 << 2) + 0] + y*a[(1 << 2) + 0] + z*a[(2 << 2) + 0] + a[(3 << 2) + 0];
             dest[1] = x*a[(0 << 2) + 1] + y*a[(1 << 2) + 1] + z*a[(2 << 2) + 1] + a[(3 << 2) + 1];
             dest[2] = x*a[(0 << 2) + 2] + y*a[(1 << 2) + 2] + z*a[(2 << 2) + 2] + a[(3 << 2) + 2];
             return dest;*/
            float[] vtxTrans = new float[3];
            for (int i = 0; i < 3; i++)
            {
                float c0 = v[0] * this[0 + i];
                float c1 = v[1] * this[4 + i];
                float c2 = v[2] * this[8 + i];
                float c3 = this[12 + i];
                vtxTrans[i] = c0 + c1 + c2 + c3;
            }


            return vtxTrans;
        }

        public MTX44()
        {
            this.LoadIdentity();
        }

        /// <summary>
        /// Scale this matrix.
        /// </summary>
        /// <param name="x">X scale factor.</param>
        /// <param name="y">Y scale factor.</param>
        /// <param name="z">Z scale factor.</param>
        public void Scale(float x, float y, float z)
        {
            MTX44 m = new MTX44();
            m.LoadIdentity();


            m[0] = x;
            m[5] = y;
            m[10] = z;
            this.MultMatrix(m).CopyValuesTo(this);
        }

        /// <summary>
        /// Fill matrix with zeroes.
        /// </summary>
        public void Zero()
        {
            for (int i = 0; i < 4 * 4; ++i)
                _array[i] = 0f;
            _array = new float[4 * 4];
        }

        /// <summary>
        /// Copy values to another matrix.
        /// </summary>
        /// <param name="mtx44">Other matrix.</param>
        public void CopyValuesTo(MTX44 m)
        {
            for (int i = 0; i < 4 * 4; ++i)
                m._array[i] = this[i];
        }

        #endregion Methods 
    }
}