"""Emit mods/exlib/testing/ReleasedCodes.cs from the shipped release artifacts.

Faithfulness matters more than brevity here: the whole point of the manifest is that it is
extracted from dist/Releases rather than remembered, so this script is the derivation and is
checked in beside the output it produces.

It UNIONS with the rows already in the output file rather than replacing them, and that is
load-bearing. dist/Releases/ is gitignored and holds only the newest zip per mod, so an earlier
release's payload is gone the moment a new one is built - the checked-in rows are the only surviving
record of it. A block's variant grammar changes between releases (smex 0.9.4 shipped
`blastfurnacetap-{side}`; 0.9.8 ships it under a `refractory` tier as well) and BOTH are in player
worlds, so replacing would silently shrink the migration contract for anyone who never updated.

Consequences worth knowing: the manifest is part-derived and part-historical, it cannot be rebuilt
from a fresh clone or in CI, and a row can only ever be added here - never removed.
"""

import json
import glob
import os
import re
import itertools

REL = "dist/Releases/1.22.0"
OUT = "mods/exlib/testing/ReleasedCodes.cs"
# One shipped row as the output renders it, so the previous emission can be read back in.
ROW = re.compile(
    r'new\("(?P<dom>\w+)",\s*"(?P<path>[^"]+)",\s*"(?P<base>[^"]+)",\s*\[(?P<codes>[^\]]*)\]\)'
)
# Vanilla's abstract/horizontalorientation - a fixed, known set.
HORIZ = ["north", "east", "south", "west"]
# Property-sourced groups whose states live in the game's assets. One representative each: a
# missing state inside a props group is a different (and far rarer) failure than a missing block,
# and the per-migration tests already pin per-variant behaviour.
PROPS_SAMPLE = {
    "horizontalorientation": HORIZ,
    "rockwithdeposit": ["granite"],
    "rock": ["granite"],
}


def group_states(g):
    if g.get("states"):
        return list(g["states"])
    props = (g.get("loadFromProperties") or "").split("/")[-1]
    return PROPS_SAMPLE.get(props, ["north"])


def collect(mod):
    out = []
    pat = os.path.join(REL, mod, "assets", mod, "blocktypes", "**", "*.json")
    for f in sorted(glob.glob(pat, recursive=True)):
        d = json.load(open(f, encoding="utf-8-sig"))
        code = d.get("code")
        if not code:
            continue
        rel = f.replace(os.sep, "/").split("blocktypes/")[-1].replace(".json", "")
        groups = [group_states(g) for g in (d.get("variantgroups") or [])]
        codes = (
            ["-".join((code,) + combo) for combo in itertools.product(*groups)]
            if groups
            else [code]
        )
        out.append((rel, code, sorted(set(codes))))
    return out


def collect_entity_classes(mod):
    """Every `entityClass` a released blocktype declares, with the blocktype it came from.

    A class string is stored per block entity in the SAVE, not in any definition, so no code-level
    migration touches it. An unregistered one means the game never constructs the block entity, the
    migration's `oldState` arrives null, and the block migrates with its contents silently gone.
    """
    out = {}
    pat = os.path.join(REL, mod, "assets", mod, "blocktypes", "**", "*.json")
    for f in sorted(glob.glob(pat, recursive=True)):
        d = json.load(open(f, encoding="utf-8-sig"))
        cls = d.get("entityClass")
        if not cls or "." not in cls:
            continue  # vanilla's own (e.g. "ToolMold") carry no domain prefix
        rel = f.replace(os.sep, "/").split("blocktypes/")[-1].replace(".json", "")
        out.setdefault(cls, set()).add(rel)
    return out


def shipped_version(mod):
    """The version of `mod` on disk, taken from its zip name so the label cannot go stale.

    The label was hardcoded once and the manifest then sat at 0.6.4/0.9.4 while 0.6.8/0.9.8 shipped,
    which is how nine released blocktypes ended up outside the migration contract.
    """
    names = glob.glob(os.path.join(REL, f"{mod}_*.zip"))
    versions = sorted(
        re.sub(rf"^{mod}_|_.*$|\.zip$", "", os.path.basename(n)) for n in names
    )
    return versions[-1] if versions else "unknown"


def parse_existing(path):
    """Rows from a previous emission, keyed (domain, asset path) -> (base code, set of codes)."""
    if not os.path.exists(path):
        return {}
    text = open(path, encoding="utf-8").read()
    out = {}
    for m in ROW.finditer(text):
        codes = set(re.findall(r'"([^"]+)"', m.group("codes")))
        out[(m.group("dom"), m.group("path"))] = (m.group("base"), codes)
    return out


def merge(previous, mod, derived):
    """Derived rows unioned onto the previous ones for `mod`; returns rows and a change summary.

    collect() yields bare codes and the emitted file stores them domain-qualified, so the derived
    side is qualified before comparing. Without that every row reads as changed and a real grammar
    change is buried in the noise.
    """
    kept = {k: v for k, v in previous.items() if k[0] == mod}
    added_paths, widened = [], []
    for rel, code, codes in derived:
        key = (mod, rel)
        qualified = {f"{mod}:{c}" for c in codes}
        if key not in kept:
            kept[key] = (f"{mod}:{code}", qualified)
            added_paths.append(rel)
            continue
        base, existing = kept[key]
        fresh = qualified - existing
        if fresh:
            widened.append((rel, sorted(fresh)))
        kept[key] = (base, existing | qualified)
    rows = [
        (path, base, sorted(codes)) for (_, path), (base, codes) in sorted(kept.items())
    ]
    return rows, added_paths, widened


def main():
    lines = []
    a = lines.append
    a("// <auto-generated-by-derivation>")
    a("//   Extracted from dist/Releases/1.22.0 by infra/tools/gen-released-codes.py, UNIONED onto")
    a("//   the rows already here. dist/Releases/ is gitignored and keeps only the newest zip per mod,")
    a("//   so rows from an earlier release can no longer be re-derived - this file is the only record")
    a("//   of them. A row may be added here; a row may never be removed.")
    a("//   Hand-edit only to record a NEW release; never to make a failing test pass.")
    a("// </auto-generated-by-derivation>")
    a("")
    a("using System.Collections.Generic;")
    a("")
    a("namespace ExpandedLib.Testing;")
    a("")
    # Kept deliberately terse: CommentStyleGuards caps a doc comment at 16 lines and 3 <para>
    # blocks, and the reasoning for the union lives in this script's module docstring instead.
    a("/// <summary>")
    a("/// Every block code that has ever shipped, read from the release artifacts in <c>dist/Releases/</c>")
    a("/// rather than from the live registry. This is the migration contract: a code in this list must")
    a("/// still reach a live block after migration; a code absent from it never shipped and needs no")
    a("/// migrator.")
    a("/// <para>")
    a("/// Three mods have shipped: <c>exlib</c>, <c>ppex</c> (now iiex, with its HP half extracted to")
    a("/// hpex) and <c>smex</c> (whose ironmaking half became iiex); no iiex, siex or hpex")
    a("/// build has been released. <c>exlib</c> 0.7.0 ships no blocktype JSON, but its structure filler is")
    a("/// in released worlds because the shipped ppex and smex layouts name it in <c>blockNumbers</c>.")
    a("/// </para>")
    a("/// <para>")
    a("/// Rows accumulate across releases and are never pruned: a variant grammar can change between them")
    a("/// and both spellings are in player worlds, while <c>dist/Releases/</c> keeps only the newest zip.")
    a("/// </para>")
    a("/// </summary>")
    a("public static class ReleasedCodes")
    a("{")
    a("  /// <summary>One shipped blocktype: where it lived, its base code, and every concrete code it")
    a("  /// expanded to. Property-sourced variant groups are sampled rather than enumerated (the game")
    a("  /// holds their states), so <see cref=\"Codes\"/> is representative for those, exact otherwise.</summary>")
    a("  public sealed record Shipped(string Domain, string AssetPath, string BaseCode, string[] Codes);")
    a("")
    a("  /// <summary>A block-entity class string a released blocktype declared, and the blocktypes that")
    a("  /// declared it.</summary>")
    a("  public sealed record ShippedEntityClass(string Domain, string Class, string[] AssetPaths);")
    a("")

    previous = parse_existing(OUT)
    all_entries, report = [], []
    for mod in ("ppex", "smex"):
        rows, added, widened = merge(previous, mod, collect(mod))
        all_entries.append((mod, rows))
        report.append((mod, rows, added, widened))

    for mod, rows in all_entries:
        total = sum(len(c) for _, _, c in rows)
        a(
            f"  /// <summary>{mod}, every release up to and including {shipped_version(mod)} - "
            f"{len(rows)} blocktypes, {total} concrete codes.</summary>"
        )
        a(f"  public static readonly IReadOnlyList<Shipped> {mod.capitalize()} =")
        a("  [")
        for rel, base, codes in rows:
            rendered = ", ".join(f'"{c}"' for c in codes)
            a(f'    new("{mod}", "{rel}", "{base}", [{rendered}]),')
        a("  ];")
        a("")

    a("  /// <summary>exlib 0.7.0 - no blocktype JSON, but the filler is written into released worlds by")
    a("  /// every mega-block footprint, so it carries the same migration contract as a placed block.</summary>")
    a('  public static readonly IReadOnlyList<Shipped> Exlib =')
    a("  [")
    a('    new("exlib", "structurefiller", "exlib:structurefiller", ["exlib:structurefiller"]),')
    a("  ];")
    a("")
    a("  /// <summary>Every block-entity class string a released blocktype declared, with the blocktypes")
    a("  /// that declared it. A class string lives in the SAVE and no code migration touches it, so an")
    a("  /// unregistered one drops the block entity: the block arrives, its contents do not.</summary>")
    a("  public static readonly IReadOnlyList<ShippedEntityClass> EntityClasses =")
    a("  [")
    for mod in ("ppex", "smex"):
        for cls, rels in sorted(collect_entity_classes(mod).items()):
            rendered = ", ".join(f'"{r}"' for r in sorted(rels))
            a(f'    new("{mod}", "{cls}", [{rendered}]),')
    a("  ];")
    a("")
    a("  /// <summary>Every shipped blocktype across all three released mods.</summary>")
    a("  public static IEnumerable<Shipped> All => [.. Ppex, .. Smex, .. Exlib];")
    a("")
    a("  /// <summary>Every concrete code that has ever been placed in a released world.</summary>")
    a("  public static IEnumerable<string> AllCodes =>")
    a("    System.Linq.Enumerable.SelectMany(All, s => s.Codes);")
    a("}")

    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")

    for mod, rows, added, widened in report:
        total = sum(len(c) for _, _, c in rows)
        print(f"{mod} @ {shipped_version(mod)}: {len(rows)} blocktypes, {total} concrete codes")
        for rel in added:
            print(f"  + new blocktype: {rel}")
        for rel, fresh in widened:
            print(f"  ~ {rel}: {len(fresh)} code(s) added by a grammar change")
            for c in fresh[:4]:
                print(f"      {c}")
            if len(fresh) > 4:
                print(f"      ... and {len(fresh) - 4} more")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
