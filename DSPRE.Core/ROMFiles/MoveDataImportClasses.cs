using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.MoveData;

namespace DSPRE
{
    // ── Shared import data classes (moved from MoveDataEditor inner scope) ────

    public class MoveImportError
    {
        public int    LineNumber { get; }
        public string Message    { get; }
        public MoveImportError(int lineNumber, string message) { LineNumber = lineNumber; Message = message; }
        public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
    }

    public class MoveImportWarning
    {
        public int    LineNumber { get; }
        public string Message    { get; }
        public MoveImportWarning(int lineNumber, string message) { LineNumber = lineNumber; Message = message; }
        public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
    }

    public class MoveNameMismatch
    {
        public int    MoveId     { get; }
        public string RomName    { get; }
        public string CsvName    { get; }
        public int    LineNumber { get; }
        public MoveNameMismatch(int moveId, string romName, string csvName, int lineNumber)
        { MoveId = moveId; RomName = romName; CsvName = csvName; LineNumber = lineNumber; }
        public override string ToString() => $"Move ID {MoveId}: ROM has '{RomName}', CSV has '{CsvName}'";
    }

    public class MoveDataImportEntry
    {
        public int          MoveID                { get; set; }
        public string       MoveName              { get; set; }
        public PokemonType  MoveType              { get; set; }
        public MoveSplit    Split                 { get; set; }
        public byte         Power                 { get; set; }
        public byte         Accuracy              { get; set; }
        public sbyte        Priority              { get; set; }
        public byte         SideEffectProbability { get; set; }
        public byte         PP                    { get; set; }
        public ushort       Range                 { get; set; }
    }

    public class MoveRowValidationResult
    {
        public int                   LineNumber      { get; set; }
        public MoveDataImportEntry   Entry           { get; set; }
        public List<MoveImportError>   Errors        { get; set; } = new List<MoveImportError>();
        public List<MoveImportWarning> Warnings      { get; set; } = new List<MoveImportWarning>();
        public List<MoveNameMismatch>  NameMismatches{ get; set; } = new List<MoveNameMismatch>();
        public bool IsValid { get; set; }
    }

    public class MoveDataImportResult
    {
        public List<MoveDataImportEntry> ValidEntries  { get; set; } = new List<MoveDataImportEntry>();
        public List<MoveImportError>     Errors        { get; set; } = new List<MoveImportError>();
        public List<MoveImportWarning>   Warnings      { get; set; } = new List<MoveImportWarning>();
        public List<MoveNameMismatch>    NameMismatches{ get; set; } = new List<MoveNameMismatch>();
        public int  TotalRowsRead { get; set; }

        public bool HasErrors         => Errors.Count > 0;
        public bool HasWarnings       => Warnings.Count > 0;
        public bool HasNameMismatches => NameMismatches.Count > 0;
        public int  ValidCount        => ValidEntries.Count;
        public int  ErrorCount        => Errors.Count;

        public List<MoveNameMismatch> UniqueNameMismatches =>
            NameMismatches.GroupBy(m => m.MoveId).Select(g => g.First()).ToList();
    }
}
