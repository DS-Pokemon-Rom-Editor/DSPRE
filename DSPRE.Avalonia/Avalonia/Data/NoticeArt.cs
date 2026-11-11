using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Who stands beside HGSS notices: Shuckle, the Pokémon in <see cref="NoticeArt.FollowerSpecies"/>, or a random one.</summary>
    internal enum NoticeFollowerKind { Shuckle, Specific, Random }

    /// <summary>The open game's art for notices and dialogs: its message frame, the speech bubbles and who stands there.</summary>
    internal static class NoticeArt
    {
        /// <summary>Shuckle, the follower beside HGSS notices unless another is chosen.</summary>
        public const int DefaultSpecies = 213;

        // Each project keeps its own choice in dspre_prefs.json.
        public static NoticeFollowerKind Follower
        {
            get => Enum.TryParse(ProjectPrefs.Get("NoticeFollower", ""), out NoticeFollowerKind kind) && Enum.IsDefined(kind) ? kind : NoticeFollowerKind.Shuckle;
            set => ProjectPrefs.Set("NoticeFollower", value.ToString());
        }

        public static int FollowerSpecies
        {
            get => ProjectPrefs.GetInt("NoticeFollowerSpecies", DefaultSpecies);
            set => ProjectPrefs.SetInt("NoticeFollowerSpecies", value);
        }

        public static int FrameIndex
        {
            get => Math.Clamp(ProjectPrefs.GetInt("NoticeFrame", 0), 0, FieldWindowFrame.FrameCount - 1);
            set => ProjectPrefs.SetInt("NoticeFrame", value);
        }

        public static bool PlayerGirl
        {
            get => ProjectPrefs.GetBool("NoticePlayerGirl", false);
            set => ProjectPrefs.SetBool("NoticePlayerGirl", value);
        }

        /// <summary>Whether this project's notices show an HGSS follower instead of the player.</summary>
        public static bool ShowsFollower => Ready && GameFamily == GameFamilies.HGSS && FollowersReadable();

        // Only the retail follower tables are read, so an hg-engine build or moved tables get the player.
        private static bool FollowersReadable() => _followersReadable ??= HgssFollowers.WhyNot() == null;

        // The speech bubble textures in HGSS's field effect archive, found by name.
        private static readonly Dictionary<SpeechBubble, string> Textures = new()
        {
            [SpeechBubble.Smile] = "fuki_kao4", [SpeechBubble.Note] = "fuki_onpu", [SpeechBubble.Question] = "fuki_hatena",
            [SpeechBubble.Exclamation] = "fuki_bikkuri", [SpeechBubble.Troubled] = "fuki_komari", [SpeechBubble.Heart] = "fuki_heart",
        };

        private static readonly Color FallbackInk = Color.FromRgb(0x40, 0x40, 0x40);
        // Kept only to subscribe when the class is first used.
        private static readonly IDisposable Watch =
            AppEvents.WhenSaved(() => Dispatcher.UIThread.Post(Reset), DirNames.WindowFrames, DirNames.FieldEffectModels, DirNames.Fonts);

        static NoticeArt() => ProjectSession.ClearOnLoad(() => Dispatcher.UIThread.Post(Reset));

        // Notices take the plain look while a project loads, since its folders and tables are changing.
        private static bool Ready => AvaloniaEditorLauncher.IsRomLoaded && !AvaloniaRomLoader.Loading;

        private static bool? _followersReadable;
        private static readonly Dictionary<int, int> SpriteOf = new();
        private static FieldWindowFrame _frame;
        private static bool _frameRead;
        private static Color? _ink;
        private static Dictionary<SpeechBubble, Bitmap[]> _bubbles;

        /// <summary>Clears the art so the next notice reads it again. The old bitmaps aren't disposed, since a notice or dialog still showing may be drawing them.</summary>
        public static void Reset()
        {
            _bubbles = null;
            _frame = null;
            _frameRead = false;
            _ink = null;
            _followersReadable = null;
            SpriteOf.Clear();
        }

        /// <summary>Whether notices can be drawn the game's way: a project is open and its message frame can be read.</summary>
        public static bool Available => Frame() != null;

        private static FieldWindowFrame Frame()
        {
            if (!Ready) return null;
            if (_frameRead) return _frame;
            // Not marked as read while the archive is packed, so notices switch to the game's look once an editor unpacks it.
            if (!ArchiveMembers.IsUnpacked(DirNames.WindowFrames)) return null;
            // A failed read is tried again by the next notice.
            try { _frame = WindowFrames.Load(FrameIndex, ifUnpacked: true); }
            catch (Exception ex) { AppLogger.WarnOnce("NoticeArt.Frame", ex); }
            _frameRead = _frame != null;
            return _frame;
        }

        /// <summary>The message frame round a writing area of the given size in tiles, with its paper colour, at 1x.</summary>
        public static Bitmap FrameBitmap(int tilesWide, int tilesHigh)
        {
            FieldWindowFrame frame = Frame();
            if (frame == null) return null;
            byte[] rgba = frame.Compose(tilesWide, tilesHigh, out int w, out int h, frame.PaperArgb);
            return ImageConverter.FromRgba(rgba, w, h);
        }

        /// <summary>The system font's writing colour, as the game draws message text.</summary>
        public static Color Ink()
        {
            if (!Ready) return FallbackInk;
            if (_ink == null)
            {
                if (!ArchiveMembers.IsUnpacked(DirNames.Fonts)) return FallbackInk;
                uint[] colours = null;
                try { colours = WindowFrames.LoadSystemFontColours(ifUnpacked: true); }
                catch (Exception ex) { AppLogger.WarnOnce("NoticeArt.Ink", ex); }
                _ink = colours != null && colours.Length > 1 ? Color.FromUInt32(colours[1] | 0xFF000000u) : FallbackInk;
            }
            return _ink.Value;
        }

        /// <summary>The bubble's pictures, or null outside HGSS or when it can't be read.</summary>
        public static Bitmap[] Bubble(SpeechBubble bubble)
        {
            if (bubble == SpeechBubble.None || !Ready || GameFamily != GameFamilies.HGSS) return null;
            if (_bubbles == null)
            {
                if (!ArchiveMembers.IsUnpacked(DirNames.FieldEffectModels)) return null;
                _bubbles = ReadBubbles();
            }
            return _bubbles.TryGetValue(bubble, out Bitmap[] pictures) ? pictures : null;
        }

        private static Dictionary<SpeechBubble, Bitmap[]> ReadBubbles()
        {
            Dictionary<SpeechBubble, Bitmap[]> found = new();
            try
            {
                ScriptNarc models = ScriptNarc.IfUnpacked(DirNames.FieldEffectModels);
                for (int i = 0; i < models.Count && found.Count < Textures.Count; i++)
                {
                    byte[] member = models.Get(i);
                    Tex0Block block = member == null ? null : Tex0Block.ReadBtx0(member, out _);
                    string name = block?.Textures.FirstOrDefault()?.Name;
                    if (name == null) continue;
                    // The pictures are named "fuki_kao4.1", "fuki_kao4.2" and so on.
                    string stem = name.TrimEnd('\0', ' ').Split('.')[0];
                    foreach ((SpeechBubble kind, string texture) in Textures)
                    {
                        if (found.ContainsKey(kind) || !stem.Equals(texture, StringComparison.OrdinalIgnoreCase)) continue;
                        found[kind] = OverworldSprites.Pictures(member)
                            .Select(p => ImageConverter.FromRgba(p.Rgba, p.Width, p.Height)).ToArray();
                    }
                }
            }
            catch (Exception ex) { AppLogger.WarnOnce("NoticeArt.ReadBubbles", ex); }
            if (found.Count < Textures.Count)
                AppLogger.Warn($"Notices: {found.Count} of {Textures.Count} speech bubbles found in the field effect archive.");
            return found;
        }

        /// <summary>A random HGSS follower that can be drawn, or 0 frames when none can.</summary>
        public static (ushort Entry, int Frames) RandomFollower()
        {
            for (int tries = 0; tries < 16; tries++)
                if (FollowerOf(1 + Random.Shared.Next(HgssFollowers.SpeciesCount)) is (ushort entry, int frames) && frames > 0) return (entry, frames);
            return (0, 0);
        }

        private static (ushort Entry, int Frames) FollowerOf(int species)
        {
            if (!FollowersReadable()) return (0, 0);
            if (!SpriteOf.TryGetValue(species, out int entry)) SpriteOf[species] = entry = HgssFollowers.SpriteOf(species);
            return entry < 0 ? ((ushort)0, 0) : ((ushort)entry, OverworldSprites.FrameCount((ushort)entry));
        }

        /// <summary>
        /// The overworld beside a notice: the HGSS follower chosen in Settings, then Shuckle, then the player chosen in
        /// Settings. 0 frames when nothing can be drawn.
        /// </summary>
        public static (ushort Entry, int Frames) Who()
        {
            if (!Ready) return (0, 0);
            try
            {
                if (!OverworldSprites.Unpacked) return (0, 0);
                ushort player = (ushort)(PlayerGirl ? HeroineOverworld : HeroOverworld);
                if (GameFamily != GameFamilies.HGSS) return (player, OverworldSprites.FrameCount(player));
                if (Follower == NoticeFollowerKind.Random && RandomFollower() is (ushort picked, int pickedFrames) && pickedFrames > 0)
                    return (picked, pickedFrames);
                foreach (int s in new[] { Follower == NoticeFollowerKind.Specific ? FollowerSpecies : DefaultSpecies, DefaultSpecies })
                    if (FollowerOf(s) is (ushort entry, int frames) && frames > 0) return (entry, frames);
                return (player, OverworldSprites.FrameCount(player));
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("NoticeArt.Who", ex);
                return (0, 0);
            }
        }
    }
}
