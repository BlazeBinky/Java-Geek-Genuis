#!/usr/bin/env python3
"""Source-level guard for Odyssey's HTTPS verification defaults on the MUI port.

This is a regression check, not a substitute for testing the exact built binary,
CA bundle contents, TLS library versions, or real HTTPS endpoints.
"""
from __future__ import annotations

from pathlib import Path
import sys


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: test_upstream_security.py ODYSSEY_SOURCE_DIR", file=sys.stderr)
        return 2

    root = Path(sys.argv[1])
    curl_context = root / "Source/WebCore/platform/network/curl/CurlContext.cpp"
    mui_ssl = root / "Source/WebCore/platform/network/mui/CurlSSLHandleMUI.cpp"
    if not curl_context.is_file() or not mui_ssl.is_file():
        print("ERROR: expected Odyssey MUI curl networking sources were not found.", file=sys.stderr)
        return 2

    source = curl_context.read_text(encoding="utf-8")
    cert_source = mui_ssl.read_text(encoding="utf-8")

    def require(condition: bool, description: str) -> None:
        if not condition:
            raise AssertionError(description)

    ssl_start = source.find("void CurlHandle::enableSSLForHost")
    ssl_end = source.find("CURLcode CurlHandle::willSetupSslCtx", ssl_start)
    require(ssl_start >= 0 and ssl_end > ssl_start, "Could not locate the HTTPS configuration function.")
    ssl_block = source[ssl_start:ssl_end]

    require(
        "setSslVerifyPeer(CurlHandle::VerifyPeer::Enable);" in ssl_block,
        "The normal HTTPS path must verify certificate chains.",
    )
    require(
        "setSslVerifyHost(CurlHandle::VerifyHost::StrictNameCheck);" in ssl_block,
        "The normal HTTPS path must strictly verify host names.",
    )
    require(
        "setSslVerifyPeer(CurlHandle::VerifyPeer::Disable);" in ssl_block
        and "setSslVerifyHost(CurlHandle::VerifyHost::LooseNameCheck);" in ssl_block,
        "The explicit certificate-ignore override should remain separately identifiable for review.",
    )
    require(
        "if (url.protocolIs(\"https\"))" in source and "enableSSLForHost(m_url.host().toString());" in source,
        "HTTPS navigation must enter the explicit SSL configuration path.",
    )
    require(
        "CURLPROTO_HTTP | CURLPROTO_HTTPS" in source and "CURLPROTO_FILE" in source,
        "The permitted Curl URL protocol list changed; review local-file and HTTP navigation security.",
    )
    require(
        "ENV:SYS/Certificates/ca-bundle.crt" in cert_source,
        "The MUI port's expected CA-bundle path changed; verify certificate loading before changing it.",
    )

    print("PASS: source-level HTTPS verification defaults and CA-bundle path are present.")
    print("LIMIT: does not verify that the target OS has a current CA bundle or modern TLS libraries.")
    print("LIMIT: run invalid-certificate and hostname-mismatch tests on the compiled AmigaOS binary.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, AssertionError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
