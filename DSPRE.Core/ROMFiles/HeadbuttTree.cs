using System.IO;

namespace DSPRE.ROMFiles
{
    public class HeadbuttTree
    {
        public bool Picked = false;

        private ushort _globalX;
        private ushort _globalY;
        private ushort _matrixX;
        private ushort _matrixY;
        private ushort _mapX;
        private ushort _mapY;

        public bool IsUnused
        { 
            get
            { 
                return GlobalX == ushort.MaxValue && GlobalY == ushort.MaxValue; 
            } 
        }

        public enum Types
        {
            Normal,
            Special,
        }

        public HeadbuttTree(ushort globalX = ushort.MaxValue, ushort globalY = ushort.MaxValue)
        {
            this.GlobalX = globalX;
            this.GlobalY = globalY;
        }

        public HeadbuttTree(HeadbuttTree original)
        {
            this.GlobalX = original.GlobalX;
            this.GlobalY = original.GlobalY;
        }

        public ushort GlobalX
        {
            get { return _globalX; }
            set
            {
                _globalX = value;
                _matrixX = (ushort)(_globalX / MapFile.MapSize);
                _mapX = (ushort)(_globalX % MapFile.MapSize);
            }
        }

        public ushort GlobalY
        {
            get { return _globalY; }
            set
            {
                _globalY = value;
                _matrixY = (ushort)(_globalY / MapFile.MapSize);
                _mapY = (ushort)(_globalY % MapFile.MapSize);
            }
        }

        public ushort MatrixX
        {
            get
            {
                return _matrixX;
            }
            set
            {
                _matrixX = value;
                _globalX = (ushort)(_matrixX * MapFile.MapSize + _mapX);
                _mapX = (ushort)(_globalX % MapFile.MapSize);
            }
        }

        public ushort MatrixY
        {
            get
            {
                return _matrixY;
            }
            set
            {
                _matrixY = value;
                _globalY = (ushort)(_matrixY * MapFile.MapSize + _mapY);
                _mapY = (ushort)(_globalY % MapFile.MapSize);
            }
        }

        public ushort MapX
        {
            get
            {
                return _mapX;
            }
            set
            {
                _mapX = value;
                _globalX = (ushort)(_matrixX * MapFile.MapSize + _mapX);
                _matrixX = (ushort)(_globalX / MapFile.MapSize);
            }
        }

        public ushort MapY
        {
            get
            {
                return _mapY;
            }
            set
            {
                _mapY = value;
                _globalY = (ushort)(_matrixY * MapFile.MapSize + _mapY);
                _matrixY = (ushort)(_globalY / MapFile.MapSize);
            }
        }

        public override string ToString()
        {
            if (IsUnused)
            {
                return "Unused tree";
            }
            return $"Tree at {GlobalX}, {GlobalY}";
        }
    }
}
