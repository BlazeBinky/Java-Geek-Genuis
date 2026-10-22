#!/usr/bin/env bash
set -Eeuo pipefail

REPO_URL="https://github.com/deadwood2/OdysseyWebBrowser.git"
BRANCH="odyssey"
SOURCE_DIR="${ODYSSEY_SOURCE_DIR:-$PWD/odyssey-upstream}"

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
command -v git >/dev/null 2>&1 || fail "git is required."
command -v make >/dev/null 2>&1 || fail "make is required."
command -v x86_64-aros-gcc >/dev/null 2>&1 || fail "x86_64-aros-gcc not found. Install the AROS cross compiler and SDK first."
command -v x86_64-aros-g++ >/dev/null 2>&1 || fail "x86_64-aros-g++ not found. Install the AROS cross compiler and SDK first."

if [[ -e "$SOURCE_DIR/.git" ]]; then
  git -C "$SOURCE_DIR" fetch origin "$BRANCH"
  git -C "$SOURCE_DIR" checkout "$BRANCH"
  git -C "$SOURCE_DIR" pull --ff-only origin "$BRANCH"
else
  git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$SOURCE_DIR"
fi

cd "$SOURCE_DIR"
[[ -x ./rebuild.sh ]] || fail "Upstream rebuild.sh was not found or is not executable."

printf '\nUpstream source ready at: %s\n' "$SOURCE_DIR"
printf 'Starting upstream configuration. Choose option 11: x86_64-aros (Release).\n'
./rebuild.sh

BUILD_DIR="$SOURCE_DIR/cross-build-x86_64-aros"
[[ -d "$BUILD_DIR" ]] || fail "Expected build directory $BUILD_DIR was not created. Confirm option 11 was selected."
cmake --build "$BUILD_DIR" --parallel "${BUILD_JOBS:-2}"

printf '\nBuild command completed. Check the upstream output directory and run the result in a matching AROS environment.\n'
