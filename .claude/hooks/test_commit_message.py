#!/usr/bin/env python3
"""Exercises the phase-gate commit-message resolver over every invocation form.

Run it directly:  python3 .claude/hooks/test_commit_message.py

The Resolve_* cases call the resolver as a function. The Gate_* cases drive
phase-gate.sh itself against a throwaway repository, where it has no
AgentSmith.sln and no skills validator and therefore reports "nothing to gate"
instead of running the four .NET checks — that line is the tell that the gate
recognised the commit as a phase commit at all. The ledger cases give the gate a
repository that does carry a skills validator, so it reaches a real verdict
without a .NET build, and read the line it leaves behind.
"""

import contextlib
import importlib.util
import json
import os
import pathlib
import shutil
import subprocess
import tempfile
import traceback

HOOKS_DIR = pathlib.Path(__file__).resolve().parent
REPOSITORY_ROOT = HOOKS_DIR.parent.parent
RESOLVER_PATH = HOOKS_DIR / "commit-message.py"
GATE_PATH = HOOKS_DIR / "phase-gate.sh"
GATE_ENTERED = "nothing to gate"
MARKER_MESSAGE = "feat: something (p9999)"
# p0507: the second id namespace — a date and a four-hex random suffix.
MINTED_MARKER_MESSAGE = "feat: something (2026-08-24-8a3f)"

_GIT_IDENTITY = ["-c", "user.email=gate@example.invalid", "-c", "user.name=Phase Gate"]
_resolver_module = None


def _environment():
    """A git environment that ignores the operator's global and system config.

    2026-09-22-7c41a: it also drops the gate's OWN step-0 variables. This file is run BY
    the gate with PHASE_GATE_SELFTEST=1 (and a redirected ledger), and every gate a test
    spawns inherited them — so under the gate the tests measured a gate that skips the
    very step they exist to check, and four of them failed on every phase commit. The
    recursion guard is unaffected: the gate under test sets the variable itself for the
    hook test it runs, and that copy is what the probe passes on.
    """
    environment = dict(os.environ)
    environment.pop("CLAUDE_PROJECT_DIR", None)
    environment.pop("PHASE_GATE_SELFTEST", None)
    environment.pop("PHASE_GATE_LOG", None)
    environment["GIT_CONFIG_GLOBAL"] = os.devnull
    environment["GIT_CONFIG_SYSTEM"] = os.devnull
    return environment


def _resolve(command, cwd):
    global _resolver_module
    if _resolver_module is None:
        spec = importlib.util.spec_from_file_location("commit_message", RESOLVER_PATH)
        _resolver_module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_resolver_module)
    return _resolver_module.resolve(command, cwd)


def _git(repo, *args):
    subprocess.run(["git", *_GIT_IDENTITY, *args], cwd=repo, env=_environment(),
                   check=True, capture_output=True, text=True)


@contextlib.contextmanager
def _repository(*messages):
    """A throwaway repository carrying one commit per given message."""
    with tempfile.TemporaryDirectory() as path:
        _git(path, "init", "-q")
        for message in messages:
            (pathlib.Path(path) / "tracked.txt").write_text(message)
            _git(path, "add", "-A")
            _git(path, "commit", "-q", "-m", message)
        yield path


def _run_gate(command, cwd, ledger=None):
    """Drive the gate; without a ledger path its record goes nowhere."""
    payload = json.dumps({"tool_input": {"command": command}, "cwd": cwd})
    environment = _environment()
    environment["PHASE_GATE_LOG"] = str(ledger) if ledger else os.devnull
    return subprocess.run(["bash", str(GATE_PATH)], input=payload, cwd=cwd,
                          env=environment, capture_output=True, text=True)


@contextlib.contextmanager
def _catalog_repository(validator_exit=0):
    """A throwaway repository shaped like the skills catalog, with a validator that
    exits as told — the gate reaches a verdict there without a .NET build."""
    with _repository("seed") as repo:
        validator = pathlib.Path(repo) / "scripts" / "validate-skills.sh"
        validator.parent.mkdir()
        validator.write_text(f"#!/usr/bin/env bash\nexit {validator_exit}\n")
        validator.chmod(0o755)
        _git(repo, "add", "-A")
        _git(repo, "commit", "-q", "-m", "chore: a validator to gate on")
        yield repo


def _ledger(path):
    """The ledger as a list of fields per line."""
    path = pathlib.Path(path)
    if not path.exists():
        return []
    return [line.split("\t") for line in path.read_text().splitlines() if line]


def Resolve_DashM_ReturnsTheMessage():
    result = _resolve('git commit -m "feat: the gate reads the message (p0508)"', ".")
    assert result.message == "feat: the gate reads the message (p0508)", result


def Resolve_RepeatedDashM_ReturnsAllParagraphs():
    result = _resolve('git commit -m "feat: subject (p0508)" -m "body paragraph"', ".")
    assert result.message == "feat: subject (p0508)\n\nbody paragraph", result


def Resolve_HeredocMessage_StillResolves():
    command = "git commit -m \"$(cat <<'EOF'\nfeat: heredoc subject (p0508)\n\nbody\nEOF\n)\""
    result = _resolve(command, ".")
    assert result.message is not None and "(p0508)" in result.message, result


def Resolve_DashFFile_ReadsIt():
    with tempfile.TemporaryDirectory() as path:
        message_file = pathlib.Path(path) / "message.txt"
        message_file.write_text("feat: from a file (p0508)\n")
        result = _resolve(f'git commit -F "{message_file}"', ".")
        assert result.message is not None and "(p0508)" in result.message, result


def Resolve_DashFFileRelativeToALeadingCd_ReadsIt():
    with tempfile.TemporaryDirectory() as path:
        (pathlib.Path(path) / "message.txt").write_text("feat: relative file (p0508)\n")
        with tempfile.TemporaryDirectory() as elsewhere:
            result = _resolve(f"cd {path} && git commit -F message.txt", elsewhere)
            assert result.message is not None and "(p0508)" in result.message, result


def Resolve_DashFFileAfterASemicolonCd_ReadsIt():
    # A `;` glued to the cd argument by shlex hid the commit segment and the gate
    # passed the commit through ungated (seen on 2026-09-04-cf3d's first commit).
    with tempfile.TemporaryDirectory() as path:
        (pathlib.Path(path) / "message.txt").write_text("feat: after a semicolon (p0508)\n")
        with tempfile.TemporaryDirectory() as elsewhere:
            result = _resolve(f"cd {path}; git commit -F message.txt", elsewhere)
            assert result.message is not None and "(p0508)" in result.message, result


def Resolve_AmendNoEdit_ReturnsThePreviousMessage():
    with _repository("feat: the previous message (p0508)") as repo:
        result = _resolve("git commit --amend --no-edit", repo)
        assert result.message is not None, result
        assert result.message.strip() == "feat: the previous message (p0508)", result


def Resolve_DashCRev_ReturnsThatRevisionsMessage():
    with _repository("feat: older (p0507)", "feat: newer (p0508)") as repo:
        result = _resolve("git commit -C HEAD~1", repo)
        assert result.message is not None, result
        assert result.message.strip() == "feat: older (p0507)", result


def Resolve_BareCommit_ReportsUnresolvable():
    result = _resolve("git commit -a", ".")
    assert result.message is None, result
    assert result.reason, result


def Resolve_DashFStdin_ReportsUnresolvable():
    result = _resolve("git commit -F -", ".")
    assert result.message is None, result
    assert result.reason, result


def Gate_AmendNoEditCarryingAPhaseMarker_IsGated():
    with _repository(MARKER_MESSAGE) as repo:
        completed = _run_gate("git commit --amend --no-edit", repo)
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_DashFFileCarryingAPhaseMarker_IsGated():
    with _repository("seed") as repo:
        (pathlib.Path(repo) / "message.txt").write_text(MARKER_MESSAGE + "\n")
        completed = _run_gate("git commit -F message.txt", repo)
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_MessageWithoutAMarker_PassesThrough():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: no phase here"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


def Gate_UnresolvableMessage_PassesThroughAndSaysSo():
    with _repository("seed") as repo:
        completed = _run_gate("git commit -F -", repo)
        assert completed.returncode == 0, completed
        assert "could not read" in completed.stderr, completed.stderr
        assert GATE_ENTERED not in completed.stderr, completed.stderr


def Gate_InvocationFromThisSession_LeavesATrace():
    with _catalog_repository() as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        completed = _run_gate(f'git commit -m "{MARKER_MESSAGE}"', repo, ledger)
        assert completed.returncode == 0, completed
        lines = _ledger(ledger)
        assert len(lines) == 1, lines
        assert lines[0][1] == "passed" and lines[0][2] == "p9999", lines


def Gate_InvocationFromAWorktree_LeavesATraceOrIsRecordedAsNotRunning():
    with _catalog_repository() as repo, tempfile.TemporaryDirectory() as elsewhere:
        worktree = pathlib.Path(elsewhere) / "worktree"
        _git(repo, "worktree", "add", "-q", "-b", "gate-probe", str(worktree))
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        completed = _run_gate(f'git commit -m "{MARKER_MESSAGE}"', str(worktree), ledger)
        assert completed.returncode == 0, completed
        lines = _ledger(ledger)
        assert len(lines) == 1 and lines[0][1] == "passed", lines
        assert os.path.realpath(lines[0][3]) == os.path.realpath(worktree), lines


def Gate_PassingRun_IsDistinguishableFromNoRun():
    with _catalog_repository() as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        _run_gate('git commit -m "chore: no phase here"', repo, ledger)
        assert _ledger(ledger) == [], _ledger(ledger)
        _run_gate(f'git commit -m "{MARKER_MESSAGE}"', repo, ledger)
        assert [line[1] for line in _ledger(ledger)] == ["passed"], _ledger(ledger)


def Gate_BlockedCommit_IsRecordedAsBlocked():
    with _catalog_repository(validator_exit=1) as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        completed = _run_gate(f'git commit -m "{MARKER_MESSAGE}"', repo, ledger)
        assert completed.returncode == 2, completed
        assert [line[1] for line in _ledger(ledger)] == ["blocked"], _ledger(ledger)


def Gate_MessageBuiltByTheShell_PassesThroughAndSaysSo():
    with _catalog_repository() as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        completed = _run_gate('git commit -m "$(cat message.txt)"', repo, ledger)
        assert completed.returncode == 0, completed
        assert "built by the shell" in completed.stderr, completed.stderr
        assert [line[1] for line in _ledger(ledger)] == ["not-gated"], _ledger(ledger)


def Gate_UnresolvableMessage_IsRecordedAsNotGated():
    with _catalog_repository() as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        _run_gate("git commit -F -", repo, ledger)
        assert [line[1] for line in _ledger(ledger)] == ["not-gated"], _ledger(ledger)


def Gate_CommitCarryingADateMintedId_Gates():
    """A p0507 id must reach the gate exactly as a counter id does. It would not have:
    the marker pattern required a `p`, so a date-minted phase commit exited 0 unchecked —
    a skip indistinguishable from a pass, since both are silent."""
    with _repository("seed") as repo:
        completed = _run_gate(f'git commit -m "{MINTED_MARKER_MESSAGE}"', repo)
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_DateMintedIdIsRecordedInTheLedger():
    """The ledger must name the phase, not "unknown" — a phase commit with no line never
    met the gate, so the id has to survive into the record as well as into the decision."""
    with _catalog_repository() as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        _run_gate(f'git commit -m "{MINTED_MARKER_MESSAGE}"', repo, ledger)
        lines = _ledger(ledger)
        assert [line[2] for line in lines] == ["2026-08-24-8a3f"], lines


def Gate_MalformedDateMintedMarker_PassesThrough():
    """Widening must not turn any parenthesised text into a phase marker."""
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: nightly (2026-13-99-zzzz)"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


# 2026-09-18-7b31: the smallest tree the repository's own mirror generator accepts. It
# resolves its root from its own location, exits 2 when the events directory is missing,
# warns and carries on when the system-event file is absent, and passes the record
# comparison when no records are declared — so a two-entry C# enum against a TypeScript
# mirror that carries one entry is the whole of the drift, and nothing else is needed.
_MIRROR_EVENT_TYPE_CSHARP = """namespace AgentSmith.Contracts.Events;

public enum EventType
{
    Alpha = 0,
    Beta = 1,
}
"""
_MIRROR_HUB_EVENTS_TS = {
    "intact": "export enum EventType {\n  Alpha = 0,\n  Beta = 1,\n}\n",
    "drifted": "export enum EventType {\n  Alpha = 0,\n}\n",
}
_MIRROR_SCRIPT = "node ../../tools/build-hub-event-types.mjs --check"


@contextlib.contextmanager
def _dashboard_repository(dashboard_exit=0, mirror=None):
    """A throwaway repository shaped like THIS one — it carries an AgentSmith.sln, so
    the gate runs the solution checks, and a src/dashboard whose pnpm is a stub that
    exits as told. The dashboard step runs first, so a failing stub blocks the phase
    commit before a single .NET command is reached.

    `mirror` gives the tree a generated mirror to check: this repository's own generator,
    a C# event enum and a TypeScript mirror that either keeps up with it ("intact") or has
    fallen behind it ("drifted"), and the package.json script that is what makes the gate
    run the step at all. Without it the package.json declares no such script, which is the
    shape of every tree cut before 2026-09-18-7b31 — and, until that phase, of every
    fixture here."""
    with _repository("seed") as repo:
        root = pathlib.Path(repo)
        (root / "AgentSmith.sln").write_text("# not a real solution\n")
        (root / "src" / "dashboard").mkdir(parents=True)
        package = {"name": "dashboard"}
        if mirror:
            package["scripts"] = {"gen:hub-events": _MIRROR_SCRIPT}
            (root / "tools").mkdir()
            shutil.copy(REPOSITORY_ROOT / "tools" / "build-hub-event-types.mjs",
                        root / "tools" / "build-hub-event-types.mjs")
            events = root / "src" / "backend" / "AgentSmith.Contracts" / "Events"
            events.mkdir(parents=True)
            (events / "EventType.cs").write_text(_MIRROR_EVENT_TYPE_CSHARP)
            types = root / "src" / "dashboard" / "src" / "types"
            types.mkdir(parents=True)
            (types / "hub-events.ts").write_text(_MIRROR_HUB_EVENTS_TS[mirror])
        (root / "src" / "dashboard" / "package.json").write_text(json.dumps(package) + "\n")
        stub_dir = root / "stub-bin"
        stub_dir.mkdir()
        pnpm = stub_dir / "pnpm"
        pnpm.write_text(
            "#!/usr/bin/env bash\n"
            'echo "pnpm $*" >>"$PNPM_TRACE"\n'
            # One exit code cannot serve every step once one of them has to do real work.
            # The gate runs each step through pnpm from src/dashboard, so the mirror check
            # runs the real generator from there exactly as the dashboard's own script
            # does; every other step still answers with the code the fixture was given.
            'if [ "$1" = "gen:hub-events" ]; then\n'
            f'  exec {_MIRROR_SCRIPT}\n'
            'fi\n'
            f"exit {dashboard_exit}\n")
        pnpm.chmod(0o755)
        _git(repo, "add", "-A")
        _git(repo, "commit", "-q", "-m", "chore: a dashboard to gate on")
        yield repo, stub_dir


def _run_gate_with_stub_pnpm(command, repo, stub_dir, trace, ledger=None):
    """Drive the gate with the stub pnpm ahead of anything real on PATH."""
    payload = json.dumps({"tool_input": {"command": command}, "cwd": repo})
    environment = _environment()
    environment["PHASE_GATE_LOG"] = str(ledger) if ledger else os.devnull
    environment["PATH"] = f"{stub_dir}{os.pathsep}{environment['PATH']}"
    environment["PNPM_TRACE"] = str(trace)
    return subprocess.run(["bash", str(GATE_PATH)], input=payload, cwd=repo,
                          env=environment, capture_output=True, text=True)


def Gate_RunsTheDashboardBuildAndTests():
    """2026-08-25-39ab: the dashboard workflow is path-filtered, so a backend-only payload
    change never ran a dashboard test. The gate has to run them itself — and it runs them
    FIRST, so a red dashboard blocks before the .NET checks are reached."""
    with _dashboard_repository() as (repo, stub_dir), tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "pnpm.trace"
        trace.write_text("")
        completed = _run_gate_with_stub_pnpm(
            f'git commit -m "{MARKER_MESSAGE}"', repo, stub_dir, trace)
        invoked = trace.read_text()
        assert "pnpm install --frozen-lockfile" in invoked, invoked
        assert "pnpm test" in invoked, invoked
        assert "pnpm build" in invoked, invoked
        assert "1/5 dashboard build + tests" in completed.stderr, completed.stderr


def Gate_TheDashboardTestsFail_BlocksTheCommit():
    """A red dashboard is a blocked phase commit, named as such — not a warning."""
    with _dashboard_repository(dashboard_exit=1) as (repo, stub_dir), \
            tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "pnpm.trace"
        trace.write_text("")
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        completed = _run_gate_with_stub_pnpm(
            f'git commit -m "{MARKER_MESSAGE}"', repo, stub_dir, trace, ledger)
        assert completed.returncode == 2, completed
        assert "dashboard: pnpm install" in completed.stderr, completed.stderr
        assert [line[1] for line in _ledger(ledger)] == ["blocked"], _ledger(ledger)


def Gate_MirrorDrift_BlocksTheCommitAndNamesTheEvent():
    """2026-09-18-7b31: a C# event contract outgrew its TypeScript mirror, this gate let
    the commit through and CI failed on the pull request — install, test and build cannot
    see drift in a file the dashboard never imports. The gate runs the check itself now,
    and the committer reads which event went missing without opening the log."""
    with _dashboard_repository(mirror="drifted") as (repo, stub_dir), \
            tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "pnpm.trace"
        trace.write_text("")
        completed = _run_gate_with_stub_pnpm(
            f'git commit -m "{MARKER_MESSAGE}"', repo, stub_dir, trace)
        assert completed.returncode == 2, completed
        assert "pnpm gen:hub-events" in trace.read_text(), trace.read_text()
        assert "missing in TS: Beta" in completed.stderr, completed.stderr
        assert "dashboard: pnpm gen:hub-events" in completed.stderr, completed.stderr


def Gate_MirrorIntact_RunsTheStepAndBlocksOnlyLater():
    """The negative of the case above: the step ran, it passed, and whatever the gate
    blocks on afterwards is not the mirror's doing. It cannot assert an allowed commit —
    the gate goes on to a dotnet build against a solution file that is not one — so it
    asserts the trace and the step's own line, the shape the dashboard marker case
    already uses. The step sits after install, where the existing stderr assertion on a
    failing dashboard needs it, and in the order the dashboard's own workflow runs."""
    with _dashboard_repository(mirror="intact") as (repo, stub_dir), \
            tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "pnpm.trace"
        trace.write_text("")
        completed = _run_gate_with_stub_pnpm(
            f'git commit -m "{MARKER_MESSAGE}"', repo, stub_dir, trace)
        assert trace.read_text().splitlines() == [
            "pnpm install --frozen-lockfile", "pnpm gen:hub-events",
            "pnpm test", "pnpm build"], trace.read_text()
        assert "dashboard: pnpm gen:hub-events ok" in completed.stderr, completed.stderr
        assert "missing in TS" not in completed.stderr, completed.stderr


def Gate_DashboardWithoutTheScript_SaysNothingToCheckAndIsNotBlockedByTheMirrorStep():
    """A tree cut before this phase declares no gen:hub-events script. Whether the step
    runs is read from package.json rather than inferred from pnpm's exit code, which
    answers a missing script with a non-zero this gate reads as a failure — so such a
    tree is skipped, says so, and whatever blocks it later is something else."""
    with _dashboard_repository() as (repo, stub_dir), tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "pnpm.trace"
        trace.write_text("")
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        completed = _run_gate_with_stub_pnpm(
            f'git commit -m "{MARKER_MESSAGE}"', repo, stub_dir, trace, ledger)
        assert "no gen:hub-events script" in completed.stderr, completed.stderr
        assert "nothing to check" in completed.stderr, completed.stderr
        assert "gen:hub-events" not in trace.read_text(), trace.read_text()
        assert [line[5] for line in _ledger(ledger)] == ["build"], _ledger(ledger)


def Gate_TheBlockedLedgerLine_NamesTheMirrorStep():
    """The ledger is what tells a pass from an absence once the run's output is gone, so a
    commit blocked by the mirror has to read as that in the line itself. It does without
    new code: the failure message renders the step word, and the mirror step is one word."""
    with _dashboard_repository(mirror="drifted") as (repo, stub_dir), \
            tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "pnpm.trace"
        trace.write_text("")
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        completed = _run_gate_with_stub_pnpm(
            f'git commit -m "{MARKER_MESSAGE}"', repo, stub_dir, trace, ledger)
        assert completed.returncode == 2, completed
        assert [(line[1], line[5]) for line in _ledger(ledger)] == [
            ("blocked", "dashboard: pnpm gen:hub-events")], _ledger(ledger)


# Everything the gate itself shells out to. A PATH assembled from exactly these —
# and nothing else — is a PATH with no pnpm on it, on any machine.
_GATE_TOOLS = ["bash", "python3", "git", "grep", "sed", "head", "tail", "tr", "date",
               "mktemp", "base64", "cat", "dirname", "env", "uname"]


@contextlib.contextmanager
def _path_without_pnpm():
    """A bin directory holding a symlink to each tool the gate needs, and nothing more."""
    with tempfile.TemporaryDirectory() as bin_dir:
        for tool in _GATE_TOOLS:
            resolved = shutil.which(tool)
            if resolved:
                os.symlink(resolved, os.path.join(bin_dir, tool))
        assert shutil.which("pnpm", path=bin_dir) is None, bin_dir
        yield bin_dir


def Gate_NoPnpmForADashboardThatExists_FailsLoudlyInsteadOfSkipping():
    """A missing toolchain leaves the dashboard unproven, and an unproven commit must not
    look like a passing one — the gate blocks and says what is missing."""
    with _dashboard_repository() as (repo, _stub_dir), _path_without_pnpm() as bin_dir:
        payload = json.dumps({"tool_input": {"command": f'git commit -m "{MARKER_MESSAGE}"'},
                              "cwd": repo})
        environment = _environment()
        environment["PHASE_GATE_LOG"] = os.devnull
        environment["PATH"] = bin_dir
        completed = subprocess.run([os.path.join(bin_dir, "bash"), str(GATE_PATH)],
                                   input=payload, cwd=repo, env=environment,
                                   capture_output=True, text=True)
        assert completed.returncode == 2, completed
        assert "pnpm" in completed.stderr, completed.stderr


# 2026-09-21-9ae2: the gate runs the hook tests of the tree it gates, before anything
# else it checks — the files below had sat beside it for three phases with nothing
# running them, which is how its command detection drifted from the forms agents write.
# A stand-in for one of those files: it records the environment the gate hands it, writes
# a ledger line of its own the way a real fixture does, and drives the gate once more,
# which is the recursion the step has to survive.
_PROBE_HOOK_TEST = '''"""A stand-in hook test, run by the gate against its own tree."""
import json
import os
import pathlib
import subprocess

ledger = os.environ.get("PHASE_GATE_LOG", "")
with pathlib.Path(os.environ["PROBE_TRACE"]).open("a") as trace:
    trace.write(f"ran\\tledger={ledger}\\n")
if ledger:
    with pathlib.Path(ledger).open("a") as handle:
        handle.write("probe\\twritten-by-a-hook-test\\n")
if not os.environ.get("PROBE_DEPTH"):
    payload = json.dumps({"tool_input": {"command": os.environ["PROBE_COMMAND"]},
                          "cwd": os.environ["PROBE_TREE"]})
    subprocess.run(["bash", os.environ["PROBE_GATE"]], input=payload, text=True,
                   cwd=os.environ["PROBE_TREE"], capture_output=True, timeout=120,
                   env=dict(os.environ, PROBE_DEPTH="1"))
'''
_FAILING_HOOK_TEST = "raise SystemExit('the hook tests are red')\n"


@contextlib.contextmanager
def _repository_with_hook_tests(hook_test=_PROBE_HOOK_TEST):
    """A throwaway repository carrying a skills validator — so the gate reaches a verdict
    without a .NET build — and one hook test of its own for the gate to run."""
    with _catalog_repository() as repo:
        hooks = pathlib.Path(repo) / ".claude" / "hooks"
        hooks.mkdir(parents=True)
        (hooks / "test_probe.py").write_text(hook_test)
        yield repo


def _run_gate_over_hook_tests(repo, ledger, trace):
    """Drive the gate against a tree whose hook test is the probe, telling the probe how
    to drive the gate again."""
    payload = json.dumps({"tool_input": {"command": f'git commit -m "{MARKER_MESSAGE}"'},
                          "cwd": repo})
    environment = _environment()
    environment["PHASE_GATE_LOG"] = str(ledger)
    environment["PROBE_TRACE"] = str(trace)
    environment["PROBE_GATE"] = str(GATE_PATH)
    environment["PROBE_TREE"] = str(repo)
    environment["PROBE_COMMAND"] = f'git commit -m "{MARKER_MESSAGE}"'
    return subprocess.run(["bash", str(GATE_PATH)], input=payload, cwd=repo,
                          env=environment, capture_output=True, text=True)


def Gate_TheHookTests_AreRunByTheGate():
    with _repository_with_hook_tests() as repo, tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "probe.trace"
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        completed = _run_gate_over_hook_tests(repo, ledger, trace)
        assert completed.returncode == 0, completed
        assert trace.read_text().startswith("ran\t"), trace.read_text()
        assert "hook tests: test_probe.py ok" in completed.stderr, completed.stderr


def Gate_TheHookTestsRunByTheGate_WriteNoLedgerLine():
    """The tests drive the gate against throwaway repositories, so their verdicts must
    not reach this one — a ledger line that names no commit is worse than none."""
    with _repository_with_hook_tests() as repo, tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "probe.trace"
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        _run_gate_over_hook_tests(repo, ledger, trace)
        recorded = trace.read_text().partition("ledger=")[2].strip()
        assert recorded and recorded != str(ledger), trace.read_text()
        assert [line[1] for line in _ledger(ledger)] == ["passed"], _ledger(ledger)


def Gate_TheHookTestsRunByTheGate_AreNotRunAgainByTheGateTheyDrive():
    """The gate they invoke skips this step instead of running the tests that invoked
    it — the probe therefore runs once, not once per level."""
    with _repository_with_hook_tests() as repo, tempfile.TemporaryDirectory() as scratch:
        trace = pathlib.Path(scratch) / "probe.trace"
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        _run_gate_over_hook_tests(repo, ledger, trace)
        assert len(trace.read_text().splitlines()) == 1, trace.read_text()


def Gate_RedHookTests_BlockTheCommit():
    """A step that never blocks is decoration; the gate that cannot prove itself does
    not let the phase commit past."""
    with _repository_with_hook_tests(_FAILING_HOOK_TEST) as repo, \
            tempfile.TemporaryDirectory() as scratch:
        ledger = pathlib.Path(scratch) / "phase-gate.log"
        completed = _run_gate(f'git commit -m "{MARKER_MESSAGE}"', repo, ledger)
        assert completed.returncode == 2, completed
        assert "the hook tests are red" in completed.stderr, completed.stderr
        assert [(line[1], line[5]) for line in _ledger(ledger)] == [
            ("blocked", "hook tests: test_probe.py")], _ledger(ledger)


def Gate_ASpawnedGate_InheritsNoSelftestFlag():
    """The gate sets PHASE_GATE_SELFTEST when it runs these tests, so every gate the suite
    spawns would skip its own hook-test step if the helper passed the flag on. The four tests
    above would then be green by hand and red whenever the gate ran them, which is how they
    shipped. Pinned here rather than in each of them, because the next spawned gate is the one
    that would forget."""
    os.environ["PHASE_GATE_SELFTEST"] = "1"
    try:
        assert "PHASE_GATE_SELFTEST" not in _environment(), _environment()
    finally:
        os.environ.pop("PHASE_GATE_SELFTEST", None)


def _cases():
    return [(name, case) for name, case in list(globals().items())
            if callable(case) and (name.startswith("Resolve_") or name.startswith("Gate_"))]


def main():
    cases = _cases()
    failed = []
    for name, case in cases:
        try:
            case()
            print(f"PASS {name}")
        except Exception:
            failed.append(name)
            print(f"FAIL {name}")
            traceback.print_exc()
    print(f"\n{len(cases) - len(failed)}/{len(cases)} passed")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
