#!/usr/bin/env python3
"""Coverage ratchet for the mod assemblies.

Parses a cobertura report (produced by `dotnet-coverage collect -f cobertura`) and fails if the
line coverage of any mod assembly - or the combined total - drops below a floor. The game DLLs are
instrumented too but irrelevant, so only exlib / ppex / smex are considered.

Usage: python scripts/coverage_gate.py <coverage.xml>

Floors are intentionally a few points below the current measured coverage, so the gate catches a
real regression (deleting or disabling tests) without flapping on small refactors. Raise them as
coverage climbs - that is the ratchet.
"""

import sys
import xml.etree.ElementTree as ET

# assembly name in the report -> (display, min line %)
FLOORS = {
    "exlib": ("ExpandedLib", 31.0),
    "PipesAndPowerExpanded": ("ppex", 39.0),
    "SteelmakingExpanded": ("smex", 34.0),
}
TOTAL_FLOOR = 35.0


def line_coverage(pkg):
    covered = total = 0
    for cls in pkg.find("classes"):
        seen = {}
        for ln in cls.find("lines"):
            n = ln.get("number")
            seen[n] = max(seen.get(n, 0), int(ln.get("hits")))
        total += len(seen)
        covered += sum(1 for v in seen.values() if v > 0)
    return covered, total


def main(path):
    root = ET.parse(path).getroot()
    gc = gt = 0
    failures = []
    print(f"{'assembly':<14}{'lines':>14}{'%':>8}{'floor':>8}")
    for pkg in root.find("packages"):
        name = pkg.get("name")
        if name not in FLOORS:
            continue
        display, floor = FLOORS[name]
        cov, tot = line_coverage(pkg)
        gc += cov
        gt += tot
        pct = 100 * cov / tot if tot else 0
        flag = "" if pct >= floor else "  << BELOW FLOOR"
        print(f"{display:<14}{cov:>7}/{tot:<6}{pct:>7.1f}{floor:>8.1f}{flag}")
        if pct < floor:
            failures.append(f"{display} {pct:.1f}% < {floor:.1f}%")

    total_pct = 100 * gc / gt if gt else 0
    print(f"{'TOTAL':<14}{gc:>7}/{gt:<6}{total_pct:>7.1f}{TOTAL_FLOOR:>8.1f}")
    if total_pct < TOTAL_FLOOR:
        failures.append(f"TOTAL {total_pct:.1f}% < {TOTAL_FLOOR:.1f}%")

    if failures:
        print("\nCOVERAGE GATE FAILED:")
        for f in failures:
            print(f"  - {f}")
        return 1
    print("\nCoverage gate passed.")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
