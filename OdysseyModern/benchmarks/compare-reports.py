#!/usr/bin/env python3
"""Compare two JSON reports exported by browser-benchmark.html (v2)."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys
from typing import Any

CATEGORIES = (
    ("javascript", "JavaScript loop"),
    ("arrays", "Array fill + scan"),
    ("layout", "DOM/layout"),
    ("canvas", "Canvas drawing"),
)


def load_report(path: Path) -> dict[str, Any]:
    try:
        report = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"cannot read valid JSON from {path}: {exc}") from exc
    if report.get("benchmark") != "odyssey-browser-performance" or report.get("version") != 2:
        raise ValueError(f"{path} is not an Odyssey browser benchmark v2 report")
    medians = report.get("mediansMs")
    if not isinstance(medians, dict):
        raise ValueError(f"{path} has no mediansMs object")
    return report


def numeric_median(report: dict[str, Any], category: str) -> float | None:
    value = report["mediansMs"].get(category)
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)) or value < 0:
        raise ValueError(f"invalid median value for {category}: {value!r}")
    return float(value)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Compare balanced.json (baseline) with fast.json (candidate)."
    )
    parser.add_argument("baseline", type=Path, help="baseline JSON report, usually the balanced build")
    parser.add_argument("candidate", type=Path, help="candidate JSON report, usually the fast build")
    args = parser.parse_args()

    try:
        baseline = load_report(args.baseline)
        candidate = load_report(args.candidate)
        if baseline.get("userAgent") != candidate.get("userAgent"):
            print("WARNING: browser user-agent differs; these runs may use different engines or versions.")
        if baseline.get("screen") != candidate.get("screen"):
            print("WARNING: screen dimensions differ.")
        print("Category           Baseline ms   Candidate ms   Change")
        print("-----------------  ------------  -------------  ----------------")
        for key, label in CATEGORIES:
            before = numeric_median(baseline, key)
            after = numeric_median(candidate, key)
            if before is None or after is None:
                before_text = "n/a" if before is None else f"{before:.2f}"
                after_text = "n/a" if after is None else f"{after:.2f}"
                print(f"{label:<17}  {before_text:>12}  {after_text:>13}  unavailable")
                continue
            if before == 0:
                change = "baseline is 0 ms"
            else:
                change_pct = (after - before) / before * 100.0
                if change_pct < -0.5:
                    change = f"{abs(change_pct):.1f}% faster"
                elif change_pct > 0.5:
                    change = f"{change_pct:.1f}% slower"
                else:
                    change = "no meaningful change"
            print(f"{label:<17}  {before:>12.2f}  {after:>13.2f}  {change}")
        print("\nLower milliseconds are better. Compare only matching benchmark versions and hardware.")
        print("This synthetic benchmark is not a complete browser performance score.")
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
