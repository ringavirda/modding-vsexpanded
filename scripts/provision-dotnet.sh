#!/usr/bin/env bash
# Linux/macOS counterpart of provision-dotnet.ps1. Builds a SELF-CONTAINED .NET install under .dotnet
# (SDK + base runtimes) so a fresh clone runs the tests and launches the game without hand-installing
# .NET 7/8/10. Each VS version is pinned to one major (net10=1.22, net8=1.21, net7=1.20) and won't roll
# forward across majors. The global `dotnet` muxer ignores DOTNET_ROOT, so the test/launch entry points
# call .dotnet/dotnet when a needed runtime is missing system-wide. No Desktop runtime on Linux.
#
#   provision-dotnet.sh [latest|all|1.22|1.21|1.20]
set -uo pipefail

version="${1:-latest}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
dotnet_dir="$repo_root/.dotnet"

declare -A channels=( [1.22]=10.0 [1.21]=8.0 [1.20]=7.0 )
sdk_channel=10.0
case "$version" in
  latest) wanted=(1.22) ;;
  all)    wanted=(1.22 1.21 1.20) ;;
  1.22|1.21|1.20) wanted=("$version") ;;
  *) echo "Usage: provision-dotnet.sh [latest|all|1.22|1.21|1.20]" >&2; exit 1 ;;
esac

framework_present() { local p="$dotnet_dir/shared/$1"; [[ -d "$p" ]] && compgen -G "$p/$2.*" > /dev/null; }
sdk_present()       { local p="$dotnet_dir/sdk";       [[ -d "$p" ]] && compgen -G "$p/$1.*" > /dev/null; }

cache="$dotnet_dir/.cache"; mkdir -p "$cache"
installer="$cache/dotnet-install.sh"
if [[ ! -f "$installer" ]]; then
  echo "Fetching the official dotnet-install script"
  curl -fsSL 'https://dot.net/v1/dotnet-install.sh' -o "$installer"; chmod +x "$installer"
fi

if ! sdk_present "${sdk_channel%%.*}"; then
  echo "Installing the .NET $sdk_channel SDK into .dotnet ..."
  "$installer" --channel "$sdk_channel" --install-dir "$dotnet_dir" --no-path
fi

for v in "${wanted[@]}"; do
  chan="${channels[$v]}"; major="${chan%%.*}"
  if ! framework_present "Microsoft.NETCore.App" "$major"; then
    echo "Installing the .NET $chan runtime for Vintage Story $v ..."
    "$installer" --runtime dotnet --channel "$chan" --install-dir "$dotnet_dir" --no-path
  fi
done

echo "Self-contained .NET ready in .dotnet for version(s): ${wanted[*]}"
