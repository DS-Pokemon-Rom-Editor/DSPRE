using DSPRE;
using System;
using System.IO;
using static DSPRE.RomInfo;

public class GameCamera
{

    public const byte PERSPECTIVE = 0;
    public const byte ORTHO = 1;

    public uint Distance { get; set; }

    public short VertRot { get; private set; }
    public short HoriRot { get; private set; }
    public short ZRot { get; private set; }
    public short Unk1 { get; private set; }

    public byte PerspMode { get; private set; }

    public byte Unk2 { get; private set; }


    public ushort Fov { get; private set; }
    public uint NearClip { get; private set; }
    public uint FarClip { get; private set; }

    public int? XOffset { get; private set; }
    public int? YOffset { get; private set; }
    public int? ZOffset { get; private set; }

    public object this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return Distance;
                case 1:
                    return VertRot;
                case 2:
                    return HoriRot;
                case 3:
                    return ZRot;
                case 4:
                    return PerspMode;
                case 5:
                    return Fov;
                case 6:
                    return NearClip;
                case 7:
                    return FarClip;
                case 8:
                    return XOffset;
                case 9:
                    return YOffset;
                case 10:
                    return ZOffset;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        set
        {
            try
            {
                switch (index)
                {
                    case 0:
                        Distance = Convert.ToUInt32(value);
                        break;
                    case 1:
                        VertRot = Convert.ToInt16(value);
                        break;
                    case 2:
                        HoriRot = Convert.ToInt16(value);
                        break;
                    case 3:
                        ZRot = Convert.ToInt16(value);
                        break;
                    case 4:
                        PerspMode = (byte)(Convert.ToBoolean(value) ? 1 : 0);
                        break;
                    case 5:
                        Fov = Convert.ToUInt16(value);
                        break;
                    case 6:
                        NearClip = Convert.ToUInt32(value);
                        break;
                    case 7:
                        FarClip = Convert.ToUInt32(value);
                        break;
                    case 8:
                        XOffset = Convert.ToInt32(value);
                        break;
                    case 9:
                        YOffset = Convert.ToInt32(value);
                        break;
                    case 10:
                        ZOffset = Convert.ToInt32(value);
                        break;
                }
            }
            catch (OverflowException e)
            {
                AppMessages.Error("The value you selected is invalid.\n\n" + '"' + e.Message + '"', "Overflow");
            }
            catch (FormatException)
            {
                AppMessages.Error("Only numeric values are allowed.", "Invalid input");
            }
        }
    }

    public GameCamera(uint distance = 0x29AEC1, short vertRot = 0xD62, short horiRot = 0, short zRot = 0,
                        short unk1 = 0, byte perspMode = PERSPECTIVE, byte unk2 = 0,
                        ushort fov = 1473, uint nearClip = 614400, uint farClip = 0x384000,
                        int? xOffset = null, int? yOffset = null, int? zOffset = null)
    {

        this.Distance = distance;
        this.VertRot = vertRot;
        this.HoriRot = horiRot;
        this.ZRot = zRot;

        this.Unk1 = unk1;
        this.PerspMode = perspMode;
        this.Unk2 = unk2;

        this.Fov = fov;
        this.NearClip = nearClip;
        this.FarClip = farClip;

        this.XOffset = xOffset;
        this.YOffset = yOffset;
        this.ZOffset = zOffset;
    }

    public GameCamera(byte[] camData)
    {
        if (camData.Length != 36 && camData.Length != 24)
        {
            AppMessages.Error("This is not a camera file.\nMake sure the file is 36 or 24 bytes long and try again.", "Wrong file!");
            return;
        }
        try
        {
            using (BinaryReader b = new BinaryReader(new MemoryStream(camData)))
            {
                Distance = b.ReadUInt32();
                VertRot = b.ReadInt16();
                HoriRot = b.ReadInt16();
                ZRot = b.ReadInt16();

                Unk1 = b.ReadInt16();
                PerspMode = b.ReadByte();
                Unk2 = b.ReadByte();

                Fov = b.ReadUInt16();
                NearClip = b.ReadUInt32();
                FarClip = b.ReadUInt32();

                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    XOffset = b.ReadInt32();
                    YOffset = b.ReadInt32();
                    ZOffset = b.ReadInt32();
                }
            }
        }
        catch (EndOfStreamException)
        {
            AppMessages.Info("You might have to manually fill in the last three camera fields, since DPPt cameras don't have them.", "DPPt Cam detected");
        }
    }
    public byte[] ToByteArray()
    {
        MemoryStream newData = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(newData))
        {
            writer.Write(Distance);
            writer.Write(VertRot);
            writer.Write(HoriRot);
            writer.Write(ZRot);

            writer.Write(Unk1);
            writer.Write(PerspMode);
            writer.Write(Unk2);

            writer.Write(Fov);
            writer.Write(NearClip);
            writer.Write(FarClip);

            if (XOffset != null)
            {
                writer.Write((int)XOffset);
            }

            if (YOffset != null)
            {
                writer.Write((int)YOffset);
            }

            if (ZOffset != null)
            {
                writer.Write((int)ZOffset);
            }
        }

        return newData.ToArray();
    }

    // NOTE: the WinForms grid helper ShowInGridView(DataGridView, int) moved to
    // GameCameraGridExtensions (DSPRE WinForms project) so this class stays core/cross-platform.
}
