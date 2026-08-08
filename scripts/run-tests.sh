#!/usr/bin/env bash
# Linux/macOS counterpart of run-tests.ps1. Runs the test suite per game version, each version's
# projects in parallel. Mods stay single-target; legacy versions build the test projects with
# -p:Legacy=true and that version's TFM. Each build auto-provisions its game version on demand.
#
#   run-tests.sh [latest|all|1.22|1.21|1.20]
set -uo pipefail

version="${1:-latest}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

declare -A tfms=( [1.22]=net10.0 [1.21]=net8.0 [1.20]=net7.0 )
# Every test project, in dependency order (exlib -> iwex -> lpex -> hpex/smex). One per mod: there
# is no shared cross-mod project, so a test lives with the top mod it touches.
projects=(ExpandedLib.Tests IronworkingExpanded.Tests LowPressureExpanded.Tests \
          HighPressureExpanded.Tests SteelmakingExpanded.Tests)

case "$version" in
  latest) wanted=(1.22) ;;
  all)    wanted=(1.22 1.21 1.20) ;;
  1.22|1.21|1.20) wanted=("$version") ;;
  *) echo "Usage: run-tests.sh [latest|all|1.22|1.21|1.20]" >&2; exit 1 ;;
esac

# Minimum tests each suite must DISCOVER, from the one file both runners read. A suite that loses its
# assembly reports no failure at all - see the file's header for the mechanism - so the exit code
# alone cannot be trusted to mean "the tests ran". Parsed up front so a malformed row fails fast.
floors_file="$script_dir/test-floors.txt"
[[ -f "$floors_file" ]] || { echo "Missing $floors_file - the per-suite test-count floors." >&2; exit 1; }
declare -A floors=()
while IFS= read -r line || [[ -n "$line" ]]; do
  [[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
  if [[ "$line" =~ ^[[:space:]]*([A-Za-z0-9_.]+)[[:space:]]*=[[:space:]]*([0-9]+)[[:space:]]*$ ]]; then
    floors["${BASH_REMATCH[1]}"]="${BASH_REMATCH[2]}"
  else
    echo "Bad row in test-floors.txt: '$line'" >&2; exit 1
  fi
done < "$floors_file"
# A project with no floor would be silently ungated, which is the exact hole this guard closes.
for p in "${projects[@]}"; do
  [[ -n "${floors[$p]:-}" ]] || { echo "No test-count floor for: $p - add a row to scripts/test-floors.txt." >&2; exit 1; }
done

# Pick the dotnet host: the system one if it already has every runtime major we need, else a
# self-contained .dotnet (provisioned on demand) and ITS muxer - the global muxer ignores DOTNET_ROOT,
# so a local muxer is the only reliable way to run on locally-installed runtimes. Lets a fresh clone
# without .NET 7/8 run the legacy suites.
declare -A majors=( [1.22]=10 [1.21]=8 [1.20]=7 )
sys_runtimes="$(dotnet --list-runtimes 2>/dev/null || true)"
missing=()
for v in "${wanted[@]}"; do
  grep -q "Microsoft.NETCore.App ${majors[$v]}\." <<< "$sys_runtimes" || missing+=("${majors[$v]}")
done
dotnet_bin="dotnet"
if [[ ${#missing[@]} -gt 0 ]]; then
  echo "Missing .NET runtime major(s) system-wide: ${missing[*]} - provisioning a local .dotnet..."
  "$script_dir/provision-dotnet.sh" "$version"
  dotnet_bin="$repo_root/.dotnet/dotnet"
fi
echo "Using dotnet host: $dotnet_bin"

mkdir -p "$repo_root/.game/.cache"
log_dir="$(mktemp -d)"

# Build phase, SERIAL: the test projects share the mod projects (exlib/lpex/smex), so building them
# concurrently would race on the same intermediate DLLs. Building here also auto-provisions each
# version's game binaries once. The test phase then runs in parallel with --no-build.
combos=()
for v in "${wanted[@]}"; do for p in "${projects[@]}"; do combos+=("$v/$p"); done; done
echo "Building ${#combos[@]} test target(s) across version(s): ${wanted[*]}"
for c in "${combos[@]}"; do
  v="${c%%/*}"; p="${c##*/}"; tfm="${tfms[$v]}"
  args=(build "$repo_root/test/$p/$p.csproj" -f "$tfm" --nologo -v q)
  [[ "$tfm" != "net10.0" ]] && args+=(-p:Legacy=true)
  "$dotnet_bin" "${args[@]}" > "$log_dir/${c//\//_}.build.log" 2>&1 || echo "$c" >> "$log_dir/buildfail"
done

# ⛔⛔ STOP HERE if anything failed to build. This file recorded build failures and never read them, and the
# test phase runs with --no-build - so a project that stopped compiling was tested as its LAST GOOD DLL and
# reported PASS. Discovered 2026-08-05: LowPressureExpanded.Tests had a CS0104 and the run printed
# "PASS ... Passed: 190" from a binary a day old. The floor check below could not see it either, because a
# stale assembly has the same test count as the one it went stale from.
#
# ⚠ This is the THIRD silent-pass mode this script has had to close (the others: an assembly that discovers
# nothing and exits 0, and a suite that quietly loses tests). They share one root: a green line here must
# mean "the code in the tree ran", and every step between the tree and the run is a place that can stop
# being true without failing.
if [[ -s "$log_dir/buildfail" ]]; then
  echo "BUILD FAILED - not running any tests, because --no-build would test a STALE binary and pass:" >&2
  while IFS= read -r c; do
    echo "  $c" >&2
    grep -hE '(^|[^a-zA-Z])error [A-Z]+[0-9]+' "$log_dir/${c//\//_}.build.log" | head -5 >&2
  done < "$log_dir/buildfail"
  rm -rf "$log_dir"
  exit 1
fi

echo "Running tests in parallel..."
pids=()
names=()
for c in "${combos[@]}"; do
  v="${c%%/*}"; p="${c##*/}"; tfm="${tfms[$v]}"
  args=(test "$repo_root/test/$p/$p.csproj" -f "$tfm" --no-build --nologo)
  [[ "$tfm" != "net10.0" ]] && args+=(-p:Legacy=true)
  "$dotnet_bin" "${args[@]}" > "$log_dir/${c//\//_}.log" 2>&1 &
  pids+=($!)
  names+=("$c")
done
fail=0
for i in "${!pids[@]}"; do
  if wait "${pids[$i]}"; then status=PASS; else status=FAIL; fi
  log="$log_dir/${names[$i]//\//_}.log"
  line="$(grep -hE 'Passed!|Failed!|error' "$log" | tail -1 | tr -s ' ')"

  # ⚠ The exit code is NOT enough. An assembly that fails to load during discovery prints "No test is
  # available in ..." and exits 0, with no summary line to grep - so the run reads as a blank PASS
  # while every test in the suite has silently vanished. Trust the count the summary carries, and
  # treat its ABSENCE as the failure it is.
  total="$(grep -hoE 'Total:[[:space:]]*[0-9]+' "$log" | tail -1 | grep -oE '[0-9]+')"
  floor="${floors[${names[$i]##*/}]}"
  if [[ -z "$total" ]]; then
    status=FAIL
    line="NO TEST SUMMARY - the assembly discovered no tests (a type-load failure during discovery does this and still exits 0). See scripts/test-floors.txt."
  elif (( total < floor )); then
    status=FAIL
    line="ONLY $total TEST(S), FLOOR IS $floor - tests vanished rather than failed. If the deletion was deliberate, lower the floor in scripts/test-floors.txt."
  fi

  [[ "$status" == FAIL ]] && fail=$((fail+1))
  printf '%s  %-40s %s\n' "$status" "${names[$i]}" "$line"
done

rm -rf "$log_dir"
[[ $fail -eq 0 ]] || { echo "$fail test run(s) failed." >&2; exit 1; }
echo "All ${#pids[@]} test run(s) passed."
