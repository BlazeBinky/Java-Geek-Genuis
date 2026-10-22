#!/usr/bin/env python3
"""Offline regression tests for the AmigaOS 4 upstream patcher."""
from __future__ import annotations

from pathlib import Path
import hashlib
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
PATCHER = ROOT / "prepare-os4-upstream.py"

PLATFORM = """#if defined(__AROS__)
#define WTF_OS_AROS 1
#endif

#if defined(__MORPHOS__)
#define WTF_OS_MORPHOS 1
#endif

#if OS(WINDOWS)
#define WTF_PLATFORM_WIN 1
#elif OS(AROS)
#define WTF_PLATFORM_MUI 1
#endif
"""

MUI_CMAKE = """list(APPEND WTF_SOURCES
    mui/execallocator.cpp
    OSAllocatorAROS.cpp
    posix/ThreadingPOSIX.cpp
    posix/FileSystemPOSIX.cpp
    generic/WorkQueueGeneric.cpp
    generic/RunLoopGeneric.cpp
    text/mui/TextBreakIteratorInternalICUMorphOS.cpp
    mui/CPUTimeAROS.cpp
    mui/LanguageMorphOS.cpp
    generic/MemoryPressureHandlerGeneric.cpp
    generic/MemoryFootprintGeneric.cpp
    generic/MainThreadGeneric.cpp
)
"""

BAL_BASE = """#include <wtf/Platform.h>
#if OS(AMIGAOS4)
#include "AmigaOS4/BALTypeAmigaOS4.h"
#endif
"""

APP_CMAKE = """set(OdysseyWebBrowser_LIBRARIES
    JavaScriptCore
    WebKitLegacy
    -Wl,--start-group WebCore -Wl,--end-group
    -lz
    -lcairo
    -lpixman
    -lfontconfig
    -lfreetype2
    -lxml2
    -lz
    -lcurl
    -ljpeg
    -lpng
    -lssl
    -lcrypto
    -licui18n
    -licuuc
    -licudata
    -lsqlite3
    -lxslt
    -ldebug
    -lopenurl
    -lrexxsyslib
    -lavformat
    -lbz2
    -lavcodec
    -liconv
    -lz
    -lavutil
    -lswresample
    -lswscale
    -lpsl
    -latomic
    ${ZLIB_LIBRARIES}
)
"""
def run(source: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(PATCHER), str(source)],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="odyssey-os4-patcher-test-") as temp:
        source = Path(temp) / "upstream"
        (source / "Source" / "WTF" / "wtf").mkdir(parents=True)
        (source / "Source" / "WebCore" / "platform" / "mui").mkdir(parents=True)
        (source / "Tools" / "OdysseyWebBrowser").mkdir(parents=True)
        (source / "CMakeLists.txt").write_text("# fixture\n", encoding="utf-8")
        (source / "Tools" / "OdysseyWebBrowser" / "CMakeLists.txt").write_text(APP_CMAKE, encoding="utf-8")
        (source / "Source" / "WTF" / "wtf" / "Platform.h").write_text(PLATFORM, encoding="utf-8")
        (source / "Source" / "WTF" / "wtf" / "PlatformMUI.cmake").write_text(MUI_CMAKE, encoding="utf-8")
        (source / "Source" / "WebCore" / "platform" / "mui" / "BALBase.h").write_text(BAL_BASE, encoding="utf-8")

        first = run(source)
        assert first.returncode == 0, first.stdout + first.stderr

        platform = (source / "Source" / "WTF" / "wtf" / "Platform.h").read_text(encoding="utf-8")
        cmake = (source / "Source" / "WTF" / "wtf" / "PlatformMUI.cmake").read_text(encoding="utf-8")
        bal = (source / "Source" / "WebCore" / "platform" / "mui" / "BALBase.h").read_text(encoding="utf-8")
        app_cmake = (source / "Tools" / "OdysseyWebBrowser" / "CMakeLists.txt").read_text(encoding="utf-8")
        allocator = source / "Source" / "WTF" / "wtf" / "OSAllocatorAmigaOS4.cpp"
        strcmp = source / "Source" / "WTF" / "wtf" / "strcasecmpAmigaOS4.cpp"

        assert "defined(__amigaos4__) || defined(__AMIGAOS4__)" in platform
        assert "#define WTF_OS_AMIGAOS4 1" in platform
        assert "#elif OS(AROS) || OS(AMIGAOS4)" in platform
        assert "OSAllocatorAmigaOS4.cpp" in cmake
        assert "strcasecmpAmigaOS4.cpp" in cmake
        assert "OSAllocatorAROS.cpp" in cmake
        assert '#include "AmigaOS4/BALTypeAmigaOS4.h"' not in bal
        assert "#undef INLINE" in bal and "#undef DOUBLE" in bal
        assert "if (ODYSSEY_TARGET_AMIGAOS4)" in app_cmake
        os4_link_block = app_cmake.split("else ()", 1)[0]
        os4_core_libraries = os4_link_block.split("if (ENABLE_XSLT)", 1)[0]
        assert "-lxslt" not in os4_core_libraries
        assert "-lavformat" not in os4_core_libraries
        assert "if (ENABLE_XSLT)" in os4_link_block
        assert "if (ENABLE_VIDEO OR ENABLE_MEDIA_SOURCE)" in os4_link_block
        assert "-lavformat" in app_cmake.split("else ()", 1)[1], "non-OS4 link dependencies were lost"
        assert allocator.is_file() and "AllocVecTags" in allocator.read_text(encoding="utf-8")
        assert strcmp.is_file() and 'extern "C" int strcasecmp' in strcmp.read_text(encoding="utf-8")

        tracked = [
            source / "Source" / "WTF" / "wtf" / "Platform.h",
            source / "Source" / "WTF" / "wtf" / "PlatformMUI.cmake",
            source / "Source" / "WebCore" / "platform" / "mui" / "BALBase.h",
            source / "Tools" / "OdysseyWebBrowser" / "CMakeLists.txt",
            allocator,
            strcmp,
        ]
        before = {path: digest(path) for path in tracked}
        second = run(source)
        assert second.returncode == 0, second.stdout + second.stderr
        after = {path: digest(path) for path in tracked}
        assert before == after, "The second patcher run changed files; patching is not idempotent."

    print("PASS: OS4 compatibility patch applies, emits required files, and is idempotent.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
