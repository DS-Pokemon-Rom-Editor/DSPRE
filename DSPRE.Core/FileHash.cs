using System;
using System.IO;
using System.Security.Cryptography;

namespace DSPRE
{
    /// <summary>Hashes of whole files, read as a stream.</summary>
    public static class FileHash
    {
        /// <summary>Lower-case hex, as ROM hashes are written.</summary>
        public static string Sha1(string path)
        {
            using FileStream f = File.OpenRead(path);
            return Convert.ToHexString(SHA1.HashData(f)).ToLowerInvariant();
        }

        /// <summary>Upper-case hex, matching the stored archive fingerprints and database markers.</summary>
        public static string Sha256(string path)
        {
            using FileStream f = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(f));
        }
    }
}
