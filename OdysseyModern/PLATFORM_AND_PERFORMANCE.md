# Platform and performance reality check

## Target platform distinction

The upstream `odyssey` README documents a **Release x86_64 AROS** build. The MUI port sources also use AROS/MorphOS-specific integrations. This is not proof of a native AmigaOS 4.1 Update 3 build; an AROS x86_64 executable cannot be treated as an AmigaOS 4/PPC executable.

Before claiming an AmigaOS 4.1 build, this project needs a supported OS4/PPC compiler and SDK, a real OS4 platform/backend, and fixes for any ABI, GUI, threading, memory, networking, TLS, graphics and media differences. Do not distribute the AROS binary as an OS4 executable.

## Performance decisions

- The MUI options in the upstream branch already enable JavaScriptCore JIT and FTL JIT on x86_64. Preserve that optimized code path in the AROS profile.
- The upstream AROS toolchain uses `-O2 -DNDEBUG` for Release. The downstream prepare script exposes an experimental `ODYSSEY_BUILD_PROFILE=fast` / `-O3` build. It may increase code size and compile time and is not assumed faster without on-target benchmarks.
- Do not add `-march=native` to cross builds: it describes the build host, not necessarily the target CPU.
- Do not force WebAssembly, WebCrypto, Web Audio, Service Workers or WebGL on just to make a feature checklist look better. The upstream MUI options say some features are disabled or do not compile for this port. Enabling them without working implementations/dependencies can break the browser.
- Compilation is memory-intensive. The build script chooses conservative parallelism based on host RAM and allows `BUILD_JOBS` to override it.

## How to measure whether a change helped

Serve the repository root with a local HTTP server, open:

- `OdysseyModern/benchmarks/browser-benchmark.html`
- `OdysseyModern/tests/modern-web-check.html`

Capture at least five results before and after a change using the same machine, build type, test page, and power configuration. Compare the median, not the best run. Save user-agent, date, target OS, compiler flags and logs alongside the results.

A successful C++ compile only demonstrates buildability. A fast browser also needs real-site tests, stable layout/rendering, memory tests, working HTTPS validation, and current enough JavaScript/CSS/network implementations.

## AmigaOS 4 memory-allocation hot path

The experimental `OSAllocatorAmigaOS4.cpp` preserves WebKit's zeroed-reservation contract by asking Exec to clear each initial allocation. It uses `MEMF_SHARED`, not `MEMF_PRIVATE`, because WebKit's workers and garbage collector can touch heap pages outside the allocating task; `AVT_Lock, FALSE` avoids permanently pinning the entire browser heap in physical RAM. Because this fallback cannot physically decommit pages, `decommit()` clears released ranges; `commit()` can therefore be a logical no-op rather than writing the same bytes a second time. Likewise, `reserveAndCommit()` reuses the already-zeroed reservation instead of clearing it again. The patcher tests assert shared/swappable allocations, the zeroing contract, and no duplicate clear pass. This reduces redundant writes and avoids task-private memory misuse, but is not yet a measured whole-browser speedup.

## Current experimental PPC feature profile

The current AmigaOS 4 build script deliberately uses the JavaScript C-loop interpreter: the upstream source has no PowerPC JIT assembler backend, and this OS4 port does not provide executable-memory/cache-flush support. Do not enable JIT/FTL without a real PPC backend and correct instruction-cache synchronization.

The OS4 profile currently disables WebAssembly, Web Audio, Web Crypto, WebGL, Service Workers, and Media Source/Video by default; XSLT is also optional when the matching target dependency is installed. These are real implementation/build limitations, not merely hidden UI switches. The compatibility test page exercises Promise callbacks, modules, actual grid/flex/custom-property layout, IndexedDB read/write, Fetch, WebAssembly, and Web Crypto when those APIs exist. Run it on the target binary and treat failed/missing entries as work items.

Until current TLS validation, certificate handling, supported cipher suites, and current-site compatibility are tested on the actual target, do not use this experimental build for sensitive logins or treat a successful home-page load as proof of safe modern browsing.

