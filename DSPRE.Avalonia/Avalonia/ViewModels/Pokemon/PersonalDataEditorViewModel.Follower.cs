using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        // Size is in the overlay 1 object table, height and motion in a/1/4/1. One set per model.
        internal sealed record FollowerStaged(ushort Bits, bool TooTall, byte Motion);

        private string _followerWhyNot;
        private HgssFollowers.Model[] _followerModels = Array.Empty<HgssFollowers.Model>();
        private FollowerStaged[] _followers, _followersLoaded;

        public bool ShowRetailFollower => RomInfo.GameFamily == GameFamilies.HGSS && !HgEngineProject.IsActive;
        public bool RetailFollowerEditable => ShowRetailFollower && _followerWhyNot == null && _followerModels.Length > 0;
        public string RetailFollowerNote => !ShowRetailFollower ? "" : _followerWhyNot ?? (_followerModels.Length == 0 ? "This Pokémon has no follower." : "");
        public bool HasRetailFollowerNote => RetailFollowerNote.Length > 0;

        /// <summary>The overworld table entry of the follower model being edited, or -1.</summary>
        public int FollowerSpriteEntry => RetailFollowerEditable && _followerModelIndex < _followerModels.Length
            ? HgssFollowers.FirstSprite + _followerModels[_followerModelIndex].Index : -1;

        public ObservableCollection<string> FollowerModelNames { get; } = new();
        public bool HasFollowerModelChoice => FollowerModelNames.Count > 1;
        public string[] FollowerSizeOptions { get; } = { "Small", "Small without a shadow", "Large" };
        public string[] FollowerMotionOptions { get; } = { "No", "Hovers", "Flies", "Unknown (01)" };

        // The game reads motion from the base or form model, never female, and too tall from the base.
        private bool OnBaseModel => _followerModelIndex == 0;
        private bool OnFemaleModel => _followerModelIndex < _followerModels.Length && _followerModels[_followerModelIndex].Label == HgssFollowers.FemaleLabel;
        public bool FollowerTooTallEditable => OnBaseModel;
        public bool FollowerMotionEditable => !OnFemaleModel;
        public string FollowerTooTallTip => OnBaseModel ? "Maps that keep tall followers out" : BaseModelUsed;
        public string FollowerMotionTip => OnFemaleModel ? BaseModelUsed : null;
        private const string BaseModelUsed = "The game uses the base model's value";

        private int _followerModelIndex;
        public int FollowerModelIndex
        {
            get => _followerModelIndex;
            set { if (value >= 0 && value < _followerModels.Length && Set(ref _followerModelIndex, value)) ShowFollower(); }
        }

        private FollowerStaged Staged => _followers != null && _followerModelIndex < _followers.Length ? _followers[_followerModelIndex] : null;

        private void Stage(FollowerStaged next)
        {
            if (_loading || Staged == null || next == Staged) return;
            _followers[_followerModelIndex] = next;
            ShowFollower();
            SetDirty();
        }

        public int FollowerSizeIndex
        {
            get => Staged == null ? -1 : HgssFollowers.SizeOf(Staged.Bits) switch
            {
                HgssFollowers.Size.Small => 0, HgssFollowers.Size.SmallNoShadow => 1, HgssFollowers.Size.Large => 2, _ => -1,
            };
            set
            {
                if (Staged == null || value < 0 || value > 2) return;
                HgssFollowers.Size size = value == 0 ? HgssFollowers.Size.Small : value == 1 ? HgssFollowers.Size.SmallNoShadow : HgssFollowers.Size.Large;
                // Too tall also picks the large Hall of Fame art, so it follows the base model's size.
                bool tooTall = OnBaseModel ? size == HgssFollowers.Size.Large : Staged.TooTall;
                Stage(Staged with { Bits = HgssFollowers.BitsOf(size, Staged.Bits), TooTall = tooTall });
            }
        }

        public string FollowerRawBits
        {
            get => Staged == null ? "" : Staged.Bits.ToString("X4");
            set
            {
                if (Staged == null || !ushort.TryParse(value?.Trim(), System.Globalization.NumberStyles.HexNumber, null, out ushort bits)) return;
                Stage(Staged with { Bits = bits });
            }
        }

        public bool FollowerTooTall
        {
            get => Staged?.TooTall ?? false;
            set { if (Staged != null && FollowerTooTallEditable) Stage(Staged with { TooTall = value }); }
        }

        public int FollowerMotionIndex
        {
            get => Staged?.Motion switch { HgssFollowers.Walks => 0, HgssFollowers.Hovers => 1, HgssFollowers.Flies => 2, HgssFollowers.Unknown01 => 3, _ => -1 };
            set
            {
                if (Staged == null || !FollowerMotionEditable || value < 0 || value > 3) return;
                Stage(Staged with { Motion = value switch { 0 => HgssFollowers.Walks, 1 => HgssFollowers.Hovers, 2 => HgssFollowers.Flies, _ => HgssFollowers.Unknown01 } });
            }
        }

        public string FollowerArtWarning
        {
            get
            {
                if (Staged == null || _followerModelIndex >= _followerModels.Length) return "";
                HgssFollowers.Model m = _followerModels[_followerModelIndex];
                bool large = HgssFollowers.SizeOf(Staged.Bits) == HgssFollowers.Size.Large;
                return large && m.TextureWidth > 0 && m.TextureWidth < 64 ? $"Its art is {m.TextureWidth}x{m.TextureHeight}; a large follower needs 64x64 art." : "";
            }
        }
        public bool HasFollowerArtWarning => FollowerArtWarning.Length > 0;

        private void ShowFollower()
        {
            foreach (string name in new[] { nameof(FollowerModelIndex), nameof(FollowerSizeIndex), nameof(FollowerRawBits), nameof(FollowerTooTall),
                                         nameof(FollowerMotionIndex), nameof(FollowerArtWarning), nameof(HasFollowerArtWarning),
                                         nameof(RetailFollowerEditable), nameof(RetailFollowerNote), nameof(HasRetailFollowerNote),
                                         nameof(HasFollowerModelChoice), nameof(FollowerTooTallEditable), nameof(FollowerMotionEditable),
                                         nameof(FollowerTooTallTip), nameof(FollowerMotionTip), nameof(FollowerSpriteEntry) })
                OnPropertyChanged(name);
        }

        private void LoadRetailFollower()
        {
            _followerModels = Array.Empty<HgssFollowers.Model>();
            _followers = _followersLoaded = null;
            FollowerModelNames.Clear();
            _followerModelIndex = 0;
            if (ShowRetailFollower)
            {
                try
                {
                    _followerWhyNot = HgssFollowers.WhyNot();
                    // The art's size and the preview are read from the unpacked overworld textures.
                    if (_followerWhyNot == null) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.OWSprites });
                    if (_followerWhyNot == null)
                        _followerModels = HgssFollowers.ModelsOf(_currentId)
                            .Select(m => HgssFollowers.Read(m.index, m.label)).Where(m => m != null).ToArray();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    _followerWhyNot = e.Message;
                }
                foreach (HgssFollowers.Model m in _followerModels) FollowerModelNames.Add(m.Label);
                _followers = _followerModels.Select(m => new FollowerStaged(m.Bits, m.TooTall, m.Motion)).ToArray();
                _followersLoaded = (FollowerStaged[])_followers.Clone();
            }
            ShowFollower();
        }

        private string SaveRetailFollower()
        {
            if (_followers == null || _followersLoaded == null) return null;
            try
            {
                for (int i = 0; i < _followers.Length; i++)
                {
                    if (_followers[i] == _followersLoaded[i]) continue;
                    HgssFollowers.Model m = _followerModels[i];
                    m.Bits = _followers[i].Bits; m.TooTall = _followers[i].TooTall; m.Motion = _followers[i].Motion;
                    HgssFollowers.Write(m);
                    _followersLoaded[i] = _followers[i];
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                return e.Message;
            }
            return null;
        }
    }
}
