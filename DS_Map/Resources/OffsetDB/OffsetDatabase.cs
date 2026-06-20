using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Resources.OffsetDB
{
    public class OffsetDatabase
    {
        private Dictionary<string, Anchor> anchors;

        // Primary access method - hides all the complexity
        public uint GetOffset(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.GetOffset(gameLanguage, gameVersion);
        }

        public int GetLength(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.Length;
        }

        // For when you need the full anchor (e.g., to know the source)
        public Anchor GetAnchor(string anchorName)
        {
            if (!anchors.TryGetValue(anchorName, out var anchor))
                throw new KeyNotFoundException($"Anchor '{anchorName}' not found");
            return anchor;
        }

        // For reading data directly
        public byte[] ReadAnchorData(string anchorName, RomInfo romInfo)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.ReadData(romInfo);
        }
    }

    public class Anchor
    {
        public string Name { get; set; }
        public AnchorSource Source { get; set; }
        public int Length { get; set; }
        public DecompReference DecompRef { get; set; }

        // The nested structure: Language -> (Offset OR Version -> Offset)
        public Dictionary<GameLanguages, object> Offsets { get; set; }

        // Smart resolution: language with version fallback, then English fallback
        public uint GetOffset(GameLanguages language, GameVersions version)
        {
            // Try exact language match
            if (Offsets.TryGetValue(language, out var value))
            {
                // Check if it's a version-split structure
                if (value is Dictionary<GameVersions, uint> versionMap)
                {
                    if (versionMap.TryGetValue(version, out var versionOffset))
                        return versionOffset;
                }
                else if (value is uint directOffset)
                {
                    return directOffset;
                }
            }

            // Fall back to English
            if (language != GameLanguages.English &&
                Offsets.TryGetValue(GameLanguages.English, out var englishValue))
            {
                if (englishValue is Dictionary<GameVersions, uint> versionMap)
                {
                    if (versionMap.TryGetValue(version, out var versionOffset))
                        return versionOffset;
                }
                else if (englishValue is uint directOffset)
                {
                    return directOffset;
                }
            }

            throw new InvalidOperationException(
                $"No offset found for anchor '{Name}' (language={language}, version={version})");
        }

        public byte[] ReadData(RomInfo romInfo)
        {
            uint offset = GetOffset(RomInfo.gameLanguage, RomInfo.gameVersion);

            if (offset >= RomInfo.synthOverlayLoadAddress && Source.Type != SourceType.SyntheticOverlay)
            {
                Source.Type = SourceType.SyntheticOverlay;  // Auto-detect synthetic overlay if offset is in that range but source isn't set to it
            }

            switch (Source.Type)
            {
                case SourceType.ARM9:
                    if (offset >= ARM9.address)
                    {
                        offset -= ARM9.address; // Convert to file offset
                    }
                    return ARM9.ReadBytes(offset, Length);
                case SourceType.Overlay:
                    if (!Source.OverlayNumber.HasValue)
                        throw new InvalidOperationException($"Overlay number must be specified for overlay source in anchor '{Name}'");
                    uint ramAddress = OverlayUtils.OverlayTable.GetRAMAddress(Source.OverlayNumber.Value);
                    if (offset >= ramAddress)
                    {
                        offset -= ramAddress;   // Convert to overlay file offset
                    }
                    return OverlayUtils.ReadBytes(Source.OverlayNumber.Value, offset, Length);
                case SourceType.SyntheticOverlay:
                    if (offset >= RomInfo.synthOverlayLoadAddress)
                    {
                        offset -= RomInfo.synthOverlayLoadAddress; // Convert to file offset
                    }
                    using (DSUtils.EasyReader ovReader = new DSUtils.EasyReader(Filesystem.expArmPath, offset))
                    {
                        return ovReader.ReadBytes(Length);
                    }
                default:
                    throw new InvalidOperationException($"Unknown source type: {Source.Type}");
            }
        }
    }

    public class AnchorSource
    {
        public SourceType Type { get; set; }
        public int? OverlayNumber { get; set; }  // Only for overlay type
    }

    public enum SourceType
    {
        ARM9,
        Overlay,
        SyntheticOverlay
    }

    public class DecompReference
    {
        public string Symbol { get; set; }
        public int Offset { get; set; }
    }
}
