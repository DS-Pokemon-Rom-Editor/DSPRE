using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Advanced map-header search (field / operator / value) — extracted from the WinForms
    /// <c>HeaderSearch</c> form so both shells share the same query logic. Core, UI-free.
    /// </summary>
    public static class HeaderSearchEngine
    {
        public static readonly Dictionary<MapHeader.SearchableFields, string> SearchableFields = new Dictionary<MapHeader.SearchableFields, string>()
        {
            [MapHeader.SearchableFields.AreaDataID] = "Area Data (ID)",
            [MapHeader.SearchableFields.CameraAngleID] = "Camera Angle (ID)",
            [MapHeader.SearchableFields.EventFileID] = "Event File (ID)",
            [MapHeader.SearchableFields.InternalName] = "Internal Name",
            [MapHeader.SearchableFields.LevelScriptID] = "Level Script (ID)",
            [MapHeader.SearchableFields.MatrixID] = "Matrix (ID)",
            [MapHeader.SearchableFields.MusicDayID] = "Music Day (ID)",
            [MapHeader.SearchableFields.MusicNightID] = "Music Night (ID)",
            [MapHeader.SearchableFields.ScriptFileID] = "Script File (ID)",
            [MapHeader.SearchableFields.TextArchiveID] = "Text Archive (ID)",
            [MapHeader.SearchableFields.WeatherID] = "Weather (ID)"
        };

        public enum NumOperators : byte
        {
            //Order matters!
            Equal,
            Different,
            Less,
            Greater,
            LessOrEqual,
            GreaterOrEqual
        };

        public enum TextOperators : byte
        {
            //Order matters!
            Contains,
            DoesNotContain,
            IsExactly,
            IsNot
        };

        public static readonly Dictionary<NumOperators, string> NumOperatorNames = new Dictionary<NumOperators, string>()
        {
            //Order matters!
            [NumOperators.Equal] = "Equals",
            [NumOperators.Different] = "Is Different than",
            [NumOperators.Less] = "Is Less than",
            [NumOperators.Greater] = "Is Greater than",
            [NumOperators.LessOrEqual] = "Is Less than or Equal to",
            [NumOperators.GreaterOrEqual] = "Is Greater than or Equal to",
        };

        public static readonly Dictionary<TextOperators, string> TextOperatorNames = new Dictionary<TextOperators, string>()
        {
            //Order matters!
            [TextOperators.Contains] = "Contains",
            [TextOperators.DoesNotContain] = "Does not contain",
            [TextOperators.IsExactly] = "Is Exactly",
            [TextOperators.IsNot] = "Is Not",
        };

        /// <summary>Does this searchable field take numeric operators (vs. text operators)?</summary>
        public static bool IsNumericField(int fieldToSearch) =>
            (MapHeader.SearchableFields)fieldToSearch != MapHeader.SearchableFields.InternalName;

        public static HashSet<string> AdvancedSearch(ushort startID, ushort finalID, List<string> intNames, int fieldToSearch, int oper, string valToSearch)
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
                        switch (oper)
                        {
                            case (int)TextOperators.IsExactly:
                                if (intNames[i].Equals(valToSearch))
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)TextOperators.IsNot:
                                if (!intNames[i].Equals(valToSearch))
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)TextOperators.Contains:
                                if (intNames[i].IndexOf(valToSearch, StringComparison.InvariantCultureIgnoreCase) >= 0)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)TextOperators.DoesNotContain:
                                if (intNames[i].IndexOf(valToSearch, StringComparison.InvariantCultureIgnoreCase) < 0)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            default:
                                AppLogger.Error("Unrecognized operand!!!");
                                break;
                        }
                    }
                    break;
                default:
                    string[] fieldSplit = SearchableFields[(MapHeader.SearchableFields)fieldToSearch].Split();

                    fieldSplit[0] = fieldSplit[0].ToLower();
                    fieldSplit[fieldSplit.Length - 1] = fieldSplit[fieldSplit.Length - 1].Replace("(", "").Replace(")", ""); //Remove ( and ) from string

                    PropertyInfo property = typeof(MapHeader).GetProperty(String.Join("", fieldSplit));
                    ushort numToSearch;

                    try
                    {
                        numToSearch = ushort.Parse(valToSearch);
                    }
                    catch (OverflowException)
                    {
                        AppMessages.Error("Your input exceeds the range of 16-bit integers (" + ushort.MinValue + " - " + ushort.MaxValue + ").", "Overflow Error");
                        return null;
                    }

                    bool dynamicHeaders = RomPatchState.flag_DynamicHeadersPatchApplied || PatchToolboxLogic.CheckFilesDynamicHeadersPatchApplied();

                    for (ushort i = startID; i < finalID; i++)
                    {
                        MapHeader h;
                        if (dynamicHeaders)
                        {
                            h = MapHeader.LoadFromFile(Path.Combine(RomInfo.gameDirs[DirNames.dynamicHeaders].unpackedDir, i.ToString("D4")), i, 0);
                        }
                        else
                        {
                            h = MapHeader.LoadFromARM9(i);
                        }

                        int headerField = int.Parse(property.GetValue(h, null).ToString());

                        switch (oper)
                        {
                            case (int)NumOperators.Less:
                                if (headerField < numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.Equal:
                                if (headerField == numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.Greater:
                                if (headerField > numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.LessOrEqual:
                                if (headerField <= numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.GreaterOrEqual:
                                if (headerField >= numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
                                }
                                break;
                            case (int)NumOperators.Different:
                                if (headerField != numToSearch)
                                {
                                    result.Add(i.ToString("D3") + MapHeader.nameSeparator + intNames[i]);
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
