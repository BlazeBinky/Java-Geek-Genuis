#!/usr/bin/env python3
"""Syntax-check inline JavaScript in the browser smoke-test HTML files using Node."""
from __future__ import annotations

from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
HTML_FILES = [
    ROOT / "tests" / "modern-web-check.html",
    ROOT / "benchmarks" / "browser-benchmark.html",
]
SCRIPT_RE = re.compile(r"<script\b([^>]*)>(.*?)</script\s*>", re.IGNORECASE | re.DOTALL)
SRC_RE = re.compile(r"\bsrc\s*=", re.IGNORECASE)


def main() -> int:
    node = shutil.which("node")
    if node is None:
        print("ERROR: Node.js is required to syntax-check the inline browser test scripts.", file=sys.stderr)
        return 2

    checks = 0
    with tempfile.TemporaryDirectory(prefix="odyssey-js-check-") as temp:
        temp_dir = Path(temp)
        for html_path in HTML_FILES:
            if not html_path.is_file():
                print(f"ERROR: missing test page: {html_path}", file=sys.stderr)
                return 2
            source = html_path.read_text(encoding="utf-8")
            matches = [(attrs, js) for attrs, js in SCRIPT_RE.findall(source) if not SRC_RE.search(attrs)]
            if not matches:
                print(f"ERROR: no inline script found in {html_path}", file=sys.stderr)
                return 1
            for index, (_, js) in enumerate(matches, start=1):
                js_path = temp_dir / f"{html_path.stem}-{index}.js"
                js_path.write_text(js, encoding="utf-8")
                result = subprocess.run(
                    [node, "--check", str(js_path)],
                    text=True,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    check=False,
                )
                if result.returncode:
                    print(f"ERROR: JavaScript syntax error in {html_path.relative_to(ROOT)} script {index}", file=sys.stderr)
                    print(result.stdout, file=sys.stderr)
                    print(result.stderr, file=sys.stderr)
                    return result.returncode
                checks += 1
                print(f"PASS: {html_path.relative_to(ROOT)} inline script {index}")

    print(f"PASS: syntax-checked {checks} inline JavaScript block(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
