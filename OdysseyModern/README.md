# Odyssey Modernization Workbench

This directory is an isolated modernization/build layer for the upstream Odyssey Web Browser source. It does **not** replace or copy the upstream WebKit engine.

## Upstream and target

- Upstream: https://github.com/deadwood2/OdysseyWebBrowser
- Source branch: `odyssey`
- Upstream build instructions currently describe an **x86_64 AROS** build using an AROS cross-compiler/SDK. That is not the same target as AmigaOS 4.1 Update 3; a native AmigaOS 4 build needs its own supported toolchain and platform-port work.
- The engine is a large WebKit codebase. A new UI or a few patches cannot make an old engine fully compatible with today's websites.

## Get the real source

Run `./OdysseyModern/fetch-and-build-aros.sh` from this repository on Linux/WSL, or run `OdysseyModern/fetch-source.ps1` on Windows to download and unpack the upstream branch. Source is fetched from the original project, not vendored here.

## Build (AROS x86_64)

1. Install the AROS cross compiler and SDK following the instructions in the upstream README: https://github.com/deadwood2/OdysseyWebBrowser/blob/odyssey/README.md
2. Confirm `x86_64-aros-gcc` and `x86_64-aros-g++` are on `PATH`.
3. Run `./OdysseyModern/fetch-and-build-aros.sh`.
4. The script invokes the upstream `rebuild.sh`, then builds `cross-build-x86_64-aros`.

The upstream project notes that compilation is resource-intensive (its documented examples use 24–32 GB RAM). A smaller machine may run out of memory.

## What is and is not implemented here

This folder provides repeatable source acquisition, build orchestration, and a browser compatibility/test plan. It does not claim that the source compiles in this environment, that an executable has been produced, or that modern-site compatibility is complete. No binary has been built or tested by this commit.

## Modernization order

1. Establish a reproducible baseline build and save compiler logs.
2. Fix compile/runtime regressions without silently changing upstream engine behavior.
3. Add automated engine tests and a compatibility matrix.
4. Audit TLS/certificate validation, supported TLS versions, cookies, storage, CSP, mixed content, downloads, and process isolation before treating the browser as safe for daily use.
5. Update WebKit and its bundled dependencies in coherent upstream-compatible steps; then test current JavaScript, CSS, HTML, HTTP/2/HTTP/3 support, WebAssembly, and real sites. Protocol and platform support depends on the selected port and libraries.

## Licensing

Preserve the upstream license and notices when modifying or redistributing Odyssey. Review the license files in the fetched source before distributing binaries.
