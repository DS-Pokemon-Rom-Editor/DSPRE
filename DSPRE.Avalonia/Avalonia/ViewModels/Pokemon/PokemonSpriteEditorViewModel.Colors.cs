using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Editors.Utils;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;
using FormSpriteData = DSPRE.Graphics.AlternateFormSprites.Form;
using Avalonia.Media;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PokemonSpriteEditorViewModel
    {

        // Keep the PNG's palette order. First-seen scanning scrambles authored indices.
        private static bool TryReadIndexedOrQuantize(byte[] fileBytes, RawImage decoded, out byte[] indices, out uint[] palette, out int usedCount)
        {
            if (IndexedPng.TryRead(fileBytes, out byte[] realIndices, out uint[] realPalette, out int w, out int h) &&
                w == decoded.Width && h == decoded.Height && realPalette.Length <= 16)
            {
                indices = realIndices;
                usedCount = realPalette.Length;
                palette = Pad16(realPalette);
                return true;
            }
            return TryReadImageColors(decoded, out indices, out palette, out usedCount);
        }

        private static bool TryReadImageColors(RawImage img, out byte[] indices, out uint[] palette, out int usedCount)
        {
            int n = img.Width * img.Height;
            indices = new byte[n];
            palette = new uint[16];
            usedCount = 0;

            Dictionary<uint, byte> seen = new System.Collections.Generic.Dictionary<uint, byte>();
            for (int p = 0; p < n; p++)
            {
                int o = p * 4;
                uint c = OpaqueOrClear(img.Bgra, o);
                if (!seen.TryGetValue(c, out byte idx))
                {
                    if (seen.Count >= 16) { indices = null; palette = null; usedCount = 0; return false; }
                    idx = (byte)seen.Count;
                    seen[c] = idx;
                    palette[idx] = c;
                }
                indices[p] = idx;
            }
            usedCount = seen.Count;
            for (int i = usedCount; i < 16; i++) palette[i] = 0xFF000000u;
            return true;
        }

        // Fully transparent pixels are one colour whatever RGB they carry.
        private static uint OpaqueOrClear(byte[] bgra, int o) =>
            bgra[o + 3] == 0 ? 0u : 0xFF000000u | ((uint)bgra[o + 2] << 16) | ((uint)bgra[o + 1] << 8) | bgra[o];

        private static bool PaletteEqualsUpTo(uint[] existing, uint[] candidate, int count)
        {
            for (int i = 0; i < count; i++) if (existing[i] != candidate[i]) return false;
            return true;
        }

        private static bool[] MakeUsedMask(int usedCount)
        {
            bool[] a = new bool[16];
            for (int i = 0; i < usedCount && i < 16; i++) a[i] = true;
            return a;
        }

        // One old index showing two colors means the artwork's shape changed, not only its colors.
        private static bool TryDeriveRecolorPalette(byte[] oldIndices, RawImage newImg, out uint[] palette, out bool[] used)
        {
            palette = new uint[16];
            used = new bool[16];
            if (oldIndices == null || newImg == null || oldIndices.Length != newImg.Width * newImg.Height) return false;
            for (int p = 0; p < oldIndices.Length; p++)
            {
                int idx = oldIndices[p] & 0xF;
                int o = p * 4;
                uint c = OpaqueOrClear(newImg.Bgra, o);
                if (!used[idx]) { palette[idx] = c; used[idx] = true; }
                else if (palette[idx] != c) return false;
            }
            return true;
        }

        private static int CountUnmatchedColors(uint[] newPalette, int usedCount, uint[] existingPalette)
        {
            int unmatched = 0;
            for (int i = 0; i < usedCount; i++)
            {
                bool found = false;
                for (int j = 0; j < 16; j++) if (existingPalette[j] == newPalette[i]) { found = true; break; }
                if (!found) unmatched++;
            }
            return unmatched;
        }

        private static int CountFreeSlots(bool[] used)
        {
            int free = 0;
            foreach (bool u in used) if (!u) free++;
            return free;
        }

        // One palette covers every pose, so this always asks.
        private async Task<bool> ConfirmFullPaletteReplaceAsync(Window owner)
        {
            return await DialogHelper.AskYesNoAsync(
                "Replacing the palette changes every normal sprite that uses it. " +
                "Shiny sprites share the artwork and can change too. Replace it anyway?",
                "Other sprites change too", owner);
        }

        private async Task<bool> ConfirmOverwriteIfNeededAsync(uint[] newPalette, int usedCount, uint[] existingPalette, bool[] existingUsed, Window owner)
        {
            int unmatched = CountUnmatchedColors(newPalette, usedCount, existingPalette);
            int free = CountFreeSlots(existingUsed);
            if (unmatched <= free) return true;
            int overwrite = unmatched - free;
            return await DialogHelper.AskYesNoAsync(
                $"This image has {Plural.Of(overwrite, "colour")} not in the saved palette " +
                "and no free slots for them.\n\n" +
                "Existing colours will be overwritten to make room, and the shiny sprites change too. Import anyway?",
                "Existing colours are overwritten", owner);
        }

        // Matches colors by value. Unmatched ones take a placeholder slot, a used one only if none left.
        private static byte[] RemapToExistingPalette(byte[] newIndices, uint[] newPalette, int usedCount, uint[] existingPalette, bool[] existingUsed, out uint[] mergedPalette, out bool[] mergedUsed)
        {
            byte[] indexMap = new byte[16];
            bool[] claimed = new bool[16];

            for (int i = 0; i < usedCount; i++)
            {
                int found = -1;
                for (int j = 0; j < 16; j++)
                {
                    if (!claimed[j] && existingPalette[j] == newPalette[i]) { found = j; break; }
                }
                indexMap[i] = found >= 0 ? (byte)found : (byte)255;
                if (found >= 0) claimed[found] = true;
            }

            mergedPalette = (uint[])existingPalette.Clone();
            mergedUsed = (bool[])(existingUsed ?? AllUsed()).Clone();
            for (int i = 0; i < usedCount; i++)
            {
                if (indexMap[i] != 255) continue;
                int freeSlot = -1;
                for (int j = 0; j < 16; j++) if (!claimed[j] && !mergedUsed[j]) { freeSlot = j; break; }
                if (freeSlot < 0)
                    for (int j = 0; j < 16; j++) if (!claimed[j]) { freeSlot = j; break; } // no placeholder left, don't drop the color
                if (freeSlot < 0) freeSlot = 0; // unreachable: usedCount <= 16
                claimed[freeSlot] = true;
                indexMap[i] = (byte)freeSlot;
                mergedPalette[freeSlot] = newPalette[i];
                mergedUsed[freeSlot] = true;
            }

            byte[] outIdx = new byte[newIndices.Length];
            for (int p = 0; p < newIndices.Length; p++) outIdx[p] = indexMap[newIndices[p]];
            return outIdx;
        }

        // Palette from a reference image: each artwork index takes the color at its first pixel.
        private static uint[] DeriveAlternatePalette(byte[] parentIndices, byte[] childIndices, uint[] childPalette, out bool[] used)
        {
            used = null;
            if (parentIndices == null || childIndices == null || parentIndices.Length != childIndices.Length) return null;
            uint[] result = new uint[16];
            bool[] found = new bool[16];
            for (int p = 0; p < parentIndices.Length; p++)
            {
                int i = parentIndices[p] & 0xF;
                if (!found[i]) { result[i] = childPalette[childIndices[p]]; found[i] = true; }
            }
            for (int i = 0; i < 16; i++) if (!found[i]) result[i] = 0xFF000000u;
            used = found;
            return result;
        }
    }
}
