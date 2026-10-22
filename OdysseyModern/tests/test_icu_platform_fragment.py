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

        self.assertIn('CXXFLAGS="$ICU_TARGET_CXXFLAGS"', source)
        self.assertIn('ICU_TARGET_CXXFLAGS+=" -athread=native"', source)
        self.assertIn('LIBS="${AMIGAOS4_ICU_LIBS:--lpthread}"', source)
        self.assertIn('-athread=native', toolchain)
        self.assertIn('${ZLIB_LIBRARIES} -lpthread', patcher)

    def test_icu_optimization_and_cpu_flags_match_the_selected_profile(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")

        # The exact same selected -O2/-O3 profile is applied to both WebKit and
        # ICU, and any manually selected flags describe the target PPC CPU.
        self.assertIn('ICU_OPT_LEVEL="${ODYSSEY_OPT_LEVEL:-2}"', source)
        self.assertIn('ICU_TARGET_CFLAGS="${AMIGAOS4_ICU_CFLAGS:--O${ICU_OPT_LEVEL}}"', source)
        self.assertIn('ICU_TARGET_CXXFLAGS="${AMIGAOS4_ICU_CXXFLAGS:--O${ICU_OPT_LEVEL}}"', source)
        self.assertIn('ICU_TARGET_CFLAGS+=" $ICU_CPU_FLAGS"', source)
        self.assertIn('ICU_TARGET_CXXFLAGS+=" $ICU_CPU_FLAGS"', source)
        self.assertIn('CFLAGS="$ICU_TARGET_CFLAGS"', source)
        self.assertIn('CXXFLAGS="$ICU_TARGET_CXXFLAGS"', source)

    def test_target_data_generators_read_from_completed_host_data(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")

        self.assertIn('ICU_HOST_DATA_DIR="$HOST_BUILD/data/out/build/icudt55l"', source)
        self.assertIn('old = "-i $(BUILDDIR)"', source)
        self.assertIn('new = "-i $(ICU_HOST_DATA_DIR)"', source)
        self.assertIn('expected = 29', source)
        self.assertIn('old_count == expected and new_count == 0', source)
        self.assertIn('old_count == 0 and new_count == expected', source)
        patch_at = source.index('old = "-i $(BUILDDIR)"')
        target_configure_at = source.index("Configuring target ICU for AmigaOS 4 PPC")
        self.assertLess(patch_at, target_configure_at, "Makefile.in must be patched before target configure")
        self.assertIn('ICU_HOST_DATA_DIR="$HOST_BUILD/data/out/build/icudt55l" make -j', source)


if __name__ == "__main__":
    unittest.main()
