#!/usr/bin/env bash
# Docs screenshot rig: a scratch SQLite database seeded with the demo story, the real server and
# dashboard on free ports, and headless Chrome at 2x in dark mode. See README.md.
#
#   tools/screenshots/run.sh                # seed, start, shoot, stop
#   tools/screenshots/run.sh --keep         # leave server + dashboard running afterwards
#
# Nothing here touches Docker or a running installation: the database, repos and logs live
# under $DEMO_ROOT (default: a fresh temp directory).
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
DEMO_ROOT="${DEMO_ROOT:-$(mktemp -d -t codeably-demo)}"
SERVER_PORT="${SERVER_PORT:-5917}"
DASHBOARD_PORT="${DASHBOARD_PORT:-3917}"
KEEP=false
[[ "${1:-}" == "--keep" ]] && KEEP=true

echo "demo root: $DEMO_ROOT"
mkdir -p "$DEMO_ROOT/config" "$DEMO_ROOT/repos" "$DEMO_ROOT/logs"
TRACKER_PORT="${TRACKER_PORT:-5918}"
sed -e "s#\${DEMO_ROOT}#$DEMO_ROOT#g" -e "s#\${TRACKER_URL}#http://127.0.0.1:$TRACKER_PORT#g" \
  "$REPO/tools/demo-seed/demo-config.yml" > "$DEMO_ROOT/config/agentsmith.yml"
# Real credentials in the caller's shell must never reach the rig.
unset ANTHROPIC_API_KEY OPENAI_API_KEY GITHUB_TOKEN JIRA_TOKEN AZURE_DEVOPS_TOKEN GITLAB_TOKEN

export CONFIG_PATH="$DEMO_ROOT/config/agentsmith.yml"
export GITHUB_TOKEN="demo-not-a-token"
# A throwaway key for the stored-secret envelope; base64 of 32 random bytes.
export AGENTSMITH_SECRET_MASTER_KEY="${AGENTSMITH_SECRET_MASTER_KEY:-$(openssl rand -base64 32)}"
export REDIS_URL="${REDIS_URL:-127.0.0.1:6399}"   # nothing listens there: the server runs degraded, by design

echo "== build"
dotnet build "$REPO/src/backend/AgentSmith.Cli/AgentSmith.Cli.csproj" -v q -nologo >/dev/null
dotnet build "$REPO/src/backend/AgentSmith.Server/AgentSmith.Server.csproj" -v q -nologo >/dev/null
dotnet build "$REPO/tools/demo-seed/DemoSeed.csproj" -v q -nologo >/dev/null
CLI=(dotnet "$REPO/src/backend/AgentSmith.Cli/bin/Debug/net10.0/AgentSmith.Cli.dll")

echo "== database + config"
(cd "$DEMO_ROOT" && "${CLI[@]}" database migrate >"$DEMO_ROOT/logs/migrate.log" 2>&1)
(cd "$DEMO_ROOT" && "${CLI[@]}" config import "$CONFIG_PATH" >"$DEMO_ROOT/logs/import.log" 2>&1 || true)

PIDS=()
echo "== demo story"
dotnet "$REPO/tools/demo-seed/bin/Debug/net10.0/DemoSeed.dll" "$DEMO_ROOT"

cleanup() {
  if ! $KEEP; then
    for pid in "${PIDS[@]}"; do pkill -P "$pid" 2>/dev/null || true; kill "$pid" 2>/dev/null || true; done
  fi
}
trap cleanup EXIT

echo "== tracker stub on :$TRACKER_PORT"
node "$REPO/tools/screenshots/tracker-stub.mjs" "$TRACKER_PORT" >"$DEMO_ROOT/logs/tracker-stub.log" 2>&1 &
PIDS+=($!)

echo "== server on :$SERVER_PORT"
(cd "$DEMO_ROOT" && exec env ASPNETCORE_URLS="http://127.0.0.1:$SERVER_PORT" ASPNETCORE_ENVIRONMENT=Production \
  dotnet "$REPO/src/backend/AgentSmith.Server/bin/Debug/net10.0/AgentSmith.Server.dll" \
  >"$DEMO_ROOT/logs/server.log" 2>&1) &
PIDS+=($!)

echo "== dashboard on :$DASHBOARD_PORT"
(cd "$REPO/src/dashboard" && { [[ -d node_modules ]] || pnpm install --frozen-lockfile >/dev/null; } && pnpm gen:tokens >/dev/null && pnpm gen:types >/dev/null)
(cd "$REPO/src/dashboard" && AGENTSMITH_BACKEND_URL="http://127.0.0.1:$SERVER_PORT" \
  exec pnpm exec next dev -p "$DASHBOARD_PORT" >"$DEMO_ROOT/logs/dashboard.log" 2>&1) &
PIDS+=($!)

for url in "http://127.0.0.1:$SERVER_PORT/health" "http://127.0.0.1:$DASHBOARD_PORT/"; do
  for _ in $(seq 1 120); do curl -sf -o /dev/null "$url" && break; sleep 1; done
done

echo "== screenshots"
(cd "$REPO/tools/screenshots" && { [[ -d node_modules ]] || npm install --no-audit --no-fund >/dev/null; })
BASE_URL="http://127.0.0.1:$DASHBOARD_PORT" OUT_DIR="$REPO/docs/assets/screenshots" DEMO_ROOT="$DEMO_ROOT" \
  node "$REPO/tools/screenshots/shoot.mjs"

$KEEP && echo "left running: server :$SERVER_PORT, dashboard :$DASHBOARD_PORT (pids ${PIDS[*]})"
