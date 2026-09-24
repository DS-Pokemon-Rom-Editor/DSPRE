using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Map textures the field animates (data/fldtanime.narc). Member 0 lists texture names with up to 18
    /// frame/duration pairs; member n+1 is a texture pack holding entry n's frames. The game finds each name
    /// in the loaded map texture pack and copies the current frame's texels over it every step.
    /// </summary>
    public sealed class FieldTextureAnimations
    {
        public const int MostFrames = 18, NameLength = 16, EntrySize = NameLength + MostFrames * 2;
        public const byte EndOfFrames = 0xFF;

        public sealed class Entry
        {
            public string Name;
            public List<(byte Frame, byte Duration)> Frames = new List<(byte, byte)>();
            public byte[] FramePack;
        }

        public List<Entry> Entries { get; } = new List<Entry>();

        public static bool Available => gameDirs != null && gameDirs.ContainsKey(DirNames.fieldTextureAnimations);

        private static string Folder => gameDirs[DirNames.fieldTextureAnimations].unpackedDir;

        public Entry For(string textureName) =>
            string.IsNullOrEmpty(textureName) ? null : Entries.FirstOrDefault(e => e.Name == textureName);

        public static FieldTextureAnimations Load()
        {
            if (!Available) return null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.fieldTextureAnimations });
            string header = Path.Combine(Folder, "0000");
            if (!File.Exists(header)) return null;

            var read = Parse(File.ReadAllBytes(header), i =>
            {
                string p = Path.Combine(Folder, (i + 1).ToString("D4"));
                return File.Exists(p) ? File.ReadAllBytes(p) : null;
            });
            return read;
        }

        public static FieldTextureAnimations Parse(byte[] header, Func<int, byte[]> framePack)
        {
            var set = new FieldTextureAnimations();
            if (header == null || header.Length < 4) return set;
            int count = BitConverter.ToInt32(header, 0);
            for (int i = 0; i < count && 4 + (i + 1) * EntrySize <= header.Length; i++)
            {
                int at = 4 + i * EntrySize;
                int n = 0;
                while (n < NameLength && header[at + n] != 0) n++;
                var entry = new Entry { Name = Encoding.ASCII.GetString(header, at, n), FramePack = framePack?.Invoke(i) };
                for (int f = 0; f < MostFrames; f++)
                {
                    byte frame = header[at + NameLength + f * 2], duration = header[at + NameLength + f * 2 + 1];
                    if (frame == EndOfFrames) break;
                    entry.Frames.Add((frame, duration));
                }
                set.Entries.Add(entry);
            }
            return set;
        }

        public byte[] HeaderBytes()
        {
            var b = new byte[4 + Entries.Count * EntrySize];
            BitConverter.GetBytes(Entries.Count).CopyTo(b, 0);
            for (int i = 0; i < Entries.Count; i++)
            {
                int at = 4 + i * EntrySize;
                var name = Encoding.ASCII.GetBytes(Entries[i].Name ?? "");
                Array.Copy(name, 0, b, at, Math.Min(name.Length, NameLength - 1));
                for (int f = 0; f < MostFrames; f++)
                {
                    bool used = f < Entries[i].Frames.Count;
                    b[at + NameLength + f * 2] = used ? Entries[i].Frames[f].Frame : EndOfFrames;
                    b[at + NameLength + f * 2 + 1] = used ? Entries[i].Frames[f].Duration : (byte)0;
                }
            }
            return b;
        }

        /// <summary>Writes the list and every frame pack back to the unpacked archive; Save ROM packs it.</summary>
        public void Save()
        {
            if (!Available) throw new InvalidOperationException("This game has no field texture animations.");
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, "0000"), HeaderBytes());
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].FramePack != null)
                    File.WriteAllBytes(Path.Combine(Folder, (i + 1).ToString("D4")), Entries[i].FramePack);
            for (int i = Entries.Count + 1; File.Exists(Path.Combine(Folder, i.ToString("D4"))); i++)
                File.Delete(Path.Combine(Folder, i.ToString("D4")));
        }
    }
}
