#!/usr/bin/env bash
# Regenerate the k8s ConfigMap from config/agentsmith.yml.
# Run from the repository root:
#
#   ./deploy/k8s/regenerate-configmap.sh
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONFIG_FILE="${REPO_ROOT}/config/agentsmith.yml"
OUTPUT_FILE="${REPO_ROOT}/deploy/k8s/3-configmap.yaml"

if [ ! -f "$CONFIG_FILE" ]; then
  echo "Error: $CONFIG_FILE not found." >&2
  exit 1
fi

kubectl create configmap agentsmith-config \
  --from-file=agentsmith.yml="$CONFIG_FILE" \
  -n agentsmith \
  --dry-run=client -o yaml > "$OUTPUT_FILE"

echo "ConfigMap written to $OUTPUT_FILE"
