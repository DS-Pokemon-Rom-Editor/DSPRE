"""Small helpers for writing Pokedex page specs that serialize to the C# ScreenPage model."""

ALIGN = {"left": 0, "centre": 1, "right": 2, "block": 3}
KIND = {"message": 0, "name": 1, "number": 2, "category": 3, "height": 4, "weight": 5, "entry": 6, "count": 7,
        "fentry": 8, "fname": 9, "fcategory": 10, "place": 11, "search_height": 12, "search_weight": 13,
        "player": 14}
READOUT = {None: 0, "height": 1, "weight": 2}
MON = {"front": 0, "footprint": 1, "back": 2, "icon": 3}
SIZE = {None: 0, "pokemon": 1, "trainer": 2, "pokemon_pan": 3, "trainer_pan": 4, "pokemon_on_pan": 5,
        "trainer_on_pan": 6, "beam": 7}


def piece(screen, tx=0, ty=0, fx=0, fy=0, w=-1, h=-1, pal=-1, when=None, fill=-1):
    return {"Screen": screen, "TileX": tx, "TileY": ty, "FromX": fx, "FromY": fy, "Width": w, "Height": h,
            "Palette": pal, "Fill": fill, "When": when}


def fill(entry, tx, ty, w, h, when=None):
    return piece(-1, tx, ty, w=w, h=h, fill=entry, when=when)


def bg(n, prio, drawing, pieces, bpp=4, sx=0, sy=0, mw=32, mh=32, when=None):
    return {"Bg": n, "Priority": prio, "Bpp": bpp, "Drawing": drawing, "MapWidth": mw, "MapHeight": mh,
            "ScrollX": sx, "ScrollY": sy, "Pieces": pieces, "When": when}


def pal(file, to=0, frm=0, rows=16, when=None):
    return {"File": file, "FromRow": frm, "ToRow": to, "Rows": rows, "When": when}


def spr(name, cells, drawing, anim, seq, colours, x, y, prio=0, row=0, tslot=0, tdraw=-1, frame=0, w=0, h=0, size=None,
        readout=None, step=0, place=0, when=None):
    return {"Name": name, "Cells": cells, "Drawing": drawing, "Anim": anim, "Seq": seq, "Frame": frame, "Colours": colours,
            "Width": w, "Height": h, "Size": SIZE[size], "Readout": READOUT[readout], "Step": step, "Place": place,
            "Row": row, "X": x, "Y": y, "Priority": prio, "TypeSlot": tslot, "TypeDrawing": tdraw, "When": when}


def txt(kind, x, y, line=-1, bgn=1, align="left", row=0, ink=2, shadow=1, font=0, sample=0, offset=0,
        colours=-1, prio=0, above=-1, box=0, win=None, keep=False, args=None, when=None):
    wx, wy, ww, wh = win if win else (0, 0, 0, 0)
    return {"Kind": KIND[kind], "Line": line, "Bg": bgn, "X": x, "Y": y, "Align": ALIGN[align], "BoxHeight": box, "Row": row,
            "Ink": ink, "Shadow": shadow, "Font": font, "Sample": sample, "Offset": offset, "Colours": colours,
            "Priority": prio, "Above": above, "WinX": wx, "WinY": wy, "WinW": ww, "WinH": wh, "KeepUnder": keep,
            "Args": args, "When": when}


def msg(line, x, y, **kw):
    return txt("message", x, y, line=line, **kw)


def mon(kind, x, y, prio=2, darken=0, offset=0, size=None, when=None):
    return {"Kind": MON[kind], "X": x, "Y": y, "Priority": prio, "Darken": darken, "Offset": offset, "Size": SIZE[size],
            "When": when}


def screen(bgs=(), pals=(), sprites=(), texts=(), mons=(), rects=(), darken=0, habitat=False):
    return {"Bgs": list(bgs), "Palettes": list(pals), "Sprites": list(sprites), "Texts": list(texts), "Mons": list(mons),
            "Rects": list(rects), "Darken": darken, "Habitat": habitat}


def rect(bgn, x, y, w, h, row, colour, when=None):
    return {"Bg": bgn, "X": x, "Y": y, "Width": w, "Height": h, "Row": row, "Colour": colour, "When": when}


def page(pid, name, main, sub, variants=(), main_on_top=True):
    return {"Id": pid, "Name": name, "Variants": list(variants), "Main": main, "Sub": sub, "MainOnTop": main_on_top}


def merge(base, extra):
    """A page's screen over its shared frame: the page's backgrounds replace the frame's of the same number."""
    out = {k: (list(v) if isinstance(v, list) else v) for k, v in base.items()}
    out["Darken"] = extra.get("Darken", 0) or base.get("Darken", 0)
    out["Habitat"] = extra.get("Habitat", False) or base.get("Habitat", False)
    own = {b["Bg"] for b in extra["Bgs"]}
    out["Bgs"] = [b for b in base["Bgs"] if b["Bg"] not in own] + list(extra["Bgs"])
    for k in ("Palettes", "Sprites", "Texts", "Mons", "Rects"):
        out[k] = list(base.get(k, [])) + list(extra.get(k, []))
    return out
