#!/usr/bin/env python3
"""Compatibility bridge for building Odyssey's MUI port with the old OS4 toolchain.

The current source detects PLATFORM(MUI) from OS(AROS), whereas the AmigaOS 4
cross-build sets CMAKE_SYSTEM_NAME=Generic. Define the MUI platform explicitly
for this isolated build. Also avoid including ucontext.h when the target does
not advertise HAVE(MACHINE_CONTEXT).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path


def patch_toolchain(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    for variable in ("CMAKE_C_FLAGS", "CMAKE_CXX_FLAGS"):
        pattern = re.compile(rf'(?m)^(SET\({variable}\s+"[^"]*)(")')
        match = pattern.search(text)
        if not match:
            raise ValueError(f"{variable} definition not found in {path}")
        prefix = match.group(1)
        if "-DWTF_PLATFORM_MUI=1" not in prefix:
            text = text[:match.start(1)] + prefix + " -DWTF_PLATFORM_MUI=1" + text[match.end(1):]
    path.write_text(text, encoding="utf-8")


def patch_registers(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    old = """#elif OS(WINDOWS)
#include <windows.h>
#else
#include <ucontext.h>
#endif"""
    new = """#elif OS(WINDOWS)
#include <windows.h>
#elif HAVE(MACHINE_CONTEXT)
#include <ucontext.h>
#endif"""
    if old in text:
        text = text.replace(old, new, 1)
    elif new not in text:
        raise ValueError(f"unexpected PlatformRegisters.h include block in {path}")
    path.write_text(text, encoding="utf-8")


def main() -> int:
    if len(sys.argv) != 2:
        print(f"Usage: {Path(sys.argv[0]).name} UPSTREAM_SOURCE_DIR", file=sys.stderr)
        return 2
    root = Path(sys.argv[1]).resolve()
    toolchain = root / "Source/cmake/AMIGAOS4.cmake"
    registers = root / "Source/WTF/wtf/PlatformRegisters.h"
    try:
        if not toolchain.is_file() or not registers.is_file():
            raise FileNotFoundError("missing OS4 toolchain file or PlatformRegisters.h")
        patch_toolchain(toolchain)
        patch_registers(registers)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print("Applied MUI platform define and optional machine-context include guard.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
