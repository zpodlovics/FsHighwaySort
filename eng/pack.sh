#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
mkdir -p "$OUT"

dotnet pack "$ROOT/src/FsFsFsHighwaySort.Native.Runtime/FsFsFsHighwaySort.Native.Runtime.csproj" -c Release -o "$OUT"
dotnet pack "$ROOT/src/FsFsHighwaySort.Interop/FsFsHighwaySort.Interop.csproj" -c Release -o "$OUT"
