#!/usr/bin/env bash
set -Eeuo pipefail

REPO_URL="https://github.com/deadwood2/OdysseyWebBrowser.git"
BRANCH="odyssey"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="${ODYSSEY_SOURCE_DIR:-$PWD/odyssey-upstream}"
PROFILE="${ODYSSEY_BUILD_PROFILE:-balanced}"

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
command -v git >/dev/null 2>&1 || fail "git is required."
command -v python3 >/dev/null 2>&1 || fail "python3 is required."
command -v cmake >/dev/null 2>&1 || fail "cmake is required."
command -v make >/dev/null 2>&1 || fail "make is required."
command -v x86_64-aros-gcc >/dev/null 2>&1 || fail "x86_64-aros-gcc not found. Install the AROS cross compiler and SDK first."
command -v x86_64-aros-g++ >/dev/null 2>&1 || fail "x86_64-aros-g++ not found. Install the AROS cross compiler and SDK first."

CCACHE_MODE="${ODYSSEY_CCACHE:-auto}"
USE_CCACHE=0
case "$CCACHE_MODE" in
  auto) if command -v ccache >/dev/null 2>&1; then USE_CCACHE=1; fi ;;
  on) command -v ccache >/dev/null 2>&1 || fail "ODYSSEY_CCACHE=on, but ccache is not installed."; USE_CCACHE=1 ;;
  off) ;;
  *) fail "ODYSSEY_CCACHE must be auto, on, or off." ;;
esac
if [[ "$USE_CCACHE" == "1" ]]; then
  export CCACHE_DIR="${CCACHE_DIR:-${XDG_CACHE_HOME:-$HOME/.cache}/odyssey-ccache-aros}"
  mkdir -p "$CCACHE_DIR"
  export ODYSSEY_COMPILER_LAUNCHER=ccache
  printf 'Compiler cache: enabled (%s)\n' "$CCACHE_DIR"
else
  unset ODYSSEY_COMPILER_LAUNCHER || true
  printf 'Compiler cache: unavailable/disabled\n'
fi
case "$PROFILE" in
  balanced) export ODYSSEY_OPT_LEVEL=2 ;;
  fast)     export ODYSSEY_OPT_LEVEL=3 ;;
  *) fail "ODYSSEY_BUILD_PROFILE must be 'balanced' or 'fast'." ;;
esac

# The upstream rebuild.sh hard-codes its developer's SDK path. Derive a local
# default from the compiler installation, but let users specify the SDK folder.
COMPILER_PATH="$(command -v x86_64-aros-gcc)"
COMPILER_PREFIX="$(dirname -- "$(dirname -- "$COMPILER_PATH")")"
SDK_DIR="${ODYSSEY_SDK_DIR:-$COMPILER_PREFIX/Development}"
[[ -d "$SDK_DIR/include" ]] || fail "SDK include directory not found at '$SDK_DIR/include'. Set ODYSSEY_SDK_DIR to the AROS SDK Development directory."

if [[ -d "$SOURCE_DIR/.git" ]]; then
  ORIGIN="$(git -C "$SOURCE_DIR" remote get-url origin)"
  [[ "$ORIGIN" == "$REPO_URL" || "$ORIGIN" == "${REPO_URL%.git}" ]] || fail "Existing source directory has a different Git origin: $ORIGIN"
  # Preserve user changes. Only the two files changed by prepare-upstream.py may
  # be dirty; anything else requires a fresh checkout or a manual backup first.
  while IFS= read -r status_line; do
    [[ -z "$status_line" ]] && continue
    changed_path="${status_line:3}"
    case "$changed_path" in
      rebuild.sh|Source/cmake/AROS.cmake) ;;
      *) fail "Refusing to discard local source changes in '$changed_path'. Back them up or choose a clean ODYSSEY_SOURCE_DIR." ;;
    esac
  done < <(git -C "$SOURCE_DIR" status --porcelain)
  git -C "$SOURCE_DIR" fetch --depth=1 origin "$BRANCH"
  git -C "$SOURCE_DIR" checkout "$BRANCH"
  git -C "$SOURCE_DIR" reset --hard "origin/$BRANCH"
else
  git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$SOURCE_DIR"
fi
python3 "$SCRIPT_DIR/prepare-upstream.py" "$SOURCE_DIR"
cd "$SOURCE_DIR"
[[ -x ./rebuild.sh ]] || fail "Upstream rebuild.sh was not found or is not executable."

# Conservative automatic parallelism: WebKit's compile is memory-heavy. Override
# BUILD_JOBS manually only if the build machine has sufficient RAM.
# Read the container's memory cap first. /proc/meminfo can expose host RAM
# inside Docker, which would otherwise cause parallel C++ compiles to OOM.
detect_memory_kb() {
  local raw=""
  if [[ -r /sys/fs/cgroup/memory.max ]]; then
    raw="$(cat /sys/fs/cgroup/memory.max)"
    if [[ "$raw" =~ ^[0-9]+$ ]]; then printf '%s\n' "$((raw / 1024))"; return; fi
  fi
  if [[ -r /sys/fs/cgroup/memory/memory.limit_in_bytes ]]; then
    raw="$(cat /sys/fs/cgroup/memory/memory.limit_in_bytes)"
    # cgroup v1 uses a huge sentinel value to mean "no limit".
    if [[ "$raw" =~ ^[0-9]+$ ]] && (( raw < 1125899906842624 )); then
      printf '%s\n' "$((raw / 1024))"; return
    fi
  fi
  if [[ -r /proc/meminfo ]]; then
    awk '/MemTotal:/ {print $2; exit}' /proc/meminfo
    return
  fi
  printf '0\n'
}

if [[ -n "${BUILD_JOBS:-}" ]]; then
  JOBS="$BUILD_JOBS"
else
  MEM_KB="$(detect_memory_kb)"
  if (( MEM_KB == 0 || MEM_KB < 16000000 )); then JOBS=1
  elif (( MEM_KB < 32000000 )); then JOBS=2
  elif (( MEM_KB < 64000000 )); then JOBS=4
  else JOBS=6
  fi
fi
[[ "$JOBS" =~ ^[1-9][0-9]*$ ]] || fail "BUILD_JOBS must be a positive integer."

export ODYSSEY_SDK_DIR="$SDK_DIR"
printf '\nOdyssey source: %s\nSDK: %s\nProfile: %s (-O%s)\nBuild jobs: %s\n' "$SOURCE_DIR" "$SDK_DIR" "$PROFILE" "$ODYSSEY_OPT_LEVEL" "$JOBS"
printf 'Configuring the upstream x86_64-aros Release build...\n'
printf '11\n' | ./rebuild.sh

BUILD_DIR="$SOURCE_DIR/cross-build-x86_64-aros"
[[ -f "$BUILD_DIR/CMakeCache.txt" ]] || fail "CMake configuration did not produce $BUILD_DIR/CMakeCache.txt."
grep -E 'CMAKE_BUILD_TYPE:STRING=Release|CMAKE_BUILD_TYPE:UNINITIALIZED=Release' "$BUILD_DIR/CMakeCache.txt" >/dev/null || printf 'WARNING: CMake build type not found as expected; inspect CMakeCache.txt.\n'
for feature in ENABLE_JIT ENABLE_FTL_JIT; do
  grep -E "^$feature:[^=]+=ON$" "$BUILD_DIR/CMakeCache.txt" >/dev/null || fail "$feature is not ON in CMakeCache.txt. This build would miss the x86_64 JIT performance path."
done
printf 'Verified: JavaScriptCore JIT and FTL JIT are enabled.\n'
printf 'Upstream revision: %s\n' "$(git rev-parse HEAD)"

printf '\nStarting compile. A low-memory PC may still run out of memory; the upstream README reports builds using 24–32 GB RAM.\n'
BUILD_START=$SECONDS
if [[ "$USE_CCACHE" == "1" ]]; then ccache --zero-stats; fi
cmake --build "$BUILD_DIR" --parallel "$JOBS" 2>&1 | tee "$BUILD_DIR/odyssey-build.log"
BUILD_SECONDS=$((SECONDS - BUILD_START))
printf '\nBuild command completed in %s seconds. Check cross-build-x86_64-aros/bin and run in a matching x86_64 AROS environment.\n' "$BUILD_SECONDS"
if [[ "$USE_CCACHE" == "1" ]]; then
  printf '\nCompiler-cache statistics (these affect build time, not page-rendering speed):\n'
  ccache --show-stats
fi
printf 'No browser compatibility claim is made until the produced binary passes OdysseyModern/tests and real-site tests.\n'
