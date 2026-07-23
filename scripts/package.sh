#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="${DOTNET:-dotnet}"
if [[ -x "$root/.dotnet/dotnet" ]]; then dotnet="$root/.dotnet/dotnet"; fi
version="${VERSION:-1.0.0.0}"
output="$root/artifacts"
dll="$root/Jellyfin.Plugin.Jellygram/bin/Release/net10.0/Jellyfin.Plugin.Jellygram.dll"
archive="$output/Jellygram_${version}.zip"

"$dotnet" test "$root/Jellyfin.Plugin.Jellygram.sln" -c Release
mkdir -p "$output"
rm -f "$archive"
(
    cd "$(dirname "$dll")"
    zip -X -j "$archive" "$(basename "$dll")"
)

if command -v md5sum >/dev/null; then
    md5sum "$archive" | awk '{print $1}' > "$archive.md5"
else
    md5 -q "$archive" > "$archive.md5"
fi

printf 'Created %s\nMD5: %s\n' "$archive" "$(cat "$archive.md5")"
