# Speed tuning without guessing

The fastest safe build is the one that can be measured and reproduced. There are separate knobs for **compile time** and **browser runtime**; a compiler cache improves the former only.

## AmigaOS 4.1 PPC

Default build, safer optimization and persistent compile cache when ccache exists:

```powershell
.\OdysseyModern\run-os4-docker.ps1 -Profile balanced -Ccache auto
```

Try the aggressive release optimization profile:

```powershell
.\OdysseyModern\run-os4-docker.ps1 -Profile fast -Ccache auto
```

Equivalent Linux/WSL commands:

```bash
ODYSSEY_BUILD_PROFILE=balanced ODYSSEY_CCACHE=auto bash OdysseyModern/run-os4-docker.sh
ODYSSEY_BUILD_PROFILE=fast ODYSSEY_CCACHE=auto bash OdysseyModern/run-os4-docker.sh
```

The Windows wrapper persists its cache under `%LOCALAPPDATA%\OdysseyBrowser\ccache`; the Linux wrapper uses `${XDG_CACHE_HOME:-$HOME/.cache}/odyssey-ccache`. Set `ODYSSEY_CCACHE=off` / `-Ccache off` to disable caching or `on` to require ccache. Logs and cache-hit statistics are recorded by the build script.

- `balanced` means GCC `-O2`; use this first while fixing platform-port build issues.
- `fast` means GCC `-O3`; it can make the executable larger and may not improve every workload. Compare medians from the same hardware using `benchmarks/browser-benchmark.html`.
- The AmigaOS 4 build now applies the same selected `-O2`/`-O3` profile and optional `AMIGAOS4_CPU_FLAGS` to the statically built ICU library as well as WebKit. This makes the optimization profile consistent for text shaping/collation workloads; it is a hypothesis to benchmark, not a claimed speedup.
- `BUILD_JOBS` (or `-Jobs` in PowerShell) changes compile parallelism. More jobs can make builds faster but can exhaust RAM. Odyssey's own build guide reports large memory requirements; start conservatively on an 8 GB machine.
- `AMIGAOS4_CPU_FLAGS` lets an expert supply flags for the **actual target PPC CPU**; both Docker wrappers forward it into the container. Example (only after confirming the real CPU): `AMIGAOS4_CPU_FLAGS="-mcpu=750" bash OdysseyModern/run-os4-docker.sh`, or `-CpuFlags "-mcpu=750"` with PowerShell. There is deliberately no hard-coded G3/G4/G5/embedded CPU flag: unsupported instruction flags can produce a binary that crashes on another OS4 machine. Confirm the CPU model and compiler support before setting it.

## Runtime performance limits

The newer MUI configuration enables JavaScriptCore's JIT/FTL paths on x86-64 where upstream supports them, but the experimental PPC/AmigaOS 4 profile explicitly disables JIT/FTL JIT and uses the C-loop interpreter. GCC `-O3` helps optimize the interpreter's C/C++ code; it does not turn an interpreter into a JIT. JavaScript-heavy websites may therefore remain significantly slower on OS4/PPC than on contemporary desktop browsers.

Do not enable JIT by editing a boolean unless the engine has a correct PPC machine-code backend, executable-memory support, cache coherency handling, exception/unwind integration, and tests. Incorrect executable-memory handling is a crash/security risk. A real PPC JIT port is a separate engine project.

## Benchmark procedure

1. Build `balanced`, open `benchmarks/browser-benchmark.html` in the target browser, let one warm-up run complete, then run the five-round benchmark.
2. Save the entire report, including user-agent, screen size and date.
3. Repeat with `fast` using the same machine, power settings, browser cache state, test-page revision and background workload.
4. Select and copy the machine-readable JSON from the benchmark page into files such as `balanced.json` and `fast.json`.
5. Compare them with `python OdysseyModern/benchmarks/compare-reports.py balanced.json fast.json`. The tool reports percentage faster/slower and treats sub-0.5% changes as noise.
6. Compare the median for every category, not one result. If a difference is not repeatable, keep the smaller/more stable build.
7. Separately test several real HTTPS sites. Synthetic JavaScript timings do not measure certificate negotiation, layout complexity, networking, memory pressure or overall compatibility.

No speedup is claimed until results are recorded on the actual target.
