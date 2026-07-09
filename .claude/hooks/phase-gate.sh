#!/usr/bin/env bash
# Blocking phase-commit gate (PreToolUse on Bash).
#
# Fires on every Bash call but only gates a `git commit` whose message carries a
# phase id, e.g. `feat: ... (p0272)` — the format produced by execute-phase
# Step 10. Any other command passes through instantly.
#
# When it gates, the deterministic phase checks must all be green or the commit
# is blocked (exit 2, stderr fed back to Claude):
#   1. build           — dotnet build (errors fail)
#   2. unit + harness xUnit tests — dotnet test (this is the harness pass/fail gate)
#   3. CLI dry-runs    — <command> --help for each pipeline
#   4. harness presets — every preset from `--list`, stub tier, CRASH-ONLY check
#
# Step 4 note: the console `--preset` runner returns the *pipeline result* as its
# exit code — exit 1 (pipeline FAIL, e.g. fix-bug "no code changes") is a valid
# outcome, NOT a test failure. So step 4 only fails the gate on a real crash
# (exit >= 2 or an unhandled exception), which catches composition-root / DI
# wiring breakage in RealCompositionHarness. The actual harness pass/fail
# assertions live in the xUnit tests run by step 2.
#
# The --docker harness tier is intentionally NOT in the blocking gate: it needs
# a docker daemon + redis and is too heavy/flaky for a commit hook. Run it
# manually via `/smoke all` when you want the full end-to-end matrix.
set -uo pipefail

input=$(cat)
cmd=$(printf '%s' "$input" \
  | python3 -c 'import sys,json; print(json.load(sys.stdin).get("tool_input",{}).get("command",""))' 2>/dev/null) || exit 0

# Only gate an actual `git commit` invocation (command word at start or after a
# shell separator) whose message names a phase, e.g. (p0272) / (p73a). This
# deliberately ignores commands that merely *mention* git commit (grep, echo,
# this script's own tests).
printf '%s' "$cmd" | grep -Eq '(^|[;&|]|&&)[[:space:]]*git[[:space:]]+commit\b' || exit 0
printf '%s' "$cmd" | grep -Eq '\(p[0-9]+[a-z]?\)' || exit 0

cd "${CLAUDE_PROJECT_DIR:-.}" || { echo "phase-gate: cannot cd to project dir" >&2; exit 2; }

tmp=$(mktemp -d 2>/dev/null || echo /tmp)
log()  { echo "[phase-gate] $*" >&2; }
fail() { echo "" >&2; echo "PHASE GATE BLOCKED COMMIT — $1 failed. Fix it before committing the phase." >&2; exit 2; }

log "phase commit detected — running blocking gate (build, tests, dry-runs, harness presets)"

log "1/4 build..."
if ! dotnet build AgentSmith.sln -clp:ErrorsOnly >"$tmp/build.log" 2>&1; then
  tail -40 "$tmp/build.log" >&2; fail "build"
fi

log "2/4 unit + harness xUnit tests..."
if ! dotnet test AgentSmith.sln --no-build >"$tmp/test.log" 2>&1; then
  tail -50 "$tmp/test.log" >&2; fail "dotnet test"
fi

log "3/4 CLI dry-runs..."
for c in api-scan security-scan fix feature; do
  if ! dotnet run --no-build --project src/backend/AgentSmith.Cli -- "$c" --help >/dev/null 2>"$tmp/dry-$c.log"; then
    cat "$tmp/dry-$c.log" >&2; fail "dry-run: $c --help"
  fi
done

log "4/4 harness presets (stub tier, crash-only)..."
if ! presets=$(dotnet run --no-build --project tests/AgentSmith.PipelineHarness -- --list 2>"$tmp/harness-list.log"); then
  cat "$tmp/harness-list.log" >&2; fail "harness --list"
fi
while IFS= read -r p; do
  [ -z "$p" ] && continue
  out=$(dotnet run --no-build --project tests/AgentSmith.PipelineHarness -- --preset "$p" 2>&1); rc=$?
  # exit 1 = pipeline returned FAIL (valid outcome); >=2 or an unhandled
  # exception = a real crash in the harness composition.
  if [ "$rc" -ge 2 ] || printf '%s' "$out" | grep -qiE 'unhandled exception|System\.[A-Za-z.]+Exception'; then
    printf '%s\n' "$out" | tail -30 >&2; fail "harness preset crashed: $p"
  fi
  log "    preset ran: $p (rc=$rc)"
done <<< "$presets"

log "all green — commit allowed"
exit 0
