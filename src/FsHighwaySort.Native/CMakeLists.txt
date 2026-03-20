cmake_minimum_required(VERSION 3.20...3.31)

project(highway_sort_wrapper LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

include(FetchContent)

if(POLICY CMP0111)
  set(CMAKE_POLICY_DEFAULT_CMP0111 NEW)
endif()

option(HIGHWAY_SORT_FETCH_HIGHWAY "Fetch Highway via CMake FetchContent" ON)
set(HIGHWAY_VERSION_TAG "1.2.0" CACHE STRING "Highway git tag to fetch")

if(HIGHWAY_SORT_FETCH_HIGHWAY)
  FetchContent_Declare(
    highway
    GIT_REPOSITORY https://github.com/google/highway.git
    GIT_TAG ${HIGHWAY_VERSION_TAG}
    GIT_SHALLOW TRUE
  )
  FetchContent_MakeAvailable(highway)
else()
  find_package(hwy REQUIRED)
endif()

add_library(highway_sort_wrapper SHARED
  src/highway_sort_wrapper.cc
)

target_include_directories(highway_sort_wrapper
  PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_definitions(highway_sort_wrapper
  PRIVATE
    HIGHWAY_SORT_WRAPPER_BUILD=1
)

if(TARGET hwy_contrib AND TARGET hwy)
  target_link_libraries(highway_sort_wrapper PRIVATE hwy_contrib hwy)
elseif(TARGET hwy::hwy_contrib AND TARGET hwy::hwy)
  target_link_libraries(highway_sort_wrapper PRIVATE hwy::hwy_contrib hwy::hwy)
else()
  message(FATAL_ERROR "Could not find Highway contrib targets")
endif()

if(MSVC)
  target_compile_definitions(highway_sort_wrapper PRIVATE _CRT_SECURE_NO_WARNINGS)
endif()

set_target_properties(highway_sort_wrapper PROPERTIES
  OUTPUT_NAME "highway_sort_wrapper"
  WINDOWS_EXPORT_ALL_SYMBOLS OFF
)

install(TARGETS highway_sort_wrapper
  RUNTIME DESTINATION .
  LIBRARY DESTINATION .
  ARCHIVE DESTINATION .
)
