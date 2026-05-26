#!/usr/bin/env bash
# Pull the skill catalog into ./test-skills/ for local test runs and CI.
# Mirrors what the server does at boot — version comes from CONTENT_VERSION env
# or falls back to the version pinned in .agentsmith/skills.version.
set -euo pipefail

REPO_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." && pwd )"
cd "${REPO_ROOT}"

VERSION="${SKILLS_VERSION:-${CONTENT_VERSION:-}}"
if [[ -z "${VERSION}" && -f .agentsmith/skills.version ]]; then
  VERSION=$(cat .agentsmith/skills.version)
fi
if [[ -z "${VERSION}" ]]; then
  echo "fetch-skills.sh: SKILLS_VERSION not set and .agentsmith/skills.version missing" >&2
  exit 1
fi

OUTPUT="${1:-./test-skills}"
mkdir -p "${OUTPUT}"

dotnet run --project src/AgentSmith.Cli -- skills pull \
  --version "${VERSION}" \
  --output "${OUTPUT}"
