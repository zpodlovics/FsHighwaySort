open System
open System.Diagnostics
open FsHighwaySort.Interop

[<EntryPoint>]
let main _ =
    let length = 10_000_000
    let random = Random 42
    let keys = Array.zeroCreate<double> length
    let valuesWarmup = Array.zeroCreate<uint32> length
    let valuesMeasured = Array.zeroCreate<uint32> length

    for i = 0 to length - 1 do
        keys[i] <- float (random.NextInt64())
        valuesWarmup[i] <- uint32 i
        valuesMeasured[i] <- uint32 i

    printfn "UseLocalNativeRuntime = %s"
        (match Environment.GetEnvironmentVariable("UseLocalNativeRuntime") with
         | null -> "(MSBuild property only)"
         | v -> v)    
    
    printfn $"SIMD pack/unpack: {HighwayNative.SimdBackend}"
    printfn "Disable with: HIGHWAYBENCHMARK_SIMD_DISABLE=true"

    HighwayNative.SortKeysValuesAscending(ReadOnlySpan keys, Span valuesWarmup)

    let sw = Stopwatch.StartNew()
    HighwayNative.SortKeysValuesAscending(ReadOnlySpan keys, Span valuesMeasured)
    sw.Stop()

    let elapsedMs = sw.Elapsed.TotalMilliseconds
    let throughput = float length / sw.Elapsed.TotalSeconds / 1_000_000.0
    let logicalBandwidth = (float length * 16.0) / sw.Elapsed.TotalSeconds / 1_000_000_000.0

    printfn "Native highway call:"
    printfn $"  Elapsed: {elapsedMs:F3} ms"
    printfn $"  Throughput: {throughput:F3} Million/s"
    printfn $"  Logical bandwidth: {logicalBandwidth:F3} GB/s"
    printfn "  Logical traffic: 16.000 bytes/element"
    0
