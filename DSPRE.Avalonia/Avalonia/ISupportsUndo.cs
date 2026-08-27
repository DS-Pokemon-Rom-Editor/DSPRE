namespace DSPRE.Avalonia
{
    /// <summary>
    /// An editor view-model that supports multi-level undo/redo. <see cref="EditorWindowChrome"/> wires
    /// Ctrl+Z / Ctrl+Y to this when present, and toolbars bind ↶ / ↷ buttons to <see cref="CanUndo"/> /
    /// <see cref="CanRedo"/>. Implementations typically back this with an <see cref="UndoHistory{T}"/> of
    /// state snapshots (e.g. a file's <c>ToByteArray()</c>).
    /// </summary>
    public interface ISupportsUndo
    {
        bool CanUndo { get; }
        bool CanRedo { get; }
        void Undo();
        void Redo();
    }
}
