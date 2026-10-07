using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Which map headers link a given file, for the editors that remove the last file of an archive: a header
    /// still pointing at a removed file would load garbage, so removal is refused while one does.
    /// </summary>
    public static class HeaderLinks
    {
        public enum Kind { Script, LevelScript, Event, Text, Matrix, AreaData, Encounters }

        public static List<(ushort Header, string Name)> HeadersLinking(Kind kind, int fileId)
        {
            List<(ushort Header, string Name)> uses = new List<(ushort Header, string Name)>();
            int count;
            try { count = GetHeaderCount(); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { return uses; }
            for (ushort id = 0; id < count; id++)
            {
                MapHeader header;
                try { header = MapHeader.GetMapHeader(id); }
                catch (Exception e) when (e is System.IO.IOException || e is ArgumentException || e is IndexOutOfRangeException) { continue; }
                if (header == null) continue;
                int linked = kind switch
                {
                    Kind.Script => header.scriptFileID,
                    Kind.LevelScript => header.levelScriptID,
                    Kind.Event => header.eventFileID,
                    Kind.Text => header.textArchiveID,
                    Kind.Matrix => header.matrixID,
                    Kind.AreaData => header.areaDataID,
                    Kind.Encounters => header.wildPokemon,
                    _ => -1,
                };
                if (linked == fileId) uses.Add((id, InternalName(id)));
            }
            return uses;
        }

        /// <summary>The headers as a short list for a message, one per line.</summary>
        public static string Describe(IReadOnlyList<(ushort Header, string Name)> uses, int shown = 12)
        {
            string list = string.Join(Environment.NewLine, uses.Take(shown).Select(u => $"- header {u.Header} ({u.Name})"));
            return uses.Count > shown ? list + Environment.NewLine + $"- and {uses.Count - shown} more" : list;
        }

        private static string InternalName(ushort id)
        {
            try
            {
                byte[] raw = DSUtils.ReadFromFile(internalNamesPath, (long)id * internalNameLength, internalNameLength);
                return Encoding.ASCII.GetString(raw).TrimEnd('\0', ' ');
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { return ""; }
        }
    }
}
