#!/usr/bin/env python3
"""Exercises what spec-gate.sh RECOGNISES as a spec commit (2026-09-09-8fce).

Two things decide that: the MARKER the message carries, and the SHAPE of the
command (2026-09-21-9ae2). Both tables live here — the forms that must gate and
the forms that must not.

Run it directly:  python3 .claude/hooks/test_spec_gate_marker.py

The helpers come from test_commit_message.py, which owns the throwaway-repository
and ledger fixtures. These cases live in their own file only so the marker work
does not collide with uncommitted changes in that one; fold them in when it is
free.

A repository with no AgentSmith.sln and no skills validator makes the gate report
"nothing to gate" — that line is the tell that it recognised the commit as a spec
commit at all, without running a single .NET check.
"""

import contextlib
import importlib.util
import os
import subprocess
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

TWO_IDS = "docs: two specs in one commit (2026-09-09-72fd, 2026-09-09-3d2a)"
NO_MARKER = "docs: the set moves into specs for 2026-09-09-e1c8"


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
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        _run_gate(f'git commit -m "{TWO_IDS}"', repo, ledger)
        lines = _ledger(ledger)
        assert [line[2] for line in lines] == ["2026-09-09-72fd"], lines


def Gate_SubjectNamingASpecWithoutAMarker_IsRecordedAsNotGated():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        _run_gate(f'git commit -m "{NO_MARKER}"', repo, ledger)
        assert [line[1] for line in _ledger(ledger)] == ["not-gated"], _ledger(ledger)


def Gate_SubjectNamingASpecWithoutAMarker_SaysSoOnStderr():
    with _repository("seed") as repo:
        completed = _run_gate(f'git commit -m "{NO_MARKER}"', repo)
        assert completed.returncode == 0, completed
        assert "no marker the gate reads" in completed.stderr, completed.stderr


def Gate_SubjectNamingASpecWithoutAMarker_NamesTheSpecInTheLedger():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        _run_gate(f'git commit -m "{NO_MARKER}"', repo, ledger)
        assert [line[2] for line in _ledger(ledger)] == ["2026-09-09-e1c8"], _ledger(ledger)


def Gate_MarkerCarryingASeriesId_IsGated():
    """2026-09-03-2f81: the marker required a closing parenthesis straight after four hex
    digits, so a series commit exited zero with no stderr line and no ledger row — the
    dashboard build, the backend build, the suite and the harness presets all skipped,
    and nothing recording that they had been."""
    with _repository("seed") as repo:
        completed = _run_gate(
            'git commit -m "feat: the first slice (2026-08-24-8a3fa)"', repo)
        assert completed.returncode == 0, completed
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_MarkerCarryingASeriesId_RecordsTheWholeIdInTheLedger():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        _run_gate('git commit -m "feat: the first slice (2026-08-24-8a3fa)"', repo, ledger)
        lines = _ledger(ledger)
        assert [line[2] for line in lines] == ["2026-08-24-8a3fa"], lines


def Gate_BodyNamingASpecWithAMarkerlessSubject_StaysSilent():
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


def Gate_AMessageWithNoSpecAnywhere_StaysSilent():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: no spec here"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


def Gate_AMalformedDateMintedMarker_StaysSilent():
    with _repository("seed") as repo:
        completed = _run_gate('git commit -m "chore: nightly (2026-13-99-zzzz)"', repo)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr


# The command-shape table (2026-09-21-9ae2). git's global options sit between the
# program and the subcommand; a commit addressed with a path is the form an agent told
# to use absolute paths writes, and it slipped the gate entirely — no checks, no ledger
# line, nothing to tell the skip from a pass.
SHAPED = "feat: the shape of the command (p9999)"


def Gate_APlainCommit_IsStillGated():
    with _repository("seed") as repo:
        completed = _run_gate(f'git commit -m "{SHAPED}"', repo)
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_ACommitAddressedWithAPath_IsGated():
    """`git -C <tree> commit` — and it gates THAT tree, not the session's: a gate that
    recognises the commit but checks somewhere else reports numbers from code the commit
    does not contain."""
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        completed = _run_gate(f'git -C {repo} commit -m "{SHAPED}"', elsewhere, ledger)
        assert GATE_ENTERED in completed.stderr, completed.stderr
        lines = _ledger(ledger)
        assert len(lines) == 1, lines
        assert os.path.realpath(lines[0][3]) == os.path.realpath(repo), lines


def Gate_ACommitCarryingAConfigOption_IsGated():
    with _repository("seed") as repo:
        completed = _run_gate(
            f'git -c user.name=Someone commit -m "{SHAPED}"', repo)
        assert GATE_ENTERED in completed.stderr, completed.stderr


def Gate_ACommitAfterALeadingChangeOfDirectory_IsStillGated():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        completed = _run_gate(f'cd {repo} && git commit -m "{SHAPED}"', elsewhere, ledger)
        assert GATE_ENTERED in completed.stderr, completed.stderr
        lines = _ledger(ledger)
        assert len(lines) == 1, lines
        assert os.path.realpath(lines[0][3]) == os.path.realpath(repo), lines


def Gate_ACommandThatOnlyMentionsACommit_IsStillNotGated():
    """Accepting options must not become accepting anything: a command that merely
    quotes the shape stays untouched, marker and all."""
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        completed = _run_gate(
            f'echo "git -C {repo} commit -m \'{SHAPED}\'"', repo, ledger)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr
        assert _ledger(ledger) == [], _ledger(ledger)


def Gate_AGitSubcommandThatIsNotCommit_IsStillNotGated():
    """`commit-graph` begins with the word and is a different subcommand. The marker in
    the trailing comment is what makes the case bite: were the subcommand allowed to run
    on past `commit`, the whole command line would be read as the message and gate."""
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        completed = _run_gate(
            f'git -C {repo} commit-graph write  # {SHAPED}', repo, ledger)
        assert completed.returncode == 0, completed
        assert completed.stderr == "", completed.stderr
        assert _ledger(ledger) == [], _ledger(ledger)


# The worktree table (2026-11-17-f270). The gate runs from the main checkout, the commit
# runs in a worktree, and the hook payload's cwd is the main checkout — so every shape an
# agent writes to reach its worktree must resolve to the worktree, or the gate builds the
# wrong tree and its ledger line names it. Each case gives the resolver the MAIN checkout
# as the cwd, the way the hook does.
COMMIT = "git " + "commit"
_resolve = _fixtures._resolve
_git = _fixtures._git
_catalog_repository = _fixtures._catalog_repository


@contextlib.contextmanager
def _main_and_worktree():
    """A repository (the "main checkout") and a linked worktree of it, both real paths."""
    with _repository("seed") as main, tempfile.TemporaryDirectory() as elsewhere:
        worktree = os.path.join(os.path.realpath(elsewhere), "wt")
        _git(main, "worktree", "add", "-q", "-b", "wt-probe", worktree)
        yield os.path.realpath(main), worktree


def _tree_of(command, cwd):
    result = _resolve(command, cwd)
    assert result.is_commit, (command, result)
    assert result.problem is None, (command, result)
    return os.path.realpath(result.tree)


_WORKTREE_SHAPES = [
    "cd {wt}; {commit} -m 'feat: x (p9999)'",
    "cd {wt} && git add -A && git status --short && {commit} -m 'feat: x (p9999)'",
    "cd {wt}\ngit add -A\n{commit} -m 'feat: x (p9999)'",
    "pkill -f 'until grep' ; cd {wt} && {commit} -m 'feat: x (p9999)'",
    "(cd {wt} && {commit} -m 'feat: x (p9999)')",
    "git -C {wt} commit -m 'feat: x (p9999)'",
    "git --git-dir={wt}/.git --work-tree={wt} commit -m 'feat: x (p9999)'",
    "GIT_DIR={wt}/.git GIT_WORK_TREE={wt} {commit} -m 'feat: x (p9999)'",
    "export GIT_DIR={wt}/.git GIT_WORK_TREE={wt}\n{commit} -m 'feat: x (p9999)'",
    "WT={wt}; cd \"$WT\" && {commit} -m 'feat: x (p9999)'",
    "pushd {wt} >/dev/null && {commit} -m 'feat: x (p9999)'",
    "env GIT_AUTHOR_NAME=x git status 2>&1 | tail -1; true; cd {wt} && {commit} -m 'feat: x (p9999)'",
    "bash -c 'cd {wt} && {commit} -m \"feat: x (p9999)\"'",
    "eval \"cd {wt} && {commit} -m 'feat: x (p9999)'\"",
    "cd {wt} && git -C \"$PWD\" commit -m 'feat: x (p9999)'",
    "cd {wt} 2>/dev/null || cd /; {commit} -m 'feat: x (p9999)'",
    "cd {wt} && cd \"$(git rev-parse --show-toplevel)\" && {commit} -m 'feat: x (p9999)'",
    "cd {wt} && WT=$(pwd) && cd / && cd \"$WT\" && /usr/bin/git commit -m 'feat: x (p9999)'",
    "cat > /dev/null <<'EOF'\nit's a body with \"quotes\" and a ) paren\nEOF\ncd {wt} && {commit} -m 'feat: x (p9999)'",
    "cd {wt} && git add -A && {commit} -F - <<'EOF'\nfeat: it's the worktree (p9999)\nEOF",
]


def Tree_EveryShapeAnAgentWrites_ResolvesToTheWorktree():
    with _main_and_worktree() as (main, worktree):
        for shape in _WORKTREE_SHAPES:
            command = shape.format(wt=worktree, commit=COMMIT)
            assert _tree_of(command, main) == worktree, command


def Tree_ASubshellsCd_EndsWithTheSubshell():
    with _main_and_worktree() as (main, worktree):
        assert _tree_of(f"(cd {worktree}) && {COMMIT} -m x", main) == main


def Tree_ACdTheGateCannotFollow_IsAProblemNotTheCwd():
    with _main_and_worktree() as (main, _):
        for command in (f'cd "$SPEC_GATE_UNSET_PROBE" && {COMMIT} -m x',
                        f'WT=$(cat wt.txt); cd "$WT" && {COMMIT} -m x',
                        f"cd - && {COMMIT} -m x"):
            result = _resolve(command, main)
            assert result.tree is None and result.problem, (command, result)


def Message_DashFStdinFromTheCommandsHeredoc_IsRead():
    for command in (f"{COMMIT} -F - <<'EOF'\nfeat: it's read (p9999)\n\nbody\nEOF",
                    f"{COMMIT} -qF- <<EOF\nfeat: it's read (p9999)\nEOF\n",
                    f"{COMMIT} -F - <<-EOF\n\tfeat: it's read (p9999)\n\tEOF",
                    f"{COMMIT} -F /dev/stdin <<'EOF'\nfeat: it's read (p9999)\nEOF",
                    f'{COMMIT} -F - <<< "feat: it\'s read (p9999)"'):
        result = _resolve(command, ".")
        assert result.message is not None and result.message.startswith("feat: it's read (p9999)"), (command, result)


def Message_AFileTheSameCommandWrites_IsReadFromTheCommand():
    """On disk the file does not exist yet when the hook runs — or holds the previous
    attempt's text, which is worse."""
    with tempfile.TemporaryDirectory() as path:
        stale = pathlib.Path(path) / "msg.txt"
        stale.write_text("chore: the previous attempt\n")
        for command in (f"cat > {stale} <<'EOF'\nfeat: this attempt (p9999)\nEOF\n{COMMIT} -F {stale}",
                        f"cat <<'EOF' > msg.txt\nfeat: this attempt (p9999)\nEOF\n{COMMIT} -F msg.txt",
                        f"tee {stale} >/dev/null <<'EOF'\nfeat: this attempt (p9999)\nEOF\n{COMMIT} -F {stale}"):
            result = _resolve(command, path)
            assert result.message == "feat: this attempt (p9999)\n", (command, result)


def Message_AFileWrittenAtRunTime_IsUnreadableNotStale():
    with tempfile.TemporaryDirectory() as path:
        (pathlib.Path(path) / "msg.txt").write_text("chore: the previous attempt\n")
        result = _resolve(f"git log -1 --format=%B > msg.txt && {COMMIT} -F msg.txt", path)
        assert result.message is None and "same command" in result.reason, result


def Message_APipedMessage_StaysUnreadable():
    result = _resolve(f"cat msg.txt | {COMMIT} -F -", ".")
    assert result.message is None and "piped" in result.reason, result


def Message_TextTheCommandPrintsItself_IsRead():
    with tempfile.TemporaryDirectory() as path:
        for command in (f"cat <<'EOF' | {COMMIT} -F -\nfeat: it's piped (p9999)\nEOF",
                        f"echo \"feat: it's piped (p9999)\" | {COMMIT} -F -",
                        f"printf 'feat: it\\'s piped (p9999)\\n' | {COMMIT} -F -".replace("\\'", "'\"'\"'"),
                        f"echo 'feat: it'\"'\"'s piped (p9999)' > m.txt && {COMMIT} -F m.txt",
                        f"{COMMIT} -m $'feat: it\\'s piped (p9999)'"):
            result = _resolve(command, path)
            assert result.message is not None and result.message.rstrip("\n") == "feat: it's piped (p9999)", (command, result)


def Message_SigningOptionsWithAttachedKeys_AreNotMessages():
    result = _resolve(f"{COMMIT} -Smykey -m 'feat: x (p9999)'", ".")
    assert result.message == "feat: x (p9999)", result


def Message_CatHeredocSubstitution_IsTheBody():
    result = _resolve(f"{COMMIT} -m \"$(cat <<'EOF'\nfeat: it's the body (p9999)\n\nmore\nEOF\n)\"", ".")
    assert result.message == "feat: it's the body (p9999)\n\nmore\n", result


def Detect_CommandsThatOnlyMentionACommit_AreNotCommits():
    for command in ("git add commit.txt",
                    f"echo {COMMIT} -m 'feat: x (p9999)'",
                    f"cat <<'EOF'\n{COMMIT} -m 'feat: x (p9999)'\nEOF",
                    f"ls  # {COMMIT} -m 'feat: x (p9999)'",
                    "git commit-graph write",
                    f"gh pr create --body \"$(cat <<'EOF'\n{COMMIT} -m it's (p9999)\nEOF\n)\""):
        assert not _resolve(command, ".").is_commit, command


def Detect_WrappedAndSubshelledCommits_AreCommits():
    for command in (f"env FOO=1 {COMMIT} -m x", f"GIT_AUTHOR_NAME=x {COMMIT} -m x",
                    f"({COMMIT} -m x)", f"{{ {COMMIT} -m x; }}", f"timeout 600 {COMMIT} -m x",
                    f"command {COMMIT} -m x", f"nice -n 5 {COMMIT} -m x", f"env -u X {COMMIT} -m x", f"if {COMMIT} -m x; then :; fi", f"git -c a.b=c --no-pager commit -m x"):
        assert _resolve(command, ".").is_commit, command


def Gate_AHeredocCommitInAWorktree_IsGatedThereAndNamesItsHead():
    """The failure of 2026-11-17: a heredoc body with an apostrophe, a cd into the worktree,
    a session cwd in the main checkout — the line named the main checkout at its HEAD."""
    with _catalog_repository() as main, tempfile.TemporaryDirectory() as elsewhere:
        worktree = os.path.join(os.path.realpath(elsewhere), "wt")
        _git(main, "worktree", "add", "-q", "-b", "wt-probe", worktree)
        _git(worktree, "commit", "-q", "--allow-empty", "-m", "chore: the worktree moves on")
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        command = f"cd {worktree} && git add -A && {COMMIT} -F - <<'EOF'\nfeat: it's gated (p9999)\nEOF"
        completed = _run_gate(command, main, ledger)
        assert completed.returncode == 0, completed
        lines = _ledger(ledger)
        assert [line[1] for line in lines] == ["passed"], lines
        assert os.path.realpath(lines[0][3]) == worktree, lines
        head = subprocess.run(["git", "-C", worktree, "rev-parse", "--short", "HEAD"],
                              capture_output=True, text=True).stdout.strip()
        assert lines[0][4] == head, (lines, head)


def Gate_ASpecCommitInAnUnknownTree_IsBlocked():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        completed = _run_gate(f'cd "$SPEC_GATE_UNSET_PROBE" && {COMMIT} -m "{SHAPED}"', repo, ledger)
        assert completed.returncode == 2, completed
        assert "cannot tell which git tree" in completed.stderr, completed.stderr
        assert [line[1] for line in _ledger(ledger)] == ["blocked"], _ledger(ledger)


def Gate_AnOrdinaryCommitInAnUnknownTree_PassesSilently():
    with _repository("seed") as repo:
        completed = _run_gate(f'cd "$SPEC_GATE_UNSET_PROBE" && {COMMIT} -m "chore: x"', repo)
        assert completed.returncode == 0 and completed.stderr == "", completed


def Gate_ACommandTheReaderCannotRead_CarryingAMarker_IsBlocked():
    with _repository("seed") as repo:
        completed = _run_gate(f"{COMMIT} -m 'feat: unterminated (p9999)", repo)
        assert completed.returncode == 2, completed
        assert "could not read this command" in completed.stderr, completed.stderr


def Gate_APipedSpecMessage_IsRecordedNotGatedUnderItsSpec():
    with _repository("seed") as repo, tempfile.TemporaryDirectory() as elsewhere:
        ledger = pathlib.Path(elsewhere) / "spec-gate.log"
        completed = _run_gate(f"cat msg.txt | sed 's/x/y/' | {COMMIT} -F - # {SHAPED}", repo, ledger)
        assert completed.returncode == 0 and "piped" in completed.stderr, completed
        lines = _ledger(ledger)
        assert [(line[1], line[2]) for line in lines] == [("not-gated", "p9999")], lines
        assert os.path.realpath(lines[0][3]) == os.path.realpath(repo), lines


def main():
    cases = [(name, case) for name, case in list(globals().items())
             if callable(case) and name.startswith(("Gate_", "Tree_", "Message_", "Detect_"))]
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
