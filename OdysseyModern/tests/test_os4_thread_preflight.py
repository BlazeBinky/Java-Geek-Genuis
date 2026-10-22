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
        fetch_at = source.index('git clone --depth 1 --branch "$BRANCH"')
        icu_at = source.index('bash "$SCRIPT_DIR/build-icu-os4.sh"')
        self.assertLess(check_at, fetch_at, "toolchain check should happen before source cloning")
        self.assertLess(check_at, icu_at, "toolchain check should happen before the costly ICU build")

    def test_cross_compiled_thread_smoke_test_is_not_executed(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('-o "$THREAD_CHECK_DIR/os4-thread-check"', source)
        self.assertNotIn(' "$THREAD_CHECK_DIR/os4-thread-check"\n', source)


if __name__ == "__main__":
    unittest.main()
