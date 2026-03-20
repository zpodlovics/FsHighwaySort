#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
mkdir -p "$OUT"

dotnet pack "$ROOT/src/HighwaySort.Native.Runtime/HighwaySort.Native.Runtime.csproj" -c Release -o "$OUT"
dotnet pack "$ROOT/src/HighwaySort.Interop/HighwaySort.Interop.csproj" -c Release -o "$OUT"
