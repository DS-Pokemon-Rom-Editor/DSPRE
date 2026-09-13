using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>One Ball Capsule seal as the ROM defines it.</summary>
    public sealed class BallSeal
    {
        public int Id { get; init; }
        public string Name { get; init; }
        /// <summary>Its sticker drawing in the capsule editor's archive.</summary>
        public int Sprite { get; init; }
        /// <summary>Its particle file in the ball particle archive.</summary>
        public int Particle { get; init; }
        /// <summary>Letter seals A to Z, ! and ?, which always wait the same short time before bursting.</summary>
        public bool IsLetter { get; init; }
    }

    /// <summary>
    /// The arm9 seal table, 10-byte rows in Diamond, Pearl and Platinum and 4-byte rows in HeartGold and SoulSilver,
    /// row 0 unused. Found by shape because its address differs per language.
    /// </summary>
    public static class BallSeals
    {
        public const int Count = 80;
        private const int FirstLetter = 50, LastLetter = 77;

        /// <summary>Every seal, indexed by id; index 0 is null. Empty when the table is not found.</summary>
        public static IReadOnlyList<BallSeal> Read()
        {
            var seals = new BallSeal[Count + 1];
            byte[] arm9;
            try { arm9 = File.ReadAllBytes(arm9Path); }
            catch { return Array.Empty<BallSeal>(); }

            bool johto = gameFamily == GameFamilies.HGSS;
            int at = FindTable(arm9, johto);
            if (at < 0) return Array.Empty<BallSeal>();

            List<string> names = null;
            try { names = new TextArchive(SealNamesTextNumber).messages; } catch { }

            for (int id = 1; id <= Count; id++)
            {
                int sprite, nameLine, particle;
                bool letter;
                if (johto)
                {
                    int row = at + id * 4;
                    sprite = arm9[row]; nameLine = arm9[row + 1]; particle = arm9[row + 2];
                    letter = id >= FirstLetter && id <= LastLetter;
                }
                else
                {
                    int row = at + id * 10;
                    sprite = BitConverter.ToUInt16(arm9, row); nameLine = arm9[row + 2]; particle = arm9[row + 4];
                    letter = arm9[row + 5] == 1;
                }
                string name = names != null && nameLine < names.Count ? names[nameLine]?.Trim() : null;
                seals[id] = new BallSeal
                {
                    Id = id, Sprite = sprite, Particle = particle, IsLetter = letter,
                    Name = string.IsNullOrEmpty(name) ? $"Seal {id}" : name,
                };
            }
            return seals;
        }

        /// <summary>Where row 0 of the table starts in arm9, or -1.</summary>
        public static int FindTable(byte[] arm9, bool johto)
        {
            int rowSize = johto ? 4 : 10;
            int firstSprite = johto ? 40 : 185, firstParticle = johto ? 53 : 37;
            var stickerTaken = new bool[Count];
            var particleTaken = new bool[Count];
            for (int at = 0; at + rowSize * (Count + 1) <= arm9.Length; at += johto ? 1 : 2)
            {
                if (SpriteAt(arm9, at, johto) != firstSprite - 1) continue;
                Array.Clear(stickerTaken);
                Array.Clear(particleTaken);
                bool fits = true;
                // Each seal owns one sticker and one particle file, in any order; DPPt byte 3 is always palette 0x25.
                for (int id = 1; id <= Count && fits; id++)
                {
                    int row = at + id * rowSize;
                    int sticker = SpriteAt(arm9, row, johto) - firstSprite;
                    int particle = arm9[row + (johto ? 2 : 4)] - firstParticle;
                    fits = sticker >= 0 && sticker < Count && !stickerTaken[sticker]
                        && particle >= 0 && particle < Count && !particleTaken[particle]
                        && (johto || arm9[row + 3] == 0x25);
                    if (fits) stickerTaken[sticker] = particleTaken[particle] = true;
                }
                if (fits) return at;
            }
            return -1;
        }

        private static int SpriteAt(byte[] arm9, int row, bool johto) => johto ? arm9[row] : BitConverter.ToUInt16(arm9, row);
    }
}
