#!/usr/bin/env python3
"""Small offline regression test for prepare-upstream.py."""
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PATCHER = ROOT / "prepare-upstream.py"

REBUILD = """#!/bin/bash
process_selection ()
{
    case $1 in
        11)
        BUILD_PROCESSOR=x86_64
        SDK_DIR=/ssd/deadwood/repo-github-dd-core/cross-$BUILD_PROCESSOR-aros/Development
        ;;
    esac

    case $1 in
        11)
        BUILD_TYPE=Release
        ;;
    esac
}
"""
TOOLCHAIN = """SET(CMAKE_SYSTEM_NAME Generic)
SET(CMAKE_C_FLAGS_RELEASE "-O2 -DNDEBUG" CACHE STRING "Flags used by the C compiler during RELEASE builds.")
SET(CMAKE_CXX_FLAGS_RELEASE "-O2 -DNDEBUG" CACHE STRING "Flags used by the CXX compiler during RELEASE builds.")
"""


def run(source: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(PATCHER), str(source)],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )


def main() -> int:
    with tempfile.TemporaryDirectory() as temp:
        source = Path(temp)
        (source / "Source" / "cmake").mkdir(parents=True)
        (source / "rebuild.sh").write_text(REBUILD, encoding="utf-8")
        (source / "Source" / "cmake" / "AROS.cmake").write_text(TOOLCHAIN, encoding="utf-8")

        first = run(source)
        assert first.returncode == 0, first.stdout + first.stderr
        rebuild = (source / "rebuild.sh").read_text(encoding="utf-8")
        toolchain = (source / "Source" / "cmake" / "AROS.cmake").read_text(encoding="utf-8")
        assert "ODYSSEY_SDK_DIR" in rebuild
        assert 'ODYSSEY_OPT_LEVEL "2"' in toolchain
        assert '"-O${ODYSSEY_OPT_LEVEL} -DNDEBUG"' in toolchain

        second = run(source)
        assert second.returncode == 0, second.stdout + second.stderr
        assert (source / "rebuild.sh").read_text(encoding="utf-8") == rebuild
        assert (source / "Source" / "cmake" / "AROS.cmake").read_text(encoding="utf-8") == toolchain

    print("PASS: source patches apply and are idempotent.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
