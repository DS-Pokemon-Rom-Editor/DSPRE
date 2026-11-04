"""Builds ShinyOdds.bin from Eclipse's ShinyChance_HG_SS.asm.

Assemble the asm three times with armips and -definelabel PATCH 1. Each run needs its own folder with a copy of
US HeartGold's arm9/arm9.bin and arm9_overlays/ov002.bin, and an empty unpacked/synthOverlay/0000.

  a: unchanged
  b: INJECT_ADDR set to 0x023C9000 and the synthetic overlay opened at 0x023C7000, so the payload lands at 0x2000
  c: ShinyOdds set to 2048

Then run: python build_shiny_odds_bin.py <original project> <a> <b> <c> <out.bin>
"""
import struct
import sys

ORIGINAL, A, B, C, OUT = sys.argv[1:6]
BASE_A, BASE_B = 0x023C8000, 0x023C9000
END = b"ShinyChance_end"
TARGETS = [("arm9/arm9.bin", 0x02000000), ("arm9_overlays/ov002.bin", 0x02245B80)]
KIND_BRANCH_OUT, KIND_POINTER, KIND_BRANCH_IN = 0, 1, 2


def read(path):
    with open(path, "rb") as f:
        return f.read()


def branch_target(code, at, source):
    hi, lo = struct.unpack_from("<HH", code, at)
    if hi >> 11 != 0b11110 or lo >> 11 not in (0b11111, 0b11101):
        raise ValueError(f"no BL at 0x{source:X}")
    offset = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1)
    if offset & 0x400000:
        offset -= 0x800000
    blx = lo >> 11 == 0b11101
    return (((source + 4) & ~3) if blx else source + 4) + offset, blx


def groups(a, b, length):
    out = []
    for i in range(length):
        if a[i] != b[i]:
            if out and i <= out[-1] + 3:
                continue
            out.append(i)
    return out


synth_a = read(A + "/unpacked/synthOverlay/0000")
synth_b = read(B + "/unpacked/synthOverlay/0000")
size = synth_a.find(END) + len(END)
payload = synth_a[:size]
moved = synth_b[0x2000:0x2000 + size]

payload_relocs = []
for at in groups(payload, moved, size):
    word = at & ~3
    a_word, b_word = struct.unpack_from("<I", payload, word)[0], struct.unpack_from("<I", moved, word)[0]
    if b_word - a_word == BASE_B - BASE_A:
        payload_relocs.append((KIND_POINTER, 0, word, a_word - BASE_A))
        continue
    at &= ~1
    target, blx = branch_target(payload, at, BASE_A + at)
    payload_relocs.append((KIND_BRANCH_OUT, int(blx), at, target))

odds = read(C + "/unpacked/synthOverlay/0000")[:size]
odds_at = [i for i in range(size) if odds[i] != payload[i]]
print(f"payload 0x{size:X} bytes, {len(payload_relocs)} relocations, odds word at 0x{odds_at[0] & ~3:X}")

targets = []
for path, base in TARGETS:
    original, a, b = read(ORIGINAL + "/" + path), read(A + "/" + path), read(B + "/" + path)
    spans, relocs = [], []
    for at in groups(original, a, len(original)):
        at &= ~1
        target_a, _ = branch_target(a, at, base + at)
        target_b, _ = branch_target(b, at, base + at)
        if target_b - target_a != BASE_B - BASE_A:
            raise ValueError(f"{path} 0x{base + at:X} isn't a hook into the payload")
        spans.append((at, original[at:at + 4], a[at:at + 4]))
        relocs.append((KIND_BRANCH_IN, 0, at, target_a - BASE_A))
    targets.append((path, spans, relocs))
    print(f"{path}: {len(spans)} hooks")


def reloc_bytes(r):
    return struct.pack("<BBHII", r[0], r[1], 0, r[2], r[3])


out = bytearray(b"SHOD") + struct.pack("<H", 1) + struct.pack("<i", size) + payload
out += struct.pack("<i", len(payload_relocs)) + b"".join(reloc_bytes(r) for r in payload_relocs)
out += struct.pack("<H", len(targets))
for path, spans, relocs in targets:
    name = path.encode("ascii")
    out += struct.pack("<H", len(name)) + name + struct.pack("<i", len(spans))
    for at, original, patched in spans:
        out += struct.pack("<Ii", at, len(original)) + original + patched
    out += struct.pack("<i", len(relocs)) + b"".join(reloc_bytes(r) for r in relocs)
with open(OUT, "wb") as f:
    f.write(out)
