#!/usr/bin/env python3
"""Convert a hand-authored Blockbench shape in workbench/shapes/ into a shipped runtime shape.

The editable files are the maintainer's working copies: they carry Blockbench's `editor` block, an empty
`textureSizes`, and **absolute local texture paths** (//wsl.localhost/<distro>/.../workbench/textures/NAME). A shipped
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
    python infra/tools/convert-shape.py <editable-name> <runtime-path> [<editable-name> <runtime-path> ...]
    python infra/tools/convert-shape.py --check          # report unmapped textures across all editables
    python infra/tools/convert-shape.py --merge <runtime-path> <editable-name> [<editable-name> ...]

Example:
    python infra/tools/convert-shape.py mp-castiron-flywheel mods/iiex/assets/iiex/shapes/mpenergy/flywheel.json
"""

import collections
import json
import os
import sys

EDITABLE = "workbench/shapes"

# Texture key -> domain path. Every entry below is taken from a shape ALREADY SHIPPED in this repo, not
# invented, so a conversion matches the precedent its siblings set. Several keys are ambiguous across the
# codebase (iron3/iron4/iron5 appear as both `sheet-plain` and `riveted`/`sheet`); the value here is the one
# the mpenergy/casting families actually use. Add a key only after checking a shipped sibling.
TEXTURES = {
    "cast-iron1": "iiex:block/metal/castiron",
    "iron": "game:block/metal/tarnished/iron",
    # Bright ingot iron, for a piece that is meant to read as freshly worked rather than as old stock:
    # the puddled ball and the ball on the paddle's head. A separate key because `iron` is tarnished and
    # a dozen shipped shapes want it that way.
    "ironingot": "game:block/metal/ingot/iron",
    "iron2": "game:block/metal/sheet-plain/iron2",
    "iron3": "game:block/metal/riveted/iron3",
    "iron32": "game:block/metal/riveted/iron3",
    # iron4 is the `sheet` face and iron5 the `sheet-plain` one, uniformly across the suite. Until
    # 2026-08-23 iron4 was mapped to sheet-plain, and because the remap overwrites by KEY it beat whatever
    # the editable declared: the tuyere and the burdenmaker both author `sheet/iron4` and both shipped
    # wearing sheet-plain. Only the shapes converted before this tool existed (the Cornish boiler, the
    # reinforced hopper) still carry the authored value.
    "iron4": "game:block/metal/sheet/iron4",
    "iron5": "game:block/metal/sheet-plain/iron5",
    # The two raw coals a firebox burns. Coke and charcoal were already here; these complete the set so a
    # fuel bed can be authored against the coal it actually holds.
    "bituminous": "game:block/coal/bituminous",
    "anthracite": "game:block/coal/anthracite",
    # Deliberately absent, do NOT add blindly:
    #   iron42 - a second slot the author points at a different texture per shape, so it must be set by
    #            hand after converting a pipe.
    #   lore-marked / scrolls-rotten (design table) - no shipped precedent yet.
    #   steel32 / steel42 - the same second-slot problem as iron42, one tier up.
    # UNDECIDED, surfaced 2026-08-21 when --check stopped scanning only the top directory: `basalt` (17
    # shapes) and `hematite` (1). Neither is deliberate; both want a decision before the shapes that use
    # them are exported. Not added here - a wrong mapping is silent, and this file's own rule is to cite a
    # shipped sibling.
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
    # The blister bars standing in a crucible pot. Vanilla's own ingot texture for the metal, the same
    # family `ironingot` above resolves in.
    "blistersteel": "game:block/metal/ingot/blistersteel",
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
    "steamup",
}

# Every clip whose name ends this way is a held state, whatever it opens: `open`, `lidopen`,
# `mainhatchopen`, `manhatchopen`. A suffix rather than a name list because the exact-match set is what let
# the scar above happen twice - `open` was added by hand in 2026-08-04, and the boiler's `lidopen` and the
# two hatch clips the reworked art introduced would each have needed remembering separately.
HOLD_SUFFIXES = ("open",)


def holds(name):
    """Whether a clip is a held state pose, and so must end on Hold rather than Repeat."""
    return bool(name) and (
        name in HOLD_CLIPS or name.endswith(HOLD_SUFFIXES)
    )

# Clips that must run ONCE and stop, keeping Blockbench's EaseOut. Everything else defaults to Repeat,
# because a spin or a held pose that eases out makes an RCC-suppressed mesh vanish - the scar this whole
# rewrite exists for. But a genuine one-shot motion must NOT loop: the puddling door's `paddle` is a single
# gather-and-withdraw whose END is the event (the player receives the iron ball), so looping it would both
# read wrong and never finish. Add a clip here only when its stop is the thing that matters.
ONESHOT_CLIPS = {"paddle"}
# `idle` keeps the built mesh visible and every shipped idle uses Repeat.


# A rotation this far apart between a clip's first and last keyframe is a whole turn being unwound
# across the wrap, not a pose difference. Matches LoopingAnimationTests.UnwindDegrees.
UNWIND_DEGREES = 180
ROT_AXES = ("X", "Y", "Z")


def close_loop(anim):
    """Makes a repeating clip's wrap explicit, which Blockbench cannot express.

    Two rules, both guarded by LoopingAnimationTests and both invisible outside the game:

      1. Every element the clip touches must be posed at BOTH ends of it, at the same pose. An element
         missing from the last keyframe drifts back to its frame-0 pose across the wrap instead of
         returning through the drawn motion; one missing from the first holds its mid-clip pose from
         the loop point until the animator next reaches it, then jumps - a hitch at the same place
         every cycle. The element's own earliest pose is the one written to both ends: it is the state
         the artist drew it resting at, and for an element already posed at frame 0 that is frame 0's
         pose, which is what this rule has always copied forward.
      2. A shaft that turns a whole revolution reads as `0 -> 360` and the animator unwinds it
         backwards unless the last keyframe carries `rotShortestDistance<Axis>`.
    """
    frames = sorted(
        (kf for kf in anim.get("keyframes", []) if "frame" in kf),
        key=lambda kf: kf["frame"],
    )
    if len(frames) < 2:
        return  # a single held pose has no wrap to close

    first, last = frames[0], frames[-1]
    first_elems = first.setdefault("elements", collections.OrderedDict())
    last_elems = last.setdefault("elements", collections.OrderedDict())

    # The pose each element rests at, taken from the earliest keyframe that names it.
    rest = collections.OrderedDict()
    for kf in frames:
        for element, pose in (kf.get("elements") or {}).items():
            rest.setdefault(element, pose)

    for element, pose in rest.items():
        if element not in first_elems:
            first_elems[element] = collections.OrderedDict(pose)
        if element not in last_elems:
            last_elems[element] = collections.OrderedDict(pose)
            continue
        for axis in ROT_AXES:
            key = "rotation" + axis
            start, end = pose.get(key), last_elems[element].get(key)
            if start is None or end is None:
                continue
            if abs(end - start) >= UNWIND_DEGREES:
                last_elems[element]["rotShortestDistance" + axis] = True


def _translate(el, dx, dy, dz):
    """Shift one element's own coordinates, leaving its children alone.

    A child's `from`/`to` are relative to its PARENT's `from`, so moving a parent moves its whole subtree
    for free. `rotationOrigin` lives in that same parent-relative space and moves with it.
    """
    for key in ("from", "to", "rotationOrigin"):
        v = el.get(key)
        if isinstance(v, list) and len(v) == 3:
            el[key] = [v[0] + dx, v[1] + dy, v[2] + dz]


def unwrap_root(d):
    """Lift the children of a top-level `Root` group to the top, keeping them where they were drawn.

    Blockbench authors often wrap everything in one group for easy handling. The runtime shape must not
    carry it: element paths are what `selectiveElements` and `ExShapeElements.Pruned` match on, so a
    shipped `Root/Base/*` matches nothing against code asking for `Base/*` - and Pruned drops an unknown
    name WITHOUT raising, so the block renders as nothing with every test still green.

    The group's own `from` is folded into each lifted child first. A wrapper is usually at [0,0,0] and the
    fold is a no-op, but it need not be: the reworked Cornish boiler wraps at [16,0,16], and lifting its
    children bare moved the whole machine one cell west and one cell north - silently, since a translated
    model renders perfectly well in the wrong place. Only a top-level group named Root is lifted; anything
    else is the author's own structure and is left alone.
    """
    elements = d.get("elements")
    if not elements:
        return
    lifted = []
    for el in elements:
        if el.get("name") == "Root" and el.get("children"):
            origin = el.get("from") or [0, 0, 0]
            for child in el["children"]:
                _translate(child, origin[0], origin[1], origin[2])
            lifted.extend(el["children"])
        else:
            lifted.append(el)
    d["elements"] = lifted


def convert(src_name, dest_path):
    src = os.path.join(EDITABLE, src_name + ".json")
    with open(src, encoding="utf-8") as fh:
        d = json.load(fh, object_pairs_hook=collections.OrderedDict)

    d.pop("editor", None)
    d.pop("textureSizes", None)
    unwrap_root(d)

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
        if holds(name):
            anim["onAnimationEnd"] = "Hold"
        else:
            anim["onAnimationEnd"] = "Repeat"
            close_loop(anim)

    # Refuse before writing. Until 2026-08-21 the file was written first and the unmapped keys
    # reported after, so a failed run left the destination holding the editable's absolute
    # `F:/...` authoring paths - a runtime shape resolving to nothing, produced by the very tool
    # whose job is to prevent that. It silently replaced a correct shipped shape the first time it
    # was noticed.
    if unmapped:
        print(
            "%-38s -> REFUSED: unmapped textures %s - add them to TEXTURES first"
            % (src_name, unmapped)
        )
        return False

    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    with open(dest_path, "w", encoding="utf-8") as fh:
        json.dump(d, fh, indent=1, ensure_ascii=False)
        fh.write("\n")

    anims = [a.get("name") for a in d.get("animations", [])]
    print(
        "%-38s -> %-46s elements=%-5d anims=%s"
        % (src_name, dest_path, len(d.get("elements", [])), anims or "-")
    )
    return True


def check_all():
    """Reports every texture key no mapping covers.

    Walks the tree. It listed only the top level until 2026-08-21, and the editables had long since been
    filed into subdirectories - so it was scanning 0 of 155 shapes and reporting that everything was
    mapped. A guard that answers green because it is not looking is worse than none.
    """
    missing = collections.Counter()
    for f in sorted(
        os.path.join(root, name)
        for root, _, names in os.walk(EDITABLE)
        for name in names
    ):
        if not f.endswith(".json"):
            continue
        with open(f, encoding="utf-8") as fh:
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
        print("Every texture key in workbench/shapes is mapped.")


def geometry_of(el):
    """An element's boxes, name and nesting - everything a merge must agree on, and nothing it need not.
    UVs and texture keys are deliberately excluded: the same piece re-drawn against another atlas is the
    same piece."""
    return (
        el.get("name"),
        el.get("from"),
        el.get("to"),
        el.get("rotationOrigin"),
        [geometry_of(c) for c in el.get("children") or []],
    )


def merge(dest_path, src_names):
    """Convert several editables into ONE runtime shape, taking the union of their top-level elements.

    A stock family's stages have to live in one file: a process route names a single `shape`, and the
    renderer addresses a stage as one element inside it. The maintainer authors them across several
    editables (the shingled bar's stages above the 2.0 gap are in items/smithed/, the flat ones below it
    in items/rolled/), which is convenient to draw and cannot be shipped as-is.

    Elements are deduplicated by name, first file wins - the two bar editables both draw `Beam` at the gap
    they meet on, because it is where the two halves of that route join. A name clash between two
    genuinely different shapes is an authoring mistake and is reported rather than silently resolved.

    ⛔ The clash test compares GEOMETRY, not the element. The same piece re-drawn in a second file keeps
    its box and gets new UVs (the author lays it out against a different atlas), so a byte comparison
    calls every legitimate join a conflict.
    """
    merged = None
    seen = {}
    for name in src_names:
        src = os.path.join(EDITABLE, name + ".json")
        if not os.path.exists(src):
            sys.exit("%s: no such editable (%s)" % (name, src))
        with open(src, encoding="utf-8") as fh:
            d = json.load(fh, object_pairs_hook=collections.OrderedDict)
        d.pop("editor", None)
        d.pop("textureSizes", None)
        for key in list((d.get("textures") or {}).keys()):
            if key in TEXTURES:
                d["textures"][key] = TEXTURES[key]

        # Taken before the accumulator is emptied: for the first file `d` IS `merged`, so clearing
        # merged["elements"] would throw away the very list about to be walked.
        elements = list(d.get("elements", []))
        if merged is None:
            merged = d
            merged["elements"] = []
        else:
            merged.setdefault("textures", {}).update(d.get("textures") or {})

        for el in elements:
            el_name = el.get("name")
            if el_name in seen:
                if geometry_of(el) != geometry_of(seen[el_name]):
                    sys.exit(
                        "element '%s' is drawn at a different SIZE in %s than in the file it first "
                        "appeared in; merging would silently pick one" % (el_name, name)
                    )
                continue
            seen[el_name] = el
            merged["elements"].append(el)

    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    with open(dest_path, "w", encoding="utf-8") as fh:
        json.dump(merged, fh, indent=1, ensure_ascii=False)
        fh.write("\n")
    print(
        "%-38s -> %-46s elements=%-5d (merged %d)"
        % ("+".join(src_names), dest_path, len(merged["elements"]), len(src_names))
    )
    return True


if __name__ == "__main__":
    args = sys.argv[1:]
    if args == ["--check"]:
        check_all()
        sys.exit(0)
    if args and args[0] == "--merge":
        if len(args) < 3:
            sys.exit(__doc__)
        sys.exit(0 if merge(args[1], args[2:]) else 1)
    if not args or len(args) % 2:
        sys.exit(__doc__)
    ok = all(convert(args[i], args[i + 1]) for i in range(0, len(args), 2))
    sys.exit(0 if ok else 1)
