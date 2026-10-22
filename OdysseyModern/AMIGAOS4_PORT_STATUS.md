# AmigaOS 4.1 Update 3: experimental port status

## What exists upstream

The upstream repository has a historical branch, [`jaokim_os4_compile`](https://github.com/deadwood2/OdysseyWebBrowser/tree/jaokim_os4_compile), whose last listed commits date to April 9, 2019. It contains an `AMIGAOS4.cmake` toolchain, a Docker-based build recipe, and AmigaOS-specific compatibility edits.

That branch is valuable as a porting reference, but it is based on a much older WebKit/Odyssey tree. The newer `odyssey` branch used for this project no longer contains all of the old `AMIGAOS4` source shims. Copying the old branch wholesale would trade away years of engine changes and would not meet the modern-web goal.

## What this branch adds

- An experimental PPC toolchain file modelled on the historical setup.
- A separate `fetch-and-build-os4.sh` script that uses the newer `odyssey` source branch.
- Configurable SDK include paths, Release optimization (`-O2` default, experimental `-O3`), conservative build parallelism and separate logs.
- A pinned ICU 55.1 bootstrap that builds native ICU code-generation tools on Linux, cross-compiles static ICU libraries for PPC, and passes target paths to CMake. It specifically avoids using the source tree's macOS-only ICU headers or host Linux libraries. CI is still checking this dependency/bootstrap.
- Optional persistent `ccache` support in both OS4 Docker wrappers. This reduces repeat compile time when the same C/C++ translation units are rebuilt; it does **not** speed up page rendering.
- A benchmark page that resets canvas state each round and reports medians for JavaScript, array, DOM/layout and canvas operations. Compare `balanced` and `fast` builds using the same target and see [benchmarking and tuning](BENCHMARKING_AND_TUNING.md).
- An optional media-off baseline build because the historical OS4 port disabled video due to FFmpeg/Acinerella dependency problems. `ODYSSEY_OS4_MEDIA=on` can test the full video path when compatible target libraries are installed.

## Known blockers; not yet solved

1. The first full C++ compile stopped on the missing `unicode/ustring.h` dependency. ICU is now built from pinned source as part of the OS4 toolchain step, but that cross-build and the resulting WebKit compile/link have not yet completed successfully.
2. Other AmigaOS-specific API shims and build rules may still need to be ported forward from the historical branch; each newly exposed compiler/linker error needs a tested fix.
3. The historical branch requires a stack of target libraries (including MUI, Cairo, FreeType, ICU, curl/TLS, libxml2/libxslt, SQLite and zlib). Host Linux libraries must never be linked into the PPC target binary.
4. The current MUI configuration disables JIT and FTL JIT when the target is not x86_64 and selects the C-loop interpreter. `-O3` optimizes C++ code but does not create a PPC JavaScript JIT; heavy JavaScript pages may remain slow.
5. WebAssembly is documented as not compiling for the MUI port, and Web Audio, WebCrypto, WebGL and Service Workers are disabled in the current MUI options. Do not advertise support until the implementation and conformance tests pass.
6. A successful cross-compile is not proof that the browser starts, loads HTTPS sites safely, renders pages correctly, plays media, or remains stable on AmigaOS 4.1 Update 3.

## Trying the experimental build

Use a Linux/WSL or Docker environment that supplies `ppc-amigaos-gcc`, `ppc-amigaos-g++`, and AmigaOS 4 target headers/libraries. The default include paths mirror the historical branch; override them with `AMIGAOS4_MUI_INCLUDE_DIR`, `AMIGAOS4_CAIRO_INCLUDE_DIR`, `AMIGAOS4_FREETYPE_INCLUDE_DIR`, `AMIGAOS4_LIBXML2_INCLUDE_DIR`, and `AMIGAOS4_LIBXSLT_INCLUDE_DIR` as needed.

```bash
./OdysseyModern/fetch-and-build-os4.sh
```

For an experimental `-O3` build:

```bash
ODYSSEY_BUILD_PROFILE=fast ./OdysseyModern/fetch-and-build-os4.sh
```

To try building media support after installing compatible target dependencies:

```bash
ODYSSEY_OS4_MEDIA=on ./OdysseyModern/fetch-and-build-os4.sh
```

The first configure passed, but the first compile stopped before linking on missing ICU headers. The branch now has an ICU cross-build step and a full-build CI job; until that job produces an executable and it is run on AmigaOS 4.1 Update 3, no native OS4 binary is considered finished or verified.
