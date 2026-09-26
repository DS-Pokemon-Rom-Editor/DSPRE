using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>How often wild Pokémon hold their common or rare item, normally and with a Compound Eyes lead.</summary>
    public class WildHeldItemOddsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private WildHeldItemOdds _odds;
        private byte[] _saved;

        public WildHeldItemOddsViewModel() { }

        public WildHeldItemOddsViewModel(bool load) { if (load) Load(); }

        private void Load()
        {
            _odds = WildHeldItemOdds.Load();
            _saved = _odds.ToBytes();
            RaiseAll();
        }

        private int Get(Func<WildHeldItemOdds, int> read) => _odds == null ? 0 : read(_odds);

        private void Change(Action<WildHeldItemOdds> change)
        {
            if (_odds == null) return;
            change(_odds);
            RaiseAll();
        }

        // Each row edits "none" and "common"; "rare" is whatever is left of 100.
        public decimal NormalNone
        {
            get => Get(o => o.Normal.NonePercent);
            set => Change(o => { int common = o.Normal.CommonPercent; o.Normal.NoneBelow = Clamp(value); o.Normal.RareFrom = Math.Min(100, o.Normal.NoneBelow + common); });
        }
        public decimal NormalCommon
        {
            get => Get(o => o.Normal.CommonPercent);
            set => Change(o => o.Normal.RareFrom = Math.Min(100, o.Normal.NoneBelow + Clamp(value)));
        }
        public string NormalRare => $"{Get(o => o.Normal.RarePercent)}%";

        public decimal EyesNone
        {
            get => Get(o => o.CompoundEyes.NonePercent);
            set => Change(o => { int common = o.CompoundEyes.CommonPercent; o.CompoundEyes.NoneBelow = Clamp(value); o.CompoundEyes.RareFrom = Math.Min(100, o.CompoundEyes.NoneBelow + common); });
        }
        public decimal EyesCommon
        {
            get => Get(o => o.CompoundEyes.CommonPercent);
            set => Change(o => o.CompoundEyes.RareFrom = Math.Min(100, o.CompoundEyes.NoneBelow + Clamp(value)));
        }
        public string EyesRare => $"{Get(o => o.CompoundEyes.RarePercent)}%";

        private static int Clamp(decimal v) => (int)Math.Clamp(v, 0, 100);

        public bool HasUnsavedChanges => _odds != null && !_odds.ToBytes().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Wild held item odds";

        private string _status = "";
        public string Status { get => _status; private set { _status = value; Raise(); } }

        private void RaiseAll()
        {
            foreach (var n in new[] { nameof(NormalNone), nameof(NormalCommon), nameof(NormalRare),
                                      nameof(EyesNone), nameof(EyesCommon), nameof(EyesRare), nameof(HasUnsavedChanges) })
                Raise(n);
        }

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_odds == null) return true;
            if (_odds.Problem() is string problem) { await DialogHelper.ShowError(problem, "Wild Held Items"); return false; }
            try { _odds.Save(); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError("The odds were not saved:\n" + e.Message, "Wild Held Items");
                return false;
            }
            _saved = _odds.ToBytes();
            Raise(nameof(HasUnsavedChanges));
            Status = "Saved.";
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_saved == null) return;
            _odds = new WildHeldItemOdds(_saved);
            RaiseAll();
        }
    }
}
