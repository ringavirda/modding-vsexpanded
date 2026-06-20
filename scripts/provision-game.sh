#!/usr/bin/env bash
# Linux/macOS counterpart of provision-game.ps1. Provisions a Vintage Story install into the repo
# (no machine-wide install needed) by downloading the freely-available archive for this OS from the
# public CDN, caching it under .game/.cache, and extracting into .game/<slug>. Idempotent.
#
# OS-correct by construction: this script only ever fetches the linux-x64 archives, and its Windows
# counterpart (provision-game.ps1) only ever fetches the win-x64 installer - so a Linux checkout
# never pulls Windows binaries and vice versa.
#
#   provision-game.sh -Version <x.y.z | x.y> [-Dest .game/1.22] [-Kind server|client] [-Force]
#
# -Version takes a full patch (1.22.3, used as-is) or a major.minor series (1.22 -> latest patch).
#
# -Kind server (default) fetches the smaller dedicated-server archive, which carries every assembly
# the build + headless tests need (what CI uses); -Kind client fetches the full playable client
# tarball (for local launch/playtest). Both are plain tarballs - no installer - on Linux/macOS.
set -euo pipefail

version=""
dest=""
kind="server"
force=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    -Version) version="${2:?-Version needs a value}"; shift 2 ;;
    -Dest)    dest="${2:?-Dest needs a value}"; shift 2 ;;
    -Kind)    kind="${2:?-Kind needs a value}"; shift 2 ;;
    -Force)   force=1; shift ;;
    *)        echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

[[ -n "$version" ]] || { echo "Usage: provision-game.sh -Version <x.y.z> [-Dest <path>] [-Kind client|server] [-Force]" >&2; exit 1; }
[[ "$kind" == "client" || "$kind" == "server" ]] || { echo "-Kind must be 'client' or 'server'." >&2; exit 1; }

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

# Resolve a major.minor series (e.g. 1.22) to its newest stable patch via the VS version API; a full
# patch (e.g. 1.22.3) passes through unchanged. Lets launch track the latest patch while the build's
# compatibility floor stays pinned at the series .0 in Directory.Build.props.
if [[ "$version" =~ ^[0-9]+\.[0-9]+$ ]]; then
  echo "Resolving newest stable patch for series $version"
  latest="$(curl -fsSL --max-time 30 'https://api.vintagestory.at/stable.json' \
    | grep -oE "\"${version//./\\.}\.[0-9]+\"" | tr -d '"' | sort -V | tail -1)"
  [[ -n "$latest" ]] || { echo "No stable release found for series $version" >&2; exit 1; }
  version="$latest"
fi

# slug = major.minor (1.22.3 -> 1.22); the per-version folder name shared with Directory.Build.props.
slug="$(echo "$version" | cut -d. -f1-2)"
[[ -n "$dest" ]] || dest=".game/$slug"
dest_full="$repo_root/$dest"
cache_dir="$repo_root/.game/.cache"
mkdir -p "$cache_dir"

# Serialize concurrent provisions of the same slug (e.g. parallel MSBuild nodes both auto-provisioning
# on a fresh build). Best-effort: flock is absent on stock macOS, where parallel provisions are rare.
if command -v flock >/dev/null 2>&1; then
  exec 9>"$cache_dir/.provision-$slug.lock"
  flock 9
fi

archive="vs_${kind}_linux-x64_${version}.tar.gz"
url="https://cdn.vintagestory.at/gamefiles/stable/$archive"

# "Complete" marker: VintagestoryAPI.dll is present in every archive (the assembly the build/tests
# need); a client install additionally carries the client entry assembly. A version stamp records the
# exact patch, so resolving a newer patch re-provisions instead of being skipped on the slug folder.
api_marker="$dest_full/VintagestoryAPI.dll"
client_marker="$dest_full/Vintagestory.dll"
stamp="$dest_full/.vsversion"
installed="$( [[ -f "$stamp" ]] && tr -d '[:space:]' < "$stamp" || true )"
if [[ $force -eq 0 ]]; then
  # A client install is a SUPERSET of the server, so a server request must never downgrade an existing
  # client - it already satisfies build/test. Keeps an auto-provisioning build from clobbering a client.
  if [[ "$kind" == "server" && -f "$client_marker" ]]; then
    echo "Vintage Story client already at $dest - keeping it (it satisfies the server binaries)."
    exit 0
  fi
  if [[ -f "$api_marker" && "$installed" == "$version" ]] && { [[ "$kind" == "server" ]] || [[ -f "$client_marker" ]]; }; then
    echo "Vintage Story $version ($kind) already provisioned at $dest"
    exit 0
  fi
fi

mkdir -p "$cache_dir"
tarball="$cache_dir/$archive"
if [[ ! -f "$tarball" ]]; then
  echo "Downloading $url"
  tmp="$tarball.part"
  if ! curl -fsSL "$url" -o "$tmp"; then
    rm -f "$tmp"
    echo "Failed to download $url" >&2; exit 1
  fi
  mv -f "$tmp" "$tarball"
else
  echo "Using cached $archive"
fi

# Clean extract. The CDN tarballs unpack their files at the archive root.
rm -rf "$dest_full"
mkdir -p "$dest_full"
echo "Extracting to $dest"
tar -xzf "$tarball" -C "$dest_full"

# Robustness: if the archive ever nests everything under a single top-level dir, flatten it up.
if [[ ! -f "$api_marker" ]]; then
  inner="$(find "$dest_full" -mindepth 2 -maxdepth 2 -name VintagestoryAPI.dll -printf '%h\n' -quit 2>/dev/null || true)"
  if [[ -n "$inner" ]]; then
    shopt -s dotglob
    mv "$inner"/* "$dest_full"/
    shopt -u dotglob
  fi
fi

[[ -f "$api_marker" ]] || { echo "Extraction completed but VintagestoryAPI.dll is missing under $dest. The archive layout may have changed." >&2; exit 1; }

printf '%s' "$version" > "$stamp"
echo "Provisioned Vintage Story $version ($kind) at $dest"
