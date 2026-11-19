#!/bin/sh
set -eu
cd "$(dirname "$0")"
dotnet publish src/StellarTool.Host -c Release -o dist/host --nologo
if [ "${1:-}" = "--host-only" ]; then exit 0; fi
: "${GAME_MANAGED:?Set GAME_MANAGED to the local Sinmai_Data/Managed directory}"
: "${MELON_DIR:?Set MELON_DIR to the installed loader folder containing MelonLoader.dll and 0Harmony.dll}"
dotnet build src/StellarTool.Mod -c Release -p:MelonLoaderDir="$MELON_DIR" -p:FrameworkPathOverride="$GAME_MANAGED" -p:AutomaticallyUseReferenceAssemblyPackages=false --nologo
mkdir -p dist/mod
cp src/StellarTool.Mod/bin/Release/net472/StellarTool.Mod.dll dist/mod/
