using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Header search by field, operator and value.
    /// </summary>
    public static class HeaderSearchEngine
    {
        public static readonly Dictionary<MapHeader.SearchableFields, string> SearchableFields = new Dictionary<MapHeader.SearchableFields, string>()
        {
            [MapHeader.SearchableFields.AreaDataID] = "Area data",
            [MapHeader.SearchableFields.CameraAngleID] = "Camera angle",
            [MapHeader.SearchableFields.EventFileID] = "Event file",
            [MapHeader.SearchableFields.InternalName] = "Internal name",
            [MapHeader.SearchableFields.LevelScriptID] = "Level script",
            [MapHeader.SearchableFields.MatrixID] = "Matrix",
            [MapHeader.SearchableFields.MusicDayID] = "Music (day)",
            [MapHeader.SearchableFields.MusicNightID] = "Music (night)",
            [MapHeader.SearchableFields.ScriptFileID] = "Script file",
            [MapHeader.SearchableFields.TextArchiveID] = "Text archive",
            [MapHeader.SearchableFields.WeatherID] = "Weather"
        };

        public enum NumOperators : byte
        {
            // In the order the operator list shows them.
            Equal,
            Different,
            Less,
            Greater,
            LessOrEqual,
            GreaterOrEqual
        };

        public enum TextOperators : byte
        {
            // In the order the operator list shows them.
            Contains,
            DoesNotContain,
            IsExactly,
            IsNot
        };

        public static readonly Dictionary<NumOperators, string> NumOperatorNames = new Dictionary<NumOperators, string>()
        {
            // In the order the operator list shows them.
            [NumOperators.Equal] = "Equals",
            [NumOperators.Different] = "Isn't",
            [NumOperators.Less] = "Is less than",
            [NumOperators.Greater] = "Is more than",
            [NumOperators.LessOrEqual] = "Is at most",
            [NumOperators.GreaterOrEqual] = "Is at least",
        };

        public static readonly Dictionary<TextOperators, string> TextOperatorNames = new Dictionary<TextOperators, string>()
        {
            // In the order the operator list shows them.
            [TextOperators.Contains] = "Contains",
            [TextOperators.DoesNotContain] = "Doesn't contain",
            [TextOperators.IsExactly] = "Is exactly",
            [TextOperators.IsNot] = "Isn't",
        };

        /// <summary>Does this searchable field take numeric operators (vs. text operators)?</summary>
        public static bool IsNumericField(int fieldToSearch) =>
            (MapHeader.SearchableFields)fieldToSearch != MapHeader.SearchableFields.InternalName;

        public static HashSet<string> AdvancedSearch(ushort startID, ushort finalID, List<string> intNames, int fieldToSearch, int oper, string valToSearch, CancellationToken cancellationToken = default)
        {
            if (fieldToSearch < 0 || oper < 0 || valToSearch == "")
            {
                return null;
            }

            HashSet<string> result = new HashSet<string>();

            switch (fieldToSearch)
            {
                case (int)MapHeader.SearchableFields.InternalName:
                    for (ushort i = startID; i < finalID; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        switch (oper)
                        {
                            case (int)TextOperators.IsExactly:
                                if (intNames[i].Equals(valToSearch))
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)TextOperators.IsNot:
                                if (!intNames[i].Equals(valToSearch))
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)TextOperators.Contains:
                                if (SearchMatch.Contains(intNames[i], valToSearch))
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)TextOperators.DoesNotContain:
                                if (!SearchMatch.Contains(intNames[i], valToSearch))
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            default:
                                AppLogger.Error("Unrecognized operand!!!");
                                break;
                        }
                    }
                    break;
                default:
                    // Each searchable field is named after the MapHeader property it reads.
                    PropertyInfo property = typeof(MapHeader).GetProperty(((MapHeader.SearchableFields)fieldToSearch).ToString());
                    ushort numToSearch;

                    try
                    {
                        numToSearch = ushort.Parse(valToSearch);
                    }
                    catch (OverflowException)
                    {
                        AppMessages.Error("Enter a number from " + ushort.MinValue + " to " + ushort.MaxValue + ".", "Number too large");
                        return null;
                    }

                    bool dynamicHeaders = ROMFiles.MapHeader.UsesDynamicHeaders;
                    byte[] arm9Headers = null;
                    if (!dynamicHeaders && finalID > startID)
                    {
                        long headerOffset = RomInfo.HeaderTableOffset + (long)MapHeader.Length * startID;
                        long headerBytes = (long)MapHeader.Length * (finalID - startID);
                        arm9Headers = DSUtils.ReadFromFile(RomInfo.Arm9Path, headerOffset, headerBytes);
                    }

                    for (ushort i = startID; i < finalID; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        MapHeader h;
                        if (dynamicHeaders)
                        {
                            h = MapHeader.LoadFromFile(Path.Combine(RomInfo.GameDirs[DirNames.DynamicHeaders].unpackedDir, i.ToString("D4")), i, 0);
                        }
                        else
                        {
                            int headerOffset = (i - startID) * MapHeader.Length;
                            if (arm9Headers != null && headerOffset + MapHeader.Length <= arm9Headers.Length)
                            {
                                byte[] headerData = new byte[MapHeader.Length];
                                Buffer.BlockCopy(arm9Headers, headerOffset, headerData, 0, MapHeader.Length);
                                h = MapHeader.LoadFromByteArray(headerData, i);
                            }
                            else
                            {
                                h = MapHeader.LoadFromARM9(i);
                            }
                        }

                        int headerField = int.Parse(property.GetValue(h, null).ToString());

                        switch (oper)
                        {
                            case (int)NumOperators.Less:
                                if (headerField < numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.Equal:
                                if (headerField == numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.Greater:
                                if (headerField > numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.LessOrEqual:
                                if (headerField <= numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.GreaterOrEqual:
                                if (headerField >= numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.Different:
                                if (headerField != numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.NameSeparator + intNames[i]);
                                }
                                break;
                            default:
                                AppLogger.Error("Unrecognized operand!!!");
                                break;
                        }
                    }
                    break;
            }
            return result;
        }
    }
}
