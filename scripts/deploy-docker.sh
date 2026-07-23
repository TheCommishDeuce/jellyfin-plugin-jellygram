#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <ssh-user@jellyfin-host>" >&2
    exit 1
fi

remote="$1"
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="${DOTNET:-dotnet}"
if [[ -x "$root/.dotnet/dotnet" ]]; then dotnet="$root/.dotnet/dotnet"; fi
project="$root/Jellyfin.Plugin.Jellygram/Jellyfin.Plugin.Jellygram.csproj"
dll="$root/Jellyfin.Plugin.Jellygram/bin/Release/net10.0/Jellyfin.Plugin.Jellygram.dll"

"$dotnet" build "$project" -c Release --nologo

ssh "$remote" 'set -euo pipefail
container="$(docker ps --format "{{.Names}} {{.Image}}" | awk "tolower(\$0) ~ /jellyfin/ {print \$1; exit}")"
if [[ -z "$container" ]]; then
    echo "Could not find a running Jellyfin container." >&2
    exit 1
fi
cat > /tmp/Jellyfin.Plugin.Jellygram.dll
docker exec "$container" mkdir -p /config/plugins/Jellygram
docker cp /tmp/Jellyfin.Plugin.Jellygram.dll "$container:/config/plugins/Jellygram/Jellyfin.Plugin.Jellygram.dll"
rm -f /tmp/Jellyfin.Plugin.Jellygram.dll
docker restart "$container" >/dev/null
echo "Installed Jellygram into $container and restarted Jellyfin."' < "$dll"
