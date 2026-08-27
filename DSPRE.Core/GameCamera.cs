using DSPRE;
using System;
using System.IO;
using static DSPRE.RomInfo;

public class GameCamera {

    public const byte PERSPECTIVE = 0;
    public const byte ORTHO = 1;

    public uint distance { get; set; }

    public short vertRot { get; private set; }
    public short horiRot { get; private set; }
    public short zRot { get; private set; }
    public short unk1 { get; private set; }

    public byte perspMode { get; private set; }

    public byte unk2 { get; private set; }


    public ushort fov { get; private set; }
    public uint nearClip { get; private set; }
    public uint farClip { get; private set; }

    public int? xOffset { get; private set; }
    public int? yOffset { get; private set; }
    public int? zOffset { get; private set; }

    public object this[int index] {
        get {
            switch (index) {
                case 0:
                    return distance;
                case 1:
                    return vertRot;
                case 2:
                    return horiRot;
                case 3:
                    return zRot;
                case 4:
                    return perspMode;
                case 5:
                    return fov;
                case 6:
                    return nearClip;
                case 7:
                    return farClip;
                case 8:
                    return xOffset;
                case 9:
                    return yOffset;
                case 10:
                    return zOffset;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        set {
            try {
                switch (index) {
                    case 0:
                        distance = Convert.ToUInt32(value);
                        break;
                    case 1:
                        vertRot = Convert.ToInt16(value);
                        break;
                    case 2:
                        horiRot = Convert.ToInt16(value);
                        break;
                    case 3:
                        zRot = Convert.ToInt16(value);
                        break;
                    case 4:
                        perspMode = (byte)(Convert.ToBoolean(value) ? 1 : 0);
                        break;
                    case 5:
                        fov = Convert.ToUInt16(value);
                        break;
                    case 6:
                        nearClip = Convert.ToUInt32(value);
                        break;
                    case 7:
                        farClip = Convert.ToUInt32(value);
                        break;
                    case 8:
                        xOffset = Convert.ToInt32(value);
                        break;
                    case 9:
                        yOffset = Convert.ToInt32(value);
                        break;
                    case 10:
                        zOffset = Convert.ToInt32(value);
                        break;
                }
            } catch (OverflowException e) {
                AppMessages.Error("The value you selected is invalid.\n\n" + '"' + e.Message + '"', "Overflow");
            } catch (FormatException) {
                AppMessages.Error("Only numeric values are allowed.", "Invalid input");
            }
        }
    }

    public GameCamera(uint distance = 0x29AEC1, short vertRot = 0xD62, short horiRot = 0, short zRot = 0,
                        short unk1 = 0, byte perspMode = PERSPECTIVE, byte unk2 = 0,
                        ushort fov = 1473, uint nearClip = 614400, uint farClip = 0x384000,
                        int? xOffset = null, int? yOffset = null, int? zOffset = null) {

        this.distance = distance;
        this.vertRot = vertRot;
        this.horiRot = horiRot;
        this.zRot = zRot;

        this.unk1 = unk1;
        this.perspMode = perspMode;
        this.unk2 = unk2;

        this.fov = fov;
        this.nearClip = nearClip;
        this.farClip = farClip;

        this.xOffset = xOffset;
        this.yOffset = yOffset;
        this.zOffset = zOffset;
    }

    public GameCamera(byte[] camData) {
        if (camData.Length != 36 && camData.Length != 24) {
            AppMessages.Error("This is not a camera file.\nMake sure the file is 36 or 24 bytes long and try again.", "Wrong file!");
            return;
        }
        try {
            using (BinaryReader b = new BinaryReader(new MemoryStream(camData))) {
                distance = b.ReadUInt32();
                vertRot = b.ReadInt16();
                horiRot = b.ReadInt16();
                zRot = b.ReadInt16();

                unk1 = b.ReadInt16();
                perspMode = b.ReadByte();
                unk2 = b.ReadByte();

                fov = b.ReadUInt16();
                nearClip = b.ReadUInt32();
                farClip = b.ReadUInt32();

                if (RomInfo.gameFamily == GameFamilies.HGSS) {
                    xOffset = b.ReadInt32();
                    yOffset = b.ReadInt32();
                    zOffset = b.ReadInt32();
                }
            }
        } catch (EndOfStreamException) {
            AppMessages.Info("You might have to manually fill in the last three camera fields, since DPPt cameras don't have them.", "DPPt Cam detected");
        }
    }
    public byte[] ToByteArray() {
        MemoryStream newData = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(newData)) {
            writer.Write(distance);
            writer.Write(vertRot);
            writer.Write(horiRot);
            writer.Write(zRot);

            writer.Write(unk1);
            writer.Write(perspMode);
            writer.Write(unk2);

            writer.Write(fov);
            writer.Write(nearClip);
            writer.Write(farClip);

            if (xOffset != null) {
                writer.Write((int)xOffset);
            }

            if (yOffset != null) {
                writer.Write((int)yOffset);
            }

            if (zOffset != null) {
                writer.Write((int)zOffset);
            }
        }

        return newData.ToArray();
    }

    // NOTE: the WinForms grid helper ShowInGridView(DataGridView, int) moved to
    // GameCameraGridExtensions (DSPRE WinForms project) so this class stays core/cross-platform.
}
