using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Kind = DSPRE.Avalonia.Data.GraphicAssets.Kind;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// New copies of an art set: the set's files duplicated at the end of the encounter archive, so one intro can be
    /// painted without changing every other intro that shares the art. The copies wait in the editor and join the
    /// archive on Save, in order, so the archive never has a gap.
    /// </summary>
    public partial class VsIntroEditorViewModel
    {
        private readonly SortedDictionary<int, byte[]> _newArt = new();
        private int _artCount;

        private bool HasNewArt => _newArt.Count > 0;

        /// <summary>A member's bytes, the pending copy's where there is one.</summary>
        private byte[] ArtBytes(int member) =>
            _newArt.TryGetValue(member, out byte[] pending) ? (byte[])pending.Clone() : _art?.Get(member);

        private bool IsNewArt(IEnumerable<int> members) => members != null && members.Any(_newArt.ContainsKey);

        private string WhyNoNewArt()
        {
            if (IsHGE) return "hg-engine builds this archive from its source, so add the art there.";
            if (_art == null || !_art.Available) return "The encounter archive isn't there to copy into.";
            return null;
        }

        /// <summary>
        /// Copies <paramref name="set"/> to the end of the archive and returns the copy's members in the same order.
        /// A set whose files sit together keeps their order, so the copy is found by the same layout as the stock sets.
        /// </summary>
        private int[] CopyArtSet(int[] set, int limit)
        {
            if (set == null || set.Length == 0 || set.Any(m => m < 0 || m >= _kinds.Length)) return null;
            int first = _artCount + _newArt.Count, low = set.Min();
            bool together = set.Select(m => m - low).OrderBy(o => o).SequenceEqual(Enumerable.Range(0, set.Length));
            int[] copy = set.Select((m, k) => first + (together ? m - low : k)).ToArray();
            if (copy.Max() > limit)
            {
                StatusText = $"No room: this intro's file numbers stop at {limit}.";
                return null;
            }
            Kind[] kinds = new Kind[Math.Max(_kinds.Length, copy.Max() + 1)];
            Array.Copy(_kinds, kinds, _kinds.Length);
            for (int k = 0; k < set.Length; k++)
            {
                byte[] bytes = ArtBytes(set[k]);
                if (bytes == null) { StatusText = $"File {set[k]} couldn't be read, so it wasn't copied."; return null; }
                _newArt[copy[k]] = bytes;
                kinds[copy[k]] = _kinds[set[k]];
            }
            _kinds = kinds;
            StatusText = $"New copy in files {copy.Min()} to {copy.Max()}, added on Save.";
            return copy;
        }

        // Unused copies are written too: the members after them already carry their numbers.
        private async Task<bool> SaveNewArtAsync()
        {
            if (!HasNewArt) return true;
            try
            {
                foreach (KeyValuePair<int, byte[]> member in _newArt) _art.Put(member.Key, member.Value);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("The new art was not saved, so nothing else was:\n" + e.Message, Title);
                return false;
            }
            _newArt.Clear();
            _art.Invalidate();
            _artCount = _art.Count;
            ArtChoicesChanged();
            return true;
        }

        /// <summary>Lists the sets again, after copies were added, saved or dropped, and shows the current picks.</summary>
        private void ArtChoicesChanged()
        {
            if (IsClassRecords)
            {
                foreach (IntroArtSlot s in ArtSlots) FillSlotChoices(s);
                RaiseRecord();
                ReshowArtPicks();
                return;
            }
            if (_t == null) return;
            RebuildArtChoices();
            if (IsClassIntroPatch) RaiseCiRow(); else RefreshAll();
            ReshowArtPicks();
        }

        private bool _hidePicks;

        // A picker drops a selection whose label changed and the binding won't resend the same index, so each is cleared first.
        private void ReshowArtPicks() => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            string[] picks = { nameof(FaceSetIndex), nameof(BannerSetIndex), nameof(FramePaletteIndex), nameof(CiFaceIndex), nameof(CiBannerIndex), nameof(CiFrameIndex) };
            _suppress = true;
            try
            {
                _hidePicks = true;
                Raise(picks);
                _hidePicks = false;
                Raise(picks);
                foreach (IntroArtSlot slot in ArtSlots) { int at = slot.Index; slot.Show(-1); slot.Show(at); }
            }
            finally { _hidePicks = false; _suppress = false; }
        }, global::Avalonia.Threading.DispatcherPriority.Background);

        private void DiscardNewArt()
        {
            if (!HasNewArt) return;
            _newArt.Clear();
            Array.Resize(ref _kinds, _artCount);
            ArtChoicesChanged();
        }

        /// <summary>Paint and animation open files on disk, which a copy only becomes on Save.</summary>
        private bool RefuseUnsavedArt(IEnumerable<int> members)
        {
            if (!IsNewArt(members)) return false;
            StatusText = "Save first to paint the new copy.";
            return true;
        }

        // ── The pickers' New copy buttons ────────────────────────────────────────────────────────

        public bool CanCopyArt => WhyNoNewArt() == null;

        // Stock gym and executive records keep each file number in a byte; Elite Four records keep a 16-bit base.
        public void NewFaceCopy()
        {
            if (Rec?.FaceMembers() is not int[] m || Refused()) return;
            int[] copy = CopyArtSet(m, Rec.Has(VsIntroTables.RecordField.FaceBase) ? ushort.MaxValue : byte.MaxValue);
            if (copy == null) return;
            RebuildArtChoices();
            Rec.SetFaceMembers(copy);
            Edited();
        }

        public void NewBannerCopy()
        {
            if (Rec?.BannerMembers() is not int[] m || Refused()) return;
            int[] copy = CopyArtSet(m, byte.MaxValue);
            if (copy == null) return;
            RebuildArtChoices();
            Rec.SetBannerMembers(copy);
            Edited();
        }

        public void NewSlotCopy(IntroArtSlot slot)
        {
            if (Record == null || slot == null || Refused()) return;
            int[] copy = CopyArtSet(SetOf(Record, slot), ushort.MaxValue);
            if (copy == null) return;
            for (int i = 0; i < slot.Fields.Length; i++) Record.SetAsset(slot.Fields[i], (ushort)copy[i]);
            foreach (IntroArtSlot s in ArtSlots) FillSlotChoices(s);
            RecordEdited();
        }

        // The class intros patch keeps every file number in a byte.
        public void CiNewPortraitCopy()
        {
            if (!CiPtRecord || Refused()) return;
            int[] copy = CopyArtSet(CiIsGym ? CiRow.GymPortrait : LeagueFace(CiRow.LeaguePortrait), byte.MaxValue);
            if (copy == null) return;
            RebuildArtChoices();
            if (CiIsGym) CiRow.GymPortrait = copy; else CiRow.LeaguePortrait = copy[0];
            CiEdited();
        }

        public void CiNewBannerCopy()
        {
            if (!CiIsGym || IsDp || Refused()) return;
            int[] copy = CopyArtSet(CiRow.GymBanner, byte.MaxValue);
            if (copy == null) return;
            RebuildArtChoices();
            CiRow.GymBanner = copy;
            CiEdited();
        }

        public void CiPaintPortrait()
        {
            if (!CiPtRecord) return;
            int[] m = CiIsGym ? CiRow.GymPortrait : LeagueFace(CiRow.LeaguePortrait);
            if (!RefuseUnsavedArt(m)) Paint(m[1], m[0]);
        }

        public void CiPaintBanner()
        {
            if (!CiIsGym || IsDp) return;
            int[] m = CiRow.GymBanner;
            if (!RefuseUnsavedArt(m)) Paint(m[1], m[0]);
        }

        private bool Refused()
        {
            string why = WhyNoNewArt();
            if (why != null) StatusText = why;
            return why != null;
        }
    }
}
