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


def write_report(path: Path, values: dict[str, float | None]) -> None:
    path.write_text(
        json.dumps({
            "benchmark": "odyssey-browser-performance",
            "version": 2,
            "userAgent": "OdysseyTest/1",
            "screen": {"width": 640, "height": 480},
            "mediansMs": values,
        }),
        encoding="utf-8",
    )


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="odyssey-bench-compare-") as temp:
        root = Path(temp)
        base = root / "balanced.json"
        fast = root / "fast.json"
        write_report(base, {"javascript": 100, "arrays": 40, "layout": 10, "canvas": None})
        write_report(fast, {"javascript": 80, "arrays": 50, "layout": 10.01, "canvas": None})
        result = subprocess.run(
            [sys.executable, str(TOOL), str(base), str(fast)],
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        assert result.returncode == 0, result.stdout + result.stderr
        assert "20.0% faster" in result.stdout, result.stdout
        assert "25.0% slower" in result.stdout, result.stdout
        assert "no meaningful change" in result.stdout, result.stdout
        assert "unavailable" in result.stdout, result.stdout

        invalid = root / "invalid.json"
        invalid.write_text("not json", encoding="utf-8")
        bad = subprocess.run(
            [sys.executable, str(TOOL), str(invalid), str(fast)],
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        assert bad.returncode == 2, bad.stdout + bad.stderr

    print("PASS: benchmark comparison reports speed changes and rejects invalid reports.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
