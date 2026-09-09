#!/usr/bin/env python3
"""Exercises what phase-gate.sh RECOGNISES as a phase commit (2026-09-09-8fce).

Run it directly:  python3 .claude/hooks/test_phase_gate_marker.py

The helpers come from test_commit_message.py, which owns the throwaway-repository
and ledger fixtures. These cases live in their own file only so the marker work
does not collide with uncommitted changes in that one; fold them in when it is
free.

A repository with no AgentSmith.sln and no skills validator makes the gate report
"nothing to gate" — that line is the tell that it recognised the commit as a phase
commit at all, without running a single .NET check.
"""

import importlib.util
import pathlib
import tempfile
import traceback

HOOKS_DIR = pathlib.Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location(
    "gate_fixtures", HOOKS_DIR / "test_commit_message.py")
_fixtures = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_fixtures)

GATE_ENTERED = _fixtures.GATE_ENTERED
_repository = _fixtures._repository
_run_gate = _fixtures._run_gate
_ledger = _fixtures._ledger

TWO_IDS = "docs: two phases in one commit (2026-09-09-72fd, 2026-09-09-3d2a)"
NO_MARKER = "docs: the set moves into phases for 2026-09-09-e1c8"


def Gate_MarkerCarryingTwoIdsSeparatedByAComma_IsGated():
    with _repository("seed") as repo:
        completed = _run_gate(f'git commit -m "{TWO_IDS}"', repo)
        assert completed.returncode == 0, completed
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_MarkerCarryingTwoIdsSeparatedByASpace_IsGated():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "docs: both (p0508 2026-08-24-8a3f)"', repo)
        assert completed.returncode == 0, completed
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_MarkerCarryingTwoIds_RecordsTheFirstInTheLedger():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        _run_gate(f'git commit -m "{TWO_IDS}"', repo, ledger)
        lines = _ledger(ledger)
        assert [line[2] for line in lines] == ["2026-09-09-72fd"], lines


def Gate_SubjectNamingAPhaseWithoutAMarker_IsRecordedAsNotGated():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        _run_gate(f'git commit -m "{NO_MARKER}"', repo, ledger)
        assert [line[1] for line in _ledger(ledger)] == ["not-gated"], _ledger(ledger)


def Gate_SubjectNamingAPhaseWithoutAMarker_SaysSoOnStderr():
    with _repository("seed") as repo:
        completed = _run_gate(f'git commit -m "{NO_MARKER}"', repo)
        assert completed.returncode == 0, completed
        assert "no marker the gate reads" in completed.stderr, completed.stderr


def Gate_SubjectNamingAPhaseWithoutAMarker_NamesThePhaseInTheLedger():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "phase-gate.log"
        _run_gate(f'git commit -m "{NO_MARKER}"', repo, ledger)
        assert [line[2] for line in _ledger(ledger)] == ["2026-09-09-e1c8"], _ledger(ledger)


def Gate_BodyNamingAPhaseWithAMarkerlessSubject_StaysSilent():
    with _repository("seed") as repo:
        completed = _run_gate(
            'git commit -m "chore: tidy the hooks" -m "follows p0508"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


def Gate_AShortPLikeTokenInTheSubject_StaysSilent():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: bump p12 to the new default"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


def Gate_AMessageWithNoPhaseAnywhere_StaysSilent():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: no phase here"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


def Gate_AMalformedDateMintedMarker_StaysSilent():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: nightly (2026-13-99-zzzz)"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


def main():
    cases = [(name, case) for name, case in list(globals().items())
             if callable(case) and name.startswith("Gate_")]
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
