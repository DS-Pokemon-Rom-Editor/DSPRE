using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPRE.Editors.Data
{
    public class FlyTableRowHgss
    {
        public FlyTableRowHgss() { }

        public FlyTableRowHgss(byte flagIdx, bool isBlackoutSpawn, bool isFlyPoint,
                               ushort headerIdGameOver, byte localX, byte localY,
                               ushort headerIdFly, ushort globalX, ushort globalY,
                               ushort headerIdUnlockWarp, ushort globalXUnlock, ushort globalYUnlock)
        {
            FlagIdx = flagIdx;
            IsBlackoutSpawn = isBlackoutSpawn;
            IsFlyPoint = isFlyPoint;
            HeaderIdGameOver = headerIdGameOver;
            LocalX = localX;
            LocalY = localY;
            HeaderIdFly = headerIdFly;
            GlobalX = globalX;
            GlobalY = globalY;
            HeaderIdUnlockWarp = headerIdUnlockWarp;
            GlobalXUnlock = globalXUnlock;
            GlobalYUnlock = globalYUnlock;
        }

        public byte FlagIdx { get; set; }
        public bool IsBlackoutSpawn { get; set; }
        public bool IsFlyPoint { get; set; }
        public ushort HeaderIdGameOver { get; set; }
        public byte LocalX { get; set; }
        public byte LocalY { get; set; }
        public ushort HeaderIdFly { get; set; }
        public ushort GlobalX { get; set; }
        public ushort GlobalY { get; set; }
        public ushort HeaderIdUnlockWarp { get; set; }
        public ushort GlobalXUnlock { get; set; }
        public ushort GlobalYUnlock { get; set; }
    }
}
