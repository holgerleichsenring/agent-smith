#!/usr/bin/env python3
"""Exercises the phase-gate commit-message resolver over every invocation form.

Run it directly:  python3 .claude/hooks/test_commit_message.py

The Resolve_* cases call the resolver as a function. The Gate_* cases drive
phase-gate.sh itself against a throwaway repository, where it has no
AgentSmith.sln and no skills validator and therefore reports "nothing to gate"
instead of running the four .NET checks — that line is the tell that the gate
recognised the commit as a phase commit at all.
"""

import contextlib
import importlib.util
import json
import os
import pathlib
import subprocess
import tempfile
import traceback

HOOKS_DIR = pathlib.Path(__file__).resolve().parent
RESOLVER_PATH = HOOKS_DIR / "commit-message.py"
GATE_PATH = HOOKS_DIR / "phase-gate.sh"
GATE_ENTERED = "nothing to gate"
MARKER_MESSAGE = "feat: something (p9999)"

_GIT_IDENTITY = ["-c", "user.email=gate@example.invalid", "-c", "user.name=Phase Gate"]
_resolver_module = None


def _environment():
    """A git environment that ignores the operator's global and system config."""
    environment = dict(os.environ)
    environment.pop("CLAUDE_PROJECT_DIR", None)
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


def _run_gate(command, cwd):
    payload = json.dumps({"tool_input": {"command": command}, "cwd": cwd})
    return subprocess.run(["bash", str(GATE_PATH)], input=payload, cwd=cwd,
                          env=_environment(), capture_output=True, text=True)


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
