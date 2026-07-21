#!/bin/bash
# p0357/p0358: build the sandbox carrier from the repo Dockerfile and prove the
# python payload lands in UNMODIFIED toolchain images (dotnet-sdk + node),
# mirroring the k8s init-container injection via a compose shared volume.
#
# Usage: tools/verify-python-payload/verify.sh
# Exit 0 = python3 works in every verified image.
set -euo pipefail
cd "$(dirname "$0")"

cleanup() { docker compose down -v --remove-orphans >/dev/null 2>&1 || true; }
trap cleanup EXIT
cleanup

echo "== building carrier (agent publish + pinned python payload) =="
docker compose build inject

echo "== inject + verify in dotnet-sdk:8.0 and node:20 =="
docker compose up --exit-code-from verify-dotnet inject verify-dotnet
docker compose up --exit-code-from verify-node inject verify-node

echo "== all images verified =="
