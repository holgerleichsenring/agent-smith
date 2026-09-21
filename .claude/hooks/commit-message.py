#!/usr/bin/env python3
"""Resolves the message a `git commit` invocation will actually carry.

phase-gate.sh looks for the phase marker in that message instead of in the raw
command line, so `--amend --no-edit`, `-F <file>` and `-C <rev>` are gated like
an explicit `-m`. Splitting the command in bash would mean `eval`, and `eval`
would run the commit — hence python.

Called as a filter: the command arrives on stdin, the working directory as
argv[1]. Exit 0 prints the message on stdout; exit 3 prints, instead, why the
message cannot be read yet (a bare commit, an editor amend, `-F -`) — those
cases have no message to gate at this point, so the gate lets them through and
says so.

`--work-dir <cwd>` instead prints the directory the commit will run in, moved by
a leading `cd` and by git's own `-C` (2026-09-21-9ae2). The gate reads it to
decide which tree to check, so the tree that is built and the message that is
read come from one definition of where the commit happens.
"""

import os
import shlex
import subprocess
import sys
from dataclasses import dataclass, field

EXIT_UNRESOLVABLE = 3
COMMIT = "commit"
DIRECTORY_OPTION = "-C"
WORK_DIR_MODE = "--work-dir"
SEPARATORS = (";", "&&", "||", "|", "&")
PARAGRAPH_BREAK = "\n\n"
VALUE_SHORT_OPTIONS = "mFtCc"
LONG_OPTIONS = {
    "--message": "-m",
    "--file": "-F",
    "--template": "-t",
    "--reuse-message": "-C",
    "--reedit-message": "-c",
}


@dataclass(frozen=True)
class Resolution:
    """Either the message the commit will carry, or why it cannot be read yet."""

    message: str | None = None
    reason: str | None = None


@dataclass
class MessageSources:
    """The message-bearing options of one `git commit` invocation."""

    messages: list[str] = field(default_factory=list)
    file: str | None = None
    template: str | None = None
    revision: str | None = None
    is_amend: bool = False
    is_no_edit: bool = False


def resolve(command: str, cwd: str) -> Resolution:
    """Resolve the commit message of `command`, run from `cwd`."""
    tokens = _split(command)
    if tokens is None:
        return Resolution(message=command)
    segment = _commit_segment(tokens)
    if segment is None:
        return Resolution(message=command)
    arguments = segment[segment.index(COMMIT) + 1:]
    return _decide(_scan(arguments), _effective_directory(tokens, cwd))


def work_directory(command: str, cwd: str) -> str:
    """The directory `command` will commit in: `cwd`, moved by a leading `cd` and by
    git's own `-C`. A command that is not a commit at all moves nothing."""
    tokens = _split(command)
    return cwd if tokens is None else _effective_directory(tokens, cwd)


def _split(command: str) -> list[str] | None:
    """Tokenise the command, or None when it cannot be split (heredocs, unbalanced quotes)."""
    try:
        return shlex.split(command)
    except ValueError:
        return None


def _commit_segment(tokens: list[str]) -> list[str] | None:
    """The first `git ... commit` segment — git, its global options, the subcommand
    and its arguments."""
    for segment in _segments(tokens):
        if segment and segment[0] == "git" and COMMIT in segment:
            return segment
    return None


def _segments(tokens: list[str]) -> list[list[str]]:
    """Command segments between separators. shlex keeps a `;` glued to its neighbour
    (`cd wt; git commit` tokenises as `wt;`), so a token is split on `;` before the
    separator test — otherwise the commit segment is never found and the gate lets
    the commit through as an unresolvable message."""
    segments: list[list[str]] = [[]]
    for token in tokens:
        for part in _split_on_semicolon(token):
            if part in SEPARATORS:
                segments.append([])
            elif part:
                segments[-1].append(part)
    return segments


def _split_on_semicolon(token: str) -> list[str]:
    if token in SEPARATORS or ";" not in token:
        return [token]
    parts: list[str] = []
    for piece in token.split(";"):
        parts.append(piece)
        parts.append(";")
    return parts[:-1]


def _effective_directory(tokens: list[str], cwd: str) -> str:
    """The directory the commit runs in — a leading `cd` moves it, and git's own `-C`
    moves it again, in that order, because that is the order the shell and git apply
    them. Each is joined onto the last, so repeated `-C` accumulates as git does."""
    directory = cwd
    first = _segments(tokens)[0]
    if len(first) >= 2 and first[0] == "cd":
        directory = os.path.join(directory, os.path.expanduser(first[1]))
    for path in _directory_options(tokens):
        directory = os.path.join(directory, os.path.expanduser(path))
    return directory


def _directory_options(tokens: list[str]) -> list[str]:
    """The paths of git's `-C` global options. Only the tokens BEFORE the subcommand
    are read: `commit -C <rev>` reuses a message and names no directory at all."""
    segment = _commit_segment(tokens)
    if segment is None:
        return []
    paths: list[str] = []
    expecting = False
    for token in segment[1:segment.index(COMMIT)]:
        if expecting:
            paths.append(token)
            expecting = False
        elif token == DIRECTORY_OPTION:
            expecting = True
        elif token.startswith(DIRECTORY_OPTION):
            paths.append(token[len(DIRECTORY_OPTION):])
    return paths


def _scan(arguments: list[str]) -> MessageSources:
    sources = MessageSources()
    pending: str | None = None
    for argument in arguments:
        if pending is not None:
            _assign(sources, pending, argument)
            pending = None
            continue
        if argument in ("--amend", "--no-edit"):
            setattr(sources, "is_amend" if argument == "--amend" else "is_no_edit", True)
            continue
        option, value = _as_option(argument)
        if option is None:
            continue
        if value is None:
            pending = option
        else:
            _assign(sources, option, value)
    return sources


def _as_option(argument: str) -> tuple[str | None, str | None]:
    """Map an argument to its canonical short option plus an attached value, if any."""
    if argument.startswith("--"):
        name, separator, value = argument.partition("=")
        return LONG_OPTIONS.get(name), (value if separator else None)
    if not argument.startswith("-"):
        return None, None
    for index, character in enumerate(argument[1:]):
        if character in VALUE_SHORT_OPTIONS:
            return f"-{character}", argument[index + 2:] or None
    return None, None


def _assign(sources: MessageSources, option: str, value: str) -> None:
    if option == "-m":
        sources.messages.append(value)
    elif option == "-F":
        sources.file = value
    elif option == "-t":
        sources.template = value
    else:
        sources.revision = value


def _decide(sources: MessageSources, work_dir: str) -> Resolution:
    if sources.messages:
        return Resolution(message=PARAGRAPH_BREAK.join(sources.messages))
    if sources.file == "-":
        return Resolution(reason="the message is read from stdin (-F -)")
    if sources.file is not None:
        return _from_file(sources.file, work_dir, "-F")
    if sources.revision is not None:
        return _from_revision(sources.revision, work_dir)
    if sources.template is not None:
        return _from_file(sources.template, work_dir, "-t")
    if sources.is_amend:
        if sources.is_no_edit:
            return _from_revision("HEAD", work_dir)
        return Resolution(reason="--amend without --no-edit opens an editor")
    return Resolution(reason="no message on the command line — git will open an editor")


def _from_file(path: str, work_dir: str, option: str) -> Resolution:
    full_path = os.path.join(work_dir, os.path.expanduser(path))
    try:
        with open(full_path, encoding="utf-8", errors="replace") as handle:
            return Resolution(message=handle.read())
    except OSError as error:
        return Resolution(reason=f"cannot read the {option} message file {full_path}: {error}")


def _from_revision(revision: str, work_dir: str) -> Resolution:
    completed = subprocess.run(["git", "log", "-1", "--pretty=%B", revision],
                               cwd=work_dir, capture_output=True, text=True)
    if completed.returncode != 0:
        return Resolution(reason=f"cannot read the message of {revision}: {completed.stderr.strip()}")
    return Resolution(message=completed.stdout)


def main() -> int:
    arguments = sys.argv[1:]
    if arguments and arguments[0] == WORK_DIR_MODE:
        cwd = arguments[1] if len(arguments) > 1 else "."
        print(work_directory(sys.stdin.read(), cwd), end="")
        return 0
    cwd = arguments[0] if arguments else "."
    resolution = resolve(sys.stdin.read(), cwd)
    if resolution.message is None:
        print(resolution.reason, end="")
        return EXIT_UNRESOLVABLE
    print(resolution.message, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
