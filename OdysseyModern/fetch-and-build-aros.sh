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
if [[ -n "${BUILD_JOBS:-}" ]]; then
  JOBS="$BUILD_JOBS"
elif [[ -r /proc/meminfo ]]; then
  MEM_KB="$(awk '/MemTotal:/ {print $2}' /proc/meminfo)"
  if (( MEM_KB < 16000000 )); then JOBS=1
  elif (( MEM_KB < 32000000 )); then JOBS=2
  elif (( MEM_KB < 64000000 )); then JOBS=4
  else JOBS=6
  fi
else
  JOBS=1
fi
[[ "$JOBS" =~ ^[1-9][0-9]*$ ]] || fail "BUILD_JOBS must be a positive integer."

export ODYSSEY_SDK_DIR="$SDK_DIR"
printf '\nOdyssey source: %s\nSDK: %s\nProfile: %s (-O%s)\nBuild jobs: %s\n' "$SOURCE_DIR" "$SDK_DIR" "$PROFILE" "$ODYSSEY_OPT_LEVEL" "$JOBS"
printf 'Configuring the upstream x86_64-aros Release build...\n'
printf '11\n' | ./rebuild.sh

BUILD_DIR="$SOURCE_DIR/cross-build-x86_64-aros"
[[ -f "$BUILD_DIR/CMakeCache.txt" ]] || fail "CMake configuration did not produce $BUILD_DIR/CMakeCache.txt."
grep -E 'CMAKE_BUILD_TYPE:STRING=Release|CMAKE_BUILD_TYPE:UNINITIALIZED=Release' "$BUILD_DIR/CMakeCache.txt" >/dev/null || printf 'WARNING: CMake build type not found as expected; inspect CMakeCache.txt.\n'

printf '\nStarting compile. A low-memory PC may still run out of memory; the upstream README reports builds using 24–32 GB RAM.\n'
cmake --build "$BUILD_DIR" --parallel "$JOBS" 2>&1 | tee "$BUILD_DIR/odyssey-build.log"

printf '\nBuild command completed. Check cross-build-x86_64-aros/bin and run in a matching x86_64 AROS environment.\n'
printf 'No browser compatibility claim is made until the produced binary passes OdysseyModern/tests and real-site tests.\n'
