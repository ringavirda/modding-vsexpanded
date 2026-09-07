#!/usr/bin/env bash
# exmod launcher for a repository that consumes extools. Finds the tools checkout (EXTOOLS_HOME,
# the workspace sibling ../extools, or a clone of the tag pinned in exmod.json under .extools/),
# finds pwsh (bootstrapping it into .dotnet/tools when the machine has none) and forwards every
# argument to the dispatcher with this repository as the root. Not a second implementation of
# anything: exmod.ps1 in the tools checkout holds the commands.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
tools_dir="$repo_root/.dotnet/tools"

# The "tools" value of exmod.json, read with sed: this runs before any manifest reader exists.
tools_pin() {
  sed -nE 's/^[[:space:]]*"tools"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$repo_root/exmod.json" 2>/dev/null | head -n 1
}

# The extools checkout, first rung that holds exmod.ps1: EXTOOLS_HOME, the sibling ../extools,
# then .extools/ cloned or moved to the pinned tag.
resolve_extools() {
  if [[ -n "${EXTOOLS_HOME:-}" && -f "$EXTOOLS_HOME/exmod.ps1" ]]; then
    printf '%s' "$EXTOOLS_HOME"; return
  fi
  if [[ -f "$repo_root/../extools/exmod.ps1" ]]; then
    (cd "$repo_root/../extools" && pwd); return
  fi
  local version
  version="$(tools_pin)"
  if [[ -z "$version" ]]; then
    echo 'exmod: no extools checkout found (EXTOOLS_HOME, ../extools) and exmod.json pins no "tools" version' >&2
    exit 1
  fi
  local dest="$repo_root/.extools" url="${EXTOOLS_URL:-https://github.com/ringavirda/extools.git}" tag="v$version"
  if [[ -f "$dest/exmod.ps1" ]]; then
    local have
    have="$(git -C "$dest" describe --tags --exact-match 2>/dev/null || true)"
    if [[ "$have" != "$tag" ]]; then
      echo "exmod: moving .extools from ${have:-an untagged commit} to $tag" >&2
      git -C "$dest" fetch --quiet --depth 1 origin "refs/tags/$tag:refs/tags/$tag"
      git -c advice.detachedHead=false -C "$dest" checkout --quiet "$tag"
    fi
  else
    echo "exmod: cloning extools $tag into .extools/" >&2
    git -c advice.detachedHead=false clone --quiet --depth 1 --branch "$tag" "$url" "$dest"
  fi
  printf '%s' "$dest"
}

# pwsh: $PWSH, then PATH, then the repo-local tool install, made on first use.
resolve_pwsh() {
  if [[ -n "${PWSH:-}" ]]; then printf '%s' "$PWSH"; return; fi
  if command -v pwsh >/dev/null 2>&1; then command -v pwsh; return; fi
  if [[ -x "$tools_dir/pwsh" ]]; then printf '%s' "$tools_dir/pwsh"; return; fi
  if command -v dotnet >/dev/null 2>&1; then
    echo 'exmod: pwsh not found - installing PowerShell into .dotnet/tools ...' >&2
    dotnet tool install --tool-path "$tools_dir" PowerShell >/dev/null
    printf '%s' "$tools_dir/pwsh"; return
  fi
  cat >&2 <<'MSG'
exmod: neither pwsh nor dotnet is available. Install the .NET SDK (https://dot.net) and run this
again, or install PowerShell 7 and put pwsh on PATH.
MSG
  exit 1
}

extools="$(resolve_extools)"
pwsh_bin="$(resolve_pwsh)"
exec "$pwsh_bin" -NoProfile -File "$extools/exmod.ps1" -RepoRoot "$repo_root" "$@"
