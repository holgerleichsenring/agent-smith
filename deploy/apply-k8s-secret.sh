#!/usr/bin/env bash
# apply-k8s-secret.sh
#
# Syncs your local .env into the Kubernetes agentsmith-secrets secret.
# Run this once after initial setup and whenever you change .env.
#
# Usage:
#   chmod +x apply-k8s-secret.sh
#   ./apply-k8s-secret.sh
#   ./apply-k8s-secret.sh --namespace agentsmith   # optional: target namespace

set -euo pipefail

# --- defaults ---
ENV_FILE=".env"
SECRET_NAME="agentsmith-secrets"
NAMESPACE="default"

# --- parse optional args ---
while [[ $# -gt 0 ]]; do
  case "$1" in
    --namespace|-n) NAMESPACE="$2"; shift 2 ;;
    --env-file)     ENV_FILE="$2";  shift 2 ;;
    --help|-h)
      echo "Usage: $0 [--namespace <ns>] [--env-file <path>]"
      exit 0 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

# --- checks ---
if [[ ! -f "$ENV_FILE" ]]; then
  echo "❌  $ENV_FILE not found. Copy .env.example and fill in your values."
  exit 1
fi

if ! command -v kubectl &>/dev/null; then
  echo "❌  kubectl not found. Install it and make sure your cluster is reachable."
  exit 1
fi

if ! kubectl cluster-info &>/dev/null; then
  echo "❌  Cannot reach Kubernetes cluster. Is Docker Desktop / your cluster running?"
  exit 1
fi

echo "📦  Reading $ENV_FILE ..."

# --- load .env (skip comments and empty lines) ---
set -a
# shellcheck disable=SC1090
source <(grep -v '^\s*#' "$ENV_FILE" | grep -v '^\s*$')
set +a

# --- build kubectl args ---
LITERAL_ARGS=()

add_literal() {
  local key="$1"
  local value="${2:-}"
  LITERAL_ARGS+=("--from-literal=${key}=${value}")
}

add_literal "anthropic-api-key"   "${ANTHROPIC_API_KEY:-}"
add_literal "openai-api-key"      "${OPENAI_API_KEY:-}"
add_literal "gemini-api-key"      "${GEMINI_API_KEY:-}"
add_literal "github-token"        "${GITHUB_TOKEN:-}"
add_literal "azure-devops-token"  "${AZURE_DEVOPS_TOKEN:-}"
add_literal "gitlab-token"        "${GITLAB_TOKEN:-}"
add_literal "jira-token"          "${JIRA_TOKEN:-}"
add_literal "jira-email"          "${JIRA_EMAIL:-}"
add_literal "slack-bot-token"     "${SLACK_BOT_TOKEN:-}"
add_literal "slack-signing-secret" "${SLACK_SIGNING_SECRET:-}"

# Redis URL for K8s Jobs: the in-cluster Redis service name.
# Override via REDIS_URL in .env if your Redis runs elsewhere.
add_literal "redis-url" "${REDIS_URL:-redis:6379}"

echo "🔐  Applying secret '$SECRET_NAME' in namespace '$NAMESPACE' ..."

kubectl create secret generic "$SECRET_NAME" \
  --namespace="$NAMESPACE" \
  "${LITERAL_ARGS[@]}" \
  --dry-run=client -o yaml \
  | kubectl apply -f -

echo ""
echo "✅  Secret '$SECRET_NAME' applied successfully."
echo ""
echo "Keys written:"
kubectl get secret "$SECRET_NAME" -n "$NAMESPACE" \
  -o jsonpath='{.data}' \
  | python3 -c "
import sys, json
data = json.load(sys.stdin)
sensitive = {'anthropic-api-key','openai-api-key','gemini-api-key','github-token',
             'azure-devops-token','gitlab-token','jira-token','slack-bot-token',
             'slack-signing-secret'}
for k in sorted(data.keys()):
    val = '***' if k in sensitive else '(set)' if data[k] else '(empty)'
    print(f'  {k:<28} {val}')
"
