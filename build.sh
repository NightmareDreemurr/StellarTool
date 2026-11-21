#!/bin/sh
set -eu
cd "$(dirname "$0")"
dotnet publish src/StellarTool.Host -c Release -o dist/host --nologo
if [ "${1:-}" = "--host-only" ]; then exit 0; fi
GAME_MANAGED="${GAME_MANAGED:-$PWD/dependencies/sdez170-melon064}"
MELON_DIR="${MELON_DIR:-$PWD/dependencies/sdez170-melon064}"
dotnet build src/StellarTool.Mod -c Release -p:MelonLoaderDir="$MELON_DIR" -p:FrameworkPathOverride="$GAME_MANAGED" -p:AutomaticallyUseReferenceAssemblyPackages=false --nologo
mkdir -p dist/mod
cp src/StellarTool.Mod/bin/Release/net472/StellarTool.Mod.dll dist/mod/
