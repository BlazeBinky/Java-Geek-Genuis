# Odyssey Modernization Workbench

This directory is a downstream modernization and build layer for [deadwood2/OdysseyWebBrowser](https://github.com/deadwood2/OdysseyWebBrowser), kept isolated from this profile repository's existing files. It does not replace or vendor the entire upstream WebKit engine.

## Upstream and target

- Engine source: the upstream `odyssey` branch.
- The upstream documented active build path is **x86_64 AROS** using the AROS cross-compiler/SDK.
- For your intended **AmigaOS 4.1 Update 3 / PPC** target, I've added a separate experimental toolchain and build attempt. The latest source branch is not yet proven to compile on OS4; the 2019 [historical OS4 compile branch](https://github.com/deadwood2/OdysseyWebBrowser/tree/jaokim_os4_compile) is used as a porting reference, not substituted for the newer engine. Read [AmigaOS 4 port status](AMIGAOS4_PORT_STATUS.md).
- The current MUI configuration enables JIT and FTL JIT only for x86_64. On PPC it uses the C-loop JavaScript interpreter, so compiler `-O3` will not magically add a JavaScript JIT. The source patcher recognizes AmigaOS 4 compiler macros, corrects the missing-header reference in `BALBase.h`, and selects an OS4-specific allocator with explicit limitations. Features such as WebAssembly, WebCrypto, Web Audio, Service Workers and WebGL are disabled or unavailable in the upstream MUI configuration; do not force them on without fixing and testing their implementations.

## Preferred target: AmigaOS 4 PPC experiment

This requires a Linux/WSL or Docker environment with `ppc-amigaos-gcc`, `ppc-amigaos-g++`, and AmigaOS 4 target libraries/headers installed. The default include paths mirror the historical build setup; override them with `AMIGAOS4_SDK_ROOT`, `AMIGAOS4_MUI_INCLUDE_DIR`, `AMIGAOS4_CAIRO_INCLUDE_DIR`, `AMIGAOS4_FREETYPE_INCLUDE_DIR`, `AMIGAOS4_LIBXML2_INCLUDE_DIR`, and `AMIGAOS4_LIBXSLT_INCLUDE_DIR`.

On Windows with Docker Desktop (Linux containers) installed, you can start the same cross-build directly from PowerShell:

```powershell
.\OdysseyModern\run-os4-docker.ps1
```

On Linux/WSL with Docker, use:

```bash
bash OdysseyModern/run-os4-docker.sh
```

These wrappers use the pinned `walkero/amigagccondocker:os4-gcc11-3.5.2` image by default; override it with `ODYSSEY_OS4_DOCKER_IMAGE` on Linux or the `-Image` parameter on Windows. You can bypass Docker and run the SDK-backed script directly, from the repository root:

```bash
bash OdysseyModern/fetch-and-build-os4.sh
```

The first goal is to collect real configure/build errors and port the missing OS4 shims forward. The baseline disables XSLT because the pinned AmigaGCConDocker SDK image does not include the target libxslt headers; install a target libxslt library and headers, then set `ODYSSEY_OS4_XSLT=on` to enable it. This keeps the baseline from failing before CMake because of that optional component. The script automatically runs `prepare-os4-upstream.py` on a fresh or reset upstream `odyssey` checkout; it fails closed if expected source patterns have changed and preserves unrelated local edits. Its OS4 allocator currently uses page-aligned `AllocVecTags` memory and clears decommitted ranges instead of releasing physical pages, so it may retain more memory than a true virtual-memory backend. Guard pages are not implemented. Default `ODYSSEY_BUILD_PROFILE=balanced` uses `-O2`. For an experimental `-O3` build:

```bash
ODYSSEY_BUILD_PROFILE=fast bash OdysseyModern/fetch-and-build-os4.sh
```

Media is off by default for the first porting pass. Once all target dependencies are correctly installed, try it with `ODYSSEY_OS4_MEDIA=on`. Neither profile has been compiled or benchmarked in this environment, and neither is currently claimed to produce an OS4 executable.

## AROS reference build

On Linux/WSL with the AROS SDK installed as described in the [upstream build guide](https://github.com/deadwood2/OdysseyWebBrowser/blob/odyssey/README.md):

1. Ensure `x86_64-aros-gcc` and `x86_64-aros-g++` are on `PATH`.
2. If the SDK is not located beside the compiler installation, set `ODYSSEY_SDK_DIR` to its `Development` directory.
3. Run `bash OdysseyModern/fetch-and-build-aros.sh`.

The script saves logs in `cross-build-x86_64-aros/odyssey-build.log` and verifies JIT/FTL-JIT flags for that target. Upstream notes that compilation is very resource-intensive, with documented examples using 24–32 GB RAM; an 8 GB host may run out of memory even at one job.

## Performance and compatibility tests

Serve the repository root with a local HTTP server, then open these pages in the actual browser binary:

- [Capability smoke test](tests/modern-web-check.html) reports basic API and CSS capability signals. An exposed API is not proof of standards conformance.
- [Performance benchmark](benchmarks/browser-benchmark.html) measures JavaScript loop, array, DOM/layout, and canvas timings over five rounds; compare medians on the same hardware.

Pushes to this branch run GitHub Actions checks for Python syntax, both patchers' idempotence, JavaScript syntax, shell syntax and whitespace. A real AmigaOS 4 SDK configuration job can be launched manually from Actions, or triggered by a push commit message containing `[os4-configure]`; it downloads the pinned compiler image and uploads CMake logs. This configuration check is not a full compile. Those checks do not compile WebKit or certify browser compatibility. The native OS4 build, runtime startup, real HTTPS websites, compatibility and performance still require target testing.

## Scope and licensing

Real modernization requires engine-level changes, current TLS/certificate validation, storage and security tests, modern JS/CSS/network compatibility fixes, and target-platform validation. Preserve upstream copyright and license notices when redistributing source or binaries.
