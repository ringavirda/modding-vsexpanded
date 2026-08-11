#!/usr/bin/env python3
"""Convert a hand-authored Blockbench shape in assets/editable/shapes/ into a shipped runtime shape.

The editable files are the maintainer's working copies: they carry Blockbench's `editor` block, an empty
`textureSizes`, and **absolute local texture paths** (F:/repos/.../assets/editable/textures/NAME). A shipped
shape needs none of that and needs its textures pointed at real asset domains instead.

So conversion is exactly three things:
  1. drop `editor` and `textureSizes`
  2. remap every texture value to its domain path (TEXTURES below)
  3. keep everything else verbatim - crucially the `animations` array, which a naive re-export drops
     (that has bitten this repo before)

Loop flags: a clip that spins or cycles forever must end with `Repeat`, or the animator eases it out and an
RCC-suppressed mesh vanishes. The editables all carry Blockbench's `EaseOut` default, so spin clips are
rewritten on the way out: everything becomes Repeat except held poses (HOLD_CLIPS) and genuine one-shot
motions (ONESHOT_CLIPS), which keep their authored ending.

Usage:
    python scripts/convert-shape.py <editable-name> <runtime-path> [<editable-name> <runtime-path> ...]
    python scripts/convert-shape.py --check          # report unmapped textures across all editables

Example:
    python scripts/convert-shape.py mp-castiron-flywheel assets/iwex/shapes/mpenergy/flywheel.json
"""

import collections
import json
import os
import sys

EDITABLE = "assets/editable/shapes"

# Texture key -> domain path. Every entry below is taken from a shape ALREADY SHIPPED in this repo, not
# invented, so a conversion matches the precedent its siblings set. Several keys are ambiguous across the
# codebase (iron3/iron4/iron5 appear as both `sheet-plain` and `riveted`/`sheet`); the value here is the one
# the mpenergy/casting families actually use. Add a key only after checking a shipped sibling.
TEXTURES = {
    "cast-iron1": "iwex:block/metal/castiron",
    "iron": "game:block/metal/tarnished/iron",
    "iron2": "game:block/metal/sheet-plain/iron2",
    "iron3": "game:block/metal/riveted/iron3",
    "iron32": "game:block/metal/riveted/iron3",
    "iron4": "game:block/metal/sheet-plain/iron4",
    "iron5": "game:block/metal/sheet-plain/iron5",
    # Deliberately absent, do NOT add blindly:
    #   iron42 - shipped shapes disagree (sheet-plain/iron4 vs sheet/iron4); it is a second slot the author
    #            points at a different texture per shape, so it must be set by hand after converting a pipe.
    #   lore-marked / scrolls-rotten (design table) - no shipped precedent yet.
    "steel": "game:block/metal/plate/steel",
    # Cast billet/bloom/slab and the heating hearth: those pieces are cast from STEEL, so they wear the
    # steel sheet. Same `sheet-plain` family steel2/3/5 below already resolve to.
    "steel1": "game:block/metal/sheet-plain/steel1",
    "steel2": "game:block/metal/sheet-plain/steel2",
    "steel3": "game:block/metal/sheet-plain/steel3",
    "steel5": "game:block/metal/sheet-plain/steel5",
    "plain": "game:block/leather/plain",
    "generic": "game:block/wood/planks/generic",
    "charred": "game:block/wood/charred",
    "charcoal": "game:block/coal/charcoal",
    # The refractory brick face every furnace part wears. Written at tier3 because that is the tier the
    # editables were drawn against, but on a TIERED block this value never survives: the blocktype's own
    # `textures` map overrides by key, and the furnace parts declare
    # `game:block/clay/refractory/{tier}/front1` so the block wears what it was built from
    # (BlockBlastFurnaceCoreCold.cs:42). The shipped shape's value is the fallback for an untiered consumer.
    "front1": "game:block/clay/refractory/tier3/front1",
    # Fuel in a bed or a band. Same texture the charge pile's `coke` element already ships
    # (BlockChargePile.cs:84); the firebox swaps it per fuel type at runtime off the same key.
    "coke": "game:block/coal/coke",
    "candle": "game:block/candle",
    "andesite": "game:block/stone/sand/andesite",
    "burned": "game:block/clay/vessel/sides/burned",
    "fire1": "game:block/clay/brick/four/running/fire1",
    "granite1": "game:block/stone/cobblestone/granite1",
    "normal1": "game:block/stone/path/normal1",
    "normal4": "game:block/metal/corroded/normal4",
    "other": "game:block/clay/ceramic",
    "paper": "exlib:item/diagram/paper",
    "copper3": "game:block/metal/sheet/copper3",
    "stone": "game:block/coal/orecoalmix",
    # The puddling hearth's fettle layer. Crushed hematite, not a nugget: fettle is ground oxide and can be
    # made from ore, tap cinder or mill scale, so it must read as warm red-brown oxide rather than the cold
    # grey of one specific ore (magnetite's nugget is chromatically neutral - it looks like metal, not rust).
    "fettle": "game:item/resource/crushed/hematite",
}

# Clips that loop forever while a machine runs -> must Repeat.
# Clips that are a held state pose -> Hold (the ore mixer's `open` is the precedent).
# ⚠ `open` was named as the precedent by the line above and was NOT in this set until 2026-08-04, so every
# gate, door and lid whose held state is `open` was exported as Repeat and animated itself shut. Nothing in
# the C# suite can see this: the rewrite happens in the export, and a wrong onAnimationEnd is only visible
# in game. Affects the burdenmaker gate, the puddling door and the coke-oven lid when they land.
HOLD_CLIPS = {
    "connected",
    "disconnected",
    "drill-down-pose",
    "leaverdown",
    "open",
    "steamup",
}

# Clips that must run ONCE and stop, keeping Blockbench's EaseOut. Everything else defaults to Repeat,
# because a spin or a held pose that eases out makes an RCC-suppressed mesh vanish - the scar this whole
# rewrite exists for. But a genuine one-shot motion must NOT loop: the puddling door's `paddle` is a single
# gather-and-withdraw whose END is the event (the player receives the iron ball), so looping it would both
# read wrong and never finish. Add a clip here only when its stop is the thing that matters.
ONESHOT_CLIPS = {"paddle"}
# `idle` keeps the built mesh visible and every shipped idle uses Repeat.


def convert(src_name, dest_path):
    src = os.path.join(EDITABLE, src_name + ".json")
    with open(src, encoding="utf-8") as fh:
        d = json.load(fh, object_pairs_hook=collections.OrderedDict)

    d.pop("editor", None)
    d.pop("textureSizes", None)

    unmapped = []
    for key in list((d.get("textures") or {}).keys()):
        if key in TEXTURES:
            d["textures"][key] = TEXTURES[key]
        else:
            unmapped.append(key)

    for anim in d.get("animations", []):
        name = anim.get("name")
        if name in ONESHOT_CLIPS:
            continue  # runs once and stops; its END is the event, so leave the authored EaseOut alone
        anim["onAnimationEnd"] = "Hold" if name in HOLD_CLIPS else "Repeat"

    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    with open(dest_path, "w", encoding="utf-8") as fh:
        json.dump(d, fh, indent=1, ensure_ascii=False)
        fh.write("\n")

    anims = [a.get("name") for a in d.get("animations", [])]
    print(
        "%-38s -> %-46s elements=%-5d anims=%s"
        % (src_name, dest_path, len(d.get("elements", [])), anims or "-")
    )
    if unmapped:
        print("    !! UNMAPPED TEXTURES %s - add them to TEXTURES first" % unmapped)
    return not unmapped


def check_all():
    missing = collections.Counter()
    for f in sorted(os.listdir(EDITABLE)):
        if not f.endswith(".json"):
            continue
        with open(os.path.join(EDITABLE, f), encoding="utf-8") as fh:
            try:
                d = json.load(fh)
            except Exception:
                continue
        for key in (d.get("textures") or {}):
            if key not in TEXTURES:
                missing[key] += 1
    if missing:
        print("Texture keys with no mapping:")
        for k, c in missing.most_common():
            print("  %-20s used by %d editable shape(s)" % (k, c))
    else:
        print("Every texture key in assets/editable/shapes is mapped.")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args == ["--check"]:
        check_all()
        sys.exit(0)
    if not args or len(args) % 2:
        sys.exit(__doc__)
    ok = all(convert(args[i], args[i + 1]) for i in range(0, len(args), 2))
    sys.exit(0 if ok else 1)
