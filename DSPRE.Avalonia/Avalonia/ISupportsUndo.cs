namespace DSPRE.Avalonia
{
    /// <summary>An editor view model with undo and redo; <see cref="EditorWindowChrome"/> wires Ctrl+Z and Ctrl+Y to it.</summary>
    public interface ISupportsUndo
    {
        bool CanUndo { get; }
        bool CanRedo { get; }
        void Undo();
        void Redo();
    }
}
