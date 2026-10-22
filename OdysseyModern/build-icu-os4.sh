#!/usr/bin/env bash
# Build pinned ICU 55.1 for the AmigaOS 4 PPC target.
# ICU 55.1 matches the API generation used by this Odyssey source branch.
set -Eeuo pipefail

ICU_REPO="https://github.com/unicode-org/icu.git"
ICU_TAG="release-55-1"
ICU_SOURCE_DIR="${ODYSSEY_ICU_SOURCE_DIR:-$PWD/icu-source-os4}"
ICU_BUILD_ROOT="${ODYSSEY_ICU_BUILD_ROOT:-$PWD/cross-build-icu-os4}"
HOST_BUILD="${ODYSSEY_ICU_HOST_BUILD_DIR:-$ICU_BUILD_ROOT/host-build}"
TARGET_BUILD="${ODYSSEY_ICU_TARGET_BUILD_DIR:-$ICU_BUILD_ROOT/target-build}"
PREFIX="${ODYSSEY_ICU_PREFIX:-$ICU_BUILD_ROOT/target-install}"
SDK_ROOT="${AMIGAOS4_SDK_ROOT:-/opt/ppc-amigaos/ppc-amigaos/SDK}"

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
for tool in git make ppc-amigaos-gcc ppc-amigaos-g++ ppc-amigaos-ar ppc-amigaos-ranlib; do
  command -v "$tool" >/dev/null 2>&1 || fail "$tool is required to cross-build ICU."
done

CCACHE_MODE="${ODYSSEY_CCACHE:-auto}"
USE_CCACHE=0
case "$CCACHE_MODE" in
  auto) if command -v ccache >/dev/null 2>&1; then USE_CCACHE=1; fi ;;
  on) command -v ccache >/dev/null 2>&1 || fail "ODYSSEY_CCACHE=on but ccache is not installed."; USE_CCACHE=1 ;;
  off) ;;
  *) fail "ODYSSEY_CCACHE must be auto, on, or off." ;;
esac
HOST_CC=gcc
HOST_CXX=g++
TARGET_CC=ppc-amigaos-gcc
TARGET_CXX=ppc-amigaos-g++
if [[ "$USE_CCACHE" == "1" ]]; then
  export CCACHE_DIR="${CCACHE_DIR:-${XDG_CACHE_HOME:-$HOME/.cache}/odyssey-ccache}"
  mkdir -p "$CCACHE_DIR"
  HOST_CC="ccache gcc"
  HOST_CXX="ccache g++"
  TARGET_CC="ccache ppc-amigaos-gcc"
  TARGET_CXX="ccache ppc-amigaos-g++"
  printf 'ICU compiler cache enabled (%s)\n' "$CCACHE_DIR"
else
  printf 'ICU compiler cache unavailable/disabled\n'
fi

if [[ -d "$ICU_SOURCE_DIR/.git" ]]; then
  git -C "$ICU_SOURCE_DIR" fetch --depth=1 origin "refs/tags/$ICU_TAG:refs/tags/$ICU_TAG"
  git -C "$ICU_SOURCE_DIR" checkout --detach "$ICU_TAG"
else
  [[ ! -e "$ICU_SOURCE_DIR" ]] || fail "ICU source path exists but is not a Git checkout: $ICU_SOURCE_DIR"
  git clone --depth 1 --branch "$ICU_TAG" "$ICU_REPO" "$ICU_SOURCE_DIR"
fi

ICU_CONFIGURE="$ICU_SOURCE_DIR/icu4c/source/configure"
[[ -x "$ICU_CONFIGURE" ]] || fail "Pinned ICU configure script was not found at $ICU_CONFIGURE."
ICU_REVISION="$(git -C "$ICU_SOURCE_DIR" rev-parse HEAD)"
EXPECTED_REVISION="dd8356c7545a570b56136d3e44c62f7efdf5495f"
[[ "$ICU_REVISION" == "$EXPECTED_REVISION" ]] || fail "ICU tag resolved to unexpected revision $ICU_REVISION."

mkdir -p "$ICU_BUILD_ROOT" "$HOST_BUILD" "$TARGET_BUILD" "$PREFIX"

# ICU generates native utilities (genrb, icupkg, etc.) even for a cross build.
# Build those tools for the Linux build host first, then reuse them for PPC.
if [[ ! -f "$HOST_BUILD/config/icucross.mk" || ! -x "$HOST_BUILD/bin/genrb" ]]; then
  printf '\nConfiguring host ICU tools from pinned revision %s...\n' "$ICU_REVISION"
  (
    cd "$HOST_BUILD"
    CC="$HOST_CC" CXX="$HOST_CXX" "$ICU_CONFIGURE" \
      --prefix="$ICU_BUILD_ROOT/host-install" \
      --disable-shared --enable-static \
      --with-data-packaging=static \
      --disable-tests --disable-samples
    make -j"${ODYSSEY_ICU_HOST_JOBS:-2}"
  ) 2>&1 | tee "$ICU_BUILD_ROOT/icu-host-build.log"
fi

rm -f "$TARGET_BUILD/config.status"
printf '\nConfiguring target ICU for AmigaOS 4 PPC...\n'
BUILD_TRIPLET="$(sh "$ICU_SOURCE_DIR/icu4c/source/config.guess")"
(
  cd "$TARGET_BUILD"
  CC="$TARGET_CC" \
  CXX="$TARGET_CXX" \
  AR=ppc-amigaos-ar \
  RANLIB=ppc-amigaos-ranlib \
  CFLAGS="${AMIGAOS4_ICU_CFLAGS:--O2}" \
  CXXFLAGS="${AMIGAOS4_ICU_CXXFLAGS:--O2}" \
  "$ICU_CONFIGURE" \
    --build="$BUILD_TRIPLET" \
    --host=powerpc-unknown-amigaos \
    --prefix="$PREFIX" \
    --with-cross-build="$HOST_BUILD" \
    --disable-shared --enable-static \
    --with-data-packaging=static \
    --disable-tests --disable-samples
  make -j"${ODYSSEY_ICU_TARGET_JOBS:-1}"
  make install
) 2>&1 | tee "$ICU_BUILD_ROOT/icu-target-build.log"

for file in "$PREFIX/include/unicode/ustring.h" "$PREFIX/lib/libicuuc.a" "$PREFIX/lib/libicui18n.a" "$PREFIX/lib/libicudata.a"; do
  [[ -f "$file" ]] || fail "Expected ICU target artifact missing: $file"
done

cat > "$ICU_BUILD_ROOT/ICU-BUILD-INFO.txt" <<EOF
ICU version: 55.1
ICU git revision: $ICU_REVISION
Target: AmigaOS 4 PPC (cross-compiled)
Compiler: $(ppc-amigaos-gcc -dumpmachine)
Install prefix: $PREFIX

Host build log: $ICU_BUILD_ROOT/icu-host-build.log
Target build log: $ICU_BUILD_ROOT/icu-target-build.log
EOF
printf '\nICU 55.1 PPC static libraries installed at: %s\n' "$PREFIX"
