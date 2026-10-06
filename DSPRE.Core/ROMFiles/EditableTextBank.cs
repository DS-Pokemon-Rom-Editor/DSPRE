using System.Collections.Generic;
using DSPRE.HgEngine;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// One text archive read and written where the project keeps it: the ROM's copy, or the hg-engine
    /// source file that builds it. An archive hg-engine generates from other data is read-only.
    /// </summary>
    public sealed class EditableTextBank
    {
        public int Id { get; }
        public List<string> Messages { get; }
        /// <summary>Why it can't be saved here, or null.</summary>
        public string ReadOnlyReason { get; }

        private readonly HgEngineOwnedFile _source;

        public EditableTextBank(int id, string readOnlyReason = null)
        {
            Id = id;
            TextArchive archive = new TextArchive(id);
            Messages = archive.messages;
            HgEngineOwnedFile owned = HgEngineOwnedFiles.Get(HgEngineOwnedFiles.ArchiveOf(RomInfo.DirNames.textArchives), id);
            if (owned?.Ownership == HgEngineOwnership.Generated)
                readOnlyReason ??= "hg-engine builds this text from its own data.";
            else if (owned?.Ownership == HgEngineOwnership.EditableSource)
            {
                if (HgEngineOwnedFiles.TryReadLines(owned, out List<string> lines, out string error)) { Messages = lines; _source = owned; }
                else readOnlyReason ??= error;
            }
            ReadOnlyReason = readOnlyReason;
        }

        /// <summary>The file a save writes, for messages.</summary>
        public string SourceName => _source?.RelPath;

        /// <summary>Writes the messages back; returns an error, or null.</summary>
        public string Save(object sender = null)
        {
            if (ReadOnlyReason != null) return ReadOnlyReason;
            if (_source != null)
                return HgEngineOwnedFiles.TryWriteLines(_source, Messages, out string error) ? null : error;
            TextArchive archive = new TextArchive(Id);
            archive.messages.Clear();
            archive.messages.AddRange(Messages);
            archive.SaveToExpandedDir(Id, showSuccessMessage: false, sender: sender);
            return null;
        }
    }
}
