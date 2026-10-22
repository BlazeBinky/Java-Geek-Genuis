#!/usr/bin/env bash
set -Eeuo pipefail

REPO_URL="https://github.com/deadwood2/OdysseyWebBrowser.git"
BRANCH="odyssey"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="${ODYSSEY_SOURCE_DIR:-$PWD/odyssey-upstream-os4}"
if [[ -n "${AMIGAOS4_SDK_ROOT:-}" ]]; then
  SDK_ROOT="$AMIGAOS4_SDK_ROOT"
elif [[ -d /opt/ppc-amigaos/ppc-amigaos/SDK ]]; then
  # Default layout used by walkero/amigagccondocker OS4 images.
  SDK_ROOT="/opt/ppc-amigaos/ppc-amigaos/SDK"
else
  # Legacy adtools/Docker setups may use this path.
  SDK_ROOT="/usr/ppc-amigaos/SDK"
fi
PROFILE="${ODYSSEY_BUILD_PROFILE:-balanced}"
MEDIA="${ODYSSEY_OS4_MEDIA:-off}"
XSLT="${ODYSSEY_OS4_XSLT:-off}"

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
for tool in git python3 cmake make ppc-amigaos-gcc ppc-amigaos-g++; do
  command -v "$tool" >/dev/null 2>&1 || fail "$tool not found. Use an AmigaOS 4 PPC cross-compiler environment with the matching SDK."
done

case "$PROFILE" in
  balanced) export ODYSSEY_OPT_LEVEL=2 ;;
  fast)     export ODYSSEY_OPT_LEVEL=3 ;;
  *) fail "ODYSSEY_BUILD_PROFILE must be 'balanced' or 'fast'." ;;
esac
case "$MEDIA" in
  off) MEDIA_FLAG=OFF ;;
  on)  MEDIA_FLAG=ON ;;
  *) fail "ODYSSEY_OS4_MEDIA must be 'on' or 'off'." ;;
esac
case "$XSLT" in
  off) XSLT_FLAG=OFF ;;
  on)  XSLT_FLAG=ON ;;
  *) fail "ODYSSEY_OS4_XSLT must be 'on' or 'off'." ;;
esac

# Support both the historical SDK layout and current AmigaGCConDocker layout.
if [[ -n "${AMIGAOS4_MUI_INCLUDE_DIR:-}" ]]; then
  MUI_INCLUDE_DIR="$AMIGAOS4_MUI_INCLUDE_DIR"
elif [[ -d "$SDK_ROOT/MUI/C/include" ]]; then
  MUI_INCLUDE_DIR="$SDK_ROOT/MUI/C/include"
else
  MUI_INCLUDE_DIR="$SDK_ROOT/local/common/include"
fi
if [[ -n "${AMIGAOS4_CAIRO_INCLUDE_DIR:-}" ]]; then
  CAIRO_INCLUDE_DIR="$AMIGAOS4_CAIRO_INCLUDE_DIR"
elif [[ -d "$SDK_ROOT/local/newlib/include/cairo" ]]; then
  CAIRO_INCLUDE_DIR="$SDK_ROOT/local/newlib/include/cairo"
else
  CAIRO_INCLUDE_DIR="$SDK_ROOT/local/common/include/cairo"
fi
FREETYPE_INCLUDE_DIR="${AMIGAOS4_FREETYPE_INCLUDE_DIR:-$SDK_ROOT/local/common/include/freetype2}"
LIBXML2_INCLUDE_DIR="${AMIGAOS4_LIBXML2_INCLUDE_DIR:-$SDK_ROOT/local/common/include/libxml2}"
# Libxslt is absent from the pinned base SDK image. Enable XSLT only when its
# target headers and target library are installed.
if [[ -n "${AMIGAOS4_LIBXSLT_INCLUDE_DIR:-}" ]]; then
  LIBXSLT_INCLUDE_DIR="$AMIGAOS4_LIBXSLT_INCLUDE_DIR"
elif [[ -d "$SDK_ROOT/local/common/include/libxslt" ]]; then
  LIBXSLT_INCLUDE_DIR="$SDK_ROOT/local/common/include"
elif [[ -d "$SDK_ROOT/local/newlib/include/libxslt" ]]; then
  LIBXSLT_INCLUDE_DIR="$SDK_ROOT/local/newlib/include"
else
  LIBXSLT_INCLUDE_DIR=""
fi
export AMIGAOS4_SDK_ROOT="$SDK_ROOT"
export AMIGAOS4_MUI_INCLUDE_DIR="$MUI_INCLUDE_DIR"

for dir in "$MUI_INCLUDE_DIR" "$CAIRO_INCLUDE_DIR" "$FREETYPE_INCLUDE_DIR" "$LIBXML2_INCLUDE_DIR"; do
  [[ -d "$dir" ]] || fail "Required target include directory not found: $dir. Set the matching AMIGAOS4_*_INCLUDE_DIR variable."
done
if [[ "$XSLT_FLAG" == "ON" ]]; then
  [[ -n "$LIBXSLT_INCLUDE_DIR" && -d "$LIBXSLT_INCLUDE_DIR/libxslt" ]] || fail "XSLT was requested but a header root containing libxslt/*.h was not found. Install target libxslt and set AMIGAOS4_LIBXSLT_INCLUDE_DIR to that include root."
fi

if [[ -d "$SOURCE_DIR/.git" ]]; then
  ORIGIN="$(git -C "$SOURCE_DIR" remote get-url origin)"
  [[ "$ORIGIN" == "$REPO_URL" || "$ORIGIN" == "${REPO_URL%.git}" ]] || fail "Existing source directory has a different Git origin: $ORIGIN"

  # The patcher edits only these tracked files and adds exactly two expected
  # source files. Fail closed if the user has other source changes.
  while IFS= read -r status_line; do
    [[ -z "$status_line" ]] && continue
    changed_path="${status_line:3}"
    case "$changed_path" in
      Source/WTF/wtf/Platform.h|Source/WebCore/platform/mui/BALBase.h|Source/WTF/wtf/PlatformMUI.cmake|Source/WTF/wtf/OSAllocatorAmigaOS4.cpp|Source/WTF/wtf/strcasecmpAmigaOS4.cpp) ;;
      *) fail "Refusing to reset unrecognized local source change '$changed_path'. Back it up or choose a fresh ODYSSEY_SOURCE_DIR." ;;
    esac
  done < <(git -C "$SOURCE_DIR" status --porcelain)

  git -C "$SOURCE_DIR" fetch --depth=1 origin "$BRANCH"
  git -C "$SOURCE_DIR" checkout --detach -f "origin/$BRANCH"
  git -C "$SOURCE_DIR" reset --hard "origin/$BRANCH"
  rm -f "$SOURCE_DIR/Source/WTF/wtf/OSAllocatorAmigaOS4.cpp" "$SOURCE_DIR/Source/WTF/wtf/strcasecmpAmigaOS4.cpp"
else
  [[ ! -e "$SOURCE_DIR" ]] || fail "Source path exists but is not a clean Git checkout: $SOURCE_DIR"
  git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$SOURCE_DIR"
fi

python3 "$SCRIPT_DIR/prepare-os4-upstream.py" "$SOURCE_DIR"
REVISION="$(git -C "$SOURCE_DIR" rev-parse HEAD)"
BUILD_DIR="${ODYSSEY_OS4_BUILD_DIR:-$PWD/cross-build-amigaos4}"
mkdir -p "$BUILD_DIR"

if [[ -f "$BUILD_DIR/CMakeCache.txt" ]] && ! grep -Fq "$SCRIPT_DIR/toolchains/AMIGAOS4.cmake" "$BUILD_DIR/CMakeCache.txt"; then
  fail "Build directory already has a different toolchain configured. Choose a fresh ODYSSEY_SOURCE_DIR."
fi

printf 'Source revision: %s\nSDK root: %s\nProfile: %s (-O%s)\nMedia build: %s\n' "$REVISION" "$SDK_ROOT" "$PROFILE" "$ODYSSEY_OPT_LEVEL" "$MEDIA"
printf 'Configuring experimental AmigaOS 4 PPC build. The newer upstream branch may require additional OS4 platform-port fixes.\n'

cmake -S "$SOURCE_DIR" -B "$BUILD_DIR" \
  -DCMAKE_TOOLCHAIN_FILE="$SCRIPT_DIR/toolchains/AMIGAOS4.cmake" \
  -DCMAKE_BUILD_TYPE=Release \
  -DPORT=MUI \
  -DODYSSEY_TARGET_AMIGAOS4=ON \
  -DCMAKE_SYSTEM_PROCESSOR=ppc \
  -DCMAKE_C_COMPILER=ppc-amigaos-gcc \
  -DCMAKE_CXX_COMPILER=ppc-amigaos-g++ \
  -DCAIRO_INCLUDE_DIRS="$CAIRO_INCLUDE_DIR" \
  -DFREETYPE_INCLUDE_DIRS="$FREETYPE_INCLUDE_DIR" \
  -DLIBXML2_INCLUDE_DIR="$LIBXML2_INCLUDE_DIR" \
  -DLIBXSLT_INCLUDE_DIR="$LIBXSLT_INCLUDE_DIR" \
  -DENABLE_VIDEO="$MEDIA_FLAG" \
  -DENABLE_MEDIA_SOURCE="$MEDIA_FLAG" 2>&1 | tee "$BUILD_DIR/odyssey-configure.log"

if [[ "${ODYSSEY_CONFIGURE_ONLY:-0}" == "1" ]]; then
  printf '\\nCMake configuration completed for upstream revision %s. Compile/link/runtime validation remains outstanding.\\n' "$REVISION"
  exit 0
fi

# WebKit is a heavy C++ codebase. Keep memory use conservative unless overridden.
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

printf 'Starting compile with %s job(s). A low-memory host may run out of memory.\n' "$JOBS"
cmake --build "$BUILD_DIR" --parallel "$JOBS" 2>&1 | tee "$BUILD_DIR/odyssey-build.log"
printf '\nBuild step finished for upstream revision %s.\n' "$REVISION"
printf 'This is not yet a verified OS4 application: inspect the log, then test on AmigaOS 4.1 Update 3 hardware/emulator.\n'
