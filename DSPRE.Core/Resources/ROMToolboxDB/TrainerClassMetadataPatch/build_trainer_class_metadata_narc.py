from __future__ import annotations

import argparse
import hashlib
import struct
from pathlib import Path


CLASS_COUNT = 129
RECORD_SIZE = 0x34

OV12_BASE = 0x022378C0
OV80_BASE = 0x02229EE0
OV115_BASE = 0x0225F020
OV117_BASE = 0x0225F020

GENDER_OFFSET = 0xFFB90
TRAINER_COMBO_OFFSET = 0xFC3CA
COMBO_TABLE_OFFSET = 0xFC40A
EYE_MUSIC_OFFSET = 0xFC61C
PRIZE_TABLE_ADDRESS = 0x0226C4C4
FRONTIER_BRAIN_TABLE_ADDRESS = 0x0223DB98

TRAINER_COMBO_COUNT = 32
COMBO_COUNT = 45
EYE_MUSIC_COUNT = 44
DEFAULT_COMBO = 0x29
DEFAULT_EYE_MUSIC = 1108
OUTPUT_RELATIVE_PATH = Path("files/a/1/5/5")

OFF_GENDER = 0x00
OFF_PRIZE = 0x02
OFF_EYE_MUSIC_MAIN = 0x04
OFF_EYE_MUSIC_ALT = 0x06
OFF_BATTLE_MUSIC = 0x08
OFF_VS_STYLE = 0x0A
OFF_TRAINER_NAME = 0x0C
OFF_STYLE1_SAVED_RIVAL = 0x0E
OFF_STYLE1_MOTION = 0x10
OFF_STYLE2_DURATION = 0x14
OFF_GROUP1 = 0x16
OFF_GROUP1_RCSN1 = 0x1E
OFF_GROUP1_RCSN2 = 0x20
OFF_GROUP1_RCSN3 = 0x22
OFF_GROUP2 = 0x24
OFF_GROUP3 = 0x2C

STYLE0_SMALL_ASSETS = (0, 4, 6, 5)
STYLE0_LARGE_ASSETS = (0, 7, 9, 8)
STANDARD_SYMBOL_QUARTET = (0x003B, 0x003C, 0x003D, 0x003E)
FRONTIER_SYMBOL_QUARTET = (0x003B, 0x00CC, 0x00CD, 0x00CE)

EXPECTED_TRAINER_COMBO_ROWS = (
    66, 1091, 2118, 3144, 4170, 5195, 6217, 7244,
    8290, 9319, 10344, 11369, 12394, 13419, 14444, 15470,
    16471, 17497, 18544, 19544, 20566, 21527, 21623, 32884,
    33906, 31861, 30838, 34940, 29751, 29758, 44079, 45165,
)

EXPECTED_COMBO_EFFECTS = (
    12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26,
    27, 29, 30, 31, 32, 33, 28, 0xFFFF, 0xFFFF, 0xFFFF, 35, 36,
    34, 34, 39, 40, 41, 42, 43, 44, 37, 37, 38, 38, 37, 38,
    0xFFFF, 0xFFFF, 45, 46,
)

EXPECTED_EYE_MUSIC_CLASSES = (
    2, 3, 4, 5, 6, 8, 9, 11, 14, 20, 21, 24, 25, 69, 31, 34, 36,
    38, 42, 43, 46, 47, 49, 52, 55, 56, 60, 62, 63, 64, 65, 68,
    77, 78, 79, 82, 113, 115, 121, 122, 116, 114, 117, 118,
)

VS20_RECORD_BY_EFFECT = {
    12: 0x022603B0,
    13: 0x022603C4,
    14: 0x022603D8,
    15: 0x022603EC,
    16: 0x02260400,
    17: 0x02260414,
    18: 0x02260428,
    19: 0x0226043C,
    20: 0x02260450,
    21: 0x02260464,
    22: 0x02260478,
    23: 0x0226048C,
    24: 0x022604A0,
    25: 0x022604B4,
    26: 0x022604C8,
    27: 0x022604DC,
    28: 0x02260374,
}

VS8_RECORD_BY_EFFECT = {
    29: 0x02260388,
    30: 0x02260390,
    31: 0x02260398,
    32: 0x022603A0,
    33: 0x022603A8,
}

VS8_BACKGROUND_BY_EFFECT = {
    29: (0x2F, 0x30, 0x31, 0x32),
    30: (0x33, 0x30, 0x31, 0x32),
    31: (0x34, 0x30, 0x31, 0x32),
    32: (0x35, 0x30, 0x31, 0x32),
    33: (0x36, 0x30, 0x31, 0x32),
}

ROCKET_ADMIN_RECORD_BY_EFFECT = {
    40: 0x0225FACC,
    41: 0x0225FAD4,
    42: 0x0225FADC,
    43: 0x0225FAE4,
    44: 0x0225FAEC,
}

FRONTIER_BRAIN_ROW_BY_CLASS = {
    97: 0,
    99: 4,
    100: 1,
    101: 5,
    102: 3,
}


class BuildError(ValueError):
    pass


def read_u16(data: bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def read_u32(data: bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def write_u16(data: bytearray, offset: int, value: int) -> None:
    if not 0 <= value <= 0xFFFF:
        raise BuildError(f"value {value} does not fit in u16")
    struct.pack_into("<H", data, offset, value)


def write_u32(data: bytearray, offset: int, value: int) -> None:
    if not 0 <= value <= 0xFFFFFFFF:
        raise BuildError(f"value {value} does not fit in u32")
    struct.pack_into("<I", data, offset, value)


def write_quartet(record: bytearray, offset: int, values: tuple[int, int, int, int]) -> None:
    for index, value in enumerate(values):
        write_u16(record, offset + index * 2, value)


def checked_slice(data: bytes, offset: int, size: int, label: str) -> bytes:
    if offset < 0 or offset + size > len(data):
        raise BuildError(
            f"{label} requires bytes 0x{offset:X}..0x{offset + size - 1:X}, "
            f"but the input is only 0x{len(data):X} bytes"
        )
    return data[offset:offset + size]


def overlay_slice(
    data: bytes,
    base_address: int,
    address: int,
    size: int,
    label: str,
) -> bytes:
    return checked_slice(data, address - base_address, size, label)


def read_required_file(root: Path, relative_path: str) -> bytes:
    path = root / relative_path
    if not path.is_file():
        raise BuildError(f"required input file is missing: {path}")
    try:
        return path.read_bytes()
    except OSError as exc:
        raise BuildError(f"could not read {path}: {exc}") from exc


def parse_native_tables(
    arm9: bytes,
    ov12: bytes,
) -> tuple[
    list[int],
    dict[int, int],
    list[tuple[int, int]],
    dict[int, tuple[int, int]],
    dict[int, int],
]:
    genders = list(checked_slice(arm9, GENDER_OFFSET, CLASS_COUNT, "gender table"))

    combo_map_blob = checked_slice(
        arm9,
        TRAINER_COMBO_OFFSET,
        TRAINER_COMBO_COUNT * 2,
        "trainer-to-combo table",
    )
    combo_rows = struct.unpack(f"<{TRAINER_COMBO_COUNT}H", combo_map_blob)
    if combo_rows != EXPECTED_TRAINER_COMBO_ROWS:
        raise BuildError(
            "trainer-to-combo table is not the supported vanilla US HGSS table; "
            "post-TCM, remapped, or non-US-HGSS input is unsupported"
        )
    combo_by_class = {row & 0x03FF: row >> 10 for row in combo_rows}

    combo_blob = checked_slice(
        arm9,
        COMBO_TABLE_OFFSET,
        COMBO_COUNT * 4,
        "combo effect/music table",
    )
    combos = [struct.unpack_from("<HH", combo_blob, index * 4) for index in range(COMBO_COUNT)]
    effects = tuple(effect for effect, _music in combos)
    if effects != EXPECTED_COMBO_EFFECTS:
        raise BuildError(
            "combo effect column is not the supported vanilla US HGSS layout; "
            "changed effect relationships are unsupported"
        )

    eye_blob = checked_slice(
        arm9,
        EYE_MUSIC_OFFSET,
        EYE_MUSIC_COUNT * 6,
        "eye-contact music table",
    )
    eye_rows = [struct.unpack_from("<HHH", eye_blob, index * 6) for index in range(EYE_MUSIC_COUNT)]
    eye_classes = tuple(class_id for class_id, _main, _alt in eye_rows)
    if eye_classes != EXPECTED_EYE_MUSIC_CLASSES:
        raise BuildError(
            "eye-contact table class keys are not the supported native US HGSS layout"
        )
    eye_by_class = {class_id: (main, alt) for class_id, main, alt in eye_rows}

    prize_offset = PRIZE_TABLE_ADDRESS - OV12_BASE
    prize_blob = checked_slice(
        ov12,
        prize_offset,
        CLASS_COUNT * 4,
        "prize coefficient table",
    )
    prize_rows = [struct.unpack_from("<HH", prize_blob, index * 4) for index in range(CLASS_COUNT)]
    prize_classes = [class_id for class_id, _coefficient in prize_rows]
    if len(set(prize_classes)) != CLASS_COUNT or set(prize_classes) != set(range(CLASS_COUNT)):
        raise BuildError("prize table must contain each trainer class ID 0..128 exactly once")
    prize_by_class = dict(prize_rows)

    return genders, combo_by_class, combos, eye_by_class, prize_by_class


def write_style0(record: bytearray) -> None:
    write_quartet(record, OFF_GROUP1, STYLE0_SMALL_ASSETS)
    write_quartet(record, OFF_GROUP2, STYLE0_LARGE_ASSETS)


def write_style1(record: bytearray, source: bytes) -> None:
    write_u16(record, OFF_VS_STYLE, 1)
    selector = read_u16(source, 0x08)
    use_saved_rival = selector == 0x17
    write_u16(record, OFF_TRAINER_NAME, 0 if use_saved_rival else read_u32(source, 0x04))
    record[OFF_STYLE1_SAVED_RIVAL] = int(use_saved_rival)
    write_u32(record, OFF_STYLE1_MOTION, read_u32(source, 0x00))
    write_quartet(record, OFF_GROUP2, tuple(source[0x0C:0x10]))
    write_u16(record, OFF_GROUP1, source[0x10])
    write_u16(record, OFF_GROUP1 + 2, source[0x11])
    write_u16(record, OFF_GROUP1_RCSN1, source[0x12])
    write_quartet(record, OFF_GROUP3, STANDARD_SYMBOL_QUARTET)


def write_style2(record: bytearray, source: bytes, effect_id: int) -> None:
    write_u16(record, OFF_VS_STYLE, 2)
    write_u16(record, OFF_TRAINER_NAME, read_u16(source, 0x06))
    write_u16(record, OFF_STYLE2_DURATION, source[0x03])
    portrait_base = read_u16(source, 0x00)
    write_quartet(record, OFF_GROUP2, tuple(portrait_base + index for index in range(4)))
    write_quartet(record, OFF_GROUP1, VS8_BACKGROUND_BY_EFFECT[effect_id])
    write_quartet(record, OFF_GROUP3, STANDARD_SYMBOL_QUARTET)


def write_style3(record: bytearray, source: bytes) -> None:
    write_u16(record, OFF_VS_STYLE, 3)
    write_u16(record, OFF_TRAINER_NAME, read_u32(source, 0x04))
    write_quartet(record, OFF_GROUP2, tuple(source[0:4]))
    write_u16(record, OFF_GROUP1, 0xD7)
    write_u16(record, OFF_GROUP1 + 2, 0xD8)
    write_u16(record, OFF_GROUP1_RCSN1, 0xD9)
    write_u16(record, OFF_GROUP1_RCSN2, 0xDA)
    write_u16(record, OFF_GROUP1_RCSN3, 0xDD)


def write_style4(record: bytearray) -> None:
    write_u16(record, OFF_VS_STYLE, 4)
    write_u16(record, OFF_GROUP1, 0xA6)
    write_u16(record, OFF_GROUP1 + 2, 0xA7)
    write_u16(record, OFF_GROUP1_RCSN1, 0xA8)


def write_style5(record: bytearray) -> None:
    write_u16(record, OFF_VS_STYLE, 5)
    write_quartet(record, OFF_GROUP1, (0x01, 0x0A, 0x0C, 0x0B))


def write_style6(record: bytearray) -> None:
    write_u16(record, OFF_VS_STYLE, 6)
    write_quartet(record, OFF_GROUP1, (0x03, 0x9C, 0x9E, 0x9D))


def write_style13(record: bytearray, source: bytes) -> None:
    write_u16(record, OFF_VS_STYLE, 13)
    write_u16(record, OFF_TRAINER_NAME, read_u32(source, 0x00))
    write_quartet(record, OFF_GROUP2, tuple(source[0x04:0x08]))
    write_u16(record, OFF_GROUP1, source[0x08])
    write_u16(record, OFF_GROUP1 + 2, source[0x09])
    write_u16(record, OFF_GROUP1_RCSN1, source[0x0A])
    write_quartet(record, OFF_GROUP3, FRONTIER_SYMBOL_QUARTET)


def build_records(
    arm9: bytes,
    ov12: bytes,
    ov80: bytes,
    ov115: bytes,
    ov117: bytes,
) -> list[bytes]:
    genders, combo_by_class, combos, eye_by_class, prize_by_class = parse_native_tables(arm9, ov12)

    brain_table = overlay_slice(
        ov80,
        OV80_BASE,
        FRONTIER_BRAIN_TABLE_ADDRESS,
        6 * 0x0C,
        "Frontier Brain recipe table",
    )

    records: list[bytes] = []
    for class_id in range(CLASS_COUNT):
        record = bytearray(RECORD_SIZE)
        write_u16(record, OFF_GENDER, genders[class_id])
        write_u16(record, OFF_PRIZE, prize_by_class[class_id])

        eye_main, eye_alt = eye_by_class.get(
            class_id,
            (DEFAULT_EYE_MUSIC, DEFAULT_EYE_MUSIC),
        )
        write_u16(record, OFF_EYE_MUSIC_MAIN, eye_main)
        write_u16(record, OFF_EYE_MUSIC_ALT, eye_alt)

        combo_id = combo_by_class.get(class_id, DEFAULT_COMBO)
        effect_id, battle_music = combos[combo_id]
        write_u16(record, OFF_BATTLE_MUSIC, battle_music)

        if effect_id in VS20_RECORD_BY_EFFECT:
            source = overlay_slice(
                ov115,
                OV115_BASE,
                VS20_RECORD_BY_EFFECT[effect_id],
                0x14,
                f"Style 1 effect {effect_id} recipe",
            )
            write_style1(record, source)
        elif effect_id in VS8_RECORD_BY_EFFECT:
            source = overlay_slice(
                ov115,
                OV115_BASE,
                VS8_RECORD_BY_EFFECT[effect_id],
                0x08,
                f"Style 2 effect {effect_id} recipe",
            )
            write_style2(record, source, effect_id)
        elif effect_id in ROCKET_ADMIN_RECORD_BY_EFFECT:
            source = overlay_slice(
                ov117,
                OV117_BASE,
                ROCKET_ADMIN_RECORD_BY_EFFECT[effect_id],
                0x08,
                f"Style 3 effect {effect_id} recipe",
            )
            write_style3(record, source)
        elif class_id == 47 and effect_id == 45:
            write_style4(record)
        elif class_id == 109 and effect_id == 46:
            write_style5(record)
        elif class_id in (55, 62) and effect_id == 39:
            write_style6(record)
        else:
            write_style0(record)

        brain_row = FRONTIER_BRAIN_ROW_BY_CLASS.get(class_id)
        if brain_row is not None:
            record[OFF_VS_STYLE:] = bytes(RECORD_SIZE - OFF_VS_STYLE)
            start = brain_row * 0x0C
            write_style13(record, brain_table[start:start + 0x0C])

        records.append(bytes(record))

    return records


def pack_narc(records: list[bytes]) -> bytes:
    gmif = bytearray()
    fat = bytearray()
    for index, record in enumerate(records):
        if len(record) != RECORD_SIZE:
            raise BuildError(f"record {index} is {len(record)} bytes, expected {RECORD_SIZE}")
        start = len(gmif)
        gmif.extend(record)
        end = len(gmif)
        fat.extend(struct.pack("<II", start, end))
        while len(gmif) % 4:
            gmif.append(0xFF)

    btaf = b"BTAF" + struct.pack("<IHH", 12 + len(fat), len(records), 0) + fat
    btnf = b"BTNF" + struct.pack("<III", 16, 4, 0x00010000)
    gmif_chunk = b"GMIF" + struct.pack("<I", 8 + len(gmif)) + gmif
    total_size = 16 + len(btaf) + len(btnf) + len(gmif_chunk)
    return (
        b"NARC"
        + struct.pack("<HHIHH", 0xFFFE, 0x0100, total_size, 16, 3)
        + btaf
        + btnf
        + gmif_chunk
    )


def validate_narc(narc: bytes) -> None:
    if len(narc) < 16 or narc[:4] != b"NARC":
        raise BuildError("generated output does not have a valid NARC header")
    byte_order, version, declared_size, header_size, chunk_count = struct.unpack_from(
        "<HHIHH", narc, 4
    )
    if (byte_order, version, header_size, chunk_count) != (0xFFFE, 0x0100, 16, 3):
        raise BuildError("generated NARC header fields are invalid")
    if declared_size != len(narc):
        raise BuildError("generated NARC declared size does not match its byte length")

    btaf_offset = header_size
    if narc[btaf_offset:btaf_offset + 4] != b"BTAF":
        raise BuildError("generated NARC is missing its BTAF chunk")
    btaf_size, member_count, reserved = struct.unpack_from("<IHH", narc, btaf_offset + 4)
    if member_count != CLASS_COUNT or reserved != 0 or btaf_size != 12 + CLASS_COUNT * 8:
        raise BuildError("generated NARC has invalid BTAF metadata")

    btnf_offset = btaf_offset + btaf_size
    if narc[btnf_offset:btnf_offset + 4] != b"BTNF":
        raise BuildError("generated NARC is missing its BTNF chunk")
    btnf_size = read_u32(narc, btnf_offset + 4)
    if btnf_size != 16:
        raise BuildError("generated NARC has an invalid BTNF size")

    gmif_offset = btnf_offset + btnf_size
    if narc[gmif_offset:gmif_offset + 4] != b"GMIF":
        raise BuildError("generated NARC is missing its GMIF chunk")
    gmif_size = read_u32(narc, gmif_offset + 4)
    if gmif_offset + gmif_size != len(narc):
        raise BuildError("generated NARC has an invalid GMIF size")
    data_size = gmif_size - 8

    fat_offset = btaf_offset + 12
    previous_end = 0
    for class_id in range(CLASS_COUNT):
        start, end = struct.unpack_from("<II", narc, fat_offset + class_id * 8)
        if start != previous_end or end - start != RECORD_SIZE or end > data_size:
            raise BuildError(f"generated NARC member {class_id} has invalid bounds")
        previous_end = end
    if previous_end != data_size:
        raise BuildError("generated NARC contains unindexed GMIF data")


def resolve_paths(args: argparse.Namespace) -> tuple[Path, Path]:
    rom_contents = Path(args.rom_contents).expanduser().resolve()

    if not rom_contents.is_dir():
        raise BuildError(f"ROM contents directory does not exist: {rom_contents}")
    output = rom_contents / OUTPUT_RELATIVE_PATH
    if not output.is_file():
        raise BuildError(f"existing a155 NARC is missing: {output}")

    return rom_contents, output


def build(rom_contents: Path) -> bytes:
    arm9 = read_required_file(rom_contents, "arm9/arm9.bin")
    ov12 = read_required_file(rom_contents, "arm9_overlays/ov012.bin")
    ov80 = read_required_file(rom_contents, "arm9_overlays/ov080.bin")
    ov115 = read_required_file(rom_contents, "arm9_overlays/ov115.bin")
    ov117 = read_required_file(rom_contents, "arm9_overlays/ov117.bin")

    records = build_records(arm9, ov12, ov80, ov115, ov117)
    if len(records) != CLASS_COUNT:
        raise BuildError(f"generated {len(records)} records, expected {CLASS_COUNT}")
    narc = pack_narc(records)
    validate_narc(narc)
    return narc


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Build a 129-member trainer-class metadata NARC from near-vanilla "
            "Pokemon HeartGold or SoulSilver USA DSPRE-unpacked ROM contents. Repointed, "
            "expanded, post-TCM, and non-US-HGSS inputs are unsupported."
        ),
        epilog=(
            "Example: python build_trainer_class_metadata_narc.py "
            "--rom-contents PATH_TO_UNPACKED_HGSS_USA"
        ),
    )
    parser.add_argument(
        "--rom-contents",
        required=True,
        help="DSPRE-unpacked HeartGold or SoulSilver USA ROM-contents directory",
    )
    args = parser.parse_args()

    try:
        rom_contents, output = resolve_paths(args)
        narc = build(rom_contents)
        output.write_bytes(narc)
    except (BuildError, OSError) as exc:
        parser.error(str(exc))

    digest = hashlib.sha256(narc).hexdigest()
    print(f"output={output}")
    print(f"members={CLASS_COUNT}")
    print(f"bytes={len(narc)}")
    print(f"sha256={digest}")


if __name__ == "__main__":
    main()
