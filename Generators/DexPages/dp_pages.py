"""Diamond/Pearl Pokedex pages: the same archive members and layout as Platinum, with DP's
own message lines and the few positions that differ."""
import copy
from dsl import *
import pt_pages

# DP's bank 614 lacks Platinum's line 100 ('---') and its form names (115-124), so lines 101-114 sit one lower and
# the last three follow straight after.
LINE = {125: 114, 126: 115, 127: 116}


def dp_line(line):
    if line in LINE:
        return LINE[line]
    if line == 100 or 115 <= line <= 124:
        return -1
    return line - 1 if line > 100 else line


# pokediamond overlay 16: the foreign heading is centred on 184, the name (which carries its number) starts at 128,
# and the category ends at 242.
FOREIGN_X = {KIND["message"]: 184, KIND["fname"]: 128, KIND["fcategory"]: 242}

PAGES = copy.deepcopy(pt_pages.PAGES)
for p in PAGES:
    for side in ("Main", "Sub"):
        for t in p[side]["Texts"]:
            if t["Kind"] == 0 and t["Line"] >= 0:
                t["Line"] = dp_line(t["Line"])
    if p["Id"] == "info_foreign":
        for t in p["Main"]["Texts"]:
            if t["When"] and t["Kind"] in FOREIGN_X:
                t["X"] = FOREIGN_X[t["Kind"]]
    if p["Id"] == "forms":
        for s in p["Main"]["Sprites"]:
            s["X"] = {76: 72, 180: 184}.get(s["X"], s["X"])
        for m in p["Main"]["Mons"]:
            m["X"] = {76: 72, 180: 184}.get(m["X"], m["X"])
