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
        additions = []
        if "-DWTF_PLATFORM_MUI=1" not in prefix:
            additions.append("-DWTF_PLATFORM_MUI=1")
        if "-DUSE_PTHREADS=1" not in prefix:
            additions.append("-DUSE_PTHREADS=1")
        if "-D_POSIX_C_SOURCE=200809L" not in prefix:
            additions.append("-D_POSIX_C_SOURCE=200809L")
        if additions:
            text = text[:match.start(1)] + prefix + " " + " ".join(additions) + text[match.end(1):]
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




def patch_build_script(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    anchor = "    -DCMAKE_SYSTEM_PROCESSOR=ppc \\\n"
    addition = (
        "    -DCMAKE_C_FLAGS=\"-D__USE_INLINE__ -Wno-expansion-to-defined -D_GNU_SOURCE "
        "-DWTF_PLATFORM_MUI=1 -DUSE_PTHREADS=1 -I/usr/ppc-amigaos/SDK/MUI/C/include/\" \\\n"
        "    -DCMAKE_CXX_FLAGS=\"-D__USE_INLINE__ -Wno-expansion-to-defined -D_GNU_SOURCE "
        "-DWTF_PLATFORM_MUI=1 -DUSE_PTHREADS=1 -I/usr/ppc-amigaos/SDK/MUI/C/include/\" \\\n"
    )
    if "-DUSE_PTHREADS=1" not in text:
        if anchor not in text:
            raise ValueError(f"CMAKE_SYSTEM_PROCESSOR line not found in {path}")
        text = text.replace(anchor, anchor + addition, 1)
    if "-D_POSIX_C_SOURCE=200809L" not in text:
        text = text.replace("-DUSE_PTHREADS=1", "-DUSE_PTHREADS=1 -D_POSIX_C_SOURCE=200809L")
    path.write_text(text, encoding="utf-8")




def patch_siginfo_compat(root: Path) -> None:
    header = root / "Source/WTF/wtf/Threading.h"
    implementation = root / "Source/WTF/wtf/posix/ThreadingPOSIX.cpp"
    header_text = header.read_text(encoding="utf-8")
    impl_text = implementation.read_text(encoding="utf-8")

    header_old = "static void signalHandlerSuspendResume(int, siginfo_t*, void* ucontext);"
    header_new = "static void signalHandlerSuspendResume(int, void*, void* ucontext);"
    impl_old = "void Thread::signalHandlerSuspendResume(int, siginfo_t*, void* ucontext)"
    impl_new = "void Thread::signalHandlerSuspendResume(int, void*, void* ucontext)"

    if header_old in header_text:
        header_text = header_text.replace(header_old, header_new, 1)
    elif header_new not in header_text:
        raise ValueError("unexpected suspend/resume handler declaration")
    if impl_old in impl_text:
        impl_text = impl_text.replace(impl_old, impl_new, 1)
    elif impl_new not in impl_text:
        raise ValueError("unexpected suspend/resume handler implementation")
    assignment_old = "action.sa_sigaction = &signalHandlerSuspendResume;"
    assignment_new = "action.sa_sigaction = reinterpret_cast<decltype(action.sa_sigaction)>(&signalHandlerSuspendResume);"
    if assignment_old in impl_text:
        impl_text = impl_text.replace(assignment_old, assignment_new, 1)
    elif assignment_new not in impl_text:
        raise ValueError("unexpected POSIX signal callback assignment")

    header.write_text(header_text, encoding="utf-8")
    implementation.write_text(impl_text, encoding="utf-8")




def patch_date_math(root: Path) -> None:
    path = root / "Source/WTF/wtf/DateMath.cpp"
    text = path.read_text(encoding="utf-8")
    if "#include <cmath>" in text:
        return
    anchor = "#include <algorithm>\n"
    if anchor not in text:
        raise ValueError(f"include insertion point not found in {path}")
    path.write_text(text.replace(anchor, anchor + "#include <cmath>\n", 1), encoding="utf-8")




def patch_date_trunc(root: Path) -> None:
    path = root / "Source/WTF/wtf/DateMath.cpp"
    text = path.read_text(encoding="utf-8")
    old = "    return std::trunc(t) + 0.0;"
    new = "    return (t < 0 ? std::ceil(t) : std::floor(t)) + 0.0;"
    if old in text:
        text = text.replace(old, new, 1)
    elif new not in text:
        raise ValueError("unexpected timeClip implementation in DateMath.cpp")
    path.write_text(text, encoding="utf-8")


def main() -> int:
    if len(sys.argv) != 2:
        print(f"Usage: {Path(sys.argv[0]).name} UPSTREAM_SOURCE_DIR", file=sys.stderr)
        return 2
    root = Path(sys.argv[1]).resolve()
    toolchain = root / "Source/cmake/AMIGAOS4.cmake"
    registers = root / "Source/WTF/wtf/PlatformRegisters.h"
    build_script = root / "cross-build/build_aos4"
    threading_header = root / "Source/WTF/wtf/Threading.h"
    threading_impl = root / "Source/WTF/wtf/posix/ThreadingPOSIX.cpp"
    try:
        if not toolchain.is_file() or not registers.is_file() or not build_script.is_file() or not threading_header.is_file() or not threading_impl.is_file():
            raise FileNotFoundError("missing OS4 toolchain, build script, platform headers or POSIX threading implementation")
        patch_toolchain(toolchain)
        patch_registers(registers)
        patch_build_script(build_script)
        patch_siginfo_compat(root)
        patch_date_math(root)
        patch_date_trunc(root)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print("Applied MUI platform define and optional machine-context include guard.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
