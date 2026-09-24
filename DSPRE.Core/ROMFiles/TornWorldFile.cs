using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>One Distortion World map's data from tw_arc.</summary>
    public class TornWorldFile : RomFile
    {
        public const int HeaderSize = 20;
        public const int PlatformSize = 20;
        public const int JumpPointSize = 40;
        public const int CameraRegionSize = 24;
        public const int GhostHeaderSize = 12;
        public const int GhostPropSize = 12;
        public const int GhostTriggerSize = 20;

        public enum PlatformKind { Floor = 0, WestWall = 1, EastWall = 2, Ceiling = 3, Invalid = 4 }

        public class Bounds
        {
            public short StartX { get; set; }
            public short StartY { get; set; }
            public short StartZ { get; set; }
            public short SizeX { get; set; }
            public short SizeY { get; set; }
            public short SizeZ { get; set; }

            public bool Contains(int x, int y, int z) =>
                x >= StartX && x <= StartX + SizeX &&
                y >= StartY && y <= StartY + SizeY &&
                z >= StartZ && z <= StartZ + SizeZ;
        }

        public class FloatingPlatform
        {
            public PlatformKind Kind { get; set; }
            public int AttributeId { get; set; }
            public Bounds Bounds { get; set; } = new Bounds();
            public int TilesVertical { get; set; }
            public int TilesHorizontal { get; set; }

            public int KindIndex
            {
                get => (int)Kind;
                set => Kind = (PlatformKind)value;
            }
        }

        public class JumpPoint
        {
            public int HandlerIndex { get; set; }
            public short PlayerDirection { get; set; }
            public int Unknown04 { get; set; }
            public Bounds Bounds { get; set; } = new Bounds();
            public short DisplacementX { get; set; }
            public short DisplacementY { get; set; }
            public short DisplacementZ { get; set; }
            public short SpriteRotationAngle { get; set; }
            public short MovementSteps { get; set; }
            public int JumpAxis { get; set; }
            public int InvertedJump { get; set; }
            public short FinalFacingDirection { get; set; }
            public short TargetKind { get; set; }
            public int TargetPlatformIndex { get; set; }
        }

        public const float CameraDegreesPerUnit = 360f / 256f;

        public const int CameraBaseAngleX = -10750;
        public const int CameraBaseAngleY = 0;
        public const int CameraBaseAngleZ = 0;

        private const float DegreesPerTurnUnit = 360f / 65536f;

        public class CameraRegion
        {
            public float PitchDegrees => -(CameraBaseAngleX + AngleX * 256) * DegreesPerTurnUnit;

            public float YawDegrees => (CameraBaseAngleY + AngleY * 256) * DegreesPerTurnUnit;

            public float RollDegrees => (CameraBaseAngleZ + AngleZ * 256) * DegreesPerTurnUnit;

            public Bounds Bounds { get; set; } = new Bounds();
            public int AngleX { get; set; }
            public int AngleY { get; set; }
            public int AngleZ { get; set; }
            public short PlayerDirection { get; set; }
            public int TransitionSteps { get; set; }
        }

        public class GhostProp
        {
            public long GroupId { get; set; }
            public int PropKind { get; set; }
            public short TileX { get; set; }
            public short TileY { get; set; }
            public short TileZ { get; set; }
        }

        public class GhostTrigger
        {
            public long GroupId { get; set; }
            public short PlayerDirection { get; set; }
            public short ShowProp { get; set; }
            public Bounds Bounds { get; set; } = new Bounds();
        }

        public List<FloatingPlatform> Platforms { get; } = new List<FloatingPlatform>();
        public List<JumpPoint> JumpPoints { get; } = new List<JumpPoint>();
        public List<CameraRegion> CameraRegions { get; } = new List<CameraRegion>();
        public List<GhostProp> GhostProps { get; } = new List<GhostProp>();
        public List<GhostTrigger> GhostTriggers { get; } = new List<GhostTrigger>();

        public long DefaultVisibleGroups;

        public int Unknown00;

        public TornWorldFile() { }

        public TornWorldFile(byte[] data)
        {
            if (data == null || data.Length < HeaderSize)
                throw new ArgumentException("A Distortion World map file is at least " + HeaderSize + " bytes.");

            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                Unknown00 = reader.ReadInt32();
                int platformBytes = reader.ReadInt32();
                int jumpBytes = reader.ReadInt32();
                int cameraBytes = reader.ReadInt32();
                int ghostBytes = reader.ReadInt32();

                if (HeaderSize + platformBytes + jumpBytes + cameraBytes + ghostBytes != data.Length)
                    throw new ArgumentException("The section sizes do not add up to the file's length.");

                ReadPlatforms(reader, platformBytes);
                ReadJumpPoints(reader, jumpBytes);
                ReadCameraRegions(reader, cameraBytes);
                ReadGhostProps(reader, ghostBytes);
            }
        }

        private void ReadPlatforms(BinaryReader reader, int sectionBytes)
        {
            if (sectionBytes < sizeof(int)) return;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var platform = new FloatingPlatform { Kind = (PlatformKind)reader.ReadInt16(), AttributeId = reader.ReadUInt16() };
                ReadBounds(reader, platform.Bounds);
                platform.TilesVertical = reader.ReadUInt16();
                platform.TilesHorizontal = reader.ReadUInt16();
                Platforms.Add(platform);
            }
        }

        private void ReadJumpPoints(BinaryReader reader, int sectionBytes)
        {
            if (sectionBytes < sizeof(int)) return;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var jump = new JumpPoint { HandlerIndex = reader.ReadUInt16(), PlayerDirection = reader.ReadInt16(), Unknown04 = reader.ReadInt32() };
                ReadBounds(reader, jump.Bounds);
                jump.DisplacementX = reader.ReadInt16();
                jump.DisplacementY = reader.ReadInt16();
                jump.DisplacementZ = reader.ReadInt16();
                jump.SpriteRotationAngle = reader.ReadInt16();
                jump.MovementSteps = reader.ReadInt16();
                jump.JumpAxis = reader.ReadUInt16();
                jump.InvertedJump = reader.ReadUInt16();
                jump.FinalFacingDirection = reader.ReadInt16();
                jump.TargetKind = reader.ReadInt16();
                jump.TargetPlatformIndex = reader.ReadUInt16();
                JumpPoints.Add(jump);
            }
        }

        private void ReadCameraRegions(BinaryReader reader, int sectionBytes)
        {
            if (sectionBytes < sizeof(int)) return;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var region = new CameraRegion();
                ReadBounds(reader, region.Bounds);
                region.AngleX = reader.ReadUInt16();
                region.AngleY = reader.ReadUInt16();
                region.AngleZ = reader.ReadUInt16();
                region.PlayerDirection = reader.ReadInt16();
                region.TransitionSteps = reader.ReadInt32();
                CameraRegions.Add(region);
            }
        }

        private void ReadGhostProps(BinaryReader reader, int sectionBytes)
        {
            if (sectionBytes < GhostHeaderSize) return;
            int propCount = reader.ReadInt32();
            int triggerCount = reader.ReadInt32();
            DefaultVisibleGroups = reader.ReadUInt32();

            for (int i = 0; i < propCount; i++)
            {
                GhostProps.Add(new GhostProp
                {
                    GroupId = reader.ReadUInt32(),
                    PropKind = reader.ReadUInt16(),
                    TileX = reader.ReadInt16(),
                    TileY = reader.ReadInt16(),
                    TileZ = reader.ReadInt16(),
                });
            }

            for (int i = 0; i < triggerCount; i++)
            {
                var trigger = new GhostTrigger
                {
                    GroupId = reader.ReadUInt32(),
                    PlayerDirection = reader.ReadInt16(),
                    ShowProp = reader.ReadInt16(),
                };
                ReadBounds(reader, trigger.Bounds);
                GhostTriggers.Add(trigger);
            }
        }

        private static void ReadBounds(BinaryReader reader, Bounds bounds)
        {
            bounds.StartX = reader.ReadInt16();
            bounds.StartY = reader.ReadInt16();
            bounds.StartZ = reader.ReadInt16();
            bounds.SizeX = reader.ReadInt16();
            bounds.SizeY = reader.ReadInt16();
            bounds.SizeZ = reader.ReadInt16();
        }

        private static void WriteBounds(BinaryWriter writer, Bounds bounds)
        {
            writer.Write(bounds.StartX);
            writer.Write(bounds.StartY);
            writer.Write(bounds.StartZ);
            writer.Write(bounds.SizeX);
            writer.Write(bounds.SizeY);
            writer.Write(bounds.SizeZ);
        }

        public override byte[] ToByteArray()
        {
            byte[] platforms = PlatformBytes();
            byte[] jumps = JumpPointBytes();
            byte[] cameras = CameraBytes();
            byte[] ghosts = GhostBytes();

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Unknown00);
                writer.Write(platforms.Length);
                writer.Write(jumps.Length);
                writer.Write(cameras.Length);
                writer.Write(ghosts.Length);
                writer.Write(platforms);
                writer.Write(jumps);
                writer.Write(cameras);
                writer.Write(ghosts);
                return stream.ToArray();
            }
        }

        private byte[] PlatformBytes()
        {
            if (Platforms.Count == 0) return Array.Empty<byte>();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Platforms.Count);
                foreach (var platform in Platforms)
                {
                    writer.Write((short)platform.Kind);
                    writer.Write((ushort)platform.AttributeId);
                    WriteBounds(writer, platform.Bounds);
                    writer.Write((ushort)platform.TilesVertical);
                    writer.Write((ushort)platform.TilesHorizontal);
                }
                return stream.ToArray();
            }
        }

        private byte[] JumpPointBytes()
        {
            if (JumpPoints.Count == 0) return Array.Empty<byte>();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(JumpPoints.Count);
                foreach (var jump in JumpPoints)
                {
                    writer.Write((ushort)jump.HandlerIndex);
                    writer.Write(jump.PlayerDirection);
                    writer.Write(jump.Unknown04);
                    WriteBounds(writer, jump.Bounds);
                    writer.Write(jump.DisplacementX);
                    writer.Write(jump.DisplacementY);
                    writer.Write(jump.DisplacementZ);
                    writer.Write(jump.SpriteRotationAngle);
                    writer.Write(jump.MovementSteps);
                    writer.Write((ushort)jump.JumpAxis);
                    writer.Write((ushort)jump.InvertedJump);
                    writer.Write(jump.FinalFacingDirection);
                    writer.Write(jump.TargetKind);
                    writer.Write((ushort)jump.TargetPlatformIndex);
                }
                return stream.ToArray();
            }
        }

        private byte[] CameraBytes()
        {
            if (CameraRegions.Count == 0) return Array.Empty<byte>();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(CameraRegions.Count);
                foreach (var region in CameraRegions)
                {
                    WriteBounds(writer, region.Bounds);
                    writer.Write((ushort)region.AngleX);
                    writer.Write((ushort)region.AngleY);
                    writer.Write((ushort)region.AngleZ);
                    writer.Write(region.PlayerDirection);
                    writer.Write(region.TransitionSteps);
                }
                return stream.ToArray();
            }
        }

        private byte[] GhostBytes()
        {
            if (GhostProps.Count == 0 && GhostTriggers.Count == 0) return Array.Empty<byte>();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(GhostProps.Count);
                writer.Write(GhostTriggers.Count);
                writer.Write((uint)DefaultVisibleGroups);
                foreach (var prop in GhostProps)
                {
                    writer.Write((uint)prop.GroupId);
                    writer.Write((ushort)prop.PropKind);
                    writer.Write(prop.TileX);
                    writer.Write(prop.TileY);
                    writer.Write(prop.TileZ);
                }
                foreach (var trigger in GhostTriggers)
                {
                    writer.Write((uint)trigger.GroupId);
                    writer.Write(trigger.PlayerDirection);
                    writer.Write(trigger.ShowProp);
                    WriteBounds(writer, trigger.Bounds);
                }
                return stream.ToArray();
            }
        }

        public static (int vertical, int horizontal)? GridPosition(FloatingPlatform platform, int tileX, int tileY, int tileZ)
        {
            if (platform == null || !platform.Bounds.Contains(tileX, tileY, tileZ)) return null;

            var bounds = platform.Bounds;
            switch (platform.Kind)
            {
                case PlatformKind.Floor:
                    return (tileX - bounds.StartX, tileZ - bounds.StartZ);
                case PlatformKind.WestWall:
                    return (bounds.SizeY - (tileY - bounds.StartY), tileZ - bounds.StartZ);
                case PlatformKind.EastWall:
                    return (tileY - bounds.StartY, tileZ - bounds.StartZ);
                case PlatformKind.Ceiling:
                    return (bounds.SizeX - (tileX - bounds.StartX), tileZ - bounds.StartZ);
                default:
                    return null;
            }
        }

        public static ushort? AttributeAt(FloatingPlatform platform, ushort[] grid, int tileX, int tileY, int tileZ)
        {
            var position = GridPosition(platform, tileX, tileY, tileZ);
            if (position == null || grid == null) return null;

            int index = position.Value.vertical + position.Value.horizontal * platform.TilesVertical;
            return index >= 0 && index < grid.Length ? grid[index] : (ushort?)null;
        }
    }
}
