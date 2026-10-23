using DSPRE;
using System;
using System.IO;
using System.Threading.Tasks;
namespace NarcAPI
{
    public class Narc
    { //Nitro Archive
        public String Name { get; set; }
        private MemoryStream[] _elements;
        private int _fileNameTableOffset, _fileImageOffset;
        
        private const int NarcFileMagicNum = 0x4352414E;                 //"NARC" in ascii/unicode
        private const int FileAllocationTableOffset = 0x10;
        private const int FileAllocationTableNumElementsOffset = 0x18;
        private const int FileAllocationTableHeaderLength = 12;
        private const int FileAllocationTableElementLength = 0x8;
        private const int FileImageHeaderSize = 0x8;
        private const int FileNameTableSignatureLength = 0x4;

        private Narc(String name)
        {
            this.Name = name;
        }

        public static Narc NewEmpty(String name = "NewNarc")
        {
            Narc narc = new Narc(name);
            return narc;
        }

        // Where this archive was read from, so packing a project's folder or unpacking its archive keeps the two in step.
        private string _sourceFile, _sourceFolder;

        public static Narc Open(String filePath)
        {
            Narc narc = new Narc(Path.GetFileNameWithoutExtension(filePath)) { _sourceFile = filePath };
            using (BinaryReader br = new BinaryReader(File.OpenRead(filePath)))
            {
                if (br.BaseStream.Length < 4 || br.ReadUInt32() != NarcFileMagicNum)
                {
                    return null;
                }

                narc.ReadOffsets(br);
                narc.ReadElements(br);
            }

            AppLogger.Debug($"Loaded NARC \"{narc.Name}\" with {narc._elements.Length} elements from file: {filePath}");

            return narc;
        }

        public static Narc FromFolder(String dirPath)
        {
            Narc narc = new Narc(Path.GetFileNameWithoutExtension(dirPath)) { _sourceFolder = dirPath };
            // Members are the folder's own files; subfolders, backups and OS clutter are not.
            String[] fileNames = Directory.GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly);
            fileNames = Array.FindAll(fileNames, IsMemberFile);
            // ExtractToFolder names members by index, and past 9999 a text sort would misplace them.
            Array.Sort(fileNames, CompareMemberNames);

            uint numberOfElements = (uint)fileNames.Length;
            narc._elements = new MemoryStream[numberOfElements];

            Parallel.For(0, numberOfElements, i => {
                FileStream fs = File.OpenRead(fileNames[i]);
                MemoryStream ms = new MemoryStream();
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, (int)fs.Length);
                ms.Write(buffer, 0, (int)fs.Length);
                narc._elements[i] = ms;
                fs.Close();
            });

            AppLogger.Debug($"Loaded NARC \"{narc.Name}\" with {numberOfElements} elements from folder: {dirPath}");

            return narc;
        }

        /// <summary>How many members packing would make of a folder: its own files, less backups and OS clutter.</summary>
        public static int MemberCount(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return 0;
            int count = 0;
            // The listing carries each file's attributes, so counting asks nothing more per file.
            foreach (FileInfo file in new DirectoryInfo(dirPath).EnumerateFiles()) if (IsMember(file)) count++;
            return count;
        }

        private static bool IsMemberFile(string path)
        {
            try
            {
                return IsMember(new FileInfo(path));
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool IsMember(FileInfo file)
        {
            string ext = file.Extension.ToLowerInvariant();
            if (ext == ".bak" || ext == ".backup") return false;
            if (file.Name.StartsWith(".")
                || file.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)
                || file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return false;
            return (file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
        }

        private static int CompareMemberNames(string a, string b)
        {
            string na = Path.GetFileNameWithoutExtension(a), nb = Path.GetFileNameWithoutExtension(b);
            bool numA = long.TryParse(na, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long ia);
            bool numB = long.TryParse(nb, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long ib);
            if (numA && numB && ia != ib) return ia.CompareTo(ib);
            if (numA != numB) return numA ? -1 : 1;
            // Case-insensitive so the order matches across filesystems such as WSL.
            return StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(a), Path.GetFileName(b));
        }

        public void Save(String filePath)
        {
            uint fileSizeOffset, fileImageSizeOffset, curOffset;

            BinaryWriter bw = new BinaryWriter(File.Create(filePath));
            // Write NARC Section
            bw.Write(NarcFileMagicNum);
            bw.Write(0x0100FFFE);
            fileSizeOffset = (uint)bw.BaseStream.Position;
            bw.Write((UInt32)0x0);
            bw.Write((ushort)16);                   //full size of header section
            bw.Write((ushort)3);                    //the number of sections in the header
            // Write FATB Section
            bw.Write(0x46415442);                   // "BTAF"
            bw.Write((UInt32)(FileAllocationTableHeaderLength + _elements.Length * FileAllocationTableElementLength));
            bw.Write((UInt32)_elements.Length);      // Number of elements
            curOffset = 0;
            for (int i = 0; i < _elements.Length; i++)
            {
                while (curOffset % 4 != 0)
                {
                    curOffset++;     // Force offsets to be a multiple of 4
                }

                bw.Write(curOffset);
                curOffset += (uint)_elements[i].Length;
                bw.Write(curOffset);
            }
            // FNTB: no file names
            bw.Write(0x464E5442);       //"BTNF"
            bw.Write(0x10);             //FNTB Size
            bw.Write(0x4);              //the offset of the first name directory
            bw.Write(0x10000);          //filler data describing a file at position 0 with 1 directory in the archive
            // Write FIMG Section
            bw.Write(0x46494D47);       // "GMIF"
            fileImageSizeOffset = (uint)bw.BaseStream.Position;
            bw.Write((UInt32)0x0);      
            curOffset = 0;
            byte[] buffer;
            for (int i = 0; i < _elements.Length; i++)
            {
                while (curOffset % 4 != 0)
                { // Force offsets to be a multiple of 4
                    bw.Write((Byte)0xFF); curOffset++; 
                }     

                buffer = new byte[_elements[i].Length];
                _elements[i].Seek(0, SeekOrigin.Begin);
                _elements[i].Read(buffer, 0, (int)_elements[i].Length);
                bw.Write(buffer, 0, (int)_elements[i].Length);
                curOffset += (uint)_elements[i].Length;
            }

            // Pad to 4-byte boundary
            while (curOffset % 4 != 0)
            {
                bw.Write((Byte)0xFF);
                curOffset++;
            }


            int fileSize = (int)bw.BaseStream.Position;
            bw.Seek((int)fileSizeOffset, SeekOrigin.Begin);         // File size
            bw.Write((UInt32)fileSize);
            bw.Seek((int)fileImageSizeOffset, SeekOrigin.Begin);         // seeks back to FIMG size
            bw.Write((UInt32)curOffset + FileImageHeaderSize);   // FIMG size == Last end offset + File image header size

            AppLogger.Debug($"Saved NARC \"{Name}\" with {_elements.Length} elements and filesize {fileSize} Bytes to file: {filePath}");

            bw.Close();
            DSPRE.NarcSync.NoteSaved(_sourceFolder, filePath);
        }

        public int ElementCount => _elements.Length;

        public byte[] GetElementBytes(int index)
        {
            MemoryStream stream = _elements[index];
            byte[] buffer = new byte[stream.Length];
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        public void ExtractToFolder(String dirPath, string extension = null)
        {
            if ( string.IsNullOrWhiteSpace(dirPath) )
            {
                AppMessages.Error("The folder \"" + dirPath + "\" isn't a valid path.", "Couldn't unpack archive");
                return;
            }

            if (Directory.Exists(dirPath))
            {
                if (Directory.GetFiles(dirPath).Length > 0)
                {
                    try
                    {
                        if (dirPath.IndexOf(RomInfo.FolderSuffix, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        {
                            Directory.Delete(dirPath, true);
                            AppLogger.Debug("Deleted DSPRE-related folder \"" + dirPath + "\" without user confirmation.");
                        }
                        else
                        {
                            if (AppMessages.Confirm("The folder \"" + dirPath + "\" already has files in it. " +
                                "Delete them?", "Folder not empty"))
                            {
                                Directory.Delete(dirPath, true);
                                AppLogger.Debug("Deleted non-DSPRE-related folder \"" + dirPath + "\" after user confirmation.");
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                    catch (IOException)
                    {
                        AppMessages.Error("The archive couldn't be unpacked because " + dirPath + " couldn't be deleted. Close anything using it and try again.", "Couldn't unpack archive");
                        return;
                    }
                }
            }

            if (!Directory.Exists(dirPath))
            {
                try
                {
                    Directory.CreateDirectory(dirPath);
                    AppLogger.Debug("Created NARC folder \"" + dirPath + "\".");
                }
                catch (IOException)
                {
                    AppMessages.Error("The archive couldn't be unpacked because " + dirPath + " couldn't be created. Close anything using it and try again.", "Couldn't unpack archive");
                    return;
                }
            }

            Parallel.For(0, _elements.Length, i => {
                string path = Path.Combine(dirPath, i.ToString("D4") + (string.IsNullOrWhiteSpace(extension) ? "" : extension) );
                using (BinaryWriter wr = new BinaryWriter(File.Create(path)))
                {
                    long len = _elements[i].Length;
                    byte[] buffer = new byte[len];
                    _elements[i].Seek(0, SeekOrigin.Begin);
                    _elements[i].Read(buffer, 0, (int)len);
                    wr.Write(buffer);
                }
            });

            AppLogger.Debug($"Extracted NARC \"{Name}\" with {_elements.Length} elements to folder: {dirPath}");
            DSPRE.NarcSync.NoteExtracted(_sourceFile, dirPath);

        }

        public void Free()
        { // releases the streams
            Parallel.For(0, _elements.Length, i => {
                _elements[i].Close();
            });
        }

        public MemoryStream this[int elemIndex]
        {
            get
            {
                return _elements[elemIndex];
            }
            set
            {
                _elements[elemIndex] = value;
            }
        }

        public int GetElementsLength()
        {
            return _elements.Length;
        }

        private void ReadOffsets(BinaryReader br)
        {
            br.BaseStream.Position = FileAllocationTableNumElementsOffset;             
            _fileNameTableOffset = (int)br.ReadUInt32() * FileAllocationTableElementLength + FileAllocationTableOffset + FileAllocationTableHeaderLength;
            br.BaseStream.Position = _fileNameTableOffset + FileNameTableSignatureLength;
            _fileImageOffset = (int)br.ReadUInt32() + _fileNameTableOffset;
        }

        private void ReadElements(BinaryReader br)
        {
            uint numberOfElements;
            uint[] startOffsets, endOffsets;

            br.BaseStream.Position = FileAllocationTableNumElementsOffset;
            _elements = new MemoryStream[numberOfElements = br.ReadUInt32()];


            startOffsets = new uint[numberOfElements]; 
            endOffsets = new uint[numberOfElements];
            br.BaseStream.Position = FileAllocationTableOffset + FileAllocationTableHeaderLength;
            for (int i = 0; i < numberOfElements; i++)
            { 
                startOffsets[i] = br.ReadUInt32(); 
                endOffsets[i] = br.ReadUInt32(); 
            }

            for(int i = 0; i < numberOfElements; i++)
            {
                br.BaseStream.Position = _fileImageOffset + startOffsets[i] + FileImageHeaderSize;
                byte[] buffer = new byte[endOffsets[i] - startOffsets[i]];
                br.Read(buffer, 0, (int)(endOffsets[i] - startOffsets[i]));
                _elements[i] = new MemoryStream(buffer);
            }
        }
    }
}
