#!/usr/bin/env bash
# Pull the skill catalog into ./test-skills/ for local test runs and CI.
#
# The version comes from ONE place: the SkillsCatalogVersion the build pins in
# AgentSmith.Infrastructure.Core.csproj. That is the catalog the product ships, so
# it is the catalog the tests must run against.
#
# p0393: this used to read SKILLS_VERSION / CONTENT_VERSION / .agentsmith/skills.version,
# and CI passed `vars.SKILLS_VERSION || 'v2.1.2'` — a default from long before the current
# catalog existed. The tests therefore validated against a vocabulary the product had not
# used in months: `code` and `pr-review` came back "undeclared" in CI while the shipped
# catalog declared both. A second source of truth for the catalog version is the same
# defect class as a second source for the pipeline names, and it costs the same thing —
# a test suite that disagrees with reality and is believed anyway.
set -euo pipefail

REPO_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." && pwd )"
cd "${REPO_ROOT}"

PIN_FILE="src/backend/AgentSmith.Infrastructure.Core/AgentSmith.Infrastructure.Core.csproj"
VERSION="$(sed -n 's|.*<SkillsCatalogVersion>\(.*\)</SkillsCatalogVersion>.*|\1|p' "${PIN_FILE}" | head -1)"

if [[ -z "${VERSION}" ]]; then
  echo "fetch-skills.sh: no <SkillsCatalogVersion> found in ${PIN_FILE}" >&2
  exit 1
fi

# SKILLS_VERSION is the LEGACY input and no longer wins. It is reported, not obeyed:
# the pin is what the product ships, and a catalog that disagrees with it makes every
# vocabulary-dependent test assert against something nothing uses. Bump the pin to
# change the catalog — that edit is visible in the diff of the thing it affects.
if [[ -n "${SKILLS_VERSION:-}" && "${SKILLS_VERSION}" != "${VERSION}" ]]; then
  echo "fetch-skills.sh: ignoring SKILLS_VERSION=${SKILLS_VERSION} — the build pins ${VERSION}." >&2
  echo "  To change the catalog, edit <SkillsCatalogVersion> in ${PIN_FILE}." >&2
fi

OUTPUT="${1:-./test-skills}"
mkdir -p "${OUTPUT}"

echo "fetch-skills.sh: pulling ${VERSION} (build pin) into ${OUTPUT}"
dotnet run --project src/backend/AgentSmith.Cli -- skills pull \
  --version "${VERSION}" \
  --output "${OUTPUT}"
