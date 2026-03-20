#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
  #if defined(HIGHWAY_SORT_WRAPPER_BUILD)
    #define HIGHWAY_SORT_API __declspec(dllexport)
  #else
    #define HIGHWAY_SORT_API __declspec(dllimport)
  #endif
#else
  #define HIGHWAY_SORT_API __attribute__((visibility("default")))
#endif

#if defined(__cplusplus)
extern "C" {
#endif

typedef enum highway_sort_error_t {
  HIGHWAY_SORT_SUCCESS = 0,
  HIGHWAY_SORT_ERROR_NULL_POINTER = 1,
  HIGHWAY_SORT_ERROR_UNALIGNED_POINTER = 2,
  HIGHWAY_SORT_ERROR_INTERNAL = 3
} highway_sort_error_t;

#if defined(_MSC_VER)
  __declspec(align(16))
#else
  __attribute__((aligned(16)))
#endif
typedef struct highway_u128_wire_t {
  uint64_t lo;
  uint64_t hi;
} highway_u128_wire_t;

HIGHWAY_SORT_API int sort_u128_asc(highway_u128_wire_t* data, size_t length);
HIGHWAY_SORT_API int sort_u128_desc(highway_u128_wire_t* data, size_t length);
HIGHWAY_SORT_API const char* highway_sort_last_error(void);

#if defined(__cplusplus)
}
#endif
