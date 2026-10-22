#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
IMAGE="${ODYSSEY_OS4_DOCKER_IMAGE:-walkero/amigagccondocker:os4-gcc11-3.5.2}"
PROFILE="${ODYSSEY_BUILD_PROFILE:-balanced}"
MEDIA="${ODYSSEY_OS4_MEDIA:-off}"
XSLT="${ODYSSEY_OS4_XSLT:-off}"
UPSTREAM_REF="${ODYSSEY_UPSTREAM_REF:-8e44b6d8d2e8c57a59a878152b81304e220ceb7a}"
CCACHE_MODE="${ODYSSEY_CCACHE:-auto}"
CCACHE_HOST_DIR="${ODYSSEY_CCACHE_HOST_DIR:-${XDG_CACHE_HOME:-$HOME/.cache}/odyssey-ccache}"

command -v docker >/dev/null 2>&1 || {
  printf 'ERROR: Docker is required. Install Docker Engine/Desktop and try again.\n' >&2
  exit 1
}
[[ "$PROFILE" == "balanced" || "$PROFILE" == "fast" ]] || {
  printf 'ERROR: ODYSSEY_BUILD_PROFILE must be balanced or fast.\n' >&2; exit 1;
}
[[ "$MEDIA" == "off" || "$MEDIA" == "on" ]] || {
  printf 'ERROR: ODYSSEY_OS4_MEDIA must be off or on.\n' >&2; exit 1;
}
[[ "$XSLT" == "off" || "$XSLT" == "on" ]] || {
  printf 'ERROR: ODYSSEY_OS4_XSLT must be off or on.\n' >&2; exit 1;
}
[[ "$CCACHE_MODE" == "auto" || "$CCACHE_MODE" == "on" || "$CCACHE_MODE" == "off" ]] || {
  printf 'ERROR: ODYSSEY_CCACHE must be auto, on, or off.\n' >&2; exit 1;
}

docker pull "$IMAGE"
docker_args=(run --rm --init --volume "$REPO_ROOT:/opt/code" --workdir /opt/code
  --env "ODYSSEY_BUILD_PROFILE=$PROFILE"
  --env "ODYSSEY_OS4_MEDIA=$MEDIA"
  --env "ODYSSEY_OS4_XSLT=$XSLT"
  --env "ODYSSEY_UPSTREAM_REF=$UPSTREAM_REF"
  --env "ODYSSEY_CCACHE=$CCACHE_MODE")
if [[ "$CCACHE_MODE" != "off" ]]; then
  mkdir -p "$CCACHE_HOST_DIR"
  docker_args+=(--volume "$CCACHE_HOST_DIR:/opt/odyssey-ccache" --env "CCACHE_DIR=/opt/odyssey-ccache")
fi
if [[ -n "${AMIGAOS4_CPU_FLAGS:-}" ]]; then
  docker_args+=(--env "AMIGAOS4_CPU_FLAGS=$AMIGAOS4_CPU_FLAGS")
fi
if [[ -n "${BUILD_JOBS:-}" ]]; then
  docker_args+=(--env "BUILD_JOBS=$BUILD_JOBS")
fi
docker_args+=("$IMAGE" bash ./OdysseyModern/fetch-and-build-os4.sh)

printf '\nImage: %s\nTarget: experimental AmigaOS 4 PPC\nUpstream ref: %s\nOptimization profile: %s\nMedia: %s\nXSLT: %s\nCPU flags: %s\nCompiler cache: %s\n' "$IMAGE" "$UPSTREAM_REF" "$PROFILE" "$MEDIA" "$XSLT" "${AMIGAOS4_CPU_FLAGS:-generic PPC}" "$CCACHE_MODE"
printf 'This cross-build may need substantial RAM and may expose missing OS4 port code in the current engine.\n'
docker "${docker_args[@]}"
