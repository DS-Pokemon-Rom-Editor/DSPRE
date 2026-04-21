namespace DSPRE
{
    /// <summary>
    /// Interface for editors that depend on PokeDatabase data and need to reload
    /// when custom databases are applied
    /// </summary>
    public interface IPokedatabaseDependent
    {
        /// <summary>
        /// Called when PokeDatabase data has been updated to refresh UI elements
        /// </summary>
        void ReloadPokeDatabase();
    }
}
