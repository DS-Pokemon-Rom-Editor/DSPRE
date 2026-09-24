using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.Tests.Models
{
    internal static class MapModels
    {
        public static IEnumerable<(string name, byte[] model)> Of(string project, bool hgss)
        {
            string maps = Path.Combine(project, "unpacked", "maps");
            foreach (string path in RomFiles.Settled(maps))
            {
                byte[] model = null;
                try { model = ModelOut(File.ReadAllBytes(path), hgss); } catch { }
                if (model != null && model.Length > 16) yield return (Path.GetFileName(path), model);
            }
        }

        private static byte[] ModelOut(byte[] map, bool hgss)
        {
            int permissions = BitConverter.ToInt32(map, 0);
            int buildings = BitConverter.ToInt32(map, 4);
            int model = BitConverter.ToInt32(map, 8);

            int at = 16;
            if (hgss)
            {
                if (BitConverter.ToUInt16(map, at) != 0x1234) return null;
                at += 4 + BitConverter.ToUInt16(map, at + 2);
            }
            at += permissions + buildings;
            if (at < 0 || model < 0 || at + model > map.Length) return null;

            var bytes = new byte[model];
            Array.Copy(map, at, bytes, 0, model);
            return bytes;
        }
    }
}
