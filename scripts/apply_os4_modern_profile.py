#!/usr/bin/env python3
"""Enable a conservative set of supported WebKit MUI options for the OS4 build.

Only options declared in Source/cmake/WebKitFeatures.cmake are changed.
Explicitly-disabled features that the port cannot build (WebAssembly,
WebCrypto, WebAudio, WebGL, Service Workers) are intentionally untouched.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

FEATURES = {
    "ENABLE_CSS_SCROLL_SNAP": "CSS scroll snapping",
    "ENABLE_DATALIST_ELEMENT": "HTML datalist suggestions",
    "ENABLE_DOWNLOAD_ATTRIBUTE": "HTML download attribute",
    "ENABLE_CSS_IMAGE_SET": "CSS image-set responsive images",
    "ENABLE_CSS_DEVICE_ADAPTATION": "CSS viewport/device adaptation",
}
MARKER = "# OdysseyOS4ModernProfile (generated; keep idempotent)"


def apply(source: Path) -> tuple[list[str], list[str]]:
    opts = source / "Source/cmake/OptionsMUI.cmake"
    features = source / "Source/cmake/WebKitFeatures.cmake"
    if not opts.is_file() or not features.is_file():
        raise FileNotFoundError("expected Source/cmake/OptionsMUI.cmake and WebKitFeatures.cmake")
    options_text = opts.read_text(encoding="utf-8")
    feature_text = features.read_text(encoding="utf-8")
    declared = set(re.findall(r"WEBKIT_OPTION_DEFINE\((ENABLE_[A-Z0-9_]+)\b", feature_text))

    # Strip our former generated block first, making this safe to re-run.
    options_text = re.sub(
        r"(?m)^" + re.escape(MARKER) + r"\n(?:(?!^WEBKIT_OPTION_END\(\)).*\n)*",
        "",
        options_text,
    )
    body = [MARKER, "# Additional standards-facing features selected for this experimental build."]
    enabled: list[str] = []
    skipped: list[str] = []
    for name, description in FEATURES.items():
        if name not in declared:
            skipped.append(name)
            continue
        line = f"WEBKIT_OPTION_DEFAULT_PORT_VALUE({name} PRIVATE ON)"
        pattern = re.compile(
            r"(?m)^\s*WEBKIT_OPTION_DEFAULT_PORT_VALUE\("
            + re.escape(name)
            + r"\s+PRIVATE\s+(?:ON|OFF)\s*\)\s*$"
        )
        if pattern.search(options_text):
            options_text = pattern.sub(line, options_text)
        else:
            body.append(f"# {description}")
            body.append(line)
        enabled.append(name)

    marker_index = options_text.rfind("WEBKIT_OPTION_END()")
    if marker_index < 0:
        raise ValueError("WEBKIT_OPTION_END() not found")
    block = "\n" + "\n".join(body) + "\n"
    options_text = options_text[:marker_index] + block + options_text[marker_index:]
    opts.write_text(options_text, encoding="utf-8")
    return enabled, skipped


def main() -> int:
    if len(sys.argv) != 2:
        print(f"Usage: {Path(sys.argv[0]).name} UPSTREAM_SOURCE_DIR", file=sys.stderr)
        return 2
    try:
        enabled, skipped = apply(Path(sys.argv[1]).resolve())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print("Enabled:", ", ".join(enabled) or "(none)")
    print("Unsupported in this source:", ", ".join(skipped) or "(none)")
    return 0 if enabled else 1


if __name__ == "__main__":
    raise SystemExit(main())
