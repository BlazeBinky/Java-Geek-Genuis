# Odyssey Modernization Workbench

This directory is a downstream modernization and build layer for [deadwood2/OdysseyWebBrowser](https://github.com/deadwood2/OdysseyWebBrowser), kept isolated from this profile repository's existing files. It does not replace or vendor the entire upstream WebKit engine.

## Upstream and target

- Engine source: defaults to the verified upstream `odyssey` snapshot at commit `8e44b6d8d2e8c57a59a878152b81304e220ceb7a` for reproducible builds; set `ODYSSEY_UPSTREAM_REF=odyssey` to intentionally track the moving branch. The exact source SHA is printed in the build log and packaged metadata.
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

These wrappers use the pinned `walkero/amigagccondocker:os4-gcc11-3.5.2` image by default; override it with `ODYSSEY_OS4_DOCKER_IMAGE` on Linux or the `-Image` parameter on Windows. They persist a ccache directory across Docker runs when ccache is installed; use `ODYSSEY_CCACHE=off` (Linux) or `-Ccache off` (Windows) to disable it. See [Speed tuning and benchmarking](BENCHMARKING_AND_TUNING.md) for runtime tuning caveats. You can bypass Docker and run the SDK-backed script directly, from the repository root:

```bash
bash OdysseyModern/fetch-and-build-os4.sh
```

The OS4 build bootstraps **ICU 55.1** from a pinned upstream tag: it builds native Linux generator tools separately, then cross-compiles static ICU libraries for PPC and supplies target headers/libraries to CMake. It does not link host Linux libraries into the AmigaOS executable. Two real cross-build issues are now covered by regression tests: ICU's native generators must read the completed host-native little-endian data tree for their auxiliary `-i` inputs, while their output directories remain PPC big-endian; and static data packaging must use `pkgdata -w` so generated C arrays are compiled into target PPC objects rather than having host `genccode` try to emit an opposite-endian ELF object. A full CI cross-build is in progress with these fixes; no OS4 executable is claimed until the app links and starts on the target system.

The baseline disables XSLT because the pinned AmigaGCConDocker SDK image does not include the target libxslt headers; install target libxslt headers/library and set `ODYSSEY_OS4_XSLT=on` to enable it. The script automatically runs `prepare-os4-upstream.py` on a fresh or reset upstream `odyssey` checkout; it fails closed if expected source patterns have changed and preserves unrelated local edits. Its OS4 allocator currently uses page-aligned `AllocVecTags` memory and clears decommitted ranges instead of releasing physical pages, so it may retain more memory than a true virtual-memory backend. Guard pages are not implemented. Default `ODYSSEY_BUILD_PROFILE=balanced` uses `-O2`. For an experimental `-O3` build:

```bash
ODYSSEY_BUILD_PROFILE=fast bash OdysseyModern/fetch-and-build-os4.sh
```

Media is off by default for the first porting pass. Once all target dependencies are correctly installed, try it with `ODYSSEY_OS4_MEDIA=on`. The initial PPC compile failed before producing an executable because ICU was missing. The branch now builds ICU as part of the OS4 toolchain workflow and is attempting a clean end-to-end build; no executable is considered deliverable until the linker succeeds and the browser starts on OS4.

## AROS reference build

On Linux/WSL with the AROS SDK installed as described in the [upstream build guide](https://github.com/deadwood2/OdysseyWebBrowser/blob/odyssey/README.md):

1. Ensure `x86_64-aros-gcc` and `x86_64-aros-g++` are on `PATH`.
2. If the SDK is not located beside the compiler installation, set `ODYSSEY_SDK_DIR` to its `Development` directory.
3. Run `bash OdysseyModern/fetch-and-build-aros.sh`.

The script saves logs in `cross-build-x86_64-aros/odyssey-build.log` and verifies JIT/FTL-JIT flags for that target. It detects Docker/cgroup memory caps before choosing parallel jobs, instead of blindly using host RAM. Upstream notes that compilation is very resource-intensive, with documented examples using 24–32 GB RAM; an 8 GB host may still run out of memory even at one job. You can override the detected parallelism using `BUILD_JOBS=1` (or another positive integer), but increasing it can cause out-of-memory failures.

## Performance and compatibility tests

Serve the repository root with a local HTTP server, then open these pages in the actual browser binary:

- [Modern-web test suite](tests/modern-web-check.html) reports API signals and functional smoke tests for Promises, modules, Fetch, actual Grid/Flexbox/custom-property layout, IndexedDB, WebAssembly and Web Crypto where supported. A skipped test may require HTTP(S) or a missing API; passing smoke tests are not proof of standards conformance.
- [Performance benchmark v3](benchmarks/browser-benchmark.html) measures JavaScript arithmetic, array scans, object/property access, string/regex work, DOM/layout, and canvas over five rounds; the report comparer continues to accept old v2 reports. Compare medians on the same hardware.

Pushes to this branch run GitHub Actions checks for Python syntax, patcher behavior, allocator memory guarantees, ICU cross-build setup, the native C++ thread linker preflight, JavaScript syntax, shell syntax, CMake syntax and whitespace. For a full build, open **Actions → Odyssey modernization checks → Run workflow**, select `full-build`, then choose `balanced` (`-O2`) or `fast` (`-O3`). `configure-only` checks the SDK/CMake configuration without compiling WebKit; `checks-only` runs the static tests. A push commit message containing `[os4-build]` also triggers the full build, and adding `[fast]` to that message selects the fast profile. The workflow uploads compiler logs and only packages an executable if linking succeeds. None of these checks certifies browser compatibility or security; startup, real HTTPS websites and runtime speed still require testing on the target OS4 hardware.

## Scope and licensing

Real modernization requires engine-level changes, current TLS/certificate validation, storage and security tests, modern JS/CSS/network compatibility fixes, and target-platform validation. Preserve upstream copyright and license notices when redistributing source or binaries.
