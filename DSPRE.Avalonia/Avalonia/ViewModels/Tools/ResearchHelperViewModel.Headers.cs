using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.Avalonia;
using DSPRE.Editors.Utils;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    public partial class ResearchHelperViewModel
    {
        public void SearchHeaderInfo()
        {
            int id = HeaderSearchId;
            int headerCount = RomInfo.GetHeaderCount();

            if (id < 0 || id >= headerCount)
            { StatusText = $"Header ID must be between 0 and {headerCount - 1}."; return; }

            try
            {
                MapHeader header = MapHeader.GetMapHeader((ushort)id);
                if (header == null) { StatusText = $"Couldn't load header {id}."; return; }

                HeaderProperties.Clear();
                HeaderProperties.Add(new HeaderProperty { Name = "Header",          Value = header.ID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Script file",      Value = header.ScriptFileID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Level script",     Value = header.LevelScriptID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Event file",       Value = header.EventFileID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Text archive",     Value = header.TextArchiveID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Matrix",           Value = header.MatrixID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Area data",        Value = header.AreaDataID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Camera",     Value = header.CameraAngleID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Day music",        Value = header.MusicDayID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Night music",      Value = header.MusicNightID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Weather",          Value = header.WeatherID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Wild Pokémon",        Value = header.WildPokemon.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Location",  Value = header.LocationSpecifier.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Flags",               Value = $"0x{header.Flags:X2}" });

                IncomingWarps.Clear();
                foreach (EventFile ev in _cachedEventFiles)
                {
                    if (ev.Warps == null) continue;
                    for (int i = 0; i < ev.Warps.Count; i++)
                    {
                        Warp w = ev.Warps[i];
                        if (w.Header == id)
                            IncomingWarps.Add(new HeaderWarpResult
                            {
                                EventFileID = ev.ID,
                                WarpIndex = i,
                                Position = $"({w.XMapPosition}, {w.YMapPosition})",
                                Anchor = w.Anchor
                            });
                    }
                }

                OutgoingWarps.Clear();
                EventFile selfEv = _cachedEventFiles.FirstOrDefault(e => e.ID == header.EventFileID);
                if (selfEv?.Warps != null)
                {
                    for (int i = 0; i < selfEv.Warps.Count; i++)
                    {
                        Warp w = selfEv.Warps[i];
                        OutgoingWarps.Add(new HeaderOutgoingWarpResult
                        {
                            WarpIndex = i,
                            Position = $"({w.XMapPosition}, {w.YMapPosition})",
                            DestHeader = w.Header,
                            DestAnchor = w.Anchor
                        });
                    }
                }

                StatusText = $"Header {id}: {IncomingWarps.Count} incoming, {OutgoingWarps.Count} outgoing warps";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        public void NavigateToHeader(int id)
        {
            HeaderSearchId = id;
            SearchHeaderInfo();
        }
    }
}
