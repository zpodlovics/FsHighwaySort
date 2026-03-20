#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$ROOT/artifacts/native/linux-x64"
STAGE_DIR="$ROOT/artifacts/nuget/native/runtimes/linux-x64/native"

cmake -S "$ROOT/src/FsFsHighwaySort.Native" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE=Release
cmake --build "$BUILD_DIR" --config Release --parallel

mkdir -p "$STAGE_DIR"
cp "$BUILD_DIR/libhighway_sort_wrapper.so" "$STAGE_DIR/"
