#!/usr/bin/env python3
"""Regression guard for the ICU 55.1 AmigaOS 4 cross-build platform fragment."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "build-icu-os4.sh"


class ICUPlatformFragmentTests(unittest.TestCase):
    def test_target_fragment_is_staged_in_the_build_prefix(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('--host=powerpc-unknown-amigaos', source)
        self.assertIn('ICU_PKGDATA_DIR="$PREFIX/share/icu/55.1"', source)
        self.assertIn('mkdir -p "$ICU_PKGDATA_DIR/config"', source)
        self.assertIn(
            'install -m 0644 "$ICU_SOURCE_DIR/icu4c/source/config/mh-unknown" "$ICU_PKGDATA_DIR/config/mh-unknown"',
            source,
        )

        staging = source.index('install -m 0644 "$ICU_SOURCE_DIR/icu4c/source/config/mh-unknown" "$ICU_PKGDATA_DIR/config/mh-unknown"')
        target_build = source.index('make -j"${ODYSSEY_ICU_TARGET_JOBS:-1}"')
        self.assertLess(staging, target_build, "the fragment must exist before target make starts")

    def test_host_fragment_still_comes_from_native_host_build(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('--with-cross-build="$HOST_BUILD"', source)
        self.assertIn('HOST_CC=gcc', source)
        self.assertIn('TARGET_CC=ppc-amigaos-gcc', source)

    def test_target_cxx_uses_native_amigaos_thread_runtime(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        toolchain = (ROOT / "toolchains" / "AMIGAOS4.cmake").read_text(encoding="utf-8")
        patcher = (ROOT / "prepare-os4-upstream.py").read_text(encoding="utf-8")

        self.assertIn('CXXFLAGS="${AMIGAOS4_ICU_CXXFLAGS:--O2} -athread=native"', source)
        self.assertIn('LIBS="${AMIGAOS4_ICU_LIBS:--lpthread}"', source)
        self.assertIn('-athread=native', toolchain)
        self.assertIn('${ZLIB_LIBRARIES} -lpthread', patcher)


if __name__ == "__main__":
    unittest.main()
