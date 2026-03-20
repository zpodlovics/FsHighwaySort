# Highway Sort Wrapper

High-performance native sorting for packed 128-bit key/value pairs using Google Highway VQSort, with a stable C ABI wrapper and a managed F# interop layer.

This repository contains:

- `src/HighwaySort.Native`  
  Native C/C++ wrapper library around Highway.
- `src/HighwaySort.Native.Runtime`  
  NuGet packaging project for native runtime assets.
- `src/HighwaySort.Interop`  
  F# managed interop layer.
- `src/HighwaySort.Benchmark`  
  F# console app / benchmark sample.
- `.github/workflows`  
  CI for Linux and Windows build + packing.

## Design

The native ABI is intentionally small and stable:

- `sort_u128_asc`
- `sort_u128_desc`
- `highway_sort_last_error`

The wire type is a 16-byte aligned struct:

```c
typedef struct highway_u128_wire_t {
  uint64_t lo;
  uint64_t hi;
} highway_u128_wire_t;
```

The managed side allocates aligned unmanaged memory and packs:

- `hi = sortable key bits`
- `lo = ((uint64_t)value << 32) | originalIndex`

## Requirements

### Linux

- CMake
- C++ compiler with C++17 support
- .NET SDK
- Git
- Internet access during configure if `HIGHWAY_SORT_FETCH_HIGHWAY=ON`

### Windows

- Visual Studio 2026 with:
  - Desktop development with C++
  - CMake tools for Windows
- .NET SDK
- Git

For command-line builds on Windows, use **Developer Command Prompt for Visual Studio 2026** or **Developer PowerShell for Visual Studio 2026** so MSVC, CMake, and the build environment are initialized correctly.

## Build on Linux

From the repository root:

```bash
cmake -S src/HighwaySort.Native -B artifacts/native/linux-x64 -DCMAKE_BUILD_TYPE=Release
cmake --build artifacts/native/linux-x64 --config Release
cmake --install artifacts/native/linux-x64 --config Release --prefix artifacts/stage/linux-x64
dotnet build src/HighwaySort.Interop/HighwaySort.Interop.fsproj -c Release
dotnet build src/HighwaySort.Benchmark/HighwaySort.Benchmark.fsproj -c Release
```

## Build on Windows from the command line

Open **Developer Command Prompt for Visual Studio 2026**.

From the repository root:

```bat
cmake -S src\HighwaySort.Native -B artifacts\native\win-x64 -G "Visual Studio 18 2026" -A x64
cmake --build artifacts\native\win-x64 --config Release
cmake --install artifacts\native\win-x64 --config Release --prefix artifacts\stage\win-x64
dotnet build src\HighwaySort.Interop\HighwaySort.Interop.fsproj -c Release
dotnet build src\HighwaySort.Benchmark\HighwaySort.Benchmark.fsproj -c Release
```

### Windows notes

- Use the Visual Studio developer shell, not a plain `cmd.exe`, unless you have already initialized the MSVC environment.
- The generated native binary is typically:
  - `artifacts\stage\win-x64\highway_sort_wrapper.dll`
- The Linux native binary is typically:
  - `artifacts/stage/linux-x64/libhighway_sort_wrapper.so`

## Build with Ninja on Windows

If you prefer Ninja instead of the Visual Studio generator:

```bat
cmake -S src\HighwaySort.Native -B artifacts\native\win-x64-ninja -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build artifacts\native\win-x64-ninja
cmake --install artifacts\native\win-x64-ninja --prefix artifacts\stage\win-x64
```

Use the Developer Command Prompt / Developer PowerShell here as well.

## Pack the native runtime NuGet

The native runtime package expects staged binaries under:

- `artifacts/nuget/native/runtimes/linux-x64/native/...`
- `artifacts/nuget/native/runtimes/win-x64/native/...`

Example layout:

```text
artifacts/
  nuget/
    native/
      runtimes/
        linux-x64/
          native/
            libhighway_sort_wrapper.so
        win-x64/
          native/
            highway_sort_wrapper.dll
```

Then pack:

```bash
dotnet pack src/HighwaySort.Native.Runtime/HighwaySort.Native.Runtime.csproj -c Release
```

On Windows:

```bat
dotnet pack src\HighwaySort.Native.Runtime\HighwaySort.Native.Runtime.csproj -c Release
```

## Pack the managed interop NuGet

```bash
dotnet pack src/HighwaySort.Interop/HighwaySort.Interop.fsproj -c Release
```

On Windows:

```bat
dotnet pack src\HighwaySort.Interop\HighwaySort.Interop.fsproj -c Release
```

## Run the benchmark

Linux:

```bash
dotnet run --project src/HighwaySort.Benchmark/HighwaySort.Benchmark.fsproj -c Release
```

Windows:

```bat
dotnet run --project src\HighwaySort.Benchmark\HighwaySort.Benchmark.fsproj -c Release
```

## SIMD control

The managed pack/unpack path can use SIMD when supported.

To disable managed SIMD explicitly:

### Linux

```bash
HIGHWAYBENCHMARK_SIMD_DISABLE=true dotnet run --project src/HighwaySort.Benchmark/HighwaySort.Benchmark.fsproj -c Release
```

### Windows Command Prompt

```bat
set HIGHWAYBENCHMARK_SIMD_DISABLE=true
dotnet run --project src\HighwaySort.Benchmark\HighwaySort.Benchmark.fsproj -c Release
```

### Windows PowerShell

```powershell
$env:HIGHWAYBENCHMARK_SIMD_DISABLE = "true"
dotnet run --project src/HighwaySort.Benchmark/HighwaySort.Benchmark.fsproj -c Release
```

## Troubleshooting

### CMake deprecation warnings

This repo uses:

```cmake
cmake_minimum_required(VERSION 3.20...3.31)
```

to avoid old compatibility-mode warnings in newer CMake versions.

### `NU5017: Cannot create a package that has no dependencies nor content`

This means the native runtime package is being packed before native binaries were staged into:

- `artifacts/nuget/native/runtimes/linux-x64/native`
- `artifacts/nuget/native/runtimes/win-x64/native`

Build and stage native assets first.

### Windows DLL export problems

The wrapper uses explicit export/import macros in `highway_sort_wrapper.h` and defines `HIGHWAY_SORT_WRAPPER_BUILD=1` for the native target. This is required for reliable Windows DLL exports.

### Alignment errors

The native wrapper requires the incoming buffer to be 16-byte aligned. The managed code uses aligned unmanaged allocation and should not pass regular managed arrays directly.

## F# note

The managed interop and benchmark projects in this bundle are written in F#. Because F# cannot directly author `LibraryImport` source-generated partial methods the same way C# can, the F# interop layer uses `DllImport` against the same stable C wrapper ABI.

## CI

The GitHub Actions workflow builds native assets on Linux and Windows, stages them under `runtimes/{rid}/native`, and packs:

- native runtime NuGet
- managed interop NuGet

