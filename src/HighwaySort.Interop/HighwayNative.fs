namespace HighwaySort.Interop

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Runtime.Intrinsics
open System.Runtime.Intrinsics.Arm
open System.Runtime.Intrinsics.X86
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<Struct; StructLayout(LayoutKind.Sequential)>]
type HighwayUint128 =
    val mutable Lo: uint64
    val mutable Hi: uint64
    new(lo, hi) = { Lo = lo; Hi = hi }

type HighwaySortStatus =
    | Ok = 0
    | NullPointer = 1
    | InvalidLength = 2
    | UnalignedPointer = 3
    | Internal = 255

type HighwayUint128Ptr = nativeptr<HighwayUint128>

exception HighwaySortException of message: string * status: HighwaySortStatus

module private NativeMethods =
    [<Literal>]
    let LibraryName = "highway_sort_wrapper"

    [<DllImport(LibraryName, EntryPoint = "sort_u128_asc", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int sort_u128_asc(HighwayUint128Ptr data, unativeint length)

    [<DllImport(LibraryName, EntryPoint = "sort_u128_desc", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int sort_u128_desc(HighwayUint128Ptr data, unativeint length)

    [<DllImport(LibraryName, EntryPoint = "highway_sort_last_error", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern nativeint highway_sort_last_error()

type HighwayNative private () =
    static let simdDisableEnvVar = "HIGHWAYBENCHMARK_SIMD_DISABLE"

    static let readBooleanEnvVar (name: string) =
        let value = Environment.GetEnvironmentVariable(name)
        if String.IsNullOrWhiteSpace(value) then false
        else
            value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)

    static let simdDisabled = readBooleanEnvVar simdDisableEnvVar

    static member SimdDisabled = simdDisabled

    static member SimdBackend =
        if simdDisabled then
            "Scalar (disabled by env)"
        elif Avx512F.IsSupported then
            "AVX-512"
        elif Avx2.IsSupported then
            "AVX2"
        elif AdvSimd.Arm64.IsSupported then
            "AdvSimd.Arm64"
        else
            "Scalar"

    static member private GetLastErrorMessage() =
        let p = NativeMethods.highway_sort_last_error()
        if p = 0n then "Unknown native error."
        else Marshal.PtrToStringUTF8(p) |> Option.ofObj |> Option.defaultValue "Unknown native error."

    static member private ThrowIfFailed(status: HighwaySortStatus, error: string option) =
        if status <> HighwaySortStatus.Ok then
            let message = defaultArg error ($"Native sort failed with status {int status}.")
            raise (HighwaySortException(message, status))

    static member TrySortPackedAscending(data: HighwayUint128Ptr, length: int, error: byref<string>) =
        if length < 0 then
            error <- "Length must be non-negative."
            HighwaySortStatus.InvalidLength
        else
            let status = enum<HighwaySortStatus>(NativeMethods.sort_u128_asc(data, unativeint length))
            error <- if status = HighwaySortStatus.Ok then null else HighwayNative.GetLastErrorMessage()
            status

    static member TrySortPackedDescending(data: HighwayUint128Ptr, length: int, error: byref<string>) =
        if length < 0 then
            error <- "Length must be non-negative."
            HighwaySortStatus.InvalidLength
        else
            let status = enum<HighwaySortStatus>(NativeMethods.sort_u128_desc(data, unativeint length))
            error <- if status = HighwaySortStatus.Ok then null else HighwayNative.GetLastErrorMessage()
            status

    static member SortPackedAscending(data: HighwayUint128Ptr, length: int) =
        let mutable error = null
        let status = HighwayNative.TrySortPackedAscending(data, length, &error)
        HighwayNative.ThrowIfFailed(status, Option.ofObj error)

    static member SortPackedDescending(data: HighwayUint128Ptr, length: int) =
        let mutable error = null
        let status = HighwayNative.TrySortPackedDescending(data, length, &error)
        HighwayNative.ThrowIfFailed(status, Option.ofObj error)

    static member SortKeysValuesAscending(keys: ReadOnlySpan<double>, values: Span<uint32>) =
        if keys.Length <> values.Length then
            invalidArg (nameof values) "Keys and values must have the same length."

        let length = keys.Length
        let totalBytes = unativeint (length * sizeof<HighwayUint128>)
        let packed = NativeMemory.AlignedAlloc(totalBytes, 16un) |> NativePtr.ofVoidPtr<HighwayUint128>
        if NativePtr.toNativeInt packed = 0n then
            raise (OutOfMemoryException("NativeMemory.AlignedAlloc returned null."))

        try
            HighwayNative.Pack(keys, values, packed, length)
            HighwayNative.SortPackedAscending(packed, length)
            HighwayNative.Unpack(values, packed, length)
        finally
            NativeMemory.AlignedFree(NativePtr.toVoidPtr packed)

    static member SortKeysValuesDescending(keys: ReadOnlySpan<double>, values: Span<uint32>) =
        if keys.Length <> values.Length then
            invalidArg (nameof values) "Keys and values must have the same length."

        let length = keys.Length
        let totalBytes = unativeint (length * sizeof<HighwayUint128>)
        let packed = NativeMemory.AlignedAlloc(totalBytes, 16un) |> NativePtr.ofVoidPtr<HighwayUint128>
        if NativePtr.toNativeInt packed = 0n then
            raise (OutOfMemoryException("NativeMemory.AlignedAlloc returned null."))

        try
            HighwayNative.Pack(keys, values, packed, length)
            HighwayNative.SortPackedDescending(packed, length)
            HighwayNative.Unpack(values, packed, length)
        finally
            NativeMemory.AlignedFree(NativePtr.toVoidPtr packed)

    static member TrySortKeysValuesAscending(keys: ReadOnlySpan<double>, values: Span<uint32>, error: byref<string>) =
        if keys.Length <> values.Length then
            error <- "Keys and values must have the same length."
            HighwaySortStatus.InvalidLength
        else
            let length = keys.Length
            let totalBytes = unativeint (length * sizeof<HighwayUint128>)
            let packed = NativeMemory.AlignedAlloc(totalBytes, 16un) |> NativePtr.ofVoidPtr<HighwayUint128>
            if NativePtr.toNativeInt packed = 0n then
                error <- "NativeMemory.AlignedAlloc returned null."
                HighwaySortStatus.Internal
            else
                try
                    HighwayNative.Pack(keys, values, packed, length)
                    let status = HighwayNative.TrySortPackedAscending(packed, length, &error)
                    if status = HighwaySortStatus.Ok then HighwayNative.Unpack(values, packed, length)
                    status
                finally
                    NativeMemory.AlignedFree(NativePtr.toVoidPtr packed)

    static member TrySortKeysValuesDescending(keys: ReadOnlySpan<double>, values: Span<uint32>, error: byref<string>) =
        if keys.Length <> values.Length then
            error <- "Keys and values must have the same length."
            HighwaySortStatus.InvalidLength
        else
            let length = keys.Length
            let totalBytes = unativeint (length * sizeof<HighwayUint128>)
            let packed = NativeMemory.AlignedAlloc(totalBytes, 16un) |> NativePtr.ofVoidPtr<HighwayUint128>
            if NativePtr.toNativeInt packed = 0n then
                error <- "NativeMemory.AlignedAlloc returned null."
                HighwaySortStatus.Internal
            else
                try
                    HighwayNative.Pack(keys, values, packed, length)
                    let status = HighwayNative.TrySortPackedDescending(packed, length, &error)
                    if status = HighwaySortStatus.Ok then HighwayNative.Unpack(values, packed, length)
                    status
                finally
                    NativeMemory.AlignedFree(NativePtr.toVoidPtr packed)

    static member private Pack(keys: ReadOnlySpan<double>, values: ReadOnlySpan<uint32>, destination: HighwayUint128Ptr, length: int) =
        if not simdDisabled then
            if Avx512F.IsSupported then HighwayNative.PackAvx512(keys, values, destination, length)
            elif Avx2.IsSupported then HighwayNative.PackAvx2(keys, values, destination, length)
            elif AdvSimd.Arm64.IsSupported then HighwayNative.PackArm64(keys, values, destination, length)
            else HighwayNative.PackScalar(keys, values, destination, length)
        else
            HighwayNative.PackScalar(keys, values, destination, length)

    static member private Unpack(values: Span<uint32>, source: HighwayUint128Ptr, length: int) =
        if not simdDisabled then
            if Avx512F.IsSupported then HighwayNative.UnpackAvx512(values, source, length)
            elif Avx2.IsSupported then HighwayNative.UnpackAvx2(values, source, length)
            elif AdvSimd.Arm64.IsSupported then HighwayNative.UnpackArm64(values, source, length)
            else HighwayNative.UnpackScalar(values, source, length)
        else
            HighwayNative.UnpackScalar(values, source, length)

    static member private PackScalar(keys: ReadOnlySpan<double>, values: ReadOnlySpan<uint32>, destination: HighwayUint128Ptr, length: int) =
        for i = 0 to length - 1 do
            let mutable item = HighwayUint128()
            item.Hi <- HighwayNative.ToSortableUInt64(keys[i])
            item.Lo <- ((uint64 values[i]) <<< 32) ||| uint64 (uint32 i)
            NativePtr.set destination i item

    static member private UnpackScalar(values: Span<uint32>, source: HighwayUint128Ptr, length: int) =
        for i = 0 to length - 1 do
            values[i] <- uint32 ((NativePtr.get source i).Lo >>> 32)


    static member private UnpackAvx512(values: Span<uint32>, source: HighwayUint128Ptr, length: int) =
        let mutable i = 0
        let loBuffer = Array.zeroCreate<uint64> 8
        let shiftedBuffer = Array.zeroCreate<uint64> 8
        while i <= length - 8 do
            for lane = 0 to 7 do
                loBuffer[lane] <- (NativePtr.get source (i + lane)).Lo
            let loRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(loBuffer)
            let loVec: Vector512<uint64> = Vector512.LoadUnsafe(&loRef)
            let shifted: Vector512<uint64> = loVec >>> 32
            let shiftedRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(shiftedBuffer)
            shifted.StoreUnsafe(&shiftedRef)
            for lane = 0 to 7 do
                values[i + lane] <- uint32 shiftedBuffer[lane]
            i <- i + 8
        if i < length then
            HighwayNative.UnpackScalar(values.Slice(i), NativePtr.add source i, length - i)

    static member private UnpackAvx2(values: Span<uint32>, source: HighwayUint128Ptr, length: int) =
        let mutable i = 0
        let loBuffer = Array.zeroCreate<uint64> 4
        let shiftedBuffer = Array.zeroCreate<uint64> 4
        while i <= length - 4 do
            for lane = 0 to 3 do
                loBuffer[lane] <- (NativePtr.get source (i + lane)).Lo
            let loRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(loBuffer)
            let loVec: Vector256<uint64> = Vector256.LoadUnsafe(&loRef)
            let shifted: Vector256<uint64> = loVec >>> 32
            let shiftedRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(shiftedBuffer)
            shifted.StoreUnsafe(&shiftedRef)
            for lane = 0 to 3 do
                values[i + lane] <- uint32 shiftedBuffer[lane]
            i <- i + 4
        if i < length then
            HighwayNative.UnpackScalar(values.Slice(i), NativePtr.add source i, length - i)

    static member private UnpackArm64(values: Span<uint32>, source: HighwayUint128Ptr, length: int) =
        let mutable i = 0
        let loBuffer = Array.zeroCreate<uint64> 2
        let shiftedBuffer = Array.zeroCreate<uint64> 2
        while i <= length - 2 do
            for lane = 0 to 1 do
                loBuffer[lane] <- (NativePtr.get source (i + lane)).Lo
            let loRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(loBuffer)
            let loVec: Vector128<uint64> = Vector128.LoadUnsafe(&loRef)
            let shifted: Vector128<uint64> = loVec >>> 32
            let shiftedRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(shiftedBuffer)
            shifted.StoreUnsafe(&shiftedRef)
            for lane = 0 to 1 do
                values[i + lane] <- uint32 shiftedBuffer[lane]
            i <- i + 2
        if i < length then
            HighwayNative.UnpackScalar(values.Slice(i), NativePtr.add source i, length - i)

    static member private PackAvx512(keys: ReadOnlySpan<double>, values: ReadOnlySpan<uint32>, destination: HighwayUint128Ptr, length: int) =
        let mutable i = 0
        let hiBuffer = Array.zeroCreate<uint64> 8
        let valueBuffer = Array.zeroCreate<uint32> 8
        let signBit = Vector512.Create(0x8000000000000000UL)
        while i <= length - 8 do
            let keysRef: byref<double> = &MemoryMarshal.GetReference(keys)
            let keysVec: Vector512<double> = Vector512.LoadUnsafe(&keysRef, unativeint i)
            let sortable: Vector512<uint64> = HighwayNative.ToSortableUInt64(keysVec.AsUInt64(), signBit)
            let hiRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(hiBuffer)
            sortable.StoreUnsafe(&hiRef)
            let valuesRef: byref<uint32> = &MemoryMarshal.GetReference(values)
            let valuesVec: Vector256<uint32> = Vector256.LoadUnsafe(&valuesRef, unativeint i)
            let valueRef: byref<uint32> = &MemoryMarshal.GetArrayDataReference(valueBuffer)
            valuesVec.StoreUnsafe(&valueRef)
            for lane = 0 to 7 do
                let mutable item = HighwayUint128()
                item.Hi <- hiBuffer[lane]
                item.Lo <- ((uint64 valueBuffer[lane]) <<< 32) ||| uint64 (uint32 (i + lane))
                NativePtr.set destination (i + lane) item
            i <- i + 8
        if i < length then
            HighwayNative.PackScalar(keys.Slice(i), values.Slice(i), NativePtr.add destination i, length - i)

    static member private PackAvx2(keys: ReadOnlySpan<double>, values: ReadOnlySpan<uint32>, destination: HighwayUint128Ptr, length: int) =
        let mutable i = 0
        let hiBuffer = Array.zeroCreate<uint64> 4
        let valueBuffer = Array.zeroCreate<uint32> 4
        let signBit = Vector256.Create(0x8000000000000000UL)
        while i <= length - 4 do
            let keysRef: byref<double> = &MemoryMarshal.GetReference(keys)
            let keysVec: Vector256<double> = Vector256.LoadUnsafe(&keysRef, unativeint i)
            let sortable: Vector256<uint64> = HighwayNative.ToSortableUInt64(keysVec.AsUInt64(), signBit)
            let hiRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(hiBuffer)
            sortable.StoreUnsafe(&hiRef)
            let valuesRef: byref<uint32> = &MemoryMarshal.GetReference(values)
            let valuesVec: Vector128<uint32> = Vector128.LoadUnsafe(&valuesRef, unativeint i)
            let valueRef: byref<uint32> = &MemoryMarshal.GetArrayDataReference(valueBuffer)
            valuesVec.StoreUnsafe(&valueRef)
            for lane = 0 to 3 do
                let mutable item = HighwayUint128()
                item.Hi <- hiBuffer[lane]
                item.Lo <- ((uint64 valueBuffer[lane]) <<< 32) ||| uint64 (uint32 (i + lane))
                NativePtr.set destination (i + lane) item
            i <- i + 4
        if i < length then
            HighwayNative.PackScalar(keys.Slice(i), values.Slice(i), NativePtr.add destination i, length - i)

    static member private PackArm64(keys: ReadOnlySpan<double>, values: ReadOnlySpan<uint32>, destination: HighwayUint128Ptr, length: int) =
        let mutable i = 0
        let hiBuffer = Array.zeroCreate<uint64> 2
        let valueBuffer = Array.zeroCreate<uint32> 2
        let signBit = Vector128.Create(0x8000000000000000UL)
        while i <= length - 2 do
            let keysRef: byref<double> = &MemoryMarshal.GetReference(keys)
            let keysVec: Vector128<double> = Vector128.LoadUnsafe(&keysRef, unativeint i)
            let sortable: Vector128<uint64> = HighwayNative.ToSortableUInt64(keysVec.AsUInt64(), signBit)
            let hiRef: byref<uint64> = &MemoryMarshal.GetArrayDataReference(hiBuffer)
            sortable.StoreUnsafe(&hiRef)
            let valuesRef: byref<uint32> = &MemoryMarshal.GetReference(values)
            let valuesVec: Vector64<uint32> = Vector64.LoadUnsafe(&valuesRef, unativeint i)
            let valueRef: byref<uint32> = &MemoryMarshal.GetArrayDataReference(valueBuffer)
            valuesVec.StoreUnsafe(&valueRef)
            for lane = 0 to 1 do
                let mutable item = HighwayUint128()
                item.Hi <- hiBuffer[lane]
                item.Lo <- ((uint64 valueBuffer[lane]) <<< 32) ||| uint64 (uint32 (i + lane))
                NativePtr.set destination (i + lane) item
            i <- i + 2
        if i < length then
            HighwayNative.PackScalar(keys.Slice(i), values.Slice(i), NativePtr.add destination i, length - i)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member private ToSortableUInt64(bits: Vector512<uint64>, signBit: Vector512<uint64>) =
        let sign = bits >>> 63
        let mask = Vector512<uint64>.Zero - sign
        bits ^^^ (mask ||| signBit)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member private ToSortableUInt64(bits: Vector256<uint64>, signBit: Vector256<uint64>) =
        let sign = bits >>> 63
        let mask = Vector256<uint64>.Zero - sign
        bits ^^^ (mask ||| signBit)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member private ToSortableUInt64(bits: Vector128<uint64>, signBit: Vector128<uint64>) =
        let sign = bits >>> 63
        let mask = Vector128<uint64>.Zero - sign
        bits ^^^ (mask ||| signBit)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member private ToSortableUInt64(value: double) =
        let bits = uint64 (BitConverter.DoubleToInt64Bits(value))
        let sign = bits >>> 63
        let mask = 0UL - sign
        bits ^^^ (mask ||| 0x8000000000000000UL)
