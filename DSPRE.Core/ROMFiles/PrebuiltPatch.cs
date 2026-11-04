using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>An outside author's armips patch, assembled once so DSPRE can put its code anywhere in the synthetic overlay.</summary>
    public sealed class PrebuiltPatch
    {
        // In the payload: a branch to a fixed address, or a word that holds the payload address plus Value.
        // In arm9 and the overlays: a branch into the payload at Value, or a word that holds the payload address plus Value.
        private const byte BranchOut = 0, BranchIn = 2;

        private sealed class Reloc { public byte Kind; public bool Blx; public uint Offset, Value; }
        private sealed class Span { public uint Offset; public byte[] Original, Patched; }
        private sealed class Target { public string Path; public List<Span> Spans = new(); public List<Reloc> Relocs = new(); }

        private byte[] _payload;
        private readonly List<Reloc> _payloadRelocs = new();
        private readonly List<Target> _targets = new();

        public int PayloadSize => _payload.Length;

        /// <summary>Reads an embedded patch file that starts with <paramref name="magic"/>.</summary>
        public static PrebuiltPatch Load(string resource, string magic)
        {
            using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("The patch data is missing from this build.");
            using BinaryReader r = new BinaryReader(s);
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != magic || r.ReadUInt16() != 1) throw new InvalidDataException("Unknown patch data.");
            PrebuiltPatch p = new PrebuiltPatch { _payload = r.ReadBytes(r.ReadInt32()) };
            Reloc ReadReloc() { Reloc x = new Reloc { Kind = r.ReadByte(), Blx = r.ReadByte() != 0 }; r.ReadUInt16(); x.Offset = r.ReadUInt32(); x.Value = r.ReadUInt32(); return x; }
            for (int i = r.ReadInt32(); i > 0; i--) p._payloadRelocs.Add(ReadReloc());
            for (int f = r.ReadUInt16(); f > 0; f--)
            {
                Target t = new Target { Path = Encoding.ASCII.GetString(r.ReadBytes(r.ReadUInt16())) };
                for (int i = r.ReadInt32(); i > 0; i--)
                {
                    uint off = r.ReadUInt32(); int len = r.ReadInt32();
                    t.Spans.Add(new Span { Offset = off, Original = r.ReadBytes(len), Patched = r.ReadBytes(len) });
                }
                for (int i = r.ReadInt32(); i > 0; i--) t.Relocs.Add(ReadReloc());
                p._targets.Add(t);
            }
            return p;
        }

        private static string PathOf(string target) => target == "arm9/arm9.bin" ? RomInfo.Arm9Path : OverlayUtils.GetPath(OverlayOf(target));

        private static int OverlayOf(string target) =>
            target.StartsWith("arm9_overlays/", StringComparison.Ordinal) ? int.Parse(Path.GetFileNameWithoutExtension(target).Substring(2)) : -1;

        /// <summary>Thumb BL, or BLX into ARM code, from <paramref name="source"/> to <paramref name="target"/>.</summary>
        private static byte[] Branch(uint source, uint target, bool blx)
        {
            int offset = blx ? (int)(target - ((source + 4) & ~3u)) : (int)(target - (source + 4));
            ushort hi = (ushort)(0xF000 | ((offset >> 12) & 0x7FF));
            ushort lo = (ushort)((blx ? 0xE800 : 0xF800) | ((offset >> 1) & 0x7FF));
            return BitConverter.GetBytes(hi).Concat(BitConverter.GetBytes(lo)).ToArray();
        }

        /// <summary>The payload for a runtime address.</summary>
        public byte[] BuildPayload(uint address)
        {
            byte[] p = (byte[])_payload.Clone();
            foreach (Reloc x in _payloadRelocs)
            {
                byte[] bytes = x.Kind == BranchOut ? Branch(address + x.Offset, x.Value, x.Blx) : BitConverter.GetBytes(address + x.Value);
                bytes.CopyTo(p, (int)x.Offset);
            }
            return p;
        }

        /// <summary>Where an installed copy's payload starts, read back from arm9, or null.</summary>
        public uint? InstalledAddress()
        {
            Target arm9 = _targets.FirstOrDefault(t => t.Path == "arm9/arm9.bin");
            Reloc site = arm9?.Relocs.FirstOrDefault(x => x.Kind != BranchIn) ?? arm9?.Relocs.FirstOrDefault();
            if (site == null) return null;
            byte[] file = File.ReadAllBytes(RomInfo.Arm9Path);
            if (site.Offset + 4 > file.Length) return null;
            if (site.Kind != BranchIn) return BitConverter.ToUInt32(file, (int)site.Offset) - site.Value;
            byte[] bl = file.AsSpan((int)site.Offset, 4).ToArray();
            return PatchToolboxLogic.TryGetThumbBlTarget(ARM9.Address + site.Offset, bl, out uint target) ? target - site.Value : null;
        }

        /// <summary>The first hook that no longer holds the game's code, as "file at offset", or null.</summary>
        public string ChangedHook()
        {
            foreach (Target t in _targets)
            {
                int overlay = OverlayOf(t.Path);
                if (overlay >= 0 && OverlayUtils.IsCompressed(overlay)) OverlayUtils.Decompress(overlay);
                byte[] file = File.ReadAllBytes(PathOf(t.Path));
                foreach (Span s in t.Spans)
                    if (s.Offset + s.Original.Length > file.Length || !file.AsSpan((int)s.Offset, s.Original.Length).SequenceEqual(s.Original))
                        return $"{t.Path} at 0x{s.Offset:X}";
            }
            return null;
        }

        /// <summary>Writes every hook for a payload at <paramref name="address"/>. The caller writes the payload.</summary>
        public void WriteHooks(uint address)
        {
            foreach (Target t in _targets)
            {
                string path = PathOf(t.Path);
                byte[] file = File.ReadAllBytes(path);
                foreach (Span s in t.Spans) s.Patched.CopyTo(file, (int)s.Offset);
                uint fileBase = t.Path == "arm9/arm9.bin" ? ARM9.Address : OverlayUtils.OverlayTable.GetRAMAddress(OverlayOf(t.Path));
                foreach (Reloc x in t.Relocs)
                {
                    byte[] bytes = x.Kind == BranchIn ? Branch(fileBase + x.Offset, address + x.Value, x.Blx) : BitConverter.GetBytes(address + x.Value);
                    bytes.CopyTo(file, (int)x.Offset);
                }
                DSUtils.WriteWholeFile(path, file);
            }
        }
    }
}
