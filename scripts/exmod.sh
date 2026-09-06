#!/usr/bin/env bash
# POSIX entry point for Linux and macOS.
#
# exmod is implemented once, in exmod.ps1 and the command files beside it in exmod/, which run
# unchanged on all three platforms under PowerShell 7 - the platform differences live in $OnWindows
# branches inside them. This launcher finds pwsh and forwards to it, so a Linux or macOS checkout
# gets a native ./scripts/exmod.sh without a second copy of the logic that can drift out of step
# with the first.
#
#   ./scripts/exmod.sh                 the command list, grouped
#   ./scripts/exmod.sh help test       one command in detail
#   ./scripts/exmod.sh test 1.21
#   ./scripts/exmod.sh provision game -Version 1.22 -Kind server
#
# If pwsh is missing it is installed into .dotnet/tools as a dotnet tool, which keeps the bootstrap
# inside the repo rather than on the machine. Supply PWSH=/path/to/pwsh to override the lookup.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
tools_dir="$repo_root/.dotnet/tools"

resolve_pwsh() {
  if [[ -n "${PWSH:-}" ]]; then
    printf '%s' "$PWSH"
    return 0
  fi
  if command -v pwsh >/dev/null 2>&1; then
    command -v pwsh
    return 0
  fi
  if [[ -x "$tools_dir/pwsh" ]]; then
    printf '%s' "$tools_dir/pwsh"
    return 0
  fi
  return 1
}

if ! pwsh_bin="$(resolve_pwsh)"; then
  if command -v dotnet >/dev/null 2>&1; then
    echo "PowerShell 7 not found - installing it into .dotnet/tools ..." >&2
    dotnet tool install --tool-path "$tools_dir" PowerShell >&2
    pwsh_bin="$tools_dir/pwsh"
  else
    cat >&2 <<'MSG'
exmod needs PowerShell 7 (pwsh), and neither pwsh nor dotnet is on PATH.

Install one of:
  Debian/Ubuntu   sudo apt-get install -y powershell
  Fedora/RHEL     sudo dnf install -y powershell
  macOS           brew install --cask powershell
  any platform    https://aka.ms/powershell

Or point at an existing install:  PWSH=/path/to/pwsh ./scripts/exmod.sh ...
MSG
    exit 1
  fi
fi

exec "$pwsh_bin" -NoProfile -File "$script_dir/exmod.ps1" "$@"
