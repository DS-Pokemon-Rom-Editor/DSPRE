using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE
{
    /// <summary>The field camera table, found through the same RAM pointers the game follows.</summary>
    public static class GameCameraTable
    {
        public sealed class Location
        {
            public string OverlayPath { get; init; }
            public uint Offset { get; init; }

            /// <summary>Whether every pointer the game keeps to the table points at the same place.</summary>
            public bool PointersAgree { get; init; }
        }

        /// <summary>Raised after the table is written, so previews built from it can refresh.</summary>
        public static event EventHandler Saved;

        public static void RaiseSaved() => Saved?.Invoke(null, EventArgs.Empty);

        public static Location Locate()
        {
            RomInfo.PrepareCameraData();
            string path = OverlayUtils.GetPath(RomInfo.cameraTblOverlayNumber);
            var pointers = new uint[RomInfo.cameraTblOffsetsToRAMaddress.Length];
            using (var br = new DSUtils.EasyReader(path))
            {
                for (int i = 0; i < pointers.Length; i++)
                {
                    br.BaseStream.Position = RomInfo.cameraTblOffsetsToRAMaddress[i];
                    pointers[i] = br.ReadUInt32();
                }
            }

            return new Location
            {
                OverlayPath = path,
                Offset = pointers[0] - OverlayUtils.OverlayTable.GetRAMAddress(RomInfo.cameraTblOverlayNumber),
                PointersAgree = pointers.All(p => p == pointers[0]),
            };
        }

        public static List<GameCamera> Read(Location at)
        {
            bool hgss = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            var cameras = new List<GameCamera>(RomInfo.cameraCount);
            using (var br = new DSUtils.EasyReader(at.OverlayPath, at.Offset))
            {
                for (int i = 0; i < RomInfo.cameraCount; i++)
                {
                    cameras.Add(hgss
                        ? new GameCamera(br.ReadUInt32(), br.ReadInt16(), br.ReadInt16(), br.ReadInt16(),
                                         br.ReadInt16(), br.ReadByte(), br.ReadByte(),
                                         br.ReadUInt16(), br.ReadUInt32(), br.ReadUInt32(),
                                         br.ReadInt32(), br.ReadInt32(), br.ReadInt32())
                        : new GameCamera(br.ReadUInt32(), br.ReadInt16(), br.ReadInt16(), br.ReadInt16(),
                                         br.ReadInt16(), br.ReadByte(), br.ReadByte(),
                                         br.ReadUInt16(), br.ReadUInt32(), br.ReadUInt32()));
                }
            }
            return cameras;
        }

        /// <summary>The table as the game has it now, or null when it cannot be read.</summary>
        public static List<GameCamera> TryRead()
        {
            try
            {
                // A compressed overlay holds packed bytes, not the table; ds-rom projects keep it unpacked.
                RomInfo.PrepareCameraData();
                if (OverlayUtils.IsCompressed(RomInfo.cameraTblOverlayNumber)) return null;
                return Read(Locate());
            }
            catch (Exception ex)
            {
                AppLogger.Warn("The camera table could not be read: " + ex.Message);
                return null;
            }
        }
    }
}
