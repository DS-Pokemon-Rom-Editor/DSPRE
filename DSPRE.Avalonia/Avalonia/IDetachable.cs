namespace DSPRE.Avalonia
{
    /// <summary>
    /// A view model that listens to app-wide events. The window it is shown in calls <see cref="Detach"/> when it closes
    /// (WindowPlacement.ShowManaged); an editor embedded in another is detached by its owner.
    /// </summary>
    public interface IDetachable
    {
        void Detach();
    }
}
