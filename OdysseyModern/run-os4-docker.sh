#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
IMAGE="${ODYSSEY_OS4_DOCKER_IMAGE:-walkero/amigagccondocker:os4-gcc11}"

command -v docker >/dev/null 2>&1 || {
  printf 'ERROR: Docker is required. Install Docker Desktop/Engine and try again.\n' >&2
  exit 1
}

PROFILE="${ODYSSEY_BUILD_PROFILE:-balanced}"
MEDIA="${ODYSSEY_OS4_MEDIA:-off}"
[[ "$PROFILE" == "balanced" || "$PROFILE" == "fast" ]] || {
  printf 'ERROR: ODYSSEY_BUILD_PROFILE must be balanced or fast.\n' >&2; exit 1;
}
[[ "$MEDIA" == "off" || "$MEDIA" == "on" ]] || {
  printf 'ERROR: ODYSSEY_OS4_MEDIA must be off or on.\n' >&2; exit 1;
}

docker pull "$IMAGE"
docker_args=(run --rm --init --volume "$REPO_ROOT:/opt/code" --workdir /opt/code
  --env "ODYSSEY_BUILD_PROFILE=$PROFILE"
  --env "ODYSSEY_OS4_MEDIA=$MEDIA")
if [[ -n "${BUILD_JOBS:-}" ]]; then
  docker_args+=(--env "BUILD_JOBS=$BUILD_JOBS")
fi
docker_args+=("$IMAGE" bash ./OdysseyModern/fetch-and-build-os4.sh)

printf '\nImage: %s\nTarget: experimental AmigaOS 4 PPC\nOptimization profile: %s\nMedia: %s\n' "$IMAGE" "$PROFILE" "$MEDIA"
printf 'This cross-build may need substantial RAM and may expose missing OS4 port code in the current engine.\n'
docker "${docker_args[@]}"
