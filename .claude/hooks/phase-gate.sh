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
read -r cmd_b64 cwd_b64 <<<"$(printf '%s' "$input" | python3 -c '
import sys, json, base64
d = json.load(sys.stdin)
enc = lambda s: base64.b64encode((s or "").encode()).decode()
print(enc(d.get("tool_input", {}).get("command", "")), enc(d.get("cwd", "")))
' 2>/dev/null)" || exit 0
cmd=$(printf '%s' "$cmd_b64" | base64 -d 2>/dev/null)
hook_cwd=$(printf '%s' "$cwd_b64" | base64 -d 2>/dev/null)

# Only gate an actual `git commit` invocation (command word at start or after a
# shell separator) whose message names a phase, e.g. (p0272) / (p73a). This
# deliberately ignores commands that merely *mention* git commit (grep, echo,
# this script's own tests).
printf '%s' "$cmd" | grep -Eq '(^|[;&|]|&&)[[:space:]]*git[[:space:]]+commit\b' || exit 0

# Look for the marker in the message the commit will CARRY, not in the command
# line. `--amend --no-edit`, `-F <file>`, `-t <template>` and `-C <rev>` keep the
# phase id off the command line entirely, and matching the raw string waved every
# one of them through — a skip that is indistinguishable from a pass, since both
# exit 0. commit-message.py resolves the message (exit 3 when it cannot exist
# yet: a bare commit, an editor amend, `-F -`); those pass through, but loudly,
# because that is the one case with a human sitting in front of it.
resolver="$(dirname "${BASH_SOURCE[0]}")/commit-message.py"
if message=$(printf '%s' "$cmd" | python3 "$resolver" "${hook_cwd:-${CLAUDE_PROJECT_DIR:-.}}"); then
  printf '%s' "$message" | grep -Eq '\(p[0-9]+[a-z]?\)' || exit 0
else
  echo "[phase-gate] could not read the commit message (${message:-resolver unavailable}) — not gating; run the phase checks by hand if this is a phase commit" >&2
  exit 0
fi

# Gate the tree the commit ACTUALLY runs in, not the session's project dir. Work
# in a git worktree (a phase implemented on its own branch) lives outside
# CLAUDE_PROJECT_DIR, so the old unconditional `cd "$CLAUDE_PROJECT_DIR"` built and
# tested the main checkout and waved the worktree's changes through without ever
# compiling them — the gate reported numbers from code the commit does not contain.
# Resolution order: an explicit leading `cd <dir>` in the command, then the hook
# payload's cwd, then the project dir; each resolved to its git top level.
target_dir=""
for candidate in \
  "$(printf '%s' "$cmd" | sed -n 's/^[[:space:]]*cd[[:space:]]\{1,\}\([^&;|]*\).*/\1/p' | head -1 | sed 's/[[:space:]]*$//' | tr -d "\"'")" \
  "$hook_cwd" \
  "${CLAUDE_PROJECT_DIR:-.}"
do
  [ -n "$candidate" ] && [ -d "$candidate" ] || continue
  target_dir=$(cd "$candidate" 2>/dev/null && git rev-parse --show-toplevel 2>/dev/null) && [ -n "$target_dir" ] && break
done
[ -n "$target_dir" ] || { echo "phase-gate: cannot resolve the git tree to gate" >&2; exit 2; }
cd "$target_dir" || { echo "phase-gate: cannot cd to $target_dir" >&2; exit 2; }

tmp=$(mktemp -d 2>/dev/null || echo /tmp)
log()  { echo "[phase-gate] $*" >&2; }
fail() { echo "" >&2; echo "PHASE GATE BLOCKED COMMIT — $1 failed. Fix it before committing the phase." >&2; exit 2; }

# A phase routinely spans both repos, and the four checks below are all .NET
# solution checks. In the skills catalog the equivalent gate is its own validator
# — it guards the live-breakage classes there (description cap, frontmatter,
# name/directory match, principles templates), which is what a phase commit
# touching a master can actually break.
if [ ! -f AgentSmith.sln ]; then
  if [ -x scripts/validate-skills.sh ] || [ -f scripts/validate-skills.sh ]; then
    log "phase commit in the skills catalog — gating $target_dir (validate-skills)"
    bash scripts/validate-skills.sh >"$tmp/validate.log" 2>&1 || {
      tail -30 "$tmp/validate.log" >&2; fail "validate-skills"; }
    log "all green — commit allowed"
    exit 0
  fi
  log "no AgentSmith.sln and no skills validator in $target_dir — nothing to gate"
  exit 0
fi

log "phase commit detected — gating $target_dir (build, tests, dry-runs, harness presets)"

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
