#!/usr/bin/env python3
"""Apply small, checked downstream build-system changes to a fresh Odyssey checkout.

Only edits known upstream lines; exits rather than guessing if the upstream file
has changed. It does not claim to modernize WebKit engine features by toggling
unsupported options.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import sys


def replace_once(path: Path, old: str, new: str, description: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        print(f"Already applied: {description}")
        return False
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"Expected exactly one match for {description} in {path}; found {count}. "
            "Upstream changed; review this patch before continuing."
        )
    path.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")
    print(f"Applied: {description}")
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="Path to the OdysseyWebBrowser checkout")
    args = parser.parse_args()
    root = args.source.resolve()

    rebuild = root / "rebuild.sh"
    toolchain = root / "Source" / "cmake" / "AROS.cmake"
    if not rebuild.is_file() or not toolchain.is_file():
        print("ERROR: source path is not an Odyssey checkout (rebuild.sh / Source/cmake/AROS.cmake missing).", file=sys.stderr)
        return 2

    rebuild_old = "    esac\n\n    case $1 in"
    rebuild_new = (
        "    esac\n\n"
        "    # Downstream: allow each builder to point at its own SDK instead of\n"
        "    # relying on the original maintainer's absolute filesystem paths.\n"
        '    if [[ -n "${ODYSSEY_SDK_DIR:-}" ]]; then\n'
        '        SDK_DIR="$ODYSSEY_SDK_DIR"\n'
        "    fi\n\n"
        "    case $1 in"
    )
    replace_once(rebuild, rebuild_old, rebuild_new, "portable SDK path in rebuild.sh")

    cmake_old = (
        "    cmake \\\n"
        "        -DCMAKE_TOOLCHAIN_FILE=../Source/cmake/AROS.cmake \\\n"
    )
    cmake_new = (
        "    CMAKE_LAUNCHER_ARGS=()\n"
        '    if [[ -n "${ODYSSEY_COMPILER_LAUNCHER:-}" ]]; then\n'
        '        CMAKE_LAUNCHER_ARGS+=("-DCMAKE_C_COMPILER_LAUNCHER=${ODYSSEY_COMPILER_LAUNCHER}")\n'
        '        CMAKE_LAUNCHER_ARGS+=("-DCMAKE_CXX_COMPILER_LAUNCHER=${ODYSSEY_COMPILER_LAUNCHER}")\n'
        "    fi\n"
        '    cmake "${CMAKE_LAUNCHER_ARGS[@]}" \\\n'
        "        -DCMAKE_TOOLCHAIN_FILE=../Source/cmake/AROS.cmake \\\n"
    )
    replace_once(rebuild, cmake_old, cmake_new, "optional compiler launcher support in rebuild.sh")

    old_flags = (
        'SET(CMAKE_C_FLAGS_RELEASE "-O2 -DNDEBUG" CACHE STRING "Flags used by the C compiler during RELEASE builds.")\n'
        'SET(CMAKE_CXX_FLAGS_RELEASE "-O2 -DNDEBUG" CACHE STRING "Flags used by the CXX compiler during RELEASE builds.")'
    )
    new_flags = (
        "# Downstream optimization profile. Balanced (-O2) is the default.\n"
        "# ODYSSEY_OPT_LEVEL=3 enables an experimental speed profile; it may\n"
        "# increase compile time and executable size, so benchmark on target hardware.\n"
        'if (DEFINED ENV{ODYSSEY_OPT_LEVEL})\n'
        '    set(ODYSSEY_OPT_LEVEL "$ENV{ODYSSEY_OPT_LEVEL}")\n'
        "else ()\n"
        '    set(ODYSSEY_OPT_LEVEL "2")\n'
        "endif ()\n"
        'if (NOT ODYSSEY_OPT_LEVEL MATCHES "^[23]$")\n'
        '    message(FATAL_ERROR "ODYSSEY_OPT_LEVEL must be 2 or 3")\n'
        "endif ()\n"
        'SET(CMAKE_C_FLAGS_RELEASE "-O${ODYSSEY_OPT_LEVEL} -DNDEBUG" CACHE STRING "Flags used by the C compiler during RELEASE builds.")\n'
        'SET(CMAKE_CXX_FLAGS_RELEASE "-O${ODYSSEY_OPT_LEVEL} -DNDEBUG" CACHE STRING "Flags used by the CXX compiler during RELEASE builds.")'
    )
    replace_once(toolchain, old_flags, new_flags, "balanced/fast release optimization profile")

    print("\nDownstream build-system changes are ready.")
    print("The x86_64 MUI port keeps upstream's JIT/FTL-JIT settings; unsupported engine features are not forced on.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
