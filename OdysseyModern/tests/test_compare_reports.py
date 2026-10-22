#!/usr/bin/env python3
"""Offline regression tests for the benchmark JSON comparison utility."""
from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "benchmarks" / "compare-reports.py"


def write_report(path: Path, values: dict[str, float | None], version: int = 3) -> None:
    path.write_text(
        json.dumps({
            "benchmark": "odyssey-browser-performance",
            "version": version,
            "userAgent": "OdysseyTest/1",
            "screen": {"width": 640, "height": 480},
            "mediansMs": values,
        }),
        encoding="utf-8",
    )


def compare(base: Path, candidate: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(TOOL), str(base), str(candidate)],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="odyssey-bench-compare-") as temp:
        root = Path(temp)
        base = root / "balanced.json"
        fast = root / "fast.json"
        write_report(base, {
            "javascript": 100, "arrays": 40, "objects": 20,
            "stringsRegex": 30, "layout": 10, "canvas": None,
        })
        write_report(fast, {
            "javascript": 80, "arrays": 50, "objects": 22,
            "stringsRegex": 29, "layout": 10.01, "canvas": None,
        })
        result = compare(base, fast)
        assert result.returncode == 0, result.stdout + result.stderr
        assert "20.0% faster" in result.stdout, result.stdout
        assert "25.0% slower" in result.stdout, result.stdout
        assert "10.0% slower" in result.stdout, result.stdout
        assert "3.3% faster" in result.stdout, result.stdout
        assert "no meaningful change" in result.stdout, result.stdout
        assert "unavailable" in result.stdout, result.stdout

        # Older v2 reports remain readable; new v3-only workloads must show
        # unavailable rather than fabricating a timing for the missing fields.
        old = root / "old-v2.json"
        write_report(old, {
            "javascript": 100, "arrays": 40, "layout": 10, "canvas": None,
        }, version=2)
        mixed = compare(old, fast)
        assert mixed.returncode == 0, mixed.stdout + mixed.stderr
        assert "benchmark versions differ" in mixed.stdout, mixed.stdout
        assert "String/regex" in mixed.stdout and "unavailable" in mixed.stdout, mixed.stdout

        invalid = root / "invalid.json"
        invalid.write_text("not json", encoding="utf-8")
        bad = compare(invalid, fast)
        assert bad.returncode == 2, bad.stdout + bad.stderr

        unsupported = root / "v99.json"
        write_report(unsupported, {"javascript": 10}, version=99)
        bad_version = compare(unsupported, fast)
        assert bad_version.returncode == 2, bad_version.stdout + bad_version.stderr

    print("PASS: v2/v3 comparisons work, new categories are explicit, and invalid reports are rejected.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
