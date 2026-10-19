using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPRE.Editors.Utils
{
    /// <summary>
    /// Represents an entry in a NARC archive with its offset and size.
    /// </summary>
    public struct FileEntry
    {
        public int Ofs;
        public int Size;
    }

    public class NarcReader
    {
        public int Entrys;

        public FileEntry[] Fe;

        public FileStream Fs;

        string _fileName;

        public long Size;

        public NarcReader(string strFileName)
        {
            _fileName = strFileName;
            Fs = new FileStream(strFileName, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader binaryReader = new BinaryReader(Fs);
            byte[] array = new byte[16];
            binaryReader.Read(array, 0, 16);
            Size = BitConverter.ToUInt32(array, 8);
            int num = BitConverter.ToInt16(array, 12);
            Fs.Seek(num, SeekOrigin.Begin);
            array = new byte[12];
            binaryReader.Read(array, 0, 12);
            int num2 = BitConverter.ToInt32(array, 4);
            Entrys = BitConverter.ToInt32(array, 8);
            Fe = new FileEntry[Entrys];
            for (int i = 0; i < Entrys; i++)
            {
                Fe[i].Ofs = binaryReader.ReadInt32();
                Fe[i].Size = binaryReader.ReadInt32() - Fe[i].Ofs;
            }
            Fs.Seek(num + num2, SeekOrigin.Begin);
            array = new byte[16];
            binaryReader.Read(array, 0, 16);
            int num3 = BitConverter.ToInt32(array, 4);
            num3 = num + num3 + num2 + 8;
            for (int j = 0; j < Entrys; j++)
            {
                Fe[j].Ofs += num3;
            }
            Fs.Close();
        }

        public void Close()
        {
            Fs.Close();
        }

        public int OpenEntry(int id)
        {
            Fs = new FileStream(_fileName, FileMode.Open, FileAccess.ReadWrite);
            Fs.Seek(Fe[id].Ofs, SeekOrigin.Begin);
            return 0;
        }
    }
}
