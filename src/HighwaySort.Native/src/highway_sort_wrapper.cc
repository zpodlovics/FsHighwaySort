#include "highway_sort_wrapper.h"

#include <atomic>
#include <cstdint>
#include <cstring>
#include <exception>
#include <new>
#include <string>
#include <type_traits>

#include "hwy/contrib/sort/vqsort.h"

namespace {
thread_local std::string g_last_error;

void SetLastError(const char* message) {
  g_last_error = message != nullptr ? message : "Unknown error";
}

void ClearLastError() {
  g_last_error.clear();
}

bool IsAligned16(const void* p) {
  return (reinterpret_cast<std::uintptr_t>(p) & static_cast<std::uintptr_t>(0x0F)) == 0;
}

static_assert(sizeof(highway_u128_wire_aligned_t) == sizeof(hwy::uint128_t), "Size mismatch between wire type and hwy::uint128_t");
static_assert(alignof(hwy::uint128_t) == 16, "Expected 16-byte alignment for hwy::uint128_t");
}  // namespace

extern "C" HIGHWAY_SORT_API int sort_u128_asc(highway_u128_wire_aligned_t* data, size_t length) {
  try {
    ClearLastError();

    if (data == nullptr) {
      SetLastError("sort_u128_asc received a null data pointer.");
      return HIGHWAY_SORT_ERROR_NULL_POINTER;
    }

    if (length == 0) {
      return HIGHWAY_SORT_OK;
    }

    if (!IsAligned16(data)) {
      SetLastError("sort_u128_asc requires a 16-byte aligned buffer.");
      return HIGHWAY_SORT_ERROR_UNALIGNED_POINTER;
    }

    auto* native = reinterpret_cast<hwy::uint128_t*>(data);
    hwy::VQSort(native, length, hwy::SortAscending{});
    return HIGHWAY_SORT_OK;
  } catch (const std::bad_alloc&) {
    SetLastError("sort_u128_asc failed due to memory allocation failure.");
    return HIGHWAY_SORT_ERROR_INTERNAL;
  } catch (const std::exception& ex) {
    SetLastError(ex.what());
    return HIGHWAY_SORT_ERROR_INTERNAL;
  } catch (...) {
    SetLastError("sort_u128_asc failed with an unknown exception.");
    return HIGHWAY_SORT_ERROR_INTERNAL;
  }
}

extern "C" HIGHWAY_SORT_API int sort_u128_desc(highway_u128_wire_aligned_t* data, size_t length) {
  try {
    ClearLastError();

    if (data == nullptr) {
      SetLastError("sort_u128_desc received a null data pointer.");
      return HIGHWAY_SORT_ERROR_NULL_POINTER;
    }

    if (length == 0) {
      return HIGHWAY_SORT_OK;
    }

    if (!IsAligned16(data)) {
      SetLastError("sort_u128_desc requires a 16-byte aligned buffer.");
      return HIGHWAY_SORT_ERROR_UNALIGNED_POINTER;
    }

    auto* native = reinterpret_cast<hwy::uint128_t*>(data);
    hwy::VQSort(native, length, hwy::SortDescending{});
    return HIGHWAY_SORT_OK;
  } catch (const std::bad_alloc&) {
    SetLastError("sort_u128_desc failed due to memory allocation failure.");
    return HIGHWAY_SORT_ERROR_INTERNAL;
  } catch (const std::exception& ex) {
    SetLastError(ex.what());
    return HIGHWAY_SORT_ERROR_INTERNAL;
  } catch (...) {
    SetLastError("sort_u128_desc failed with an unknown exception.");
    return HIGHWAY_SORT_ERROR_INTERNAL;
  }
}

extern "C" HIGHWAY_SORT_API const char* highway_sort_last_error(void) {
  return g_last_error.empty() ? "" : g_last_error.c_str();
}
