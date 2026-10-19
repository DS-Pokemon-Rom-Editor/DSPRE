using System;
using System.Collections.Generic;
using DSPRE;
using System.Linq;
using System.Text;
using System.IO;
namespace MKDS_Course_Editor.NSBTA
{
    public class NSBTA
    {
        public struct NSBTA_File
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
            public srt0 SRT0;
            public struct srt0 //Scale Rotation and Translation
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
                        public Int32 MAToffset;
                    }
                }
            }
            public M_AT MAT;
            public struct M_AT
            {
                public string ID;
                public Int16 Unknown1;
                public byte Unknown2;
                public byte Unknown3;
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
                        public Int16[] Frame;
                        public Int16[] Var1;
                        public Int16[] Var2;
                        public Int16[] Var3;
                        public int[] FrameStep;
                    }
                }
            }
            public srtData[] SRTData;
            public struct srtData
            {
                public decimal[] ScaleS;
                public decimal[] ScaleT;
                public decimal[] Rotate;
                public decimal[] TranslateS;
                public decimal[] TranslateT;
            }
        }

        /// <summary>Parse from memory, for animations that live inside a NARC rather than on disk.</summary>
        public static NSBTA_File Read(byte[] data) => Read(new MemoryStream(data));

        public static NSBTA_File Read(string Filename) => Read(File.OpenRead(Filename));

        public static NSBTA_File Read(Stream stream)
        {
            EndianBinaryReader er = new EndianBinaryReader(stream, Endianness.LittleEndian);
            NSBTA_File ns = new NSBTA_File();
            ns.Header.ID = er.ReadString(Encoding.ASCII, 4);
            if (ns.Header.ID == "BTA0")
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
                ns.SRT0.ID = er.ReadString(Encoding.ASCII, 4);
                if (ns.SRT0.ID == "SRT0")
                {
                    ns.SRT0.Size = er.ReadInt32();
                    //3D Info Structure
                    ns.SRT0.Dummy = er.ReadByte();
                    ns.SRT0.NumObjs = er.ReadByte();
                    ns.SRT0.SectionSize = er.ReadInt16();
                    ns.SRT0.Unknown.HeaderSize = er.ReadInt16();
                    ns.SRT0.Unknown.SectionSize = er.ReadInt16();
                    ns.SRT0.Unknown.Constant = er.ReadInt32();
                    ns.SRT0.Unknown.Unknown1 = new short[ns.SRT0.NumObjs];
                    ns.SRT0.Unknown.Unknown2 = new short[ns.SRT0.NumObjs];
                    for (int i = 0; i < ns.SRT0.NumObjs; i++)
                    {
                        ns.SRT0.Unknown.Unknown1[i] = er.ReadInt16();
                        ns.SRT0.Unknown.Unknown2[i] = er.ReadInt16();
                    }

                    ns.SRT0.InfoBlock.HeaderSize = er.ReadInt16();
                    ns.SRT0.InfoBlock.DataSize = er.ReadInt16();
                    ns.SRT0.InfoBlock.Data = new NSBTA_File.srt0.Info.info[ns.SRT0.NumObjs];
                    for (int i = 0; i < ns.SRT0.NumObjs; i++)
                    {
                        ns.SRT0.InfoBlock.Data[i].MAToffset = er.ReadInt32();
                    }
                    ns.SRT0.Names = new string[ns.SRT0.NumObjs];
                    for (int i = 0; i < ns.SRT0.NumObjs; i++)
                    {
                        ns.SRT0.Names[i] = er.ReadString(Encoding.ASCII, 16).Replace("\0", "");
                    }

                    ns.MAT.ID = er.ReadString(Encoding.ASCII, 4);
                    if (ns.MAT.ID == "M" + (char)0x00 + "AT")
                    {
                        ns.MAT.Unknown1 = er.ReadInt16();
                        ns.MAT.Unknown2 = er.ReadByte();
                        ns.MAT.Unknown3 = er.ReadByte();
                        //3D Info Structure
                        ns.MAT.Dummy = er.ReadByte();
                        ns.MAT.NumObjs = er.ReadByte();
                        ns.MAT.SectionSize = er.ReadInt16();
                        ns.MAT.Unknown.HeaderSize = er.ReadInt16();
                        ns.MAT.Unknown.SectionSize = er.ReadInt16();
                        ns.MAT.Unknown.Constant = er.ReadInt32();
                        ns.MAT.Unknown.Unknown1 = new short[ns.MAT.NumObjs];
                        ns.MAT.Unknown.Unknown2 = new short[ns.MAT.NumObjs];
                        for (int i = 0; i < ns.MAT.NumObjs; i++)
                        {
                            ns.MAT.Unknown.Unknown1[i] = er.ReadInt16();
                            ns.MAT.Unknown.Unknown2[i] = er.ReadInt16();
                        }

                        ns.MAT.InfoBlock.HeaderSize = er.ReadInt16();
                        ns.MAT.InfoBlock.DataSize = er.ReadInt16();
                        ns.MAT.InfoBlock.Data = new NSBTA_File.M_AT.Info.info[ns.MAT.NumObjs];
                        ns.SRTData = new NSBTA_File.srtData[ns.MAT.NumObjs];
                        for (int i = 0; i < ns.MAT.NumObjs; i++)
                        {
                            ns.MAT.InfoBlock.Data[i].Frame = new short[5];
                            ns.MAT.InfoBlock.Data[i].Var1 = new short[5];
                            ns.MAT.InfoBlock.Data[i].Var2 = new short[5];
                            ns.MAT.InfoBlock.Data[i].Var3 = new short[5];
                            ns.MAT.InfoBlock.Data[i].FrameStep = new int[5];
                            for (int j = 0; j < 5; j++)
                            {
                                ns.MAT.InfoBlock.Data[i].Frame[j] = er.ReadInt16();
                                ns.MAT.InfoBlock.Data[i].Var1[j] = (short)(er.ReadInt16() / 4096);
                                ns.MAT.InfoBlock.Data[i].Var2[j] = er.ReadInt16();
                                ns.MAT.InfoBlock.Data[i].Var3[j] = (short)(er.ReadInt16() / 4096);
                            }
                            if (ns.MAT.InfoBlock.Data[i].Var1[0] > 1)
                            {
                                ns.SRTData[i].ScaleS = new decimal[1];
                                ns.SRTData[i].ScaleS[0] = Math.Abs(ns.MAT.InfoBlock.Data[i].Var2[0] / 4096);
                            }
                            else
                            {
                                ns.SRTData[i].ScaleS = new decimal[ns.MAT.InfoBlock.Data[i].Frame[0]];
                            }
                            if (ns.MAT.InfoBlock.Data[i].Var1[1] > 1)
                            {
                                ns.SRTData[i].ScaleT = new decimal[1];
                                ns.SRTData[i].ScaleT[0] = Math.Abs(ns.MAT.InfoBlock.Data[i].Var2[1] / 4096);
                            }
                            else
                            {
                                ns.SRTData[i].ScaleT = new decimal[ns.MAT.InfoBlock.Data[i].Frame[1]];
                            }
                            if (ns.MAT.InfoBlock.Data[i].Var1[2] > 1)
                            {
                                ns.SRTData[i].Rotate = new decimal[2];
                                ns.SRTData[i].Rotate[0] = ns.MAT.InfoBlock.Data[i].Var2[2];
                                ns.SRTData[i].Rotate[1] = ns.MAT.InfoBlock.Data[i].Var3[2];
                            }
                            else
                            {
                                ns.SRTData[i].Rotate = new decimal[ns.MAT.InfoBlock.Data[i].Frame[2] * 2];
                            }
                            if (ns.MAT.InfoBlock.Data[i].Var1[3] > 1)
                            {
                                ns.SRTData[i].TranslateS = new decimal[1];
                                ns.SRTData[i].TranslateS[0] = Math.Abs(ns.MAT.InfoBlock.Data[i].Var2[3] / 4096);
                            }
                            else
                            {
                                ns.SRTData[i].TranslateS = new decimal[ns.MAT.InfoBlock.Data[i].Frame[3]];
                                ns.MAT.InfoBlock.Data[i].FrameStep[3] = Math.Abs(ns.MAT.InfoBlock.Data[i].Var1[3] >> 1);

                            }
                            if (ns.MAT.InfoBlock.Data[i].Var1[4] > 1)
                            {
                                ns.SRTData[i].TranslateT = new decimal[1];
                                ns.SRTData[i].TranslateT[0] = Math.Abs(ns.MAT.InfoBlock.Data[i].Var2[4] / 4096);
                            }
                            else
                            {
                                ns.SRTData[i].TranslateT = new decimal[ns.MAT.InfoBlock.Data[i].Frame[4]];
                                ns.MAT.InfoBlock.Data[i].FrameStep[4] = Math.Abs(ns.MAT.InfoBlock.Data[i].Var1[4] >> 1);
                            }
                            //ns.SRTData[i].scaleS = new decimal[ns.MAT.infoBlock.Data[i].var1[0] == 3 ? 1 : ns.MAT.infoBlock.Data[i].frame[0]];
                            //ns.SRTData[i].scaleT = new decimal[ns.MAT.infoBlock.Data[i].var1[1] == 3 ? 1 : ns.MAT.infoBlock.Data[i].frame[1]];
                            //ns.SRTData[i].rotate = new decimal[ns.MAT.infoBlock.Data[i].var1[2] == 2 ? ns.MAT.infoBlock.Data[i].var1[2] == 3 ? 2 : 1 : ns.MAT.infoBlock.Data[i].frame[2]];
                            //ns.SRTData[i].translateS = new decimal[ns.MAT.infoBlock.Data[i].var1[3] == 3 ? 1 : ns.MAT.infoBlock.Data[i].frame[3]];
                            //ns.SRTData[i].translateT = new decimal[ns.MAT.infoBlock.Data[i].var1[4] == 3 ? 1 : ns.MAT.infoBlock.Data[i].frame[4]];
                        }
                        ns.MAT.Names = new string[ns.MAT.NumObjs];
                        for (int i = 0; i < ns.MAT.NumObjs; i++)
                        {
                            ns.MAT.Names[i] = er.ReadString(Encoding.ASCII, 16).Replace("\0", "");
                        }
                        for (int i = 0; i < ns.MAT.NumObjs; i++)
                        {
                            if (ns.SRTData[i].ScaleS.Length != 1)
                            {
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.SRT0.SectionSize + ns.MAT.InfoBlock.Data[i].Var2[0] + 8;
                                for (int j = 0; j < ns.SRTData[i].ScaleS.Length; j++)
                                {
                                    ns.SRTData[i].ScaleS[j] = (decimal)(er.ReadInt16() / 4096d);
                                }
                            }
                            if (ns.SRTData[i].ScaleT.Length != 1)
                            {
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.SRT0.SectionSize + ns.MAT.InfoBlock.Data[i].Var2[1] + 8;
                                for (int j = 0; j < ns.SRTData[i].ScaleT.Length; j++)
                                {
                                    ns.SRTData[i].ScaleT[j] = (decimal)(er.ReadInt16() / 4096d);
                                }
                            }
                            if (ns.SRTData[i].Rotate.Length != 2)
                            {
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.SRT0.SectionSize + ns.MAT.InfoBlock.Data[i].Var2[2] + 8;
                                for (int j = 0; j < ns.SRTData[i].Rotate.Length; j++)
                                {
                                    ns.SRTData[i].Rotate[j] = (decimal)(er.ReadInt16() / 4096d);
                                }
                            }
                            if (ns.SRTData[i].TranslateS.Length != 1)
                            {
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.SRT0.SectionSize + ns.MAT.InfoBlock.Data[i].Var2[3] + 8;

                                for (int j = 0; j < ns.SRTData[i].TranslateS.Length; j += (ns.MAT.InfoBlock.Data[i].FrameStep[3] == 0 ? 1 : ns.MAT.InfoBlock.Data[i].FrameStep[3]))
                                {
                                    decimal value = (decimal)(er.ReadInt16() / 4096d);
                                    for (int k = 0; k < (ns.MAT.InfoBlock.Data[i].FrameStep[3] == 0 ? 1 : ns.MAT.InfoBlock.Data[i].FrameStep[3]); k++)
                                    {
                                        ns.SRTData[i].TranslateS[j + k] = value;
                                    }
                                }
                            }
                            if (ns.SRTData[i].TranslateT.Length != 1)
                            {
                                er.BaseStream.Position = ns.Header.Section_Offset[0] + ns.SRT0.SectionSize + ns.MAT.InfoBlock.Data[i].Var2[4] + 8;
                                for (int j = 0; j < ns.SRTData[i].TranslateT.Length; j += (ns.MAT.InfoBlock.Data[i].FrameStep[4] == 0 ? 1 : ns.MAT.InfoBlock.Data[i].FrameStep[4]))
                                {
                                    decimal value = (decimal)(er.ReadInt16() / 4096d);
                                    for (int k = 0; k < (ns.MAT.InfoBlock.Data[i].FrameStep[4] == 0 ? 1 : ns.MAT.InfoBlock.Data[i].FrameStep[4]); k++)
                                    {
                                        ns.SRTData[i].TranslateT[j + k] = value;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        AppLogger.Error("Not a texture animation (NSBTA): " + ns.Header.ID + "/" + ns.SRT0.ID);
                        er.Close();
                        return ns;
                    }
                }
                else
                {
                    AppLogger.Error("Not a texture animation (NSBTA): " + ns.Header.ID + "/" + ns.SRT0.ID);
                    er.Close();
                    return ns;
                }
            }
            else
            {
                AppLogger.Error("Not a texture animation (NSBTA): " + ns.Header.ID + "/" + ns.SRT0.ID);
                er.Close();
                return ns;
            }
            er.Close();
            return ns;
        }
    }
}
