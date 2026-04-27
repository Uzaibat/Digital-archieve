# =============================================================================
# IDADRS.NativeSearch — CMake build configuration
# =============================================================================
cmake_minimum_required(VERSION 3.16)
project(search_engine C)

# ── C standard ────────────────────────────────────────────────────────────────
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

# ── Shared library target ──────────────────────────────────────────────────────
add_library(search_engine SHARED
    search_engine.c
)

# ── Symbol visibility: only IDADRS_API-marked symbols are exported ─────────────
set_target_properties(search_engine PROPERTIES
    C_VISIBILITY_PRESET     hidden    # all symbols hidden by default
    VISIBILITY_INLINES_HIDDEN ON
    # Output name matches DllImport("search_engine") on all platforms:
    #   Linux   → libsearch_engine.so
    #   macOS   → libsearch_engine.dylib
    #   Windows → search_engine.dll
    OUTPUT_NAME "search_engine"
)

# ── Compiler options ───────────────────────────────────────────────────────────
target_compile_options(search_engine PRIVATE
    -O2
    -Wall
    -Wextra
    -Wpedantic
    -Wformat=2
    -fstack-protector-strong
)

# ── Link math library (required for log2() on Linux) ──────────────────────────
target_link_libraries(search_engine PRIVATE m)

# ── Windows-specific: add SEARCH_ENGINE_EXPORTS so dllexport is active ─────────
if(WIN32)
    target_compile_definitions(search_engine PRIVATE SEARCH_ENGINE_EXPORTS)
endif()

# ── Install rules ─────────────────────────────────────────────────────────────
install(TARGETS search_engine
    RUNTIME DESTINATION bin        # .dll on Windows
    LIBRARY DESTINATION lib        # .so / .dylib on Unix
    ARCHIVE DESTINATION lib/static # .lib on Windows (import lib)
)
install(FILES search_engine.h DESTINATION include)

# =============================================================================
# Optional: standalone test binary
# =============================================================================
option(SE_BUILD_TESTS "Build the standalone C test binary" OFF)

if(SE_BUILD_TESTS)
    add_executable(se_tests search_engine_test.c)
    target_link_libraries(se_tests PRIVATE search_engine m)
    target_compile_options(se_tests PRIVATE -O2 -Wall)
endif()
