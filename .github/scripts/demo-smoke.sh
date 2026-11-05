#!/usr/bin/env bash
# 2026-11-02-2b26g: run a published binary's `demo` from an empty directory, the way a reader would.
# Usage: demo-smoke.sh <path-to-agent-smith-binary>
# Needs OPENAI_API_KEY (or ANTHROPIC_API_KEY) in the environment and a .NET 10 SDK on PATH: the demo
# builds and tests its C# sample on the host. Passes on exit 0 plus a commit beyond the baseline.
set -uo pipefail

binary="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
base="${RUNNER_TEMP:-$(mktemp -d)}"
run_dir="$(mktemp -d "$base/demo-run.XXXXXX")"
workspace="$base/demo-ws"
log="$base/demo.log"

cd "$run_dir"
"$binary" demo --workspace "$workspace" < /dev/null 2>&1 | tee "$log"
status=${PIPESTATUS[0]}

if [ "$status" -ne 0 ]; then
  echo "::error::agent-smith demo exited $status (log: $log)"
  exit 1
fi
commits=$(git -C "$workspace" rev-list --count HEAD 2>/dev/null || echo 0)
if [ "$commits" -lt 2 ]; then
  echo "::error::agent-smith demo exited 0 but left no commit beyond the baseline in $workspace"
  exit 1
fi
echo "demo smoke passed: $commits commits in $workspace"
