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
projects=(ExpandedLib.Tests PipesAndPowerExpanded.Tests SteelmakingExpanded.Tests Integration.Tests)

case "$version" in
  latest) wanted=(1.22) ;;
  all)    wanted=(1.22 1.21 1.20) ;;
  1.22|1.21|1.20) wanted=("$version") ;;
  *) echo "Usage: run-tests.sh [latest|all|1.22|1.21|1.20]" >&2; exit 1 ;;
esac

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

# Build phase, SERIAL: the test projects share the mod projects (exlib/ppex/smex), so building them
# concurrently would race on the same intermediate DLLs. Building here also auto-provisions each
# version's game binaries once. The test phase then runs in parallel with --no-build.
combos=()
for v in "${wanted[@]}"; do for p in "${projects[@]}"; do combos+=("$v/$p"); done; done
echo "Building ${#combos[@]} test target(s) across version(s): ${wanted[*]}"
for c in "${combos[@]}"; do
  v="${c%%/*}"; p="${c##*/}"; tfm="${tfms[$v]}"
  args=(build "$repo_root/test/$p/$p.csproj" -f "$tfm" --nologo -v q)
  [[ "$tfm" != "net10.0" ]] && args+=(-p:Legacy=true)
  "$dotnet_bin" "${args[@]}" > "$log_dir/${c//\//_}.build.log" 2>&1 || echo "build-failed:$c" >> "$log_dir/buildfail"
done

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
  if wait "${pids[$i]}"; then status=PASS; else status=FAIL; fail=$((fail+1)); fi
  line="$(grep -hE 'Passed!|Failed!|error' "$log_dir/${names[$i]//\//_}.log" | tail -1 | tr -s ' ')"
  printf '%s  %-40s %s\n' "$status" "${names[$i]}" "$line"
done

rm -rf "$log_dir"
[[ $fail -eq 0 ]] || { echo "$fail test run(s) failed." >&2; exit 1; }
echo "All ${#pids[@]} test run(s) passed."
