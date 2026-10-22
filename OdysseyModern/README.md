# Odyssey Modernization Workbench

This directory is a downstream modernization and build layer for [deadwood2/OdysseyWebBrowser](https://github.com/deadwood2/OdysseyWebBrowser), kept isolated from this profile repository's existing files. It does not replace or vendor the entire upstream WebKit engine.

## Upstream and target

- Source branch: `odyssey`
- The upstream build instructions document **x86_64 AROS** using the AROS cross-compiler/SDK. That is **not** the same binary target as AmigaOS 4.1 Update 3. A native OS4/PPC build requires a supported OS4 toolchain and platform port; this branch does not claim that port exists.
- The MUI configuration already enables JIT and FTL JIT on x86_64. Features such as WebAssembly, WebCrypto, Web Audio, Service Workers and WebGL are disabled or unavailable in the upstream MUI configuration; do not force them on without fixing and testing their implementations.
- See [platform/performance notes](PLATFORM_AND_PERFORMANCE.md).

## Build the source

On Linux/WSL with the AROS SDK installed as described in the [upstream build guide](https://github.com/deadwood2/OdysseyWebBrowser/blob/odyssey/README.md):

1. Ensure `x86_64-aros-gcc` and `x86_64-aros-g++` are on `PATH`.
2. If the SDK is not located beside the compiler installation, set `ODYSSEY_SDK_DIR` to its `Development` directory.
3. Run `./OdysseyModern/fetch-and-build-aros.sh` from the repository root.

The build script fetches the upstream branch, applies checked downstream changes to make the SDK path portable and set compiler optimization levels, automatically configures x86_64 AROS Release, chooses conservative build parallelism based on RAM, and saves build output in `cross-build-x86_64-aros/odyssey-build.log`.

The default profile uses `-O2`. To try the experimental `-O3` profile, run:
```bash
ODYSSEY_BUILD_PROFILE=fast ./OdysseyModern/fetch-and-build-aros.sh
```
`-O3` is a benchmark candidate, not a guarantee of faster pages; it can increase executable size and compile time. Use the same device to compare the browser benchmark before deciding.

The upstream project notes that compilation is very resource-intensive, with documented examples using 24–32 GB RAM. This machine's 8 GB configuration may run out of memory even at one job.

## Tests included

Serve the repository root with a local HTTP server, then open these pages in the actual browser binary:

- `OdysseyModern/tests/modern-web-check.html` — reports basic API/CSS capability signals.
- `OdysseyModern/benchmarks/browser-benchmark.html` — measures JavaScript, arrays, DOM/layout, and canvas timing; compare the median of five rounds on the same hardware.

Pushes to this branch run GitHub Actions checks for Python syntax, patch idempotence, shell syntax and whitespace. **Those checks do not compile WebKit or certify browser compatibility.** The first real build and on-target website tests remain outstanding.

## Scope and licensing

The immediate step is a reproducible baseline and performance/feature measurement. Real modernization still requires engine-level fixes, current TLS/certificate validation, storage and security tests, compatibility fixes, and target-platform validation. Preserve upstream copyright/license notices when redistributing source or binaries.
