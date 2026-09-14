using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One seal of a capsule, placed on a small picture of the ball.</summary>
    public sealed class CapsuleSticker
    {
        public Bitmap Sticker { get; init; }
        public double Left { get; init; }
        public double Top { get; init; }
        public string Name { get; init; }
    }

    /// <summary>Trainer capsules shared by the Trainer Editor and Ball Capsules, reloaded when Ball Capsules saves.</summary>
    public static class TrainerCapsuleCatalog
    {
        /// <summary>Raised on the window thread after the capsules were saved.</summary>
        public static event Action Changed;

        private static BallCapsule[] _capsules = Array.Empty<BallCapsule>();
        private static Dictionary<int, BallSeal> _seals = new();
        private static readonly Dictionary<int, Bitmap> _stickers = new();
        private static string _loadedFor;

        public static bool Available => gameFamily != GameFamilies.DP && TrainerCapsules.Available;

        /// <summary>Reads the capsules and seal table once per ROM. Unpacks what it needs.</summary>
        public static void Load(bool again = false)
        {
            string rom = workDir ?? "";
            if (!again && _loadedFor == rom) return;
            _loadedFor = rom;
            _stickers.Clear();
            _capsules = Array.Empty<BallCapsule>();
            _seals = new();
            if (!Available) return;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerCapsules, DirNames.sealGraphics }
                    .Where(d => gameDirs.ContainsKey(d)).ToList());
                _capsules = TrainerCapsules.ReadAll();
                _seals = BallSeals.Read().Where(s => s != null).ToDictionary(s => s.Id);
            }
            catch (Exception ex) { AppLogger.Error("Trainer capsules could not be read: " + ex.Message); }
        }

        /// <summary>Row 0 is no capsule; row N is capsule N, the number a party entry stores.</summary>
        public static List<string> Names()
        {
            Load();
            var names = new List<string> { "None" };
            for (int i = 0; i < _capsules.Length; i++)
            {
                int count = _capsules[i].Seals.Count(s => s.Seal != 0);
                names.Add(count == 0 ? $"Capsule {i + 1}, empty" : $"Capsule {i + 1}, {count} seal{(count == 1 ? "" : "s")}");
            }
            return names;
        }

        private const double Thumb = 64, Scale = Thumb / (2 * BallCapsule.BoardRadius + 32);

        /// <summary>Where each seal of capsule <paramref name="number"/> sits on a 64 by 64 ball, empty for none.</summary>
        public static IReadOnlyList<(BallSeal Seal, double Left, double Top)> Placements(int number)
        {
            Load();
            var placed = new List<(BallSeal, double, double)>();
            if (number < 1 || number > _capsules.Length) return placed;
            foreach (var s in _capsules[number - 1].Seals)
            {
                if (s.Seal == 0 || !_seals.TryGetValue(s.Seal, out var seal)) continue;
                placed.Add((seal, Thumb / 2 + (s.X - BallCapsule.BoardCentreX - 16) * Scale,
                                  Thumb / 2 + (s.Y - BallCapsule.BoardCentreY - 16) * Scale));
            }
            return placed;
        }

        /// <summary>The seals of capsule <paramref name="number"/> as pictures on the small ball.</summary>
        public static IReadOnlyList<CapsuleSticker> Stickers(int number)
        {
            var shown = new List<CapsuleSticker>();
            foreach (var (seal, left, top) in Placements(number))
            {
                if (!_stickers.TryGetValue(seal.Id, out var bitmap))
                {
                    try { bitmap = BallCapsuleGraphics.Sticker(seal); } catch { bitmap = null; }
                    _stickers[seal.Id] = bitmap;
                }
                if (bitmap != null) shown.Add(new CapsuleSticker { Sticker = bitmap, Name = seal.Name, Left = left, Top = top });
            }
            return shown;
        }

        public static double StickerSize => 32 * Scale;

        /// <summary>Tells every open editor the capsules on disk changed.</summary>
        public static void Saved()
        {
            Load(again: true);
            Changed?.Invoke();
        }

        /// <summary>Trainers whose Pokemon carry each capsule. Slow, so run it behind the busy overlay.</summary>
        public static Dictionary<int, List<string>> UsedBy()
        {
            var used = new Dictionary<int, List<string>>();
            if (!Available || !gameDirs.ContainsKey(DirNames.trainerProperties) || !gameDirs.ContainsKey(DirNames.trainerParty))
                return used;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties, DirNames.trainerParty });
            string[] names = DSPRE.TrainerNames.GetAll().ToArray();
            string props = gameDirs[DirNames.trainerProperties].unpackedDir, party = gameDirs[DirNames.trainerParty].unpackedDir;
            for (int i = 0; ; i++)
            {
                string p = Path.Combine(props, i.ToString("D4")), q = Path.Combine(party, i.ToString("D4"));
                if (!File.Exists(p) || !File.Exists(q)) break;
                try
                {
                    using var ps = File.OpenRead(p);
                    using var qs = File.OpenRead(q);
                    var file = new TrainerFile(new TrainerProperties((ushort)i, ps), qs, i < names.Length ? names[i] : "");
                    string who = i < names.Length && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : $"Trainer {i}";
                    for (int m = 0; m < file.trp.partyCount; m++)
                    {
                        var mon = file.party[m];
                        if (mon == null || mon.CheckEmpty() || mon.ballSeals == 0) continue;
                        if (!used.TryGetValue(mon.ballSeals, out var list)) used[mon.ballSeals] = list = new List<string>();
                        if (!list.Contains(who)) list.Add(who);
                    }
                }
                catch { }
            }
            return used;
        }
    }
}
