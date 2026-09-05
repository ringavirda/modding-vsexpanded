#!/usr/bin/env python3
"""Generate the staged shapes for stock walking a rolling-mill schedule.

The maintainer authors ONE shape per stock form (`item-shingledbloom`, `item-shingledslab` - the piece as it
leaves the helve hammer). Every thinner stage is that same piece flattened, so the stages are derived rather
than drawn: each is the base with its thickness taken down to a gap and its width spread accordingly.

Scaling, per element:
  thickness (Y)  exact - anchored at y=0 so the piece stays sitting on the same plane
  width (X)      (t0/t) ** e about the piece's centre, CAPPED at the form's max width. Mirrors
                 RollingPass.SpreadWidth / StockForm in the simulation, and it is the one that carries a
                 mechanic: once the piece is wider than the roll barrel it needs side-by-side passes, so the
                 art and the pass count move together.
  length (Z)     whatever volume conservation demands, given the (possibly capped) width. NOT capped - a
                 fully-rolled piece really is long, and an oversized held item is fine (vanilla has the player
                 carrying 3x3 gate blocks). Once width hits its ceiling, every further reduction runs out
                 lengthways, which is why the last stage jumps.

The exponent differs per form because spread falls as stock gets wider relative to its thickness: friction
across a wide face resists sideways flow, while a narrow bar has nothing holding it in. So a bloom (entering
square) spreads hard and reaches nearly its full width by the 1-voxel gap - one voxel thick and about eight
wide being the proportion of a vanilla plate, which is what it gets cut into - while a slab mostly draws out.

UVs are left alone - the plain iron texture stretches without reading wrong.

Usage:
    python infra/tools/generate-rolled-stock.py [--out DIR] [form ...]

With no form names every form is regenerated. Name the ones you mean when only some bases have moved -
`--out` into a scratch directory is how a change to this script is checked against the shipped art.
"""

import collections
import json
import os



# convert-shape.py is not importable by name (its filename has a hyphen), so mirror the one texture mapping
# these two shapes need. Both bases use `iron5` only.
IRON5 = "game:block/metal/sheet-plain/iron5"

EDITABLE = "workbench/shapes"
OUT_DIR = "mods/iiex/assets/iiex/shapes/forming"

# The gaps every form is drawn at. A form is also drawn at its own base thickness, which is not in this
# list for anything entering thicker than 3 - the cast bloom and slab leave the long cell at 4.
STAGES = [3.0, 2.0, 1.5, 1.0, 0.5]


def stages_for(base_thickness):
    """A form's own base, then every gap below it. A gap at or above the base is not a stage the piece can
    be rolled to, so drawing one would be art for a state that cannot exist."""
    return [base_thickness] + [s for s in STAGES if s < base_thickness]

# form -> base editable shape, the element in it that IS the piece, and the numbers in StockForm.cs. Kept in
# step by RolledStockStagesTests, which measures the emitted shapes against the simulation's own model. The
# element name matters because an editable is often a family file holding every stage of two routes: taking
# the whole file would scale nine drawn stages at once. None means the file is one piece.
#
# The two shingled paths were stale until 2026-08-14 - both bases had moved into items/smithed/ and become
# family files, so this script had been unrunnable while looking runnable. Their stages ship correct and
# were generated before the move, so pass a form name to regenerate only what you mean to.
FORMS = {
    #                base shape                          element          t0   w0   maxWidth exponent
    "shingledbar": ("items/smithed/item-shingled-bar", "ShingledBar1", 3.0, 3.0, 8.0, 0.846),
    "shingledslab": ("items/smithed/item-shingled-slab", "ShingledSlab1", 3.0, 8.0, 14.0, 1.0),
    # The cast three. Sections are stock.md's settled ones and the exponents fall straight out of the crop
    # table: a billet drawn 3 wide must reach 9 at the 1.0 gap and a bloom 4 must reach 8 at 2.0, both of
    # which are exactly e = 1; a slab spreads 12 -> 15 across a halving, which is log2(1.25).
    "castbillet": ("items/sandcast/stock/item-sandcast-castbillet", None, 3.0, 3.0, 9.0, 1.0),
    "castbloom": ("items/sandcast/stock/item-sandcast-castbloom", None, 4.0, 4.0, 8.0, 1.0),
    "castslab": ("items/sandcast/stock/item-sandcast-castslab", None, 4.0, 12.0, 15.0, 0.3219),
    # The rod is the one form that does not leave a hammer: it enters the mill at 2.0 as vanilla's
    # `game:rod-iron`, which is 2 x 2 x 10 exactly. e = 1 is read straight off the drawn flat branch -
    # 2 wide at 2.0 becoming 4 wide at 1.0 - and 4 is also its ceiling, the narrow barrel's own width,
    # which is what keeps the fork at one pass per gap down both branches.
    "rod": ("items/rolled/item-rolled-rod", "RolledRod200", 2.0, 2.0, 4.0, 1.0),
    # The beam re-enters the mill the way the rod does: a shingled bar taken flat to 2.0 IS a beam, and
    # rolling it on is a second route rather than more of the bar's. Its ceiling is 9, the width of the
    # vanilla plate it ends as.
    "beam": ("items/rolled/item-rolled-beam", "Beam", 2.0, 4.5, 9.0, 1.0),
    # Two off a shingled slab, and the wide barrel's 15 is its ceiling - the width a boiler plate is.
    "heavyplate": ("items/rolled/item-rolled-heavyplate", "HeavyPlate1", 2.0, 12.0, 15.0, 1.0),
}


def scale_element(el, t_ratio, w_scale, l_scale, cx, base=(0.0, 0.0, 0.0), moved=(0.0, 0.0, 0.0)):
    """Scale one cuboid and its children: Y anchored at 0, X about the piece centre, Z about 0.

    ⛔ A child's `from`/`to` are offsets from its PARENT's `from`, not absolute coordinates. Scaling those
    offsets about an absolute centre stretches the child away from the parent it hangs off - it made a
    regenerated slab 10.89 wide where 9.65 was intended, and it stayed invisible only while every base
    shape was a single childless cuboid. So each element is lifted into absolute space with `base` (the
    parent's original absolute `from`), scaled there, and put back relative to `moved` (the parent's scaled
    absolute `from`).
    """
    def to_abs(p):
        return [p[i] + base[i] for i in range(3)]

    def scaled(p):
        x, y, z = p
        return [
            round(cx + (x - cx) * w_scale, 4),
            round(y * t_ratio, 4),
            round(z * l_scale, 4),
        ]

    abs_from, abs_to = to_abs(el["from"]), to_abs(el["to"])
    new_from, new_to = scaled(abs_from), scaled(abs_to)
    el["from"] = [round(new_from[i] - moved[i], 4) for i in range(3)]
    el["to"] = [round(new_to[i] - moved[i], 4) for i in range(3)]
    if "rotationOrigin" in el:
        # Same space as from/to, so it travels with them.
        new_origin = scaled(to_abs(el["rotationOrigin"]))
        el["rotationOrigin"] = [round(new_origin[i] - moved[i], 4) for i in range(3)]

    for child in el.get("children", []):
        scale_element(child, t_ratio, w_scale, l_scale, cx, abs_from, new_from)


def centre_x(elements):
    """The piece's X centre, in absolute space - children are offsets from their parent's `from`, so a
    naive read of a nested cuboid puts the centre somewhere the piece is not."""
    lo, hi = float("inf"), float("-inf")
    def walk(els, ox=0.0):
        nonlocal lo, hi
        for e in els:
            if "from" in e:
                a, b = e["from"][0] + ox, e["to"][0] + ox
                lo, hi = min(lo, a, b), max(hi, a, b)
                walk(e.get("children", []), a)
            else:
                walk(e.get("children", []), ox)
    walk(elements)
    return (lo + hi) / 2.0


def generate(
    form, base_name, element, base_thickness, base_width, max_width, exponent, out_dir=None
):
    src = os.path.join(EDITABLE, base_name + ".json")
    if not os.path.exists(src):
        raise SystemExit(
            f"{form}: base shape {src} does not exist. The editables move; fix FORMS rather than "
            f"letting this generate nothing."
        )
    with open(src, encoding="utf-8") as fh:
        base = json.load(fh, object_pairs_hook=collections.OrderedDict)

    if element is not None:
        picked = [e for e in base["elements"] if e.get("name") == element]
        if not picked:
            raise SystemExit(
                f"{form}: no element named '{element}' in {src} - a family file holds every stage, so "
                f"the one that IS the piece has to be named."
            )
        base["elements"] = picked

    out_dir = out_dir or OUT_DIR
    cx = centre_x(base["elements"])
    for thickness in stages_for(base_thickness):
        d = json.loads(json.dumps(base), object_pairs_hook=collections.OrderedDict)
        d.pop("editor", None)
        d.pop("textureSizes", None)
        d["textures"] = {k: IRON5 for k in (d.get("textures") or {})}

        r = thickness / base_thickness
        width = min(max_width, base_width * (1.0 / r) ** exponent)
        w_scale = width / base_width
        # Volume conservation off the ACTUAL width, so a capped piece puts the rest into length.
        l_scale = (1.0 / r) / w_scale
        for el in d["elements"]:
            scale_element(el, r, w_scale, l_scale, cx)

        stage = str(int(round(thickness * 10)))
        dest = os.path.join(out_dir, f"stock-{form}-{stage}.json")
        os.makedirs(out_dir, exist_ok=True)
        with open(dest, "w", encoding="utf-8") as fh:
            json.dump(d, fh, indent=1, ensure_ascii=False)
            fh.write("\n")
        print(
            "%-6s t=%-4s width %5.2f%-9s length x%.2f  -> %s"
            % (
                form,
                thickness,
                width,
                "  (capped)" if width >= max_width - 1e-6 and thickness < base_thickness else "",
                l_scale,
                dest,
            )
        )


if __name__ == "__main__":
    import sys

    args = sys.argv[1:]
    out_dir = None
    if len(args) >= 2 and args[0] == "--out":
        out_dir = args[1]
        args = args[2:]

    wanted = args or list(FORMS)
    unknown = [f for f in wanted if f not in FORMS]
    if unknown:
        raise SystemExit(f"unknown form(s) {unknown}; known: {list(FORMS)}")

    for form in wanted:
        generate(form, *FORMS[form], out_dir=out_dir)
