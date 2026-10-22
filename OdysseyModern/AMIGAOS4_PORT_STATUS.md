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

## Latest build repair notes (October 22, 2026)

The previous full build stopped inside ICU before reaching WebKit. The first error showed that ICU's generated Makefiles looked for the target platform fragment under the configured install prefix, rather than only under ICU's source `config/` directory. The build now stages `mh-unknown` at `$PREFIX/share/icu/55.1/config/mh-unknown` before running target `make`, and a regression check verifies that ordering.

The next ICU issue found in the PPC port is that ICU 55.1's generic timezone code can reference a `tzname` array even when the OS4 libc configuration reports that it is unavailable. The bootstrap now patches this guarded fallback in its disposable ICU source checkout and is safe to rerun after a failed build. The static workflow tests pass with the fragment-staging check; a clean full build with both repairs is the next acceptance gate.

The OS4 allocator now uses `MEMF_SHARED` instead of `MEMF_PRIVATE`: the Exec documentation defines private memory as accessible only in the allocating task, while WebKit's worker and GC paths can access shared engine pages. `AVT_Lock, FALSE` also avoids pinning the fallback's retained allocations in RAM. New memory is still zero-initialized and decommitted ranges are cleared before reuse. This is a cross-task correctness and memory-pressure fix; it has not yet been measured on the target device.

The latest full build reached ICU data generation. A host-native `genbrk` tool was invoked with the incomplete big-endian target data directory (`icudt55b`) and failed with `U_FILE_ACCESS_ERROR`. The cross-build script now patches ICU's two intermediate-data lookup rules to use the complete host-native data directory (`icudt55l`) while leaving generated output paths directed at the PPC target tree. This matches the fact that these data-generation utilities execute on Linux, not on the target Amiga. The regression test checks the patch happens before target configure and leaves output destinations separate. A fresh end-to-end run is needed to verify that this clears the blocker and reaches WebKit compilation.

Performance remains deliberately conservative until an actual PPC binary can be benchmarked: the default profile is `-O2`, the experimental `fast` profile is `-O3`, and ccache only shortens rebuilds. The current JavaScript engine has no PPC JIT backend, so interpreter performance is still a known major limitation. Do not call it fast or fully modern without target-device measurements and a proper PPC JIT implementation.

## Native PPC C++ threading correction (October 22, 2026)

The complete ICU target build progressed beyond platform-fragment generation, then stopped linking the target `makeconv` utility with unresolved `__gthread_*` references from the AmigaOS GCC C++ runtime. This is a known toolchain configuration issue: select the native threading backend with `-athread=native` and link `-lpthread`. The ICU cross-build and the final Odyssey target link now request those settings, and the static regression test checks both. The next full PPC build must verify that ICU now links and expose any following WebKit errors.

A successful static check is not a successful browser build. There is still no verified OS4 executable, no on-device page-load benchmark, and no proof that modern sites are secure or compatible. The current engine configuration uses the C-loop JavaScript interpreter on PPC because this branch has no PowerPC JIT backend; compiler optimization flags cannot replace one.

## ICU data-generation cross-build fix (October 22, 2026)

The native-thread flags fixed ICU's target utility link errors, and the next full PPC attempt completed target utility compilation but failed at the first target break-iterator data rule: the Linux-host `genbrk` executable was told to initialize ICU from the incomplete big-endian target tree `out/build/icudt55b`, then returned `U_FILE_ACCESS_ERROR`. The host build's equivalent `icudt55l` data tree was already complete. The first attempted patch assumed two `-i $(BUILDDIR)` references, but the pinned ICU template contains 29 generator input paths; the guard rejected the mismatch before writing a partial patch. The corrected patch now redirects all 29 `-i` inputs to the completed host-data tree while preserving their separate target `-d`/`-o` output paths, and the regression test guards the exact expected count and idempotence. The next full PPC run must verify ICU generation proceeds before any Odyssey/WebKit build can be considered.

