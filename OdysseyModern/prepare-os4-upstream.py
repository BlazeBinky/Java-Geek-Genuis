#!/usr/bin/env python3
"""Apply checked AmigaOS 4 compatibility patches to a fresh Odyssey odyssey checkout.

This is a porting aid, not a claim of a completed native OS4 build. If the upstream
source structure changes, it deliberately stops instead of applying fuzzy edits.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import sys


ALLOCATOR = r'''/*
 * Experimental AmigaOS 4 memory allocator for the Odyssey MUI port.
 *
 * OSAllocator has a virtual-memory-style API. AmigaOS 4 AllocVecTags cannot
 * provide all POSIX reserve/decommit semantics, so this implementation keeps
 * an aligned allocation until release and zeroes memory on commit/decommit.
 * It does not implement guard pages or a PPC JavaScript JIT.
 */
#include "config.h"
#include <wtf/OSAllocator.h>

#if OS(AMIGAOS4)

#include <exec/exectags.h>
#include <exec/memory.h>
#include <proto/exec.h>
#include <utility/tagitem.h>
#include <string.h>

namespace WTF {

static const ULONG AmigaOS4PageSize = 4096;

void* OSAllocator::reserveUncommitted(size_t bytes, Usage usage, bool writable, bool executable, bool includesGuardPages)
{
    (void)usage;
    (void)writable;
    (void)includesGuardPages;

    // This port currently has no safe executable-memory/JIT implementation.
    if (!bytes || executable)
        return nullptr;

    return IExec->AllocVecTags(bytes,
        AVT_Type, MEMF_PRIVATE,
        AVT_Alignment, AmigaOS4PageSize,
        AVT_ClearWithValue, 0,
        TAG_END);
}

void* OSAllocator::reserveAndCommit(size_t bytes, Usage usage, bool writable, bool executable, bool includesGuardPages)
{
    return reserveUncommitted(bytes, usage, writable, executable, includesGuardPages);
}

void OSAllocator::commit(void* address, size_t bytes, bool writable, bool executable)
{
    (void)writable;
    (void)executable;
    if (address && bytes)
        memset(address, 0, bytes);
}

void OSAllocator::decommit(void* address, size_t bytes)
{
    // AmigaOS 4 has no drop-in equivalent of POSIX decommit in this port.
    // Clear the range while retaining the reservation; this is functional but
    // may retain more physical memory than a true VM-backed implementation.
    if (address && bytes)
        memset(address, 0, bytes);
}

void OSAllocator::releaseDecommitted(void* address, size_t bytes)
{
    (void)bytes;
    if (address)
        IExec->FreeVec(address);
}

void OSAllocator::hintMemoryNotNeededSoon(void* address, size_t bytes)
{
    (void)address;
    (void)bytes;
}

} // namespace WTF

#endif // OS(AMIGAOS4)
'''

STRCASECMP = r'''/*
 * AmigaOS 4 fallback for newlib configurations without strcasecmp.
 * Compares ASCII/locale characters using unsigned-char inputs to avoid
 * undefined behavior for bytes whose value would otherwise be negative.
 */
#include <ctype.h>

#if defined(__amigaos4__) || defined(__AMIGAOS4__)
extern "C" int strcasecmp(const char* left, const char* right)
{
    if (!left || !right)
        return left == right ? 0 : (left ? 1 : -1);

    while (*left && *right) {
        int a = tolower((unsigned char)*left++);
        int b = tolower((unsigned char)*right++);
        if (a != b)
            return a - b;
    }
    return tolower((unsigned char)*left) - tolower((unsigned char)*right);
}
#endif
'''


def replace_once(path: Path, old: str, new: str, description: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        print(f"Already applied: {description}")
        return False
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"Expected exactly one match for {description} in {path}; found {count}. "
            "Upstream changed; review the port patch before continuing."
        )
    path.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")
    print(f"Applied: {description}")
    return True


def write_new_source(path: Path, body: str, description: str) -> None:
    if path.exists():
        current = path.read_text(encoding="utf-8")
        if current == body:
            print(f"Already present: {description}")
            return
        raise RuntimeError(
            f"Refusing to overwrite existing file {path}; inspect it and back it up first."
        )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body, encoding="utf-8", newline="\n")
    print(f"Created: {description}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="Path to the OdysseyWebBrowser checkout")
    args = parser.parse_args()
    root = args.source.resolve()

    platform = root / "Source" / "WTF" / "wtf" / "Platform.h"
    mui_platform = root / "Source" / "WTF" / "wtf" / "PlatformMUI.cmake"
    bal_base = root / "Source" / "WebCore" / "platform" / "mui" / "BALBase.h"
    if not all(p.is_file() for p in (root / "CMakeLists.txt", platform, mui_platform, bal_base)):
        print("ERROR: source path is not the expected Odyssey checkout (required source files missing).", file=sys.stderr)
        return 2

    replace_once(
        platform,
        "#if defined(__AROS__)\n#define WTF_OS_AROS 1\n#endif\n\n#if defined(__MORPHOS__)\n#define WTF_OS_MORPHOS 1\n#endif",
        "#if defined(__AROS__)\n#define WTF_OS_AROS 1\n#endif\n\n"
        "#if defined(__MORPHOS__)\n#define WTF_OS_MORPHOS 1\n#endif\n\n"
        "#if defined(__amigaos4__) || defined(__AMIGAOS4__)\n"
        "#define WTF_OS_AMIGAOS4 1\n"
        "// AmigaOS 4 newlib compatibility used by this WebKit revision.\n"
        "#ifndef _GLIBCXX_USE_C99_STDINT_TR1\n"
        "#define _GLIBCXX_USE_C99_STDINT_TR1 1\n"
        "#endif\n"
        "#ifdef __cplusplus\nextern \"C\" {\n#endif\n"
        "int strcasecmp(const char*, const char*);\n"
        "#ifdef __cplusplus\n}\n#endif\n"
        "#endif",
        "recognize the AmigaOS 4 compiler and declare strcasecmp",
    )

    replace_once(
        platform,
        "#elif OS(AROS)\n#define WTF_PLATFORM_MUI 1\n#endif",
        "#elif OS(AROS) || OS(AMIGAOS4)\n#define WTF_PLATFORM_MUI 1\n#endif",
        "select MUI for AmigaOS 4",
    )

    replace_once(
        bal_base,
        "#if OS(AMIGAOS4)\n#include \"AmigaOS4/BALTypeAmigaOS4.h\"\n#endif",
        "#if OS(AMIGAOS4)\n"
        "// These identifiers collide with definitions in the AmigaOS 4 SDK.\n"
        "#undef INLINE\n"
        "#undef DOUBLE\n"
        "#endif",
        "remove reference to a missing AmigaOS 4 header",
    )

    replace_once(
        mui_platform,
        "list(APPEND WTF_SOURCES\n"
        "    mui/execallocator.cpp\n"
        "    OSAllocatorAROS.cpp\n"
        "    posix/ThreadingPOSIX.cpp",
        "if (ODYSSEY_TARGET_AMIGAOS4)\n"
        "    list(APPEND WTF_SOURCES\n"
        "        OSAllocatorAmigaOS4.cpp\n"
        "        strcasecmpAmigaOS4.cpp\n"
        "    )\n"
        "else ()\n"
        "    list(APPEND WTF_SOURCES\n"
        "        mui/execallocator.cpp\n"
        "        OSAllocatorAROS.cpp\n"
        "    )\n"
        "endif ()\n\n"
        "list(APPEND WTF_SOURCES\n"
        "    posix/ThreadingPOSIX.cpp",
        "select an OS4 memory allocator and compatibility function",
    )

    write_new_source(
        root / "Source" / "WTF" / "wtf" / "OSAllocatorAmigaOS4.cpp",
        ALLOCATOR,
        "AmigaOS 4 memory allocator",
    )
    write_new_source(
        root / "Source" / "WTF" / "wtf" / "strcasecmpAmigaOS4.cpp",
        STRCASECMP,
        "AmigaOS 4 strcasecmp fallback",
    )

    print("\nOS4 compatibility patches applied.")
    print("Caveat: this does not port every MUI/WebCore API or make unsupported web features work.")
    print("A native PPC build and runtime tests are still required.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
