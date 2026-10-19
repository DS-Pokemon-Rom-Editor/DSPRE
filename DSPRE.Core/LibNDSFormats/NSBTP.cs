using System;
using System.Collections.Generic;
using DSPRE;
using System.Linq;
using System.Text;
using System.IO;

namespace MKDS_Course_Editor.NSBTP
{
    public class NSBTP
    {
        public struct NSBTP_File
        {
            public header Header;
            public struct header
            {
                public string ID;
                public byte[] Magic;
                public Int32 FileSize;
                public Int16 HeaderSize;
                public Int16 NSection;
                public Int32[] Section_Offset;
            }
            public pat0 PAT0;
            public struct pat0 //Scale Rotation and Translation
            {
                public string ID;
                public Int32 Size;
                //3D Info Structure
                public byte Dummy;
                public byte NumObjs;
                public short SectionSize;
                public UnknownBlock Unknown;
                public Info InfoBlock;
                public string[] Names;

                public struct UnknownBlock
                {
                    public short HeaderSize;
                    public short SectionSize;
                    public int Constant; // 0x017F

                    public short[] Unknown1;
                    public short[] Unknown2;
                }
                public struct Info
                {
                    public short HeaderSize;
                    public short DataSize;

                    public info[] Data;

                    public struct info
                    {
                        public Int32 MPToffset;
                    }
                }
            }
            public M_PT MPT;
            public struct M_PT
            {
                public string ID;
                public Int16 NoFrames;
                public byte NoTex;
                public byte NoPal;
                public Int16 Texoffset;
                public Int16 Paloffset;
                //3D Info Structure
                public byte Dummy;
                public byte NumObjs;
                public short SectionSize;
                public UnknownBlock Unknown;
                public Info InfoBlock;
                public string[] Names;

                public struct UnknownBlock
                {
                    public short HeaderSize;
                    public short SectionSize;
                    public int Constant; // 0x017F

                    public short[] Unknown1;
                    public short[] Unknown2;
                }
                public struct Info
                {
                    public short HeaderSize;
                    public short DataSize;


                    public info[] Data;

                    public struct info
                    {
                        public Int32 KeyFrames;
                        public short Unknown1;
                        public short Offset;
                    }
                }
            }
            public animData[] AnimData;
            public struct animData
            {
                public keyFrame[] KeyFrames;
                public struct keyFrame
                {
                    public short Start;
                    public byte TexId;
                    public byte PalId;
                    public string TexName;
                    public string PalName;
                }
            }
        }
        /// <summary>Parse from memory, for animations that live inside a NARC rather than on disk.</summary>
        public static NSBTP_File Read(byte[] data) => Read(new MemoryStream(data));

        public static NSBTP_File Read(string Filename) => Read(File.OpenRead(Filename));

        public static NSBTP_File Read(Stream stream)
        {
            EndianBinaryReader er = new EndianBinaryReader(stream, Endianness.LittleEndian);
            NSBTP_File ns = new NSBTP_File();
            ns.Header.ID = er.ReadString(Encoding.ASCII, 4);

            if (ns.Header.ID == "BTP0")
            {
                ns.Header.Magic = er.ReadBytes(4);
                ns.Header.FileSize = er.ReadInt32();
                ns.Header.HeaderSize = er.ReadInt16();
                ns.Header.NSection = er.ReadInt16();
                ns.Header.Section_Offset = new Int32[ns.Header.NSection];

                for (int i = 0; i < ns.Header.NSection; i++)
                {
                    ns.Header.Section_Offset[i] = er.ReadInt32();
                }
                ns.PAT0.ID = er.ReadString(Encoding.ASCII, 4);

                if (ns.PAT0.ID == "PAT0")
                {
                    ns.PAT0.Size = er.ReadInt32();
                    //3D Info Structure
                    ns.PAT0.Dummy = er.ReadByte();
                    ns.PAT0.NumObjs = er.ReadByte();
                    ns.PAT0.SectionSize = er.ReadInt16();
                    ns.PAT0.Unknown.HeaderSize = er.ReadInt16();
                    ns.PAT0.Unknown.SectionSize = er.ReadInt16();
                    ns.PAT0.Unknown.Constant = er.ReadInt32();
                    ns.PAT0.Unknown.Unknown1 = new short[ns.PAT0.NumObjs];
                    ns.PAT0.Unknown.Unknown2 = new short[ns.PAT0.NumObjs];
                    for (int i = 0; i < ns.PAT0.NumObjs; i++)
                    {
                        ns.PAT0.Unknown.Unknown1[i] = er.ReadInt16();
                        ns.PAT0.Unknown.Unknown2[i] = er.ReadInt16();
                    }

                    ns.PAT0.InfoBlock.HeaderSize = er.ReadInt16();
                    ns.PAT0.InfoBlock.DataSize = er.ReadInt16();
                    ns.PAT0.InfoBlock.Data = new NSBTP_File.pat0.Info.info[ns.PAT0.NumObjs];

                    for (int i = 0; i < ns.PAT0.NumObjs; i++)
                    {
                        ns.PAT0.InfoBlock.Data[i].MPToffset = er.ReadInt32();
                    }

                    ns.PAT0.Names = new string[ns.PAT0.NumObjs];
                    for (int i = 0; i < ns.PAT0.NumObjs; i++)
                    {
                        ns.PAT0.Names[i] = er.ReadString(Encoding.ASCII, 16).Replace("\0", "");
                    }

                    ns.MPT.ID = er.ReadString(Encoding.ASCII, 4);

                    if (ns.MPT.ID == "M" + (char)0x00 + "PT")
                    {
                        ns.MPT.NoFrames = er.ReadInt16();
                        ns.MPT.NoTex = er.ReadByte();
                        ns.MPT.NoPal = er.ReadByte();
                        ns.MPT.Texoffset = er.ReadInt16();
                        ns.MPT.Paloffset = er.ReadInt16();
                        //3D Info Structure
                        ns.MPT.Dummy = er.ReadByte();
                        ns.MPT.NumObjs = er.ReadByte();
                        ns.MPT.SectionSize = er.ReadInt16();
                        ns.MPT.Unknown.HeaderSize = er.ReadInt16();
                        ns.MPT.Unknown.SectionSize = er.ReadInt16();
                        ns.MPT.Unknown.Constant = er.ReadInt32();
                        ns.MPT.Unknown.Unknown1 = new short[ns.MPT.NumObjs];
                        ns.MPT.Unknown.Unknown2 = new short[ns.MPT.NumObjs];

                        for (int i = 0; i < ns.MPT.NumObjs; i++)
                        {
                            ns.MPT.Unknown.Unknown1[i] = er.ReadInt16();
                            ns.MPT.Unknown.Unknown2[i] = er.ReadInt16();
                        }

                        ns.MPT.InfoBlock.HeaderSize = er.ReadInt16();
                        ns.MPT.InfoBlock.DataSize = er.ReadInt16();
                        ns.MPT.InfoBlock.Data = new NSBTP_File.M_PT.Info.info[ns.MPT.NumObjs];
                        ns.AnimData = new NSBTP_File.animData[ns.MPT.NumObjs];

                        for (int i = 0; i < ns.MPT.NumObjs; i++)
                        {
                            ns.MPT.InfoBlock.Data[i].KeyFrames = er.ReadInt32();
                            ns.MPT.InfoBlock.Data[i].Unknown1 = er.ReadInt16();
                            ns.MPT.InfoBlock.Data[i].Offset = er.ReadInt16();
                            ns.AnimData[i].KeyFrames = new NSBTP_File.animData.keyFrame[ns.MPT.InfoBlock.Data[i].KeyFrames];
                           
                            for (int j = 0; j < ns.MPT.InfoBlock.Data[i].KeyFrames; j++)
                            {
                                long curpos = er.BaseStream.Position;
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.PAT0.SectionSize + ns.MPT.InfoBlock.Data[i].Offset + j * 4 + 8;
                                ns.AnimData[i].KeyFrames[j].Start = er.ReadInt16();
                                ns.AnimData[i].KeyFrames[j].TexId = er.ReadByte();
                                ns.AnimData[i].KeyFrames[j].PalId = er.ReadByte();
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.PAT0.SectionSize + ns.MPT.Texoffset + ns.AnimData[i].KeyFrames[j].TexId * 16 + 8;
                                ns.AnimData[i].KeyFrames[j].TexName = LibNDSFormats.Utils.ReadNSBMDString(er);
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.PAT0.SectionSize + ns.MPT.Paloffset + ns.AnimData[i].KeyFrames[j].PalId * 16 + 8;
                                ns.AnimData[i].KeyFrames[j].PalName = LibNDSFormats.Utils.ReadNSBMDString(er);
                                er.BaseStream.Position = curpos;
                            }
                        }
                        ns.MPT.Names = new string[ns.MPT.NumObjs];
                        
                        for (int i = 0; i < ns.MPT.NumObjs; i++)
                        {
                            ns.MPT.Names[i] = LibNDSFormats.Utils.ReadNSBMDString(er);
                        }
                    }
                    else
                    {
                        AppMessages.Error("NSBTP Error");
                        er.Close();
                        return ns;
                    }
                }
                else
                {
                    AppMessages.Error("NSBTP Error");
                    er.Close();
                    return ns;
                }
            }
            else
            {
                AppMessages.Error("NSBTP Error");
                er.Close();
                return ns;
            }
            er.Close();
            return ns;
        }
    }
}
