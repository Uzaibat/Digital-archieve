#!/usr/bin/env bash
# build_native.sh — Compile search_engine C library for the current platform.
# Run this before `dotnet test` so the native library is present.
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$SCRIPT_DIR/IDADRS.NativeSearch"

echo "Building native search_engine library..."

cd "$SRC_DIR"

if command -v cmake &>/dev/null; then
    mkdir -p build && cd build
    cmake .. -DCMAKE_BUILD_TYPE=Release
    cmake --build . --config Release
    # Copy back to the source directory for .csproj Content inclusion
    if [ -f "libsearch_engine.so" ]; then
        cp libsearch_engine.so ../ && echo "Copied libsearch_engine.so"
    elif [ -f "libsearch_engine.dylib" ]; then
        cp libsearch_engine.dylib ../ && echo "Copied libsearch_engine.dylib"
    elif [ -f "Release/search_engine.dll" ]; then
        cp Release/search_engine.dll ../ && echo "Copied search_engine.dll"
    fi
else
    # Direct gcc fallback (Linux/macOS)
    echo "cmake not found — falling back to direct gcc build"
    gcc -O2 -Wall -std=c11 -fPIC -shared        \
        -fvisibility=hidden                      \
        -o libsearch_engine.so                   \
        search_engine.c -lm
    echo "Built libsearch_engine.so"
fi

echo "Done. Run 'dotnet test' from the solution root."
