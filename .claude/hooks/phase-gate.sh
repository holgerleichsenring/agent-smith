#!/usr/bin/env bash
# Blocking phase-commit gate (PreToolUse on Bash).
#
# Fires on every Bash call but only gates a `git commit` whose message carries a
# phase id, e.g. `feat: ... (p0272)` — the format produced by execute-phase
# Step 10. Any other command passes through instantly.
#
# When it gates, the deterministic phase checks must all be green or the commit
# is blocked (exit 2, stderr fed back to Claude):
#   1. dashboard       — pnpm install/test/build in src/dashboard (2026-08-25-39ab)
#   2. build           — dotnet build (errors fail)
#   3. unit + harness xUnit tests — dotnet test (this is the harness pass/fail gate)
#   4. CLI dry-runs    — <command> --help for each pipeline
#   5. harness presets — every preset from `--list`, stub tier, CRASH-ONLY check
#
# Step 1 note (2026-08-25-39ab): the dashboard's own workflow is path-filtered on
# src/dashboard/**, so a backend-only payload change never ran a single dashboard
# test — the half that renders the payload was proven by nothing. It runs FIRST
# because it is the cheapest complete signal (~45s against minutes of .NET) and
# because a phase that breaks the dashboard should hear so before the build.
# A tree without src/dashboard/package.json has no dashboard to check and says so.
# A tree that HAS one and no pnpm fails the gate — a missing toolchain is an
# unproven commit, and a silent skip is indistinguishable from a pass.
#
# Step 5 note: the console `--preset` runner returns the *pipeline result* as its
# exit code — exit 1 (pipeline FAIL, e.g. fix-bug "no code changes") is a valid
# outcome, NOT a test failure. So step 5 only fails the gate on a real crash
# (exit >= 2 or an unhandled exception), which catches composition-root / DI
# wiring breakage in RealCompositionHarness. The actual harness pass/fail
# assertions live in the xUnit tests run by step 3.
#
# The --docker harness tier is intentionally NOT in the blocking gate: it needs
# a docker daemon + redis and is too heavy/flaky for a commit hook. Run it
# manually via `/smoke all` when you want the full end-to-end matrix.
#
# One copy of this script serves every session: Claude Code expands
# $CLAUDE_PROJECT_DIR to the LAUNCHING session's project directory, so a subagent
# working in its own git worktree runs the shared checkout's copy, not the one
# its worktree happens to contain (p0511 measured this). An edit to the gate
# therefore takes effect only once it reaches the shared checkout's working tree.
#
# Every phase commit the gate recognises leaves one line in the ledger, whether it
# passed, was blocked, or was let through unchecked. A phase commit with no ledger
# line never met the gate — which is what tells a pass apart from an absence.
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

hooks_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ledger="${PHASE_GATE_LOG:-$hooks_dir/../phase-gate.log}"

# The phase marker a commit message carries, in either namespace: the closed counter
# id, e.g. (p0272) / (p73a), or a p0507 date-minted id, e.g. (2026-08-24-8a3f). The
# ledger and the gating decision below read this ONE definition — a marker recognised
# by one and not the other would gate a commit it never records, or record one it
# never gated.
phase_marker='\((p[0-9]+[a-z]?|[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9a-f]{4})\)'

# One line per recognised phase commit: when, what the gate decided, the phase id,
# the tree it gated and the commit the new one will sit on. That last field is what
# ties a ledger line to a commit afterwards — it is the commit's parent.
record() {
  local verdict=$1 tree=$2 detail=$3 phase parent
  phase=$(printf '%s' "${message:-}" | grep -Eo "$phase_marker" | head -1 | tr -d '()')
  parent=$(git -C "$tree" rev-parse --short HEAD 2>/dev/null || echo none)
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$verdict" \
    "${phase:-unknown}" "$tree" "$parent" "$detail" >>"$ledger" 2>/dev/null || true
}

# Only gate an actual `git commit` invocation (command word at start or after a
# shell separator) whose message names a phase in either namespace. This
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
resolver="$hooks_dir/commit-message.py"
if message=$(printf '%s' "$cmd" | python3 "$resolver" "${hook_cwd:-${CLAUDE_PROJECT_DIR:-.}}"); then
  if ! printf '%s' "$message" | grep -Eq "$phase_marker"; then
    # `-m "$(cat message.txt)"` reaches the resolver unexpanded: the shell, not the
    # command line, supplies the text. Finding no marker in `$(cat message.txt)`
    # proves nothing about the message the commit will carry, so say so instead of
    # passing in silence — silence here is what a clean pass looks like.
    printf '%s' "$message" | grep -Eq '[$]\(|`' || exit 0
    record not-gated "${hook_cwd:-.}" "message built by a shell substitution"
    echo "[phase-gate] the commit message is built by the shell ($(printf '%s' "$message" | head -c 60)) — its text never reached the gate, so it was not gated; run the phase checks by hand if this is a phase commit" >&2
    exit 0
  fi
else
  record not-gated "${hook_cwd:-.}" "unreadable message: ${message:-resolver unavailable}"
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
fail() { record blocked "$target_dir" "$1"; echo "" >&2; echo "PHASE GATE BLOCKED COMMIT — $1 failed. Fix it before committing the phase." >&2; exit 2; }

# A phase routinely spans both repos. The checks below are the .NET solution
# checks plus the dashboard's own build and tests. In the skills catalog the equivalent gate is its own validator
# — it guards the live-breakage classes there (description cap, frontmatter,
# name/directory match, principles templates), which is what a phase commit
# touching a master can actually break.
if [ ! -f AgentSmith.sln ]; then
  if [ -x scripts/validate-skills.sh ] || [ -f scripts/validate-skills.sh ]; then
    log "phase commit in the skills catalog — gating $target_dir (validate-skills)"
    bash scripts/validate-skills.sh >"$tmp/validate.log" 2>&1 || {
      tail -30 "$tmp/validate.log" >&2; fail "validate-skills"; }
    record passed "$target_dir" "validate-skills"
    log "all green — commit allowed (recorded in $ledger)"
    exit 0
  fi
  record not-gated "$target_dir" "no AgentSmith.sln and no skills validator"
  log "no AgentSmith.sln and no skills validator in $target_dir — nothing to gate"
  exit 0
fi

log "phase commit detected — gating $target_dir (dashboard, build, tests, dry-runs, harness presets)"

log "1/5 dashboard build + tests..."
if [ -f src/dashboard/package.json ]; then
  command -v pnpm >/dev/null 2>&1 \
    || fail "dashboard checks need pnpm on PATH (corepack enable, or install pnpm)"
  for step in "install --frozen-lockfile" "test" "build"; do
    # shellcheck disable=SC2086
    if ! (cd src/dashboard && pnpm $step) >"$tmp/dashboard.log" 2>&1; then
      tail -40 "$tmp/dashboard.log" >&2; fail "dashboard: pnpm ${step%% *}"
    fi
    log "    dashboard: pnpm ${step%% *} ok"
  done
else
  log "    no src/dashboard/package.json in $target_dir — no dashboard to check"
fi

log "2/5 build..."
if ! dotnet build AgentSmith.sln -clp:ErrorsOnly >"$tmp/build.log" 2>&1; then
  tail -40 "$tmp/build.log" >&2; fail "build"
fi

log "3/5 unit + harness xUnit tests..."
# Category=LiveLLM is excluded, the same way CI excludes it. Those suites drive a real
# model or a real agent CLI: they cost money or subscription quota on every phase commit,
# they need a binary the gate cannot require, and the gate runs the three test assemblies
# at once — a scan eval spawning CLI subprocesses under that contention failed once here
# and passed alone. A gate that charges for a commit and flakes is not a gate.
if ! dotnet test AgentSmith.sln --no-build --filter "Category!=LiveLLM" >"$tmp/test.log" 2>&1; then
  tail -50 "$tmp/test.log" >&2; fail "dotnet test"
fi

log "4/5 CLI dry-runs..."
for c in api-scan security-scan fix feature; do
  if ! dotnet run --no-build --project src/backend/AgentSmith.Cli -- "$c" --help >/dev/null 2>"$tmp/dry-$c.log"; then
    cat "$tmp/dry-$c.log" >&2; fail "dry-run: $c --help"
  fi
done

log "5/5 harness presets (stub tier, crash-only)..."
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

record passed "$target_dir" "dashboard,build,tests,dry-runs,harness-presets"
log "all green — commit allowed (recorded in $ledger)"
exit 0
