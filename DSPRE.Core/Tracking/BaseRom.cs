using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DSPRE.Tracking
{
    /// <summary>The ROM changes are measured against, extracted once per machine into DSPRE's data folder.</summary>
    public static class BaseRom
    {
        private const string DoneMarker = ".extracted";

        public static string CacheRoot => Path.Combine(AppPaths.DspreDataPath, "tracking", "base");

        public static string Sha1Of(string ndsPath)
        {
            using FileStream stream = File.OpenRead(ndsPath);
            return "sha1:" + Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>Game code and revision from an .nds header.</summary>
        public static bool TryReadHeader(string ndsPath, out string code, out int revision)
        {
            code = null;
            revision = -1;
            try
            {
                using FileStream stream = File.OpenRead(ndsPath);
                byte[] header = new byte[0x20];
                if (stream.Read(header, 0, header.Length) != header.Length) return false;
                code = Encoding.ASCII.GetString(header, 0x0C, 4);
                revision = header[0x1E];
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static string FolderFor(string sha1, bool dsRom) =>
            Path.Combine(CacheRoot, sha1.Replace("sha1:", "") + (dsRom ? "" : "-ndstool"));

        public static bool IsExtracted(string sha1, bool dsRom) => File.Exists(Path.Combine(FolderFor(sha1, dsRom), DoneMarker));

        /// <summary>Extracts the ROM laid out like the project, so both trees have the same paths.</summary>
        public static bool TryExtract(string ndsPath, string sha1, bool dsRom, out string root, out string error)
        {
            root = FolderFor(sha1, dsRom);
            error = null;
            if (IsExtracted(sha1, dsRom)) return true;
            string building = root + ".partial";
            try
            {
                if (Directory.Exists(building)) Directory.Delete(building, true);
                bool ok = dsRom ? DSUtils.UnpackRomDsRom(ndsPath, building) : DSUtils.UnpackRomNdstool(ndsPath, building);
                if (!ok)
                {
                    error = "The base ROM couldn't be extracted.";
                    return false;
                }
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.Move(building, root);
                PlainFiles.WriteText(Path.Combine(root, DoneMarker), sha1);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = "The base ROM couldn't be extracted: " + ex.Message;
                return false;
            }
            finally
            {
                try { if (Directory.Exists(building)) Directory.Delete(building, true); }
                catch (IOException ex) { AppLogger.Warn("BaseRom: leftover extraction not removed: " + ex.Message); }
            }
        }
    }
}
