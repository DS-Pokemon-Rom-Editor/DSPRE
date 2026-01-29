using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DSPRE
{
    /// <summary>
    /// FFI bindings for ds-rom library.
    /// Provides P/Invoke declarations for ROM extraction/building and LZ77 compression.
    /// </summary>
    /// <remarks>
    /// NOTE: Path handling uses ANSI encoding. Non-ASCII characters in paths may not be
    /// handled correctly on all systems. This is a known limitation for typical ROM hacking
    /// use cases where paths are usually ASCII-only.
    /// </remarks>
    public static class DsRomFFI
    {
        private const string DllName = "ds_rom.dll";

        // Result codes
        public const int DSROM_OK = 0;
        public const int DSROM_ERR_INVALID_INPUT = -1;
        public const int DSROM_ERR_IO = -2;
        public const int DSROM_ERR_PARSE = -3;
        public const int DSROM_ERR_BLOWFISH_NEEDED = -4;
        public const int DSROM_ERR_UNKNOWN = -99;

        /// <summary>
        /// Static constructor to verify the DLL is available.
        /// </summary>
        static DsRomFFI()
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName);
            if (!File.Exists(dllPath))
            {
                throw new DllNotFoundException(
                    $"ds_rom.dll not found at: {dllPath}\n" +
                    "Please ensure the ds-rom library is properly installed.");
            }
        }

        /// <summary>
        /// Extracts a ROM file to a directory.
        /// </summary>
        /// <param name="romPath">Path to the input ROM file</param>
        /// <param name="outDir">Path to the output directory</param>
        /// <returns>DSROM_OK on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int dsrom_extract(string romPath, string outDir);

        /// <summary>
        /// Builds a ROM from a config file.
        /// </summary>
        /// <param name="configPath">Path to the config.yaml file</param>
        /// <param name="outRom">Path to the output ROM file</param>
        /// <returns>DSROM_OK on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int dsrom_build(string configPath, string outRom);

        /// <summary>
        /// Compresses data using LZ77 algorithm.
        /// </summary>
        /// <param name="inputPtr">Pointer to input data</param>
        /// <param name="inputLen">Length of input data</param>
        /// <param name="start">Start offset for compression</param>
        /// <param name="outputPtr">Pointer to store output buffer pointer</param>
        /// <param name="outputLen">Pointer to store output buffer length</param>
        /// <returns>DSROM_OK on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dsrom_lz77_compress(
            IntPtr inputPtr,
            UIntPtr inputLen,
            UIntPtr start,
            out IntPtr outputPtr,
            out UIntPtr outputLen);

        /// <summary>
        /// Decompresses data using LZ77 algorithm.
        /// </summary>
        /// <param name="inputPtr">Pointer to input data</param>
        /// <param name="inputLen">Length of input data</param>
        /// <param name="outputPtr">Pointer to store output buffer pointer</param>
        /// <param name="outputLen">Pointer to store output buffer length</param>
        /// <returns>DSROM_OK on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dsrom_lz77_decompress(
            IntPtr inputPtr,
            UIntPtr inputLen,
            out IntPtr outputPtr,
            out UIntPtr outputLen);

        /// <summary>
        /// Frees a buffer allocated by dsrom_lz77_compress or dsrom_lz77_decompress.
        /// </summary>
        /// <param name="ptr">Pointer to the buffer</param>
        /// <param name="len">Length of the buffer</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void dsrom_free_buffer(IntPtr ptr, UIntPtr len);

        /// <summary>
        /// Compresses a file using LZ77 algorithm (in-place).
        /// </summary>
        /// <param name="filePath">Path to the file to compress</param>
        /// <param name="start">Start offset for compression</param>
        /// <returns>DSROM_OK on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int dsrom_lz77_compress_file(string filePath, UIntPtr start);

        /// <summary>
        /// Decompresses a file using LZ77 algorithm (in-place).
        /// </summary>
        /// <param name="filePath">Path to the file to decompress</param>
        /// <returns>DSROM_OK on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int dsrom_lz77_decompress_file(string filePath);

        /// <summary>
        /// Helper method to extract a ROM using FFI.
        /// </summary>
        public static bool ExtractRom(string romPath, string outDir, out string error)
        {
            error = null;
            int result = dsrom_extract(romPath, outDir);

            if (result == DSROM_OK)
                return true;

            error = GetErrorMessage(result);
            return false;
        }

        /// <summary>
        /// Helper method to build a ROM using FFI.
        /// </summary>
        public static bool BuildRom(string configPath, string outRom, out string error)
        {
            error = null;
            int result = dsrom_build(configPath, outRom);

            if (result == DSROM_OK)
                return true;

            error = GetErrorMessage(result);
            return false;
        }

        /// <summary>
        /// Helper method to compress a file using LZ77 (replaces blz.exe).
        /// </summary>
        public static bool CompressFile(string filePath, int start = 0)
        {
            int result = dsrom_lz77_compress_file(filePath, (UIntPtr)start);
            return result == DSROM_OK;
        }

        /// <summary>
        /// Helper method to decompress a file using LZ77 (replaces blz.exe).
        /// </summary>
        public static bool DecompressFile(string filePath)
        {
            int result = dsrom_lz77_decompress_file(filePath);
            return result == DSROM_OK;
        }

        /// <summary>
        /// Compresses a byte array using LZ77.
        /// </summary>
        public static byte[] CompressBytes(byte[] data, int start = 0)
        {
            IntPtr outputPtr;
            UIntPtr outputLen;

            IntPtr inputPtr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, inputPtr, data.Length);
                int result = dsrom_lz77_compress(inputPtr, (UIntPtr)data.Length, (UIntPtr)start, out outputPtr, out outputLen);

                if (result != DSROM_OK)
                    return null;

                byte[] output = new byte[(int)outputLen];
                Marshal.Copy(outputPtr, output, 0, (int)outputLen);
                dsrom_free_buffer(outputPtr, outputLen);

                return output;
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
            }
        }

        /// <summary>
        /// Decompresses a byte array using LZ77.
        /// </summary>
        public static byte[] DecompressBytes(byte[] data)
        {
            IntPtr outputPtr;
            UIntPtr outputLen;

            IntPtr inputPtr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, inputPtr, data.Length);
                int result = dsrom_lz77_decompress(inputPtr, (UIntPtr)data.Length, out outputPtr, out outputLen);

                if (result != DSROM_OK)
                    return null;

                byte[] output = new byte[(int)outputLen];
                Marshal.Copy(outputPtr, output, 0, (int)outputLen);
                dsrom_free_buffer(outputPtr, outputLen);

                return output;
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
            }
        }

        /// <summary>
        /// Converts a result code to an error message.
        /// </summary>
        private static string GetErrorMessage(int result)
        {
            switch (result)
            {
                case DSROM_OK:
                    return "Success";
                case DSROM_ERR_INVALID_INPUT:
                    return "Invalid input (null pointer or invalid path)";
                case DSROM_ERR_IO:
                    return "I/O error (file not found or permission denied)";
                case DSROM_ERR_PARSE:
                    return "Parse error (invalid ROM format or corrupted data)";
                case DSROM_ERR_BLOWFISH_NEEDED:
                    return "ROM is encrypted, ARM7 BIOS required";
                case DSROM_ERR_UNKNOWN:
                    return "Unknown error occurred";
                default:
                    return $"Unknown error (code {result})";
            }
        }
    }
}
