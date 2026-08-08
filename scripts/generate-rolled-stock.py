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
    python scripts/generate-rolled-stock.py
"""

import collections
import json
import os



# convert-shape.py is not importable by name (its filename has a hyphen), so mirror the one texture mapping
# these two shapes need. Both bases use `iron5` only.
IRON5 = "game:block/metal/sheet-plain/iron5"

EDITABLE = "assets/editable/shapes"
OUT_DIR = "assets/iwex/shapes/forming"

STAGES = [3.0, 2.0, 1.5, 1.0, 0.5]

# form -> base editable shape + the numbers in StockForm.cs. Kept in step by RolledStockStagesTests, which
# measures the emitted shapes against the simulation's own model.
FORMS = {
    #          base shape              t0   w0   maxWidth  spreadExponent
    "bloom": ("item-shingledbloom", 3.0, 3.0, 8.0, 0.846),
    "slab": ("item-shingledslab", 3.0, 8.0, 14.0, 0.463),
}


def scale_element(el, t_ratio, w_scale, l_scale, cx):
    """Scale one cuboid: Y anchored at 0, X about the piece centre, Z about 0 (the shapes are Z-symmetric)."""
    for key in ("from", "to"):
        x, y, z = el[key]
        el[key] = [
            round(cx + (x - cx) * w_scale, 4),
            round(y * t_ratio, 4),
            round(z * l_scale, 4),
        ]
    if "rotationOrigin" in el:
        x, y, z = el["rotationOrigin"]
        el["rotationOrigin"] = [
            round(cx + (x - cx) * w_scale, 4),
            round(y * t_ratio, 4),
            round(z * l_scale, 4),
        ]
    for child in el.get("children", []):
        scale_element(child, t_ratio, w_scale, l_scale, cx)


def centre_x(elements):
    lo, hi = float("inf"), float("-inf")
    def walk(els):
        nonlocal lo, hi
        for e in els:
            if "from" in e:
                lo = min(lo, e["from"][0], e["to"][0])
                hi = max(hi, e["from"][0], e["to"][0])
            walk(e.get("children", []))
    walk(elements)
    return (lo + hi) / 2.0


def generate(form, base_name, base_thickness, base_width, max_width, exponent):
    src = os.path.join(EDITABLE, base_name + ".json")
    with open(src, encoding="utf-8") as fh:
        base = json.load(fh, object_pairs_hook=collections.OrderedDict)

    cx = centre_x(base["elements"])
    for thickness in STAGES:
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
        dest = os.path.join(OUT_DIR, f"stock-{form}-{stage}.json")
        os.makedirs(OUT_DIR, exist_ok=True)
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
    for form, args in FORMS.items():
        generate(form, *args)
