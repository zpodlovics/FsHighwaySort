#include "highway_sort_wrapper.h"

#include <cstdint>
#include <exception>

#include "hwy/contrib/sort/vqsort.h"

namespace {
thread_local const char* g_last_error = "";

inline bool IsAligned16(const void* p) {
  return (reinterpret_cast<std::uintptr_t>(p) & 0x0F) == 0;
}

inline void SetLastError(const char* msg) {
  g_last_error = msg;
}

template <class OrderTag>
int SortImpl(highway_u128_wire_t* data, size_t length, OrderTag order) {
  if (data == nullptr) {
    SetLastError("data must not be null");
    return HIGHWAY_SORT_ERROR_NULL_POINTER;
  }

  if (length == 0) {
    SetLastError("");
    return HIGHWAY_SORT_SUCCESS;
  }

  if (!IsAligned16(data)) {
    SetLastError("data must be 16-byte aligned");
    return HIGHWAY_SORT_ERROR_UNALIGNED_POINTER;
  }

  try {
    static_assert(sizeof(highway_u128_wire_t) == sizeof(hwy::uint128_t),
                  "Size mismatch between wire type and hwy::uint128_t");
    static_assert(alignof(highway_u128_wire_t) == alignof(hwy::uint128_t),
                  "Alignment mismatch between wire type and hwy::uint128_t");

    auto* native_data = reinterpret_cast<hwy::uint128_t*>(data);
    hwy::VQSort(native_data, length, order);

    SetLastError("");
    return HIGHWAY_SORT_SUCCESS;
  } catch (const std::exception& ex) {
    SetLastError(ex.what());
    return HIGHWAY_SORT_ERROR_INTERNAL;
  } catch (...) {
    SetLastError("unknown internal error");
    return HIGHWAY_SORT_ERROR_INTERNAL;
  }
}
}  // namespace

extern "C" HIGHWAY_SORT_API int sort_u128_asc(highway_u128_wire_t* data, size_t length) {
  return SortImpl(data, length, hwy::SortAscending{});
}

extern "C" HIGHWAY_SORT_API int sort_u128_desc(highway_u128_wire_t* data, size_t length) {
  return SortImpl(data, length, hwy::SortDescending{});
}

extern "C" HIGHWAY_SORT_API const char* highway_sort_last_error(void) {
  return g_last_error;
}
