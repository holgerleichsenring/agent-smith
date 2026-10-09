#!/usr/bin/env bash
# Blocking phase-commit gate (PreToolUse on Bash).
#
# Fires on every Bash call but only gates a `git commit` whose message carries a
# phase id, e.g. `feat: ... (p0272)` — the format the spec-first plugin's
# deliver-spec writes. Any other command passes through instantly.
#
# When it gates, the deterministic phase checks must all be green or the commit
# is blocked (exit 2, stderr fed back to Claude):
#   0. hook tests      — this gate's own command detection and message resolver, from
#                        the tree being gated (2026-09-21-9ae2)
#   1. dashboard       — pnpm install, the generated-mirror check, test and build in
#                        src/dashboard (2026-08-25-39ab, 2026-09-18-7b31)
#   2. build           — dotnet build (errors fail)
#   3. unit + harness xUnit tests — dotnet test (this is the harness pass/fail gate)
#   4. CLI dry-runs    — <command> --help for each pipeline
#   5. harness presets — every preset from `--list`, stub tier, CRASH-ONLY check
#
# 2026-10-09-7f48: on a machine with eight or more cores the checks run in two lanes —
# [0, 1] beside [2, then 3, 4 and 5 at once] — with the test assemblies as separate
# processes and AgentSmith.Tests split in five by its TestProcess trait. Every job is
# waited for by its own pid, output is printed in a fixed order, and one ledger line names
# every failed step. Below eight cores the gate runs 0-5 in sequence and blocks on the
# first failure, exactly as before: the split puts process-spawning tests beside timing-
# bounded ones on one machine, which a small machine cannot absorb.
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
phase_id='(p[0-9]+[a-z]?|[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9a-f]{4}[a-z]?)'
phase_marker="\\(${phase_id}([,[:space:]]+${phase_id})*\\)"

# 2026-09-09-8fce: the marker takes a LIST, because a commit spanning two phases names both
# and "(id, id)" matched nothing — the gate read it as an ordinary commit, ran no check and
# wrote no line. A stricter id shape than the marker's serves the warning below: the marker
# keeps p[0-9]+ so forms like (p73a) still gate, while a warning built on that shape would
# fire on "p12" in prose, and a warning that cries wolf is one people scroll past.
strict_phase_id='(p[0-9]{4,6}[a-z]?|[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9a-f]{4}[a-z]?)'

# One line per recognised phase commit: when, what the gate decided, the phase id,
# the tree it gated and the commit the new one will sit on. That last field is what
# ties a ledger line to a commit afterwards — it is the commit's parent.
record() {
  local verdict=$1 tree=$2 detail=$3 phase parent
  phase=$(printf '%s' "${message:-}" | grep -Eo "$phase_marker" | head -1 \
    | grep -Eo "$phase_id" | head -1)
  # No marker: the one line worth investigating should still name its phase rather than
  # say "unknown", so fall back to the first id in the subject.
  [ -n "$phase" ] || phase=$(printf '%s' "${message:-}" | head -1 \
    | grep -Eo "$strict_phase_id" | head -1)
  parent=$(git -C "$tree" rev-parse --short HEAD 2>/dev/null || echo none)
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$verdict" \
    "${phase:-unknown}" "$tree" "$parent" "$detail" >>"$ledger" 2>/dev/null || true
}

# Only gate an actual `git commit` invocation (command word at start or after a
# shell separator) whose message names a phase in either namespace. This
# deliberately ignores commands that merely *mention* git commit (grep, echo,
# this script's own tests).
#
# 2026-09-21-9ae2: git's GLOBAL OPTIONS sit between the program and the subcommand, and
# `git -C <tree> commit` is what an agent told to work in absolute paths writes instead
# of changing directory first. Requiring `commit` immediately after `git` rejected every
# such form, so the hook exited silently: no checks, no ledger line, nothing to tell the
# skip from a pass. What is accepted between the two is an option — a flag, and, for the
# flags that take one, the value after it — and nothing else, so `git -p log commit` and
# `git -C <tree> rebase --continue` still pass through untouched. The subcommand ends at
# whitespace, a separator or the line, because `commit-graph` is not `commit`.
git_value="(\"[^\"]*\"|'[^']*'|[^[:space:]]+)"
git_value_flag="(-[Cc]|--(git-dir|work-tree|namespace|exec-path|super-prefix|config-env))"
git_option="(${git_value_flag}[[:space:]]*${git_value}|--[A-Za-z][A-Za-z0-9-]*(=[^[:space:]]*)?|-[A-Za-z]+)"
printf '%s' "$cmd" \
  | grep -Eq "(^|[;&|]|&&)[[:space:]]*git([[:space:]]+${git_option})*[[:space:]]+commit([[:space:]]|[;&|]|$)" \
  || exit 0

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
    if printf '%s' "$message" | grep -Eq '[$]\(|`'; then
      record not-gated "${hook_cwd:-.}" "message built by a shell substitution"
      echo "[phase-gate] the commit message is built by the shell ($(printf '%s' "$message" | head -c 60)) — its text never reached the gate, so it was not gated; run the phase checks by hand if this is a phase commit" >&2
      exit 0
    fi
    # 2026-09-09-8fce: a SUBJECT that names a phase but carries no marker the gate can read.
    # That is a phase commit by intent and an ordinary commit by the gate's reading, and the
    # silence between the two is what let a two-phase commit through unverified. The body is
    # not scanned: a good phase commit names its prerequisites there.
    if printf '%s' "$message" | head -1 | grep -Eq "(^|[^0-9A-Za-z-])${strict_phase_id}([^0-9A-Za-z-]|$)"; then
      record not-gated "${hook_cwd:-.}" "phase named in the subject without a marker"
      echo "[phase-gate] the subject names a phase but carries no marker the gate reads — write it as '(<id>)' or '(<id>, <id>)'; NOT gated, run the phase checks by hand" >&2
      exit 0
    fi
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
# Resolution order: the directory the commit itself runs in — the hook payload's cwd
# moved by a leading `cd <dir>` and by git's own `-C <dir>` — then that cwd, then the
# project dir; each resolved to its git top level. The resolver computes it, so the
# tree that is built and the message that is read come from ONE reading of the command:
# a `-C` that sent the commit elsewhere while the gate checked the session's tree would
# report numbers from code the commit does not contain, which is the same failure the
# `cd` case was written for (2026-09-21-9ae2).
target_dir=""
for candidate in \
  "$(printf '%s' "$cmd" | python3 "$resolver" --work-dir "${hook_cwd:-.}" 2>/dev/null)" \
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

# 2026-10-09-7f48: every check below REPORTS its failure — the step name goes to $fail_file and
# the function returns non-zero — instead of ending the gate, so one set of checks serves both
# shapes. Sequential (fewer than eight cores): the order and the first-failure block of every
# earlier version. Two lanes (eight or more): the dashboard lane beside the .NET lane, the test
# assemblies as separate processes, dry-runs and presets side by side; every lane and every job
# is waited for by its own pid, and ONE ledger line names every step that failed.
fail_file="$tmp/failed"
: >"$fail_file"
failed() { printf '%s\n' "$1" >>"$fail_file"; }

# A job: a command run to its own log, its exit code to its own file. In lane mode it runs in
# the background and is collected later; a missing exit-code file reads as a failure, so a job
# that died without reporting cannot pass.
job_pids=()
spawn() {
  local name=$1; shift
  if [ "$parallel" = 1 ]; then
    ( "$@" >"$tmp/job-$name.log" 2>&1; echo $? >"$tmp/job-$name.rc" ) &
    job_pids+=($!)
  else
    "$@" >"$tmp/job-$name.log" 2>&1; echo $? >"$tmp/job-$name.rc"
  fi
}
await_jobs() {
  local pid
  for pid in ${job_pids[@]+"${job_pids[@]}"}; do wait "$pid"; done
  job_pids=()
}
job_rc() { cat "$tmp/job-$1.rc" 2>/dev/null || echo missing; }

# 2026-09-21-9ae2: this gate's own tests, from the tree being gated — a commit that
# changes the hook is proven by the hook it ships, not by the copy the session started
# with. They existed for three phases with nothing running them, which is how the
# command detection above drifted from the forms agents actually write.
# The tests drive the gate against throwaway repositories, so two things keep that out
# of this run: PHASE_GATE_LOG sends any line they write to a scratch file instead of the
# ledger, and PHASE_GATE_SELFTEST makes the gate they invoke skip this step rather than
# run the tests that invoked it. A tree carrying no hook tests says so and moves on.
check_hook_tests() {
  [ -z "${PHASE_GATE_SELFTEST:-}" ] || return 0
  log "0/5 hook tests (this gate's own detection and resolver)..."
  local hook_tests hook_test
  hook_tests=$(ls .claude/hooks/test_*.py 2>/dev/null)
  if [ -z "$hook_tests" ]; then
    log "    no .claude/hooks/test_*.py in $target_dir — no hook tests to run"
  fi
  for hook_test in $hook_tests; do
    if ! PHASE_GATE_SELFTEST=1 PHASE_GATE_LOG="$tmp/selftest-phase-gate.log" \
        python3 "$hook_test" >"$tmp/hook-tests.log" 2>&1; then
      tail -40 "$tmp/hook-tests.log" >&2; failed "hook tests: $(basename "$hook_test")"; return 1
    fi
    log "    hook tests: $(basename "$hook_test") ok"
  done
}

# Step 1 (2026-08-25-39ab): the dashboard's own workflow is path-filtered on
# src/dashboard/**, so a backend-only payload change never ran a single dashboard test.
# 2026-09-18-7b31 added the generated-mirror check: a C# event contract that outgrew its
# TypeScript mirror passed this gate and failed on the pull request. It is a CONDITIONAL
# member, read from package.json rather than inferred from pnpm's exit code — real pnpm
# answers a missing script with an undocumented 254, which would block exactly the
# worktree cut from before that phase. The passed ledger line still says only `dashboard`.
check_dashboard() {
  log "1/5 dashboard build + tests..."
  if [ ! -f src/dashboard/package.json ]; then
    log "    no src/dashboard/package.json in $target_dir — no dashboard to check"
    return 0
  fi
  local steps step
  steps=("install --frozen-lockfile")
  if python3 -c '
import json, sys
try:
    scripts = json.load(open("src/dashboard/package.json")).get("scripts") or {}
except Exception:
    scripts = {}
sys.exit(0 if "gen:hub-events" in scripts else 1)
' 2>/dev/null; then
    # After install, because the step runs through pnpm and pnpm needs its modules; the
    # check only reads, so it cannot touch the tree it gates.
    steps+=("gen:hub-events")
  else
    log "    no gen:hub-events script in src/dashboard/package.json — no generated mirror, nothing to check"
  fi
  steps+=("test" "build")
  for step in "${steps[@]}"; do
    # shellcheck disable=SC2086
    if ! (cd src/dashboard && pnpm $step) >"$tmp/dashboard.log" 2>&1; then
      tail -40 "$tmp/dashboard.log" >&2; failed "dashboard: pnpm ${step%% *}"; return 1
    fi
    log "    dashboard: pnpm ${step%% *} ok"
  done
}

check_build() {
  log "2/5 build..."
  if ! dotnet build AgentSmith.sln -clp:ErrorsOnly >"$tmp/build.log" 2>&1; then
    tail -40 "$tmp/build.log" >&2; failed "build"; return 1
  fi
}

# Category=LiveLLM is excluded in every invocation, the same way CI excludes it. Those suites
# drive a real model or a real agent CLI: they cost money or quota on every phase commit, need
# a binary the gate cannot require, and flaked here under contention. A gate that charges for
# a commit and flakes is not a gate.
live="Category!=LiveLLM"

# 2026-10-09-7f48: AgentSmith.Tests as five processes, selected by the TestProcess trait
# (TestProcessTraitRuleTests ties each value to its collection). Environment variables are per
# process, so the three env shards keep the guarantee the environment collection gives while
# running at once, and the collections that run alone get a process of their own. The body is
# the NEGATION of the four values — a class nobody tagged still runs there, because a positive
# filter that matches nothing exits 0 and would drop it in silence. Every other test project in
# the solution runs as its own process, read from AgentSmith.sln so a new one is not missed.
test_projects() {
  python3 - <<'PY' 2>/dev/null
import re
try:
    sln = open("AgentSmith.sln").read()
except Exception:
    sln = ""
for path in re.findall(r'"([^"]+\.csproj)"', sln):
    path = path.replace("\\", "/")
    try:
        if "<IsTestProject>true</IsTestProject>" in open(path).read():
            print(path)
    except Exception:
        pass
PY
}

check_tests() {
  if [ "$parallel" != 1 ]; then
    log "3/5 unit + harness xUnit tests..."
    if ! dotnet test AgentSmith.sln --no-build --filter "$live" >"$tmp/test.log" 2>&1; then
      tail -50 "$tmp/test.log" >&2; failed "dotnet test"; return 1
    fi
    return 0
  fi
  log "3/5 unit + harness xUnit tests, one process per assembly and AgentSmith.Tests in five..."
  local names=() name project rc
  for project in $(test_projects); do
    if [ "$(basename "$project")" = "AgentSmith.Tests.csproj" ]; then
      spawn test-body dotnet test "$project" --no-build --filter \
        "$live&TestProcess!=env-1&TestProcess!=env-2&TestProcess!=env-3&TestProcess!=serial"
      names+=(test-body)
      for name in env-1 env-2 env-3 serial; do
        spawn "test-$name" dotnet test "$project" --no-build --filter "$live&TestProcess=$name"
        names+=("test-$name")
      done
    else
      name="test-$(basename "$project" .csproj)"
      spawn "$name" dotnet test "$project" --no-build --filter "$live"
      names+=("$name")
    fi
  done
  if [ ${#names[@]} -eq 0 ]; then
    failed "dotnet test: no test project found in AgentSmith.sln"; return 1
  fi
  await_jobs
  local status=0
  for name in "${names[@]}"; do
    rc=$(job_rc "$name")
    log "    ${name#test-}: $(grep -hE 'Passed!|Failed!' "$tmp/job-$name.log" 2>/dev/null | tail -1 | sed 's/^ *//') (rc=$rc)"
    if [ "$rc" != 0 ]; then
      tail -50 "$tmp/job-$name.log" >&2; failed "dotnet test: ${name#test-}"; status=1
    fi
  done
  return $status
}

check_dry_runs() {
  log "4/5 CLI dry-runs..."
  local c status=0
  for c in api-scan security-scan fix feature; do
    spawn "dry-$c" dotnet run --no-build --project src/backend/AgentSmith.Cli -- "$c" --help
    if [ "$parallel" != 1 ] && [ "$(job_rc "dry-$c")" != 0 ]; then
      cat "$tmp/job-dry-$c.log" >&2; failed "dry-run: $c --help"; return 1
    fi
  done
  await_jobs
  for c in api-scan security-scan fix feature; do
    if [ "$(job_rc "dry-$c")" != 0 ]; then
      cat "$tmp/job-dry-$c.log" >&2; failed "dry-run: $c --help"; status=1
    fi
  done
  return $status
}

# The console `--preset` runner returns the *pipeline result* as its exit code — exit 1
# (pipeline FAIL, e.g. fix-bug "no code changes") is a valid outcome, NOT a test failure. So
# this step only fails on a real crash (exit >= 2 or an unhandled exception), which catches
# composition-root / DI wiring breakage in RealCompositionHarness. The actual harness
# pass/fail assertions live in the xUnit tests.
preset_crashed() {
  local rc
  rc=$(job_rc "preset-$1")
  [ "$rc" = missing ] && return 0
  [ "$rc" -ge 2 ] || grep -qiE 'unhandled exception|System\.[A-Za-z.]+Exception' "$tmp/job-preset-$1.log"
}

check_presets() {
  log "5/5 harness presets (stub tier, crash-only)..."
  local presets p status=0
  if ! presets=$(dotnet run --no-build --project tests/AgentSmith.PipelineHarness -- --list 2>"$tmp/harness-list.log"); then
    cat "$tmp/harness-list.log" >&2; failed "harness --list"; return 1
  fi
  for p in $presets; do
    spawn "preset-$p" dotnet run --no-build --project tests/AgentSmith.PipelineHarness -- --preset "$p"
    if [ "$parallel" != 1 ]; then
      if preset_crashed "$p"; then
        tail -30 "$tmp/job-preset-$p.log" >&2; failed "harness preset crashed: $p"; return 1
      fi
      log "    preset ran: $p (rc=$(job_rc "preset-$p"))"
    fi
  done
  await_jobs
  [ "$parallel" = 1 ] || return 0
  for p in $presets; do
    if preset_crashed "$p"; then
      tail -30 "$tmp/job-preset-$p.log" >&2; failed "harness preset crashed: $p"; status=1
    else
      log "    preset ran: $p (rc=$(job_rc "preset-$p"))"
    fi
  done
  return $status
}

verdict() {
  if [ -s "$fail_file" ]; then
    fail "$(tr '\n' ',' <"$fail_file" | sed 's/,$//')"
  fi
}

# A phase routinely spans both repos. In the skills catalog the equivalent gate is its own
# validator — it guards the live-breakage classes there (description cap, frontmatter,
# name/directory match, principles templates), which is what a phase commit touching a
# master can actually break. The hook tests run first there, as they always have.
if [ ! -f AgentSmith.sln ]; then
  parallel=0
  check_hook_tests; verdict
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

# A tree that HAS a dashboard and no pnpm fails the gate — a missing toolchain is an
# unproven commit, and a silent skip is indistinguishable from a pass.
if [ -f src/dashboard/package.json ] && ! command -v pnpm >/dev/null 2>&1; then
  fail "dashboard checks need pnpm on PATH (corepack enable, or install pnpm)"
fi

# PHASE_GATE_CORES overrides the probe, for the hook tests that pin either shape.
cores=${PHASE_GATE_CORES:-$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 1)}
case "$cores" in ''|*[!0-9]*) cores=1 ;; esac
if [ "$cores" -ge 8 ]; then parallel=1; else parallel=0; fi

if [ "$parallel" != 1 ]; then
  log "phase commit detected — gating $target_dir in sequence ($cores cores): dashboard, build, tests, dry-runs, harness presets"
  check_hook_tests; verdict
  check_dashboard; verdict
  check_build; verdict
  check_tests; verdict
  check_dry_runs; verdict
  check_presets; verdict
else
  log "phase commit detected — gating $target_dir in two lanes ($cores cores): [hook tests, dashboard] beside [build, tests, dry-runs, harness presets]"
  # Each lane writes its own failures and its own output; the gate prints the output in a
  # fixed order once both are done, so a report never interleaves two lanes.
  ( fail_file="$tmp/failed-lane1"; : >"$fail_file"
    check_hook_tests && check_dashboard ) >"$tmp/lane1.out" 2>&1 &
  lane1=$!
  ( fail_file="$tmp/failed-lane2"; : >"$fail_file"
    if check_build; then
      # Tests, dry-runs and presets each need only the build, so they share the wait; each
      # keeps its own output and failures, gathered in this fixed order afterwards.
      ( fail_file="$tmp/failed-tests"; : >"$fail_file"; check_tests ) >"$tmp/tests.out" 2>&1 &
      tests_pid=$!
      ( fail_file="$tmp/failed-dry"; : >"$fail_file"; check_dry_runs ) >"$tmp/dry.out" 2>&1 &
      dry_pid=$!
      ( fail_file="$tmp/failed-presets"; : >"$fail_file"; check_presets ) >"$tmp/presets.out" 2>&1 &
      presets_pid=$!
      s=0
      wait "$tests_pid" || s=1
      wait "$dry_pid" || s=1
      wait "$presets_pid" || s=1
      cat "$tmp/tests.out" "$tmp/dry.out" "$tmp/presets.out" >&2
      cat "$tmp/failed-tests" "$tmp/failed-dry" "$tmp/failed-presets" >>"$fail_file" 2>/dev/null
      exit $s
    fi
    exit 1 ) >"$tmp/lane2.out" 2>&1 &
  lane2=$!
  lane1_rc=0; wait "$lane1" || lane1_rc=$?
  lane2_rc=0; wait "$lane2" || lane2_rc=$?
  cat "$tmp/lane1.out" "$tmp/lane2.out" >&2
  cat "$tmp/failed-lane1" "$tmp/failed-lane2" >>"$fail_file" 2>/dev/null
  # A lane that failed without naming a step still blocks — under its own name.
  if [ "$lane1_rc" != 0 ] && [ ! -s "$tmp/failed-lane1" ]; then failed "lane: hook tests + dashboard"; fi
  if [ "$lane2_rc" != 0 ] && [ ! -s "$tmp/failed-lane2" ]; then failed "lane: build + tests"; fi
  verdict
fi

record passed "$target_dir" "dashboard,build,tests,dry-runs,harness-presets"
log "all green — commit allowed (recorded in $ledger)"
exit 0
