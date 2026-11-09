"""HeartGold/SoulSilver Pokedex pages, from the pokeheartgold ov18 load code. Main is the touch screen."""
from dsl import *

TOUCH = 4          # FONT_TOUCH
PAGES = []
SHUCKLE = 213


def hg_page(pid, name, main, sub, variants=()):
    main["Palettes"] = [pal(2)] + main["Palettes"]
    sub["Palettes"] = [pal(18)] + sub["Palettes"]
    return page(pid, name, main, sub, variants, main_on_top=False)


def wtext(kind, wx, wy, ww, wh, ix, iy, line=-1, bgn=1, align="left", row=0, ink=2, shadow=1, font=0, keep=False,
          when=None, **kw):
    """Text in a window at tile (wx, wy), placed ix, iy inside it."""
    return txt(kind, wx * 8 + ix, wy * 8 + iy, line=line, bgn=bgn, align=align, row=row, ink=ink, shadow=shadow,
               font=font, win=(wx, wy, ww, wh), keep=keep, when=when, **kw)


def wmsg(line, wx, wy, ww, wh, ix, iy, **kw):
    return wtext("message", wx, wy, ww, wh, ix, iy, line=line, **kw)


# ── cover ───────────────────────────────────────────────────────────────────────────────────────
COVER_VARIANTS = ["Johto", "National", "No National Dex"]
PAGES.append(hg_page("cover", "Cover", variants=COVER_VARIANTS, main=screen(
    bgs=[
        bg(0, 2, 1, [piece(0), piece(7, 5, 3, when="Johto|No National Dex"), piece(8, 5, 3, when="National"),
                     fill(0x16, 3, 15, 26, 2, when="No National Dex")], mw=64),
        bg(2, 1, 4, [piece(5, 0, 19)]),
        bg(3, 3, 3, [piece(10, 0, 5 * k) for k in range(5)]),
    ],
    sprites=[spr("Dex switch", 13, 12, 14, 23, 15, 144, 128, row=1, when="Johto"),
             spr("Dex switch", 13, 12, 14, 23, 15, 112, 128, row=1, when="National")],
    texts=[wmsg(0, 2, 9, 12, 2, 96, 0, bgn=0, align="right", row=9, ink=15, shadow=8, keep=True),
           wmsg(1, 18, 9, 12, 2, 96, 0, bgn=0, align="right", row=9, ink=15, shadow=8, keep=True),
           wtext("count", 11, 11, 3, 2, 0, 4, bgn=0, row=9, ink=15, shadow=8, sample=342, keep=True),
           wtext("count", 27, 11, 3, 2, 0, 4, bgn=0, row=9, ink=15, shadow=8, sample=210, keep=True)],
), sub=screen(
    bgs=[bg(0, 0, 17, [piece(16)], mw=64), bg(3, 3, 19, [piece(20)])],
)))

# ── list ────────────────────────────────────────────────────────────────────────────────────────
# A grid of five species a row, the sample's row first; numbers are written into the background bands.
FIRST = SHUCKLE - (SHUCKLE - 1) % 5
band_fills, icons = [], []
for b in range(5):
    for j in range(5):
        n = FIRST + 5 * b + j
        digits = f"{n:03d}"
        for d in range(3):
            band_fills.append(fill((3 + int(digits[d])) | (1 << 12), 5 + 5 * j + d, 1 + 5 * b, 1, 1))
        band_fills.append(fill(2 | (1 << 12), 4 + 5 * j, 1 + 5 * b, 1, 1))
for k in range(30):
    icons.append(mon("icon", 48 + 40 * (k % 5), 24 + 40 * (k // 5), prio=3, offset=FIRST + k - SHUCKLE))
cursor = (SHUCKLE - FIRST)
LIST_VARIANTS = ["Entry", "Foreign entry", "No data"]
type_icons = [spr("First type", 33, 36, 34, 0, 35, 168, 72, prio=3, tslot=1, tdraw=36, when="Entry"),
              spr("Second type", 33, 36, 34, 0, 35, 217, 72, prio=3, tslot=2, tdraw=36, when="Entry")]
PAGES.append(hg_page("list", "List", variants=LIST_VARIANTS, main=screen(
    bgs=[
        bg(0, 2, 1, [piece(0)], mw=64, sx=256),
        bg(1, 0, -1, []),
        bg(2, 1, 4, [piece(6, 0, 19), piece(9, 0, 15),
                     piece(6, 8, 19, fx=8, w=16, pal=11, when="No data")]),
        bg(3, 3, 3, [piece(10, 0, 5 * k) for k in range(5)] + band_fills),
    ],
    sprites=[spr("Grid cursor", 13, 12, 14, 1, 15, 48 + 40 * cursor, 24, prio=3),
             spr("Scroll up", 13, 12, 14, 5, 15, 242, 12, prio=3, row=1),
             spr("Scroll down", 13, 12, 14, 8, 15, 242, 140, prio=3, row=1),
             spr("Scroll bar", 13, 12, 14, 4, 15, 242, 76, prio=3, row=1),
             spr("Scroll thumb", 13, 12, 14, 11, 15, 242, 40, prio=3, row=1)],
    texts=[wtext("name", 10, 16, 11, 2, 44, 0, align="centre", when="Entry|Foreign entry")],
    mons=icons,
), sub=screen(
    bgs=[
        bg(0, 0, 17, [piece(16)], mw=64, sx=256),
        bg(1, 1, -1, []),
        bg(3, 3, 19, [piece(20, when="Entry"), piece(21, when="Foreign entry"), piece(22, when="No data")]),
    ],
    sprites=[spr("Caught mark", 30, 29, 31, 0, 32, 112, 32, prio=3, when="Entry|Foreign entry")] + type_icons,
    texts=[
        wmsg(8, 2, 0, 8, 2, 0, 0, row=2, font=TOUCH, when="Entry|Foreign entry"),
        wtext("number", 15, 3, 4, 2, 1, 0, font=TOUCH, when="Entry|Foreign entry"),
        wtext("name", 19, 3, 9, 2, 0, 0, font=TOUCH, when="Entry|Foreign entry"),
        wtext("category", 13, 5, 18, 2, 140, 0, align="right", font=TOUCH, when="Entry"),
        wtext("entry", 2, 17, 28, 6, 112, 0, align="block", row=1, when="Entry"),
        wmsg(10, 18, 11, 5, 2, 20, 0, align="centre", row=1, when="Entry"),
        wtext("height", 23, 11, 8, 2, 4, 0, row=1, when="Entry"),
        wmsg(11, 18, 13, 5, 2, 20, 0, align="centre", row=1, when="Entry"),
        wtext("weight", 23, 13, 8, 2, 4, 0, row=1, when="Entry"),
        # The foreign view, shown in French: heading, number, name, category and entry.
        wmsg(124, 16, 8, 14, 2, 56, 0, align="centre", row=1, when="Foreign entry"),
        wmsg(9, 16, 11, 3, 2, 0, 0, row=1, args=[f"{SHUCKLE:03d}"], when="Foreign entry"),
        wtext("fname", 20, 11, 8, 2, 0, 0, line=0, row=1, when="Foreign entry"),
        wtext("fcategory", 15, 13, 16, 2, 124, 0, line=0, align="right", row=1, when="Foreign entry"),
        wtext("fentry", 2, 17, 28, 6, 112, 0, line=0, align="block", row=1, when="Foreign entry"),
    ],
    mons=[mon("front", 48, 72, prio=3, when="Entry|Foreign entry"),
          mon("footprint", 120, 80, prio=3, when="Entry")],
)))

# ── search ──────────────────────────────────────────────────────────────────────────────────────
def search_top(line2, line1=12, emblem=True):
    """The top screen during a search: the emblem over the frame and a two-line message box."""
    return screen(
        bgs=[bg(1, 1, -1, []), bg(2, 2, 58, [piece(60)]), bg(3, 3, 58, [piece(59)])],
        sprites=[spr("Search emblem", 24, 23, 25, 0, 32, 128, 80, prio=3)] if emblem else [],
        texts=[wmsg(line1, 2, 19, 28, 5, 112, 6, align="centre", row=2),
               wmsg(line2, 2, 19, 28, 5, 112, 22, align="centre", row=2)],
    )


def option_page(art, texts, sprites=(), frame=70):
    """An option page on the touch screen: its art on BG2 and the OK / CANCEL frame on BG1."""
    return screen(
        bgs=[bg(0, 1, -1, []), bg(1, 0, 4, [piece(frame, 0, 20)]), bg(2, 2, 58, art), bg(3, 3, 3, [])],
        sprites=list(sprites),
        texts=texts + ([wmsg(39, 1, 21, 8, 2, 32, 4, align="centre", row=4, font=TOUCH, keep=True),
                        wmsg(40, 23, 21, 8, 2, 32, 4, align="centre", row=4, font=TOUCH, keep=True)]
                       if frame == 70 else []),
    )


def value(line, wx, wy, ww, ix, **kw):
    return wmsg(line, wx, wy, ww, 2, ix, 0, bgn=0, align="centre", row=2, font=TOUCH, **kw)


# The ends of the height and weight search sliders, read from the slider's steps.
LAST_STEP = 152


def step_value(kind, wx, wy, ww, step):
    return wtext(kind, wx, wy, ww, 2, ww * 4, 0, bgn=0, align="centre", row=2, font=TOUCH, sample=step)


menu_texts = [
    wmsg(26, 4, 1, 9, 2, 36, 0, bgn=0, align="centre", row=2), wmsg(27, 1, 4, 5, 2, 20, 0, bgn=0, align="centre", row=2),
    wmsg(28, 1, 7, 5, 2, 20, 0, bgn=0, align="centre", row=2), wmsg(29, 1, 10, 5, 2, 20, 0, bgn=0, align="centre", row=2),
    wmsg(30, 1, 13, 5, 2, 20, 0, bgn=0, align="centre", row=2), wmsg(31, 1, 16, 5, 2, 20, 0, bgn=0, align="centre", row=2),
    wmsg(32, 25, 4, 6, 2, 24, 0, bgn=0, align="centre", row=2),
    wmsg(35, 1, 21, 8, 2, 32, 4, align="centre", row=4, font=TOUCH, keep=True),
    wmsg(36, 12, 21, 8, 2, 32, 4, align="centre", row=4, font=TOUCH, keep=True),
    wmsg(37, 23, 21, 8, 2, 32, 4, align="centre", row=4, font=TOUCH, keep=True),
    value(41, 16, 1, 11, 44),                 # order: Numerical
    value(113, 7, 4, 9, 32),                  # first letter: all
    value(64, 7, 7, 8, 29), value(64, 15, 7, 8, 29),   # types: all
    value(68, 7, 16, 7, 28),                  # area: all
    step_value("search_height", 7, 10, 6, 0), step_value("search_height", 15, 10, 6, LAST_STEP),
    step_value("search_weight", 7, 13, 9, 0), step_value("search_weight", 18, 13, 9, LAST_STEP),
]
PAGES.append(hg_page("search_menu", "Search", main=option_page([piece(57)], menu_texts, frame=69),
                     sub=search_top(13)))

ORDER_CELLS = [(3, 6), (18, 6), (3, 10), (18, 10), (3, 14), (18, 14)]
order_texts = [wmsg(26, 3, 1, 11, 2, 44, 0, bgn=0, align="centre", row=2), value(41, 18, 1, 11, 44)]
for k, (wx, wy) in enumerate(ORDER_CELLS):
    order_texts.append(wmsg(41 + k, wx, wy, 11, 2, 44, 0, bgn=0, align="centre", row=2, font=TOUCH))
# The chosen option's button is switched to palette 3; a new search starts from Numerical, no type and no area.
def chosen(screen, tx, ty, w):
    return piece(screen, tx, ty, fx=tx, fy=ty, w=w, h=2, pal=3)


PAGES.append(hg_page("search_order", "Search: order", main=option_page([piece(62), chosen(62, 3, 6, 11)], order_texts),
                     sub=search_top(16)))

name_texts = [wmsg(27, 7, 1, 6, 2, 24, 0, bgn=0, align="centre", row=2), value(113, 15, 1, 9, 36)]
for k in range(27):
    cell = k if k < 26 else 27
    line = 69 + k if k < 26 else 113
    name_texts.append(txt("message", 8 + (cell % 7) * 32 + 24, 40 + (cell // 7) * 32, line=line, bgn=0, align="centre",
                          row=2, font=TOUCH, win=(1, 5, 29, 14) if k == 0 else None))
PAGES.append(hg_page("search_name", "Search: name",
                     main=option_page([piece(63), piece(63, 27, 17, fx=27, fy=17, w=2, h=2, pal=3)], name_texts),
                     sub=search_top(17)))

TYPE_LINES = [58, 50, 60, 56, 54, 48, 63, 53, 59, 61, 62, 51, 55, 49, 52, 57, 47, 64]
type_texts = [wmsg(28, 3, 1, 7, 2, 28, 0, bgn=0, align="centre", row=2), value(64, 13, 1, 8, 29), value(64, 21, 1, 8, 29)]
for k, line in enumerate(TYPE_LINES):
    wx, wy = (k % 4) * 8, 5 + (k // 4) * 3
    type_texts.append(wmsg(line, wx, wy, 8, 2, 32, 0, bgn=0, align="centre", row=2, font=TOUCH))
PAGES.append(hg_page("search_type", "Search: type", main=option_page([piece(64), chosen(64, 8, 17, 8)], type_texts), sub=search_top(18)))


# The search starts from the whole range: the top knob (highest) at the last step on the right, the bottom one
# (lowest) at step 0 on the left, each with its outer arrow switched off. Each knob carries
# five digit sprites (pokeheartgold ov18_021F37D4).


def slider_sprites(max_seq, min_seq, readout):
    digits = [spr("Highest digit", 13, 12, 14, 0, 15, 204, 64, readout=readout, step=LAST_STEP, place=k) for k in range(5)] +              [spr("Lowest digit", 13, 12, 14, 0, 15, 52, 136, readout=readout, step=0, place=k) for k in range(5)]
    return digits + [spr("Arrow", 13, 12, 14, 56, 15, 18, 60, row=1), spr("Arrow", 13, 12, 14, 55, 15, 238, 60, row=1),
                     spr("Arrow", 13, 12, 14, 58, 15, 18, 132, row=1), spr("Arrow", 13, 12, 14, 53, 15, 238, 132, row=1),
                     spr("Highest", 13, 12, 14, max_seq, 15, 204, 68, row=1),
                     spr("Lowest", 13, 12, 14, min_seq, 15, 52, 132, row=1)]


PAGES.append(hg_page("search_height", "Search: height", main=option_page(
    [piece(65)], [wmsg(29, 3, 1, 5, 2, 20, 0, bgn=0, align="centre", row=2),
                  step_value("search_height", 10, 1, 7, 0), step_value("search_height", 19, 1, 7, LAST_STEP)],
    slider_sprites(67, 68, "height")),
    sub=search_top(20)))
PAGES.append(hg_page("search_weight", "Search: weight", main=option_page(
    [piece(66)], [wmsg(30, 1, 1, 6, 2, 24, 0, bgn=0, align="centre", row=2),
                  step_value("search_weight", 8, 1, 9, 0), step_value("search_weight", 19, 1, 9, LAST_STEP)],
    slider_sprites(41, 42, "weight")),
    sub=search_top(19)))
PAGES.append(hg_page("search_area", "Search: area", variants=["National Dex", "No National Dex"], main=option_page(
    [piece(67), chosen(67, 19, 15, 7)], [wmsg(31, 7, 1, 6, 2, 24, 0, bgn=0, align="centre", row=2), value(68, 16, 1, 7, 28),
                  wmsg(65, 6, 8, 7, 2, 28, 0, bgn=0, align="centre", row=2, font=TOUCH),
                  wmsg(66, 19, 8, 7, 2, 28, 0, bgn=0, align="centre", row=2, font=TOUCH, when="National Dex"),
                  wmsg(67, 6, 15, 7, 2, 28, 0, bgn=0, align="centre", row=2, font=TOUCH),
                  wmsg(68, 19, 15, 7, 2, 28, 0, bgn=0, align="centre", row=2, font=TOUCH)]),
    sub=search_top(22)))
shapes = [spr("Body shape", 73, 72, 74, k, 75, 32 + 48 * (k % 5), 56 + 40 * (k // 5)) for k in range(14)]
PAGES.append(hg_page("search_bodyshape", "Search: form", main=option_page(
    [piece(68)], [wmsg(32, 8, 1, 6, 3, 24, 0, bgn=0, align="centre", row=2)], shapes), sub=search_top(21)))
PAGES.append(hg_page("search_running", "Searching", variants=["Searching", "Not found"],
                     main=option_page([piece(57)], [], frame=69),
                     sub=screen(
                         bgs=[bg(1, 1, -1, []), bg(2, 2, 58, [piece(60)]), bg(3, 3, 58, [piece(59)])],
                         sprites=[spr("Search emblem", 24, 23, 25, 2, 32, 128, 80, prio=3)],
                         texts=[wmsg(12, 2, 19, 28, 5, 112, 6, align="centre", row=2, when="Searching"),
                                wmsg(25, 2, 19, 28, 5, 112, 22, align="centre", row=2, when="Searching"),
                                wmsg(23, 2, 19, 28, 5, 112, 6, align="centre", row=2, when="Not found"),
                                wmsg(24, 2, 19, 28, 5, 112, 22, align="centre", row=2, when="Not found")])))

# ── search results: the list's grid with the results bar, and on top either the criteria or the entry ─────
import copy
_list = next(p for p in PAGES if p["Id"] == "list")
res_main = copy.deepcopy(_list["Main"])
res_main["Palettes"] = []
res_main["Bgs"] = [b for b in res_main["Bgs"] if b["Bg"] != 0]
for b in res_main["Bgs"]:
    if b["Bg"] == 2:
        b["Pieces"] = [piece(71, 0, 19), piece(9, 0, 15)]
for t in res_main["Texts"]:
    t["When"] = "Entry"
res_sub = copy.deepcopy(_list["Sub"])
res_sub["Palettes"] = []
res_sub["Bgs"] = [b for b in res_sub["Bgs"] if b["Bg"] != 0]
for b in res_sub["Bgs"]:
    if b["Bg"] == 3:
        b["Pieces"] = [piece(20)]
        b["When"] = "Entry"
for group in ("Sprites", "Texts", "Mons"):
    res_sub[group] = [item for item in res_sub[group] if "Entry" in (item["When"] or "").split("|")]
    for item in res_sub[group]:
        item["When"] = "Entry"
# The criteria view: a copy of the search menu's frame (rows 0-18) over the top screen's frame.
res_sub["Bgs"].append(bg(2, 2, 58, [piece(60), piece(57, h=19)], when="Criteria"))
res_sub["Bgs"].append(bg(3, 3, 58, [piece(59)], when="Criteria"))
crit = [wmsg(14, 2, 19, 28, 5, 112, 6, align="centre", row=2, when="Criteria"),
        wmsg(15, 2, 19, 28, 5, 112, 22, align="centre", row=2, args=["25"], when="Criteria")]
for line, (wx, wy, ww, ix) in zip(range(26, 33), [(4, 1, 9, 36), (1, 4, 5, 20), (1, 7, 5, 20), (1, 10, 5, 20),
                                                  (1, 13, 5, 20), (1, 16, 5, 20), (25, 4, 6, 24)]):
    crit.append(wmsg(line, wx, wy, ww, 2, ix, 0, align="centre", row=2, when="Criteria"))
for line, (wx, wy, ww, ix) in [(41, (16, 1, 11, 44)), (113, (7, 4, 9, 32)), (64, (7, 7, 8, 29)), (64, (15, 7, 8, 29)),
                               (68, (7, 16, 7, 28))]:
    crit.append(wmsg(line, wx, wy, ww, 2, ix, 0, align="centre", row=2, font=TOUCH, when="Criteria"))
res_sub["Texts"] = crit + res_sub["Texts"]
PAGES.append(hg_page("search_results", "Search results", variants=["Criteria", "Entry"], main=res_main, sub=res_sub))

# ── detail pages: the tab bar on the touch screen, the page's art on both screens ──────────────
def tab_bar(tab):
    """BG1's detail tab bar with the chosen tab lit (0 area, 1 size, 2 forms, 3 back)."""
    return bg(1, 0, 4, [piece(11, 0, 20)]), spr("Selected tab", 13, 12, 14, 3, 15, 32 + 64 * tab, 176, row=4)


def title(line):
    return wmsg(line, 2, 0, 9, 2, 0, 0, row=2, font=TOUCH)


# AREA: the places come from the encounter data; the sample player's face is on New Bark Town.
tabs, lit = tab_bar(0)
AREA_TIMES = [("Morning", 129), ("Day", 130), ("Night", 131)]
area_texts = [title(142), wtext("name", 0, 16, 9, 2, 36, 0, align="centre", row=1),
              wmsg(132, 1, 5, 6, 2, 24, 0, align="centre", row=1, ink=5, shadow=9),
              wmsg(65, 9, 1, 7, 2, 28, 0, align="centre", row=1, ink=15, shadow=12, font=TOUCH, when="Johto|Kanto"),
              wmsg(66, 24, 1, 7, 2, 28, 0, align="centre", row=1, ink=15, shadow=12, font=TOUCH, when="Johto|Kanto"),
              wmsg(65, 18, 1, 7, 2, 28, 0, align="centre", row=1, ink=5, shadow=9, when="No National Dex"),
              wmsg(128, 13, 12, 14, 2, 56, 0, align="centre", row=1, when="Area unknown")]
area_texts.append(wmsg(130, 1, 3, 7, 2, 28, 0, align="centre", row=1))
PAGES.append(hg_page("area", "Area", variants=["Johto", "Kanto", "No National Dex", "Area unknown"], main=screen(
    bgs=[bg(0, 1, 83, [piece(81, when="Johto|Kanto|Area unknown"), piece(80, when="No National Dex")]), tabs,
         bg(2, 2, -1, []), bg(3, 3, 83, [piece(82)])],
    sprites=[lit, spr("Time of day", 13, 12, 14, 26, 15, 32, 108, row=3),
             spr("Time cursor", 13, 12, 14, 27, 15, 32, 108),
             spr("Scroll up", 13, 12, 14, 5, 15, 242, 44, row=1), spr("Scroll down", 13, 12, 14, 8, 15, 242, 148, row=1),
             spr("Scroll bar", 13, 12, 14, 63, 15, 242, 96, row=1), spr("Scroll thumb", 13, 12, 14, 14, 15, 242, 96, row=1)],
    texts=[],
), sub=screen(
    bgs=[bg(0, 3, 87, [piece(86)], mw=64, sx=-72, when="Johto|No National Dex|Area unknown"),
         bg(0, 3, 87, [piece(86)], mw=64, sx=104, when="Kanto"),
         bg(1, 0, -1, []),
         bg(3, 2, 85, [piece(84), piece(88, 10, 11, when="Area unknown")])],
    texts=[],
    # The places pulse in and out over the map; shown at their brightest (alpha 15 of 31).
    habitat={"Bg": -1, "Cells": 120, "Drawing": 119, "Anim": 121, "Colours": 122, "Absolute": True, "Priority": 3,
             "Blend": 15, "BlendUnder": 16, "Face": 116, "FaceDrawing": 115, "FaceAnim": 117, "FaceColours": 118},
    mons=[mon("icon", 36, 112, prio=0)],
)))
# Split the area texts by screen: the title, time, name and unknown label are on top.
_area = PAGES[-1]
_area["Sub"]["Texts"] = [area_texts[0], area_texts[1], area_texts[6], area_texts[7]]
_area["Main"]["Texts"] = area_texts[2:6] + [
    wtext("place", 10, 5 + 3 * k, 18, 2, 72, 0, line=k, bgn=2, align="centre", row=1, ink=15, shadow=12, font=TOUCH,
          when="Johto|Kanto|No National Dex") for k in range(6)]

tabs, lit = tab_bar(1)
PAGES.append(hg_page("size", "Size", variants=["Male player", "Female player"], main=screen(
    bgs=[tabs, bg(3, 3, 90, [piece(89)])],
    sprites=[lit, spr("Dial centre", 13, 12, 14, 33, 15, 128, 104, prio=1, row=1),
             spr("Left knob", 13, 12, 14, 32, 15, 72, 104, prio=1, row=1, size="pokemon_pan"),
             spr("Right knob", 13, 12, 14, 32, 15, 184, 104, prio=1, row=1, size="trainer_pan"),
             spr("Dial", 13, 12, 14, 31, 15, 128, 104, prio=1, row=1, size="beam"),
             spr("Player", 106, 105, 107, 0, 108, 184, 88, size="trainer_on_pan", when="Male player"),
             spr("Player", 106, 109, 107, 0, 110, 184, 88, size="trainer_on_pan", when="Female player")],
    texts=[wmsg(137, 10, 1, 12, 2, 48, 0, align="centre", row=1, ink=5, shadow=9),
           wmsg(11, 2, 17, 4, 2, 16, 0, align="centre", row=1, ink=15, shadow=5),
           wtext("weight", 7, 17, 8, 2, 32, 0, align="centre", row=1, ink=15, shadow=5),
           wmsg(11, 17, 17, 4, 2, 16, 0, align="centre", row=1, ink=15, shadow=5),
           wmsg(140, 22, 17, 8, 2, 32, 0, align="centre", row=1, ink=15, shadow=5, when="Male player"),
           wmsg(141, 22, 17, 8, 2, 32, 0, align="centre", row=1, ink=15, shadow=5, when="Female player")],
    mons=[mon("icon", 72, 88, prio=0, size="pokemon_on_pan")],
), sub=screen(
    bgs=[bg(1, 1, -1, []), bg(2, 3, 92, [piece(91)])],
    sprites=[spr("Player silhouette", 112, 111, 113, 0, 108, 184, 112, prio=1, size="trainer", when="Male player"),
             spr("Player silhouette", 112, 114, 113, 0, 110, 184, 112, prio=1, size="trainer", when="Female player")],
    texts=[title(143), wtext("name", 4, 3, 8, 2, 32, 0, align="centre", row=1),
           wtext("player", 20, 3, 8, 2, 32, 0, align="centre", row=1),
           wmsg(136, 10, 6, 12, 2, 48, 0, align="centre", row=1),
           wmsg(10, 2, 21, 4, 2, 16, 0, align="centre", row=2),
           wtext("height", 7, 21, 8, 2, 32, 0, align="centre", row=2),
           wmsg(10, 17, 21, 4, 2, 16, 0, align="centre", row=2),
           wmsg(138, 22, 21, 8, 2, 32, 0, align="centre", row=2, when="Male player"),
           wmsg(139, 22, 21, 8, 2, 32, 0, align="centre", row=2, when="Female player")],
    # The game writes the picture into an affine background in one colour; shown here as a silhouette.
    mons=[mon("front", 72, 112, prio=2, darken=16, size="pokemon")],
)))

tabs, lit = tab_bar(2)
PAGES.append(hg_page("forms", "Forms", variants=["One form", "Several forms"], main=screen(
    bgs=[bg(0, 1, 93, [piece(95), piece(100, 16, 16, when="One form")]), tabs, bg(2, 2, -1, []),
         bg(3, 3, 93, [piece(96)])],
    sprites=[lit, spr("Scroll up", 13, 12, 14, 7, 15, 242, 12, row=1), spr("Scroll down", 13, 12, 14, 10, 15, 242, 116, row=1),
             spr("Scroll bar", 13, 12, 14, 63, 15, 242, 64, row=1), spr("Scroll thumb", 13, 12, 14, 14, 15, 242, 64, row=1)],
    texts=[wmsg(168, 21, 17, 10, 2, 0, 0, row=1, ink=15, shadow=12, font=TOUCH, when="Several forms"),
           wtext("name", 8, 7, 18, 2, 0, 0, bgn=2, row=1, ink=15, shadow=12, font=TOUCH)],
    mons=[mon("icon", 48, 62, prio=0)],
), sub=screen(
    bgs=[bg(1, 1, -1, []), bg(3, 3, 94, [piece(97)])],
    texts=[title(170), wmsg(167, 4, 5, 9, 2, 72, 0, align="right", row=1, args=None)],
    mons=[mon("front", 64, 120, prio=2)],
)))
for t in PAGES[-1]["Sub"]["Texts"]:
    if t["Kind"] == 0 and t["Line"] == 167:
        t.update(txt("name", t["X"], t["Y"], align="right", row=1, win=(4, 5, 9, 2)))

PAGES.append(hg_page("forms_compare", "Forms: compare", main=screen(
    bgs=[bg(1, 0, -1, []), bg(3, 3, 93, [piece(98)])],
    texts=[wmsg(170, 13, 2, 6, 2, 24, 0, align="centre", row=1, ink=5, shadow=9),
           wmsg(171, 3, 20, 12, 3, 48, 4, align="centre", row=1, ink=15, shadow=12, font=TOUCH),
           wmsg(172, 18, 20, 12, 3, 48, 4, align="centre", row=1, ink=15, shadow=12, font=TOUCH)],
), sub=screen(
    bgs=[bg(1, 1, -1, []), bg(3, 3, 94, [piece(99)])],
    texts=[title(170)],
)))

tabs, lit = tab_bar(3)
PAGES.append(hg_page("back", "Back to the list", main=screen(
    bgs=[tabs, bg(3, 3, 102, [piece(101)])],
    sprites=[lit],
    texts=[wmsg(174, 10, 10, 15, 2, 60, 0, align="centre", row=1, ink=15, shadow=12, font=TOUCH)],
), sub=screen(
    bgs=[bg(1, 1, -1, []), bg(2, 3, 104, [piece(103)])],
    texts=[title(173)],
)))

# Registration after a catch: a separate screen laid out like the list's entry.
reg = page("catch_registration", "Registered after a catch", main=screen(
    bgs=[bg(1, 1, -1, []), bg(2, 2, 19, [piece(20)])],
    pals=[pal(18)],
    sprites=[spr("Caught mark", 30, 29, 31, 0, 32, 112, 32),
             spr("First type", 33, 36, 34, 0, 35, 168, 72, prio=0, tslot=1, tdraw=36),
             spr("Second type", 33, 36, 34, 0, 35, 217, 72, prio=0, tslot=2, tdraw=36)],
    texts=[wmsg(144, 2, 0, 28, 2, 0, 0, row=2, font=TOUCH),
           wtext("number", 15, 3, 4, 2, 1, 0, font=TOUCH), wtext("name", 19, 3, 9, 2, 0, 0, font=TOUCH),
           wtext("category", 13, 5, 18, 2, 140, 0, align="right", font=TOUCH),
           wtext("entry", 2, 17, 28, 6, 112, 0, align="block", row=1),
           wmsg(10, 18, 11, 5, 2, 20, 0, align="centre", row=1), wtext("height", 23, 11, 8, 2, 4, 0, row=1),
           wmsg(11, 18, 13, 5, 2, 20, 0, align="centre", row=1), wtext("weight", 23, 13, 8, 2, 4, 0, row=1)],
    mons=[mon("front", 48, 72), mon("footprint", 120, 80)],
), sub=screen())
PAGES.append(reg)
