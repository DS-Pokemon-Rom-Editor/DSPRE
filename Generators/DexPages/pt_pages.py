"""Platinum Pokedex pages, from the pokeplatinum ov21 load code."""
from dsl import *

SUB_FONT = 2
PAGES = []

# ── list ────────────────────────────────────────────────────────────────────────────────────────
PLATE_X = [185, 181, 177, 175, 170, 175, 177, 181, 185]
PLATE_Y = [22, 26, 42, 58, 82, 106, 122, 138, 142]
PLATE_ROW = [{0: 0, 1: 7, 2: 8}.get(abs(4 - i), 9) for i in range(9)]
plates, plate_text = [], []
for i in range(9):
    plates.append(spr(f"List row {i + 1}", 79, 81, 80, 0, 3, PLATE_X[i], PLATE_Y[i], prio=2, row=PLATE_ROW[i]))
    # FontOAM window 15x2 tiles at plate (-64,-8); number at window x22, name at x49.
    wx, wy = PLATE_X[i] - 64, PLATE_Y[i] - 8
    plate_text.append(txt("number", wx + 22, wy, offset=i - 4, font=SUB_FONT, ink=3, shadow=2,
                          colours=3, row=PLATE_ROW[i], prio=2))
    plate_text.append(txt("name", wx + 49, wy, offset=i - 4, font=SUB_FONT, ink=3, shadow=2,
                          colours=3, row=PLATE_ROW[i], prio=2))
# The game orders the plates by distance from the centre (32 + 2*|4-row|), the caught mark just in front.
order = sorted(range(9), key=lambda i: abs(4 - i))
plates = [plates[i] for i in order]
plates.insert(0, spr("Caught mark", 79, 81, 80, 1, 3, PLATE_X[4] - 54, PLATE_Y[4], prio=2))
plates.append(spr("Scroll marker", 79, 81, 80, 2, 3, 248, 85, prio=2))
for k, t in enumerate(plate_text):
    t["Above"] = 1 + order.index(k // 2)

LIST_VARIANTS = ["Sinnoh", "National", "Search results"]


def button_label(line, x, y, w_tiles, h_tiles, row, when=None):
    """FontOAM label printed at 0,0 of its w by h tile window (PokedexTextManager_DisplayMessage), on buttons_NCLR."""
    return msg(line, x, y, align="left", font=SUB_FONT, ink=3, shadow=2,
               colours=3, row=row, when=when)


PAGES.append(page("list", "List", variants=LIST_VARIANTS, main=screen(
    bgs=[
        bg(3, 3, 28, [piece(38), piece(39, 1, 4, pal=1)]),
        # Shutter copy: rows 29-31 to the top, rows 0-13 from row 18 down.
        bg(2, 1, 28, [piece(40, 0, 0, fy=29, h=3, when="Sinnoh|Search results"),
                      piece(40, 0, 18, fy=0, h=14, when="Sinnoh|Search results"),
                      piece(42, 0, 0, fy=29, h=3, when="National"),
                      piece(42, 0, 18, fy=0, h=14, when="National")]),
        bg(1, 0, -1, []),
    ],
    pals=[pal(0), pal(4, rows=1, when="Search results")],
    sprites=plates,
    texts=[
        msg(0, 8, 152, when="Sinnoh|National"), msg(109, 8, 152, when="Search results"),
        msg(1, 128, 152, when="Sinnoh|National"),
        txt("count", 48, 170, sample=342), txt("count", 180, 170, sample=210, when="Sinnoh|National"),
    ] + plate_text,
    mons=[mon("front", 56, 80)],
), sub=screen(
    bgs=[
        bg(2, 2, 29, [piece(41)]),
        bg(3, 3, 27, [piece(37)], bpp=8, sx=-120),
    ],
    pals=[pal(0), pal(25, to=3, rows=1, when="National"), pal(1, to=3, rows=1, when="Search results")],
    sprites=[
        spr("Search", 82, 84, 83, 2, 3, 48, 40, when="Sinnoh|National"),
        spr("Switch", 82, 84, 83, 0, 3, 48, 88, when="Sinnoh|National"),
        spr("Check", 82, 84, 83, 1, 3, 48, 152),
        spr("Scroll up", 82, 84, 83, 3, 3, 124, 64),
        spr("Scroll down", 82, 84, 83, 4, 3, 124, 146),
        spr("Quit", 82, 84, 83, 5, 3, 124, 8),
    ],
    texts=[
        button_label(7, 8, 26, 10, 4, 3, when="Sinnoh|National"),
        button_label(5, 8, 74, 10, 4, 3, when="Sinnoh|National"),
        button_label(29, 8, 138, 10, 4, 2),
        button_label(107, 134, 0, 14, 2, 1, when="Sinnoh|National"),
        button_label(108, 134, 0, 14, 2, 1, when="Search results"),
    ],
)))

# ── search ──────────────────────────────────────────────────────────────────────────────────────
# Methods: (label line, description line, highlight screen, tile y, button y)
METHODS = {"order": (50, 90, 44, 6, 48), "name": (47, 87, 45, 9, 80), "type": (48, 88, 46, 12, 112),
           "form": (49, 89, 47, 17, 144)}
GRID = [(48, 16), (128, 16), (48, 48), (128, 48), (48, 80), (128, 80), (48, 112), (128, 112), (48, 144), (128, 144)]


def search_page(pid, name, method, options, selected, extra_sprites=(), grid=GRID, labels=True):
    label, describe, hi_screen, hi_y, _ = METHODS[method]
    sub_sprites = [spr("Back", 126, 125, 124, 3, 3, 212, 16)]
    sub_texts = []
    for m, (mlabel, _, _, _, my) in METHODS.items():
        active = m == method
        sub_sprites.append(spr(m.capitalize(), 126, 125, 124, 2, 3, 224, my, frame=3 if active else 0))
        sub_texts.append(msg(mlabel, 224, my - (8 if active else 6), align="centre", font=SUB_FONT, ink=3, shadow=2,
                             colours=3, row=3))
    sub_sprites.append(spr("OK", 126, 125, 124, 1, 3, 212, 176))
    sub_texts.append(msg(51, 212, 176 - 6, align="centre", font=SUB_FONT, ink=3, shadow=2, colours=3, row=1))
    for k, opt in enumerate(options):
        x, y = grid[k]
        chosen = k == selected
        if isinstance(opt, tuple):      # (base seq, icon cells, icon drawing, icon anim, icon seq or None)
            base, icon = opt
            sub_sprites.append(spr(f"Option {k + 1}", 126, 125, 124, base, 3, x, y, frame=3 if chosen else 0))
            if icon is not None:
                sub_sprites.insert(len(sub_sprites) - 1, spr(f"Option {k + 1} icon", 129, 128, 127, icon, 3, x, y))
        else:
            sub_sprites.append(spr(f"Option {k + 1}", 126, 125, 124, 0, 3, x, y, frame=3 if chosen else 0))
            if labels:
                sub_texts.append(msg(opt, x, y - (8 if chosen else 6), align="centre", font=SUB_FONT, ink=3, shadow=2,
                                     colours=3, row=11))
    sub_sprites += list(extra_sprites)
    main_texts = [
        msg(describe, 128, 8, align="centre", box=32, row=7, when="Normal"),
        msg(93, 128, 8, align="centre", box=32, row=7, when="None found"),
        msg(81, 128, 52, align="centre", row=7, ink=4, shadow=3),     # sort order: Numerical
        msg(126, 128, 77, align="centre", row=7, ink=4, shadow=3),    # name filter: none
        msg(53, 128, 102, align="centre", row=7, ink=4, shadow=3),    # type filters: none
        msg(53, 128, 120, align="centre", row=7, ink=4, shadow=3),
    ]
    return page(pid, name, variants=["Normal", "None found"], main=screen(
        bgs=[bg(3, 3, 28, [piece(43), piece(hi_screen, 6, hi_y)]), bg(1, 1, -1, [])],
        pals=[pal(0), pal(5, rows=1)],
        texts=main_texts,
    ), sub=screen(
        bgs=[bg(2, 2, 29, [piece(75)])],
        pals=[pal(0)],
        sprites=sub_sprites,
        texts=sub_texts,
    ))


PAGES.append(search_page("search_order", "Search: order", "order", [81, 82, 83, 84, 85, 86], 0))
PAGES.append(search_page("search_name", "Search: name", "name", [54, 55, 56, 57, 58, 59, 60, 61, 62, 126], 9))
PAGES.append(search_page("search_type", "Search: type", "type", [64, 70, 73, 71, 72, 76, 75, 77, 80, 127], 9,
                         extra_sprites=[spr("Next types", 126, 125, 124, 5, 3, 24, 176)]))
type2 = [65, 66, 68, 67, 74, 69, 78, 79]
grid2 = GRID[:8] + [GRID[9]]
PAGES.append(search_page("search_type_2", "Search: type (second page)", "type", type2 + [127], 8, grid=grid2,
                         extra_sprites=[spr("Previous types", 126, 125, 124, 4, 3, 24, 176)]))
FORM_GRID = [(28, 16), (84, 16), (140, 16), (28, 48), (84, 48), (140, 48), (28, 80), (84, 80), (140, 80),
             (28, 112), (84, 112), (140, 112), (28, 144), (84, 144), (140, 144)]
FORM_ICONS = [0, 5, 10, 1, 6, 11, 2, 9, 12, 3, 8, 13, 4, 7, None]
PAGES.append(search_page("search_form", "Search: form", "form", [(6, i) for i in FORM_ICONS], 14, grid=FORM_GRID,
                         labels=False))

# ── searching ───────────────────────────────────────────────────────────────────────────────────
PAGES.append(page("search_running", "Searching", variants=["Sinnoh", "National"], main=screen(
    bgs=[
        # The shutter closed: rows 0-11 from source rows 20-31, rows 12-23 from source rows 0-11.
        bg(2, 1, 28, [piece(40, 0, 0, fy=20, h=12, when="Sinnoh"), piece(40, 0, 12, fy=0, h=12, when="Sinnoh"),
                      piece(42, 0, 0, fy=20, h=12, when="National"), piece(42, 0, 12, fy=0, h=12, when="National")]),
        bg(3, 3, 28, [piece(43)]),
        bg(1, 0, -1, []),
    ],
    pals=[pal(0), pal(5, rows=1)],
    sprites=[spr("Spinning ball", 79, 81, 80, 17, 3, 128, 96, prio=2)],
    texts=[msg(94, 128, 128, align="centre", row=7)],
), sub=screen(bgs=[bg(2, 2, 29, [piece(75)])], pals=[pal(0)], darken=16)))

# ── entry pages ─────────────────────────────────────────────────────────────────────────────────
ENTRY_VARIANTS = ["Sinnoh", "National"]
PAGE_BUTTON_X = [28, 68, 108, 148, 188, 228]


def entry_frame(index):
    """Banner, page title and page buttons every entry page shares; index is the page's button."""
    main = screen(
        bgs=[bg(2, 1, 33, [piece(57)]), bg(1, 0, -1, [])],
        pals=[pal(6), pal(24, rows=1, when="National")],
        texts=[msg(102 + index, 16, 0, font=SUB_FONT, ink=3, shadow=2, colours=12, prio=0)],
    )
    buttons = [spr(["Info", "Area", "Cry", "Size", "Forms", "Back"][k], 94, 96, 95, k, 11, PAGE_BUTTON_X[k], 24,
                   frame=3 if k == index else 0) for k in range(6)]
    sub = screen(bgs=[bg(1, 0, 34, [piece(59)])], pals=[pal(6), pal(24, rows=1, when="National")], sprites=buttons)
    return main, sub


def label(line, x, y, colours, row, align="centre", when=None):
    return msg(line, x, y, align=align, font=SUB_FONT, ink=3, shadow=2, colours=colours, row=row, when=when)


def name_tag_and_category():
    sprites = [spr("Name tag", 76, 78, 77, 0, 3, 172, 32), spr("Caught mark", 76, 78, 77, 1, 3, 118, 32),
               spr("Category box", 88, 90, 89, 17, 13, 192, 52),
               spr("First type", 88, 90, 89, 0, 13, 170, 72, tslot=1),
               spr("Second type", 88, 90, 89, 0, 13, 220, 72, tslot=2)]
    texts = [txt("number", 130, 24, font=SUB_FONT, ink=3, shadow=2, colours=3, above=0),
             txt("name", 157, 24, font=SUB_FONT, ink=3, shadow=2, colours=3, above=0),
             txt("category", 182, 44, align="centre", font=SUB_FONT, ink=3, shadow=2, colours=3, above=2)]
    return sprites, texts


def info_text(row=0):
    return [msg(9, 152, 88, row=row), msg(10, 152, 104, row=row), txt("height", 184, 88, row=row),
            txt("weight", 184, 104, row=row), txt("entry", 128, 136, align="block", row=row)]


INFO_BG3 = [piece(50), piece(51, 0, 3), piece(52, 12, 8), piece(54, 0, 16)]
fm, fs = entry_frame(0)
tag_sprites, tag_texts = name_tag_and_category()
PAGES.append(page("info", "Info", variants=ENTRY_VARIANTS, main=merge(fm, screen(
    bgs=[bg(3, 3, 33, INFO_BG3)],
    sprites=tag_sprites,
    texts=tag_texts + info_text(),
    mons=[mon("front", 48, 72), mon("footprint", 120, 88, prio=0)],
)), sub=merge(fs, screen(
    bgs=[bg(2, 2, 34, [piece(62), piece(60, 6, 14), piece(63, 6, 7)])],
    pals=[pal(9, to=4, rows=1), pal(9, to=5, rows=1)],
    sprites=[spr("Next", 100, 102, 101, 0, 11, 128, 132, prio=2), spr("Back", 100, 102, 101, 0, 11, 128, 76, prio=2),
             spr("Next arrow", 97, 99, 98, 13, 11, 160, 134), spr("Back arrow", 97, 99, 98, 12, 11, 96, 78)],
    texts=[label(33, 96, 126, 11, 4, align="left"), label(34, 160, 70, 11, 4, align="right")],
))))

# INFO in another language: the same frame, a foreign heading, name with number, category and entry.
LANGUAGES = [("French", 23), ("German", 24), ("Italian", 25), ("Spanish", 26), ("Japanese", 125)]
LANG_SEQ = [2, 4, 6, 8, 10, 0]          # own language, then French to Japanese
fm, fs = entry_frame(0)
tag_sprites, tag_texts = name_tag_and_category()
foreign_texts, language_buttons = [], []
for k, (lang, heading) in enumerate(LANGUAGES):
    foreign_texts += [msg(heading, 176, 72, align="centre", when=lang),
                      txt("fname", 120, 96, line=k, when=lang),
                      txt("fcategory", 240, 112, line=k, align="right", when=lang),
                      txt("fentry", 128, 136, line=k, align="block", when=lang)]
for b in range(6):
    for k, (lang, _) in enumerate(LANGUAGES):
        language_buttons.append(spr("Language", 97, 99, 98, LANG_SEQ[b] + (1 if b == k + 1 else 0), 11,
                                    PAGE_BUTTON_X[b], 176, when=lang))
PAGES.append(page("info_foreign", "Info in another language", variants=[l[0] for l in LANGUAGES], main=merge(fm, screen(
    bgs=[bg(3, 3, 33, [piece(50), piece(51, 0, 3), piece(55, 0, 16), piece(56, 12, 8)])],
    sprites=tag_sprites[:3],
    texts=tag_texts + foreign_texts,
    mons=[mon("front", 48, 72)],
)), sub=merge(fs, screen(
    bgs=[bg(2, 2, 34, [piece(62), piece(60, 6, 14), piece(63, 6, 7)])],
    pals=[pal(9, to=4, rows=1), pal(9, to=5, rows=1)],
    sprites=[spr("Next", 100, 102, 101, 0, 11, 128, 132, prio=2), spr("Back", 100, 102, 101, 0, 11, 128, 76, prio=2),
             spr("Next arrow", 97, 99, 98, 13, 11, 160, 134), spr("Back arrow", 97, 99, 98, 12, 11, 96, 78)]
            + language_buttons,
    texts=[label(33, 96, 126, 11, 4, align="left"), label(34, 160, 70, 11, 4, align="right")],
))))

# AREA: the habitat dots and dungeon markers come from the encounter archive (DexHabitatBuilder).
fm, fs = entry_frame(1)
TIMES = [("Morning", 15, 36, 32, 128), ("Day", 16, 37, 128, 96), ("Night", 17, 38, 224, 128)]
area_sub_sprites, area_sub_pals, area_main_texts = [], [], []
for k, (tname, nclr, line, ix, iy) in enumerate(TIMES):
    area_sub_sprites.append(spr("Time of day", 103, 105, 104, k, 11, ix, iy, prio=1, when=tname))
    area_sub_pals.append(pal(nclr, to=2, rows=1, when=tname))
    area_main_texts.append(label(line, 44, 32, 14, 2, when=tname))
PAGES.append(page("area", "Area", variants=[t[0] for t in TIMES] + ["Area unknown"], main=merge(fm, screen(
    bgs=[bg(3, 3, 33, [piece(64)])],
    sprites=[spr("Area unknown", 91, 93, 92, 2, 14, 160, 96, prio=1, when="Area unknown")],
    texts=area_main_texts + [label(35, 160, 88, 14, 2, when="Area unknown"),
                             label(37, 44, 32, 14, 2, when="Area unknown")],
    mons=[mon("front", 40, 120)],
    habitat=True,
)), sub=merge(fs, screen(
    bgs=[bg(2, 2, 34, [piece(69)])],
    pals=area_sub_pals + [pal(16, to=2, rows=1, when="Area unknown")],
    sprites=area_sub_sprites + [spr("Time of day", 103, 105, 104, 1, 11, 128, 96, prio=1, when="Area unknown")],
))))

fm, fs = entry_frame(2)
# The waveform at rest: a one-pixel line across the window on BG1, colour 6 of row 9 (pokeplatinum ov21_021E4ABC).
PAGES.append(page("cry", "Cry", variants=ENTRY_VARIANTS, main=merge(fm, screen(
    bgs=[bg(3, 3, 33, [piece(70)])],
    mons=[mon("front", 48, 64)],
    rects=[rect(1, 0, 151, 256, 1, 9, 6)],
)), sub=merge(fs, screen(
    bgs=[bg(2, 2, 34, [piece(71)]), bg(3, 3, 35, [piece(72)], bpp=8, sx=-48, sy=-16)],
    pals=[pal(20, to=7, frm=1, rows=1)],
    sprites=[spr("Chorus and pan switch", 114, 113, 112, 4, 18, 64, 67, prio=2, frame=8),
             spr("Dial", 114, 113, 112, 1, 18, 51, 157, prio=2),
             spr("Play", 114, 113, 112, 3, 18, 180, 131, prio=2, frame=5),
             spr("Loop", 114, 113, 112, 6, 18, 230, 166, prio=2, frame=5)],
    texts=[label(41, 64, 84, 18, 0)],
))))


def size_sub():
    return screen(
        bgs=[bg(2, 2, 34, [piece(61)])],
        pals=[pal(9, to=4, rows=1), pal(9, to=5, rows=1)],
        texts=[label(45, 68, 112, 11, 4), label(46, 188, 112, 11, 4)],
    )


fm, fs = entry_frame(3)
PAGES.append(page("size_height", "Size: height", variants=["Lucas", "Dawn"], main=merge(fm, screen(
    bgs=[bg(3, 3, 33, [piece(74)])],
    sprites=[spr("Trainer", 91, 93, 92, 5, 14, 168, 88, prio=1, size="trainer", when="Lucas"),
             spr("Trainer", 91, 93, 92, 6, 14, 168, 88, prio=1, size="trainer", when="Dawn")],
    texts=[msg(43, 128, 24, align="centre"), msg(9, 32, 168), msg(9, 152, 168), txt("name", 26, 152),
           txt("height", 110, 168, align="right"), msg(95, 230, 168, align="right", when="Lucas"),
           msg(96, 230, 168, align="right", when="Dawn")],
    mons=[mon("front", 88, 88, darken=15, size="pokemon")],
)), sub=merge(fs, size_sub())))

fm, fs = entry_frame(3)
PAGES.append(page("size_weight", "Size: weight", variants=["Lucas", "Dawn"], main=merge(fm, screen(
    bgs=[bg(3, 3, 33, [piece(73)])],
    sprites=[spr("Trainer", 91, 93, 92, 3, 14, 184, 64, prio=3, size="trainer_on_pan", when="Lucas"),
             spr("Trainer", 91, 93, 92, 4, 14, 184, 64, prio=3, size="trainer_on_pan", when="Dawn"),
             spr("Stand", 91, 93, 92, 1, 14, 128, 106, prio=1),
             spr("Pan", 91, 93, 92, 0, 14, 184, 88, prio=3, size="trainer_pan"),
             spr("Pan", 91, 93, 92, 0, 14, 72, 88, prio=3, size="pokemon_pan"),
             spr("Beam", -1, 36, -1, 0, 6, 64, 88, prio=2, w=128, h=16, size="beam")],
    texts=[msg(44, 128, 24, align="centre"), msg(10, 32, 168), msg(10, 152, 168), txt("name", 26, 152),
           txt("weight", 110, 168, align="right"), msg(97, 230, 168, align="right", when="Lucas"),
           msg(98, 230, 168, align="right", when="Dawn")],
    mons=[mon("icon", 72, 64, prio=3, size="pokemon_on_pan")],
)), sub=merge(fs, size_sub())))

fm, fs = entry_frame(4)
PAGES.append(page("forms", "Forms", variants=ENTRY_VARIANTS, main=merge(fm, screen(
    bgs=[bg(3, 3, 33, [piece(50)])],
    sprites=[spr("Front box", 123, 122, 121, 0, 22, 76, 96, prio=3),
             spr("Back box", 123, 122, 121, 2, 22, 180, 96, prio=3)],
    mons=[mon("front", 76, 88), mon("back", 180, 88)],
)), sub=merge(fs, screen(
    bgs=[bg(2, 2, 34, [piece(58)])],
    pals=[pal(9, to=4, rows=1)],
    texts=[label(101, 128, 136, 11, 4)],
))))

tag_sprites, tag_texts = name_tag_and_category()
PAGES.append(page("catch_registered", "Registered after a catch", variants=["Sinnoh", "National"], main=screen(
    bgs=[bg(3, 3, 33, INFO_BG3), bg(2, 2, 33, [piece(57)]), bg(1, 1, -1, [])],
    pals=[pal(6), pal(23, rows=1, when="Sinnoh"), pal(24, rows=1, when="National")],
    sprites=[dict(sp, Priority=2) for sp in tag_sprites],
    texts=[dict(t, Priority=2) for t in tag_texts] + [msg(110, 32, 0, row=12, ink=3, shadow=4)] + info_text(row=12),
    mons=[mon("front", 48, 72, prio=0), mon("footprint", 120, 88, prio=2)],
), sub=screen()))
