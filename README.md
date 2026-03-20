# FsHighwaySort

High-performance native sorting for packed 128-bit key/value pairs using Google Highway VQSort, with a stable C ABI wrapper and an F# interop layer.

This repository contains:

- `src/FsHighwaySort.Native`  
  Native C/C++ wrapper library around Highway.
- `src/FsHighwaySort.Native.Runtime`  
  NuGet packaging project for native runtime assets.
- `src/FsHighwaySort.Interop`  
  F# managed interop layer.
- `src/FsHighwaySort.Benchmark`  
  F# console app / benchmark sample.

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
cmake -S src/FsHighwaySort.Native -B artifacts/native/linux-x64 -DCMAKE_BUILD_TYPE=Release
cmake --build artifacts/native/linux-x64 --config Release
cmake --install artifacts/native/linux-x64 --config Release --prefix artifacts/stage/linux-x64
dotnet build src/FsHighwaySort.Interop/FsHighwaySort.Interop.fsproj -c Release
dotnet build src/FsHighwaySort.Benchmark/FsHighwaySort.Benchmark.fsproj -c Release
```

## Build on Windows from the command line

Open **Developer Command Prompt for Visual Studio 2026**.

From the repository root:

```bat
cmake -S src\FsHighwaySort.Native -B artifacts\native\win-x64 -G "Visual Studio 18 2026" -A x64
cmake --build artifacts\native\win-x64 --config Release
cmake --install artifacts\native\win-x64 --config Release --prefix artifacts\stage\win-x64
dotnet build src\FsHighwaySort.Interop\FsHighwaySort.Interop.fsproj -c Release
dotnet build src\FsHighwaySort.Benchmark\FsHighwaySort.Benchmark.fsproj -c Release
```

### Windows notes

- Use the Visual Studio developer shell, not a plain `cmd.exe`, unless you have already initialized the MSVC environment.
- The generated native binary is typically:
  - `artifacts\stage\win-x64\highway_sort_wrapper.dll`
- The Linux native binary is typically:
  - `artifacts/stage/linux-x64/libhighway_sort_wrapper.so`

## Pack the native runtime NuGet

The native runtime package expects staged binaries under:

- `artifacts/nuget/native/runtimes/linux-x64/native/...`
- `artifacts/nuget/native/runtimes/win-x64/native/...`

Then pack:

```bash
dotnet pack src/FsHighwaySort.Native.Runtime/FsHighwaySort.Native.Runtime.csproj -c Release
```

On Windows:

```bat
dotnet pack src\FsHighwaySort.Native.Runtime\FsHighwaySort.Native.Runtime.csproj -c Release
```

## Pack the managed interop NuGet

```bash
dotnet pack src/FsHighwaySort.Interop/FsHighwaySort.Interop.fsproj -c Release
```

On Windows:

```bat
dotnet pack src\FsHighwaySort.Interop\FsHighwaySort.Interop.fsproj -c Release
```

## Run the benchmark

Linux:

```bash
dotnet run --project src/FsHighwaySort.Benchmark/FsHighwaySort.Benchmark.fsproj -c Release
```

Windows:

```bat
dotnet run --project src\FsHighwaySort.Benchmark\FsHighwaySort.Benchmark.fsproj -c Release
```

## SIMD control

The managed pack/unpack path can use SIMD when supported.

To disable managed SIMD explicitly:

### Linux

```bash
HIGHWAYBENCHMARK_SIMD_DISABLE=true dotnet run --project src/FsHighwaySort.Benchmark/FsHighwaySort.Benchmark.fsproj -c Release
```

### Windows Command Prompt

```bat
set HIGHWAYBENCHMARK_SIMD_DISABLE=true
dotnet run --project src\FsHighwaySort.Benchmark\FsHighwaySort.Benchmark.fsproj -c Release
```

### Windows PowerShell

```powershell
$env:HIGHWAYBENCHMARK_SIMD_DISABLE = "true"
dotnet run --project src/FsHighwaySort.Benchmark/FsHighwaySort.Benchmark.fsproj -c Release
```

## Troubleshooting

### `NU5017: Cannot create a package that has no dependencies nor content`

This means the native runtime package is being packed before native binaries were staged into:

- `artifacts/nuget/native/runtimes/linux-x64/native`
- `artifacts/nuget/native/runtimes/win-x64/native`

Build and stage native assets first.

### Windows DLL export problems

The wrapper uses explicit export/import macros in `highway_sort_wrapper.h` and defines `HIGHWAY_SORT_WRAPPER_BUILD=1` for the native target.

### Alignment errors

The native wrapper requires the incoming buffer to be 16-byte aligned. The managed code uses aligned unmanaged allocation and should not pass regular managed arrays directly.
