#!/usr/bin/env python3
"""Guard the fast native-thread link preflight before the costly ICU build."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "fetch-and-build-os4.sh"


class NativeThreadPreflightTests(unittest.TestCase):
    def test_preflight_links_cxx_threads_before_source_fetch(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("#include <mutex>", source)
        self.assertIn("#include <thread>", source)
        self.assertIn("-athread=native", source)
        self.assertIn("-lpthread", source)
        self.assertIn("PPC native C++ thread link check passed.", source)
        check_at = source.index('ppc-amigaos-g++ -std=gnu++11 -athread=native')
        fetch_at = source.index('git -C "$SOURCE_DIR" fetch --depth=1 origin "$UPSTREAM_REF"')
        icu_at = source.index('bash "$SCRIPT_DIR/build-icu-os4.sh"')
        self.assertLess(check_at, fetch_at, "toolchain check should happen before source fetching")
        self.assertLess(check_at, icu_at, "toolchain check should happen before the costly ICU build")

    def test_upstream_ref_is_pinned_by_default_but_overridable(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('UPSTREAM_REF="${ODYSSEY_UPSTREAM_REF:-8e44b6d8d2e8c57a59a878152b81304e220ceb7a}"', source)
        self.assertIn('git -C "$SOURCE_DIR" fetch --depth=1 origin "$UPSTREAM_REF"', source)
        self.assertIn('git -C "$SOURCE_DIR" checkout --detach FETCH_HEAD', source)
        self.assertIn('git -C "$SOURCE_DIR" reset --hard FETCH_HEAD', source)

    def test_cross_compiled_thread_smoke_test_is_not_executed(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('-o "$THREAD_CHECK_DIR/os4-thread-check"', source)
        self.assertNotIn(' "$THREAD_CHECK_DIR/os4-thread-check"\n', source)


if __name__ == "__main__":
    unittest.main()
