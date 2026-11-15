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
    public class ScriptFileStats
    {
        public int ID { get; set; }
        public int Total { get; set; }
        public int Scripts { get; set; }
        public int Functions { get; set; }
        public int Actions { get; set; }

        /// <summary>Set for hg-engine files. The counts describe the last build.</summary>
        public string Source { get; set; } = "";
    }

    public class LevelScriptFileStats
    {
        public int ID { get; set; }
        public int Total { get; set; }
        public int MapChange { get; set; }
        public int ScreenReset { get; set; }
        public int LoadGame { get; set; }
        public int VariableValue { get; set; }
    }

    public class VariableUsageResult
    {
        public string FileType { get; set; }
        public int FileID { get; set; }
        public int UsageCount { get; set; }
    }

    public class FlagUsageResult
    {
        public string FileType { get; set; }
        public int FileID { get; set; }
        public string Details { get; set; }
        public int UsageCount { get; set; }
        public int EventIndex { get; set; }
    }

    public class ScriptFileReferenceResult
    {
        public string ReferenceType { get; set; }
        public int ReferenceID { get; set; }
        public string Field { get; set; }
    }

    public class ScriptIdUsageResult
    {
        public int EventFileID { get; set; }
        public string EventType { get; set; }
        public int EventIndex { get; set; }
        public string Details { get; set; }
    }

    public class HeaderWarpResult
    {
        public int EventFileID { get; set; }
        public int WarpIndex { get; set; }
        public string Position { get; set; }
        public int Anchor { get; set; }
    }

    public class HeaderOutgoingWarpResult
    {
        public int WarpIndex { get; set; }
        public string Position { get; set; }
        public int DestHeader { get; set; }
        public int DestAnchor { get; set; }
    }

    public class HeaderProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class OwEntryUsageResult
    {
        public int EventFileID { get; set; }
        public int OwIndex { get; set; }
        public int OwID { get; set; }
        public string Position { get; set; }
        public int Movement { get; set; }
        public int ScriptNumber { get; set; }
    }

    public class TrainerUsageResult
    {
        /// <summary>Event file ID, script file ID, or -1 when the row has no file.</summary>
        public int SourceId { get; set; }
        public string Type { get; set; }
        public int Index { get; set; }
        public string Details { get; set; }
        /// <summary>A script reference inside a function, which has no script number to land on.</summary>
        public bool InFunction { get; set; }

        internal static TrainerUsageResult From(TrainerReference reference) => new TrainerUsageResult
        {
            SourceId = reference.SourceId,
            Type = reference.InFunction ? reference.Kind + " (function)" : reference.Kind,
            Index = reference.Index,
            Details = reference.Location,
            InFunction = reference.InFunction,
        };
    }
}
