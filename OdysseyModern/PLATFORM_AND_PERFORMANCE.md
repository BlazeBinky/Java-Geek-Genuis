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
