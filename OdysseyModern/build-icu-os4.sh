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
# The Docker image exports CC/CXX/AR/RANLIB as PPC cross tools globally.
# Override all four for native generator utilities, or configure may try to
# execute a PPC test binary on the Linux build host.
HOST_CC=gcc
HOST_CXX=g++
HOST_AR=ar
HOST_RANLIB=ranlib
TARGET_CC=ppc-amigaos-gcc
TARGET_CXX=ppc-amigaos-g++

# Keep ICU optimized consistently with WebKit itself. ICU is hot on text,
# collation and internationalized pages, so leaving it at -O2 in the fast
# profile silently created a mixed-optimization binary. CPU flags must target
# the actual PPC machine, never the Linux build host.
ICU_OPT_LEVEL="${ODYSSEY_OPT_LEVEL:-2}"
case "$ICU_OPT_LEVEL" in
  2|3) ;;
  *) fail "ODYSSEY_OPT_LEVEL must be 2 or 3." ;;
esac
ICU_CPU_FLAGS="${AMIGAOS4_CPU_FLAGS:-}"
ICU_TARGET_CFLAGS="${AMIGAOS4_ICU_CFLAGS:--O${ICU_OPT_LEVEL}}"
ICU_TARGET_CXXFLAGS="${AMIGAOS4_ICU_CXXFLAGS:--O${ICU_OPT_LEVEL}}"
case " $ICU_TARGET_CXXFLAGS " in
  *" -athread=native "*) ;;
  *) ICU_TARGET_CXXFLAGS+=" -athread=native" ;;
esac
if [[ -n "$ICU_CPU_FLAGS" ]]; then
  ICU_TARGET_CFLAGS+=" $ICU_CPU_FLAGS"
  ICU_TARGET_CXXFLAGS+=" $ICU_CPU_FLAGS"
fi
printf 'ICU target optimization: -O%s; target CPU flags: %s\n' "$ICU_OPT_LEVEL" "${ICU_CPU_FLAGS:-generic PPC}"
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
    CC="$HOST_CC" CXX="$HOST_CXX" AR="$HOST_AR" RANLIB="$HOST_RANLIB" "$ICU_CONFIGURE" \
      --prefix="$ICU_BUILD_ROOT/host-install" \
      --disable-shared --enable-static \
      --with-data-packaging=static \
      --disable-tests --disable-samples
    make -j"${ODYSSEY_ICU_HOST_JOBS:-2}"
  ) 2>&1 | tee "$ICU_BUILD_ROOT/icu-host-build.log"
fi

# ICU's host-side generators run during the target-data build. The generated
# target data directory is big-endian (icudt55b), but those generators are
# native Linux executables; pointing u_init() at the incomplete target
# data directory makes them fail with U_FILE_ACCESS_ERROR. All 29 "-i $(BUILDDIR)"
# inputs are auxiliary data read by host-native tools, so redirect those inputs
# to the completed little-endian host data tree (icudt55l) while leaving every
# "-d"/"-o" generated output directed to the big-endian PPC target data tree.
ICU_HOST_DATA_DIR="$HOST_BUILD/data/out/build/icudt55l"
[[ -d "$ICU_HOST_DATA_DIR" ]] || fail "Host ICU data tree not found: $ICU_HOST_DATA_DIR"
python3 - "$ICU_SOURCE_DIR/icu4c/source/data/Makefile.in" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
old = "-i $(BUILDDIR)"
new = "-i $(ICU_HOST_DATA_DIR)"
old_count = text.count(old)
new_count = text.count(new)
# The pinned ICU 55.1 template has 29 generator input paths. They all name
# auxiliary input data (including gensprep/genrb), while each rule's output
# target remains controlled by its separate -d/-o arguments. Every generator
# is a native host executable during a cross-build, so every -i must point at
# the completed host-native data tree. Do not silently accept a partial patch.
expected = 29
if old_count == expected and new_count == 0:
    text = text.replace(old, new)
elif old_count == 0 and new_count == expected:
    # Idempotent after an interrupted cross-build.
    pass
else:
    raise SystemExit(
        "ERROR: ICU data-generation input-directory patch anchor changed; "
        f"expected {expected} original or patched occurrences, "
        f"found {old_count} original and {new_count} patched occurrences."
    )
path.write_text(text, encoding="utf-8")
PY

# ICU 55.1 does not know an AmigaOS 4 make fragment. Its own mh-unknown
# explicitly recommends starting from mh-linux. Use its generic GCC compile
# and dependency rules, but strip Linux ELF linker options that are not valid
# or useful for the PPC AmigaOS target; static libraries are requested below.
cp "$ICU_SOURCE_DIR/icu4c/source/config/mh-linux" "$ICU_SOURCE_DIR/icu4c/source/config/mh-unknown"
sed -i \
  -e '/^LD_RPATH[[:space:]]*=/d' \
  -e '/^LD_RPATH_PRE[[:space:]]*=/d' \
  -e '/^LD_SONAME[[:space:]]*=/d' \
  -e '/^LD_SOOPTIONS[[:space:]]*=/d' \
  -e '/^BIR_LDFLAGS[[:space:]]*=/d' \
  "$ICU_SOURCE_DIR/icu4c/source/config/mh-unknown"

# ICU's configure correctly reports U_HAVE_TZNAME=0 for this SDK, but ICU
# 55.1's generic putilimp.h still defines U_TZNAME as POSIX tzname. That makes
# putil.cpp reference a symbol the OS4 libc doesn't export. Respect the probe:
# if no timezone-name array exists, leave U_TZNAME undefined and ICU falls back
# to TZ environment IDs (or an empty abbreviation) without a bad link symbol.
python3 - "$ICU_SOURCE_DIR/icu4c/source/common/putilimp.h" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
old = """#elif U_PLATFORM == U_PF_OS400
   /* not defined */
#else
#   define U_TZNAME tzname
#endif"""
new = """#elif U_PLATFORM == U_PF_OS400
   /* not defined */
#elif defined(U_HAVE_TZNAME) && !U_HAVE_TZNAME
   /* not defined on this target */
#else
#   define U_TZNAME tzname
#endif"""
if text.count(new) == 1:
    # Idempotent: local developers can rerun the build after a failed compile.
    pass
elif text.count(old) == 1:
    path.write_text(text.replace(old, new), encoding="utf-8")
else:
    raise SystemExit("ERROR: ICU putilimp.h timezone patch anchor changed; refusing to guess.")
PY

rm -f "$TARGET_BUILD/config.status"
printf '\nConfiguring target ICU for AmigaOS 4 PPC...\n'
BUILD_TRIPLET="$(sh "$ICU_SOURCE_DIR/icu4c/source/config.guess")"
(
  cd "$TARGET_BUILD"
  CC="$TARGET_CC" \
  CXX="$TARGET_CXX" \
  AR=ppc-amigaos-ar \
  RANLIB=ppc-amigaos-ranlib \
  CFLAGS="$ICU_TARGET_CFLAGS" \
  CXXFLAGS="$ICU_TARGET_CXXFLAGS" \
  LIBS="${AMIGAOS4_ICU_LIBS:--lpthread}" \
  "$ICU_CONFIGURE" \
    --build="$BUILD_TRIPLET" \
    --host=powerpc-unknown-amigaos \
    --prefix="$PREFIX" \
    --with-cross-build="$HOST_BUILD" \
    --disable-shared --enable-static \
    --with-data-packaging=static \
    --disable-tests --disable-samples

  # ICU 55.1's generated config/Makefile.inc includes its platform fragment
  # from "$(pkgdatadir)/config/$(platform_make_fragment_name)". For a cross
  # build, that is the install prefix's share/icu/55.1/config directory, but
  # "make install" has not run yet. Staging mh-unknown only in the ICU source
  # tree is therefore insufficient: pkgdataMakefile and stubdata fail while
  # trying to include the fragment. Keep this copy in the build prefix before
  # compiling; ICU's own make install can later replace/install it normally.
  ICU_PKGDATA_DIR="$PREFIX/share/icu/55.1"
  mkdir -p "$ICU_PKGDATA_DIR/config"
  install -m 0644 "$ICU_SOURCE_DIR/icu4c/source/config/mh-unknown" "$ICU_PKGDATA_DIR/config/mh-unknown"
  [[ -s "$ICU_PKGDATA_DIR/config/mh-unknown" ]] || fail "ICU target platform fragment was not staged: $ICU_PKGDATA_DIR/config/mh-unknown"

  ICU_HOST_DATA_DIR="$HOST_BUILD/data/out/build/icudt55l" make -j"${ODYSSEY_ICU_TARGET_JOBS:-1}"
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
