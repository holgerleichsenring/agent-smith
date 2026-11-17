#!/usr/bin/env python3
"""Reads a Bash tool command the way the shell will, for the spec gate.

spec-gate.sh asks one question per Bash call: does this command run `git commit`,
and if so, in which git tree and with which message? Splitting the command in bash
would mean `eval`, and `eval` would run the commit — hence python.

2026-11-17-f270: the command is read by a small shell reader of its own instead of
shlex. shlex cannot read a heredoc — a body with one apostrophe made the whole
command unsplittable, and the gate then checked the session's cwd (the main
checkout) instead of the worktree the commit ran in. The reader knows quotes,
escapes, comments, `$( )` and backticks (kept as raw text), the operators
including newline and `( )`, heredoc bodies and here-strings. Walking the commands
up to the commit it follows every `cd`, `pushd` and `popd` (a subshell restores
the directory it started with), shell variables and exports, GIT_DIR and
GIT_WORK_TREE, git's own -C, --git-dir and --work-tree, and the files a heredoc
wrote. Which tree that is, git itself answers (`rev-parse --show-toplevel`).

Called as `commit-message.py --gate <cwd>` with the command on stdin. It prints
three header lines and then the message:

    kind      none | message | unreadable | unparsed
    tree      the git top level the commit runs in (empty when unknown)
    problem   why the tree is unknown (empty when known)
    ...       the message (kind message), or why it cannot be read yet

`none` is a command that runs no commit. `unreadable` is a commit whose message
does not exist yet or is produced at run time: a bare commit, an editor amend,
`-F -` fed by a pipe. `unparsed` is a command the reader could not read at all.
"""

import os
import re
import subprocess
import sys
from dataclasses import dataclass, field, replace

COMMIT = "commit"
GATE_MODE = "--gate"
PARAGRAPH_BREAK = "\n\n"
VALUE_SHORT_OPTIONS = "mFtCc"
OPTIONAL_VALUE_SHORT_OPTIONS = "Su"
LONG_OPTIONS = {
    "--message": "-m",
    "--file": "-F",
    "--template": "-t",
    "--reuse-message": "-C",
    "--reedit-message": "-c",
}
STDIN_FILES = ("-", "/dev/stdin")
SEPARATOR = ";"
ALTERNATIVE = "||"
PIPES = ("|", "|&")
SUBSHELL_OPEN, SUBSHELL_CLOSE = "(", ")"
# git's global options that take a value — as the next word, or after `=`.
GIT_VALUE_OPTIONS = ("-C", "-c", "--git-dir", "--work-tree", "--namespace",
                     "--super-prefix", "--config-env", "--list-cmds")
GIT_ENVIRONMENT = ("GIT_DIR", "GIT_WORK_TREE")
SHELLS = ("bash", "sh", "zsh", "dash")
PLAIN_WRAPPERS = ("command", "builtin", "exec", "nohup", "time")
# Words that open or close a compound command; the command proper follows them.
RESERVED_WORDS = ("{", "}", "!", "if", "then", "elif", "else", "while", "until", "do")
ASSIGNMENT = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)=(.*)$", re.S)
VARIABLE = re.compile(r"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))")
KNOWN_SUBSTITUTION = re.compile(r"\$\(\s*(pwd|git\s+rev-parse\s+--show-toplevel)\s*\)")
CAT_HEREDOC = re.compile(r"^\$\(\s*cat\s*<<-?\s*(['\"]?)(\w+)\1[ \t]*\n(.*?)\n\t*\2[ \t]*\n?\s*\)$", re.S)


class UnreadableCommand(ValueError):
    """The command is not shell the reader can follow (an unterminated quote, say)."""


# --- the reader: text -> tokens ------------------------------------------------


@dataclass
class Heredoc:
    """A heredoc or here-string body; a heredoc's is filled in when its line ends."""

    body: str = ""


@dataclass
class Redirection:
    operator: str
    descriptor: str
    target: str = ""
    heredoc: Heredoc | None = None


class _Lexer:
    """Turns shell text into ("word", text), ("op", text) and ("redirect", Redirection)
    tokens. Nested, it reads the inside of a `$( )` and stops at its closing paren."""

    def __init__(self, text: str, position: int = 0, nested: bool = False):
        self.text = text
        self.position = position
        self.nested = nested
        self.tokens: list[tuple[str, object]] = []
        self.word: list[str] | None = None
        self.pending: list[tuple[str, bool, Heredoc]] = []
        self.delimiter_for: tuple[bool, Heredoc] | None = None
        self.depth = 0

    def run(self) -> int:
        text = self.text
        while self.position < len(text):
            character = text[self.position]
            following = text[self.position + 1:self.position + 2]
            if character in " \t":
                self._end_word()
                self.position += 1
            elif character == "\\":
                if following != "\n":
                    self._add(following)
                self.position += 2
            elif character == "\n":
                self._end_word()
                self._operator(SEPARATOR)
                self.position += 1
                self._read_bodies()
            elif character == "#" and self.word is None:
                end = text.find("\n", self.position)
                self.position = len(text) if end < 0 else end
            elif character == "'":
                end = text.find("'", self.position + 1)
                if end < 0:
                    raise UnreadableCommand("unterminated single quote")
                self._add(text[self.position + 1:end])
                self.position = end + 1
            elif character == '"':
                self._double_quoted()
            elif character == "$" and following == "(":
                self._add(self._substitution())
            elif character == "$" and following == "'":
                self._add(self._ansi_c_quoted())
            elif character == "$" and following == "{":
                end = text.find("}", self.position)
                if end < 0:
                    raise UnreadableCommand("unterminated ${")
                self._add(text[self.position:end + 1])
                self.position = end + 1
            elif character == "`":
                self._add(self._backticks())
            elif character == ")" and self.nested and self.depth == 0:
                self._end_word()
                return self.position
            elif character in "();&|<>":
                self._punctuation(character)
            else:
                self._add(character)
                self.position += 1
        self._end_word()
        if self.nested:
            raise UnreadableCommand("unterminated $(")
        self._read_bodies()
        return self.position

    def _add(self, text: str) -> None:
        if self.word is None:
            self.word = []
        self.word.append(text)

    def _end_word(self) -> None:
        if self.word is None:
            return
        word = "".join(self.word)
        self.word = None
        self.tokens.append(("word", word))
        if self.delimiter_for is not None:
            strip_tabs, heredoc = self.delimiter_for
            self.pending.append((word, strip_tabs, heredoc))
            self.delimiter_for = None

    def _operator(self, operator: str) -> None:
        self.tokens.append(("op", operator))

    def _punctuation(self, character: str) -> None:
        text, start = self.text, self.position
        if character in "<>" or text.startswith("&>", start):
            self._redirection()
            return
        self._end_word()
        for operator in ("&&", "||", "|&", ";;", ";", "&", "|", "(", ")"):
            if text.startswith(operator, start):
                break
        self.position += len(operator)
        if operator == SUBSHELL_OPEN:
            self.depth += 1
        elif operator == SUBSHELL_CLOSE:
            self.depth -= 1
        kept = PIPES + (SUBSHELL_OPEN, SUBSHELL_CLOSE, ALTERNATIVE)
        self._operator(operator if operator in kept else SEPARATOR)

    def _redirection(self) -> None:
        descriptor = ""
        if self.word is not None and "".join(self.word).isdigit():
            descriptor = "".join(self.word)
            self.word = None
        else:
            self._end_word()
        text, start = self.text, self.position
        for operator in ("&>>", "&>", "<<<", "<<-", "<<", "<>", "<&", "<", ">>", ">|", ">&", ">"):
            if text.startswith(operator, start):
                break
        self.position += len(operator)
        redirection = Redirection(operator, descriptor)
        if operator in ("<<", "<<-"):
            redirection.heredoc = Heredoc()
            self.delimiter_for = (operator == "<<-", redirection.heredoc)
        elif operator == "<<<":
            redirection.heredoc = Heredoc()
        self.tokens.append(("redirect", redirection))

    def _read_bodies(self) -> None:
        """Heredoc bodies start on the line after their operator and end at the line that
        is exactly the delimiter (tabs stripped for `<<-`)."""
        text = self.text
        for delimiter, strip_tabs, heredoc in self.pending:
            lines: list[str] = []
            while self.position < len(text):
                end = text.find("\n", self.position)
                end = len(text) if end < 0 else end
                line = text[self.position:end]
                self.position = min(end + 1, len(text))
                if (line.lstrip("\t") if strip_tabs else line) == delimiter:
                    break
                lines.append((line.lstrip("\t") if strip_tabs else line) + "\n")
            heredoc.body = "".join(lines)
        self.pending = []

    def _double_quoted(self) -> None:
        text = self.text
        self.position += 1
        parts: list[str] = []
        while self.position < len(text):
            character = text[self.position]
            following = text[self.position + 1:self.position + 2]
            if character == '"':
                self.position += 1
                self._add("".join(parts))
                return
            if character == "\\" and following in '$`"\\\n':
                if following != "\n":
                    parts.append(following)
                self.position += 2
            elif character == "$" and following == "(":
                parts.append(self._substitution())
            elif character == "`":
                parts.append(self._backticks())
            else:
                parts.append(character)
                self.position += 1
        raise UnreadableCommand("unterminated double quote")

    def _ansi_c_quoted(self) -> str:
        """`$'…'`: backslash escapes are decoded and `\\'` does not end the quote."""
        text, index = self.text, self.position + 2
        parts: list[str] = []
        escapes = {"n": "\n", "t": "\t", "\\": "\\", "'": "'", '"': '"', "r": "\r", "e": "\x1b"}
        while index < len(text) and text[index] != "'":
            if text[index] == "\\" and index + 1 < len(text):
                parts.append(escapes.get(text[index + 1], "\\" + text[index + 1]))
                index += 2
            else:
                parts.append(text[index])
                index += 1
        if index >= len(text):
            raise UnreadableCommand("unterminated $'")
        self.position = index + 1
        return "".join(parts)

    def _substitution(self) -> str:
        """The raw text of a `$( )`, read by a nested reader so a heredoc, a quote or a
        paren inside it cannot end it early."""
        start = self.position
        end = _Lexer(self.text, start + 2, nested=True).run()
        self.position = end + 1
        return self.text[start:end + 1]

    def _backticks(self) -> str:
        start, text = self.position, self.text
        index = start + 1
        while index < len(text) and text[index] != "`":
            index += 2 if text[index] == "\\" else 1
        if index >= len(text):
            raise UnreadableCommand("unterminated backtick")
        self.position = index + 1
        return text[start:index + 1]


# --- the parser: tokens -> simple commands -------------------------------------


@dataclass
class Command:
    """One simple command: its leading assignments, its words and its redirections."""

    assignments: list[tuple[str, str]] = field(default_factory=list)
    words: list[str] = field(default_factory=list)
    redirections: list[Redirection] = field(default_factory=list)


def read_commands(text: str) -> list[object]:
    """The commands of `text` in order, with "(", ")", "|" and ";" between them."""
    lexer = _Lexer(text)
    lexer.run()
    items: list[object] = []
    current: Command | None = None
    tokens = lexer.tokens
    index = 0
    while index < len(tokens):
        kind, value = tokens[index]
        if kind == "op":
            if current is not None:
                items.append(current)
                current = None
            items.append(value)
        elif kind == "redirect":
            current = current or Command()
            if index + 1 < len(tokens) and tokens[index + 1][0] == "word":
                value.target = tokens[index + 1][1]
                index += 1
            if value.operator == "<<<":
                value.heredoc.body = value.target + "\n"
            current.redirections.append(value)
        else:
            current = current or Command()
            assignment = ASSIGNMENT.match(value)
            if assignment and not current.words:
                current.assignments.append((assignment.group(1), assignment.group(2)))
            else:
                current.words.append(value)
        index += 1
    if current is not None:
        items.append(current)
    return items


# --- the walk: commands -> where and with what the commit runs -----------------


@dataclass
class Shell:
    """What the shell knows when it reaches a command."""

    directory: str | None
    problem: str | None = None
    # A variable whose value only the shell computes (`WT=$(pwd)`) is held as None.
    variables: dict[str, str | None] = field(default_factory=dict)
    exported: dict[str, str | None] = field(default_factory=dict)
    pushed: list[tuple[str | None, str | None]] = field(default_factory=list)
    subshells: list["Shell"] = field(default_factory=list)
    written: dict[str, str | None] = field(default_factory=dict)

    def copy(self) -> "Shell":
        return replace(self, variables=dict(self.variables), exported=dict(self.exported),
                       pushed=list(self.pushed), subshells=[], written=dict(self.written))

    def leave_subshell(self) -> None:
        """A subshell's cd, variables and directory stack end with it; its files do not."""
        if self.subshells:
            saved = self.subshells.pop()
            self.directory, self.problem = saved.directory, saved.problem
            self.variables, self.exported, self.pushed = saved.variables, saved.exported, saved.pushed

    def move(self, directory: str | None, problem: str | None) -> None:
        self.variables["OLDPWD"] = self.directory
        self.directory = directory
        self.problem = None if directory is not None else problem

    def expand(self, word: str) -> str | None:
        """`word` with `~`, `$NAME`, `$(pwd)` and `$(git rev-parse --show-toplevel)` expanded,
        or None when it still holds anything the shell computes at run time. PWD is the
        walk's directory, never the hook's own: the hook runs in the main checkout."""
        def value(match: re.Match) -> str:
            name = match.group(1) or match.group(2)
            if name == "PWD":
                found = self.directory
            else:
                found = self.variables[name] if name in self.variables else os.environ.get(name)
            return "\0" if found is None else found
        def substitution(match: re.Match) -> str:
            known = self.directory if match.group(1) == "pwd" else _top_level(self.directory)
            return "\0" if known is None else known
        expanded = KNOWN_SUBSTITUTION.sub(substitution, word)
        expanded = VARIABLE.sub(value, os.path.expanduser(expanded))
        return None if "\0" in expanded or "$" in expanded or "`" in expanded else expanded

    def path(self, word: str, base: str | None = None) -> str | None:
        base = self.directory if base is None else base
        expanded = self.expand(word)
        if expanded is None or base is None:
            return None
        return os.path.normpath(os.path.join(base, expanded))


@dataclass
class CommitCall:
    """A `git commit` found in the command, with everything the walk knew about it."""

    shell: Shell
    work_directory: str | None
    problem: str | None
    git_dir: str | None
    work_tree: str | None
    arguments: list[str]
    stdin: str | None
    stdin_reason: str | None


@dataclass(frozen=True)
class Resolution:
    """Whether the command commits; if so, the tree and the message (or why not)."""

    is_commit: bool = True
    message: str | None = None
    reason: str | None = None
    tree: str | None = None
    problem: str | None = None


def resolve(command: str, cwd: str) -> Resolution:
    """Resolve `command`, run from `cwd`: does it commit, where, and with which message."""
    call = _find_commit(read_commands(command), Shell(directory=os.path.abspath(cwd)))
    if call is None:
        return Resolution(is_commit=False)
    tree, problem = _tree(call)
    message, reason = _message(call)
    return Resolution(message=message, reason=reason, tree=tree, problem=problem)


def work_directory(command: str, cwd: str) -> str | None:
    """The directory git commits from: `cwd` moved by every `cd` before the commit and
    by git's own -C. None when the command commits nowhere the gate can tell."""
    call = _find_commit(read_commands(command), Shell(directory=os.path.abspath(cwd)))
    return None if call is None else call.work_directory


@dataclass
class Pipe:
    """What flows into a command: nothing, a pipe whose text the gate knows, or a pipe
    whose text exists only at run time (text None)."""

    piped: bool = False
    text: str | None = None


def _find_commit(items: list[object], shell: Shell) -> CommitCall | None:
    pipe, previous, skip_next = Pipe(), None, False
    for item in items:
        if item == SUBSHELL_OPEN:
            shell.subshells.append(shell.copy())
        elif item == SUBSHELL_CLOSE:
            shell.leave_subshell()
        elif item == ALTERNATIVE:
            # `cd <wt> || cd <elsewhere>`: the alternative runs only when the cd failed, and
            # a cd into a directory that exists does not fail.
            skip_next = previous is not None and _is_cd(previous) and shell.directory is not None \
                and os.path.isdir(shell.directory)
        elif isinstance(item, Command):
            if skip_next:
                skip_next = False
                continue
            call = _step(item, shell, pipe)
            if call is not None:
                return call
            previous = item
        pipe = Pipe(True, _output(previous, shell)) if item in PIPES else Pipe()
    return None


def _is_cd(command: Command) -> bool:
    return bool(command.words) and command.words[0] in ("cd", "pushd")


def _output(command: Command | None, shell: Shell) -> str | None:
    """The text a command writes, when the command text alone decides it."""
    if command is None or command.assignments or not command.words:
        return None
    words = command.words
    heredoc = _stdin_heredoc(command)
    if words == ["cat"] and heredoc is not None:
        return heredoc.body
    return _literal_output(words, shell)


def _literal_output(words: list[str], shell: Shell) -> str | None:
    """`echo <text>` and `printf <text>` without a format directive."""
    expanded = [shell.expand(word) for word in words[1:]]
    if any(word is None for word in expanded):
        return None
    if words[0] == "echo":
        newline = "\n"
        if expanded and expanded[0] == "-n":
            expanded, newline = expanded[1:], ""
        if expanded and expanded[0].startswith("-"):
            return None
        return " ".join(expanded) + newline  # type: ignore[arg-type]
    if words[0] == "printf" and len(expanded) == 1 and "%" not in expanded[0]:  # type: ignore[operator]
        return expanded[0].replace("\\n", "\n").replace("\\t", "\t").replace("\\\\", "\\")  # type: ignore[union-attr]
    return None


def _step(command: Command, shell: Shell, pipe: Pipe) -> CommitCall | None:
    """Apply one simple command to `shell`; return the commit when it is one."""
    words = list(command.words)
    environment = {name: shell.expand(value) for name, value in command.assignments}
    if not words:
        shell.variables.update(environment)
        return None
    words = _unwrap(words, environment, shell)
    if not words:
        return None
    _remember_writes(command, words, shell)
    program = words[0] if words[0] in ("git",) else os.path.basename(words[0]) if words[0].endswith("/git") else words[0]
    if program in ("cd", "pushd"):
        _change_directory(words, shell)
    elif program == "popd":
        if shell.pushed:
            shell.directory, shell.problem = shell.pushed.pop()
        else:
            shell.move(None, "popd returns to a directory the gate does not track")
    elif program == "export":
        for word in words[1:]:
            assignment = ASSIGNMENT.match(word)
            if assignment:
                value = shell.expand(assignment.group(2))
                shell.variables[assignment.group(1)] = value
                shell.exported[assignment.group(1)] = value
    elif program == "unset":
        for name in words[1:]:
            shell.variables.pop(name, None)
            shell.exported.pop(name, None)
    elif os.path.basename(program) in SHELLS:
        return _shell_script(words, shell)
    elif program == "eval":
        return _script(" ".join(words[1:]), shell)
    elif program == "git":
        return _git(command, words, environment, shell, pipe)
    return None


def _unwrap(words: list[str], environment: dict[str, str | None], shell: Shell) -> list[str]:
    """Strip what runs the rest as a command: the reserved words of a compound command and
    the wrappers env, command, exec, nohup, time, timeout. env's NAME=value arguments join
    the command's environment."""
    while words:
        program = words[0]
        if program == "env":
            words = words[1:]
            while words and (words[0].startswith("-") or ASSIGNMENT.match(words[0])):
                assignment = ASSIGNMENT.match(words[0])
                if assignment:
                    environment[assignment.group(1)] = shell.expand(assignment.group(2))
                elif words[0] in ("-u", "--unset", "-C", "--chdir", "-S", "--split-string"):
                    words = words[1:]
                words = words[1:]
        elif program in RESERVED_WORDS:
            words = words[1:]
        elif program in PLAIN_WRAPPERS:
            words = words[1:]
            while words and words[0].startswith("-"):
                words = words[1:]
        elif program in ("timeout", "nice"):
            words = words[1:]
            while words and words[0].startswith("-"):
                words = words[2:] if words[0] in ("-n", "-s", "-k", "--signal", "--kill-after") else words[1:]
            if program == "timeout":
                words = words[1:]
        else:
            return words
    return words


def _change_directory(words: list[str], shell: Shell) -> None:
    arguments = [word for word in words[1:] if word not in ("-P", "-L", "-e", "--")]
    if words[0] == "pushd":
        shell.pushed.append((shell.directory, shell.problem))
    if not arguments:
        target = os.path.expanduser("~")
    elif arguments[0] == "-":
        shell.move(None, "`cd -` returns to a directory the gate does not track")
        return
    else:
        target = arguments[0]
    if shell.directory is None:
        return  # already unknown; the first unknown step names the problem
    shell.move(shell.path(target), f"`{words[0]} {target}` names a directory only the shell knows")


def _remember_writes(command: Command, words: list[str], shell: Shell) -> None:
    """A file this command writes before the commit reads it. Its content is known when a
    heredoc or here-string feeds `cat` or `tee`, or `echo`/`printf` print literal text;
    otherwise it is known only to be stale."""
    heredoc = _stdin_heredoc(command)
    targets = [r.target for r in command.redirections
               if r.operator in (">", ">>", ">|", "&>", "&>>") and r.descriptor in ("", "1")]
    if words[0] == "tee":
        targets += [word for word in words[1:] if not word.startswith("-")]
    if heredoc is not None and words[0] in ("cat", "tee"):
        content = heredoc.body
    else:
        content = _literal_output(words, shell) if words[0] in ("echo", "printf") else None
    for target in targets:
        path = shell.path(target)
        if path is not None:
            shell.written[path] = content


def _stdin_heredoc(command: Command) -> Heredoc | None:
    for redirection in reversed(command.redirections):
        if redirection.heredoc is not None:
            return redirection.heredoc
    return None


def _shell_script(words: list[str], shell: Shell) -> CommitCall | None:
    """`bash -c '<script>'` runs its script from the current directory, in a child."""
    for index, word in enumerate(words[1:-1], start=1):
        if word.startswith("-") and not word.startswith("--") and "c" in word:
            return _script(words[index + 1], shell)
    return None


def _script(text: str, shell: Shell) -> CommitCall | None:
    """A script inside the command (`bash -c`, `eval`), walked from where the shell is."""
    return _find_commit(read_commands(text), shell.copy())


def _git(command: Command, words: list[str], environment: dict[str, str | None], shell: Shell,
         pipe: Pipe) -> CommitCall | None:
    work, problem = shell.directory, shell.problem
    locations: dict[str, str | None] = {}
    for name in GIT_ENVIRONMENT:
        source = environment if name in environment else shell.exported
        if name in source:
            locations[name] = source[name]
            if source[name] is None:
                problem = problem or f"{name} holds a value only the shell computes"
    index = 1
    while index < len(words):
        word = words[index]
        name, equals, attached = word.partition("=")
        if word in GIT_VALUE_OPTIONS:
            value = words[index + 1] if index + 1 < len(words) else ""
            index += 2
        elif equals and name in GIT_VALUE_OPTIONS:
            word, value = name, attached
            index += 1
        elif word.startswith("-"):
            index += 1
            continue
        else:
            break
        if word == "-C" and work is not None:
            moved = shell.path(value, work)
            problem = problem if moved is not None else f"`git -C {value}` names a directory only the shell knows"
            work = moved
        elif word == "--git-dir":
            locations["GIT_DIR"] = shell.expand(value)
            problem = problem if locations["GIT_DIR"] is not None else f"--git-dir {value} is known only to the shell"
        elif word == "--work-tree":
            locations["GIT_WORK_TREE"] = shell.expand(value)
            problem = problem if locations["GIT_WORK_TREE"] is not None else f"--work-tree {value} is known only to the shell"
    if index >= len(words) or words[index] != COMMIT:
        return None
    stdin, stdin_reason = _stdin(command, shell, pipe)
    git_dir, work_tree = (shell.path(locations[name], work) if locations.get(name) and work else None
                          for name in GIT_ENVIRONMENT)
    return CommitCall(shell=shell, work_directory=work, problem=problem,
                      git_dir=git_dir, work_tree=work_tree, arguments=words[index + 1:],
                      stdin=stdin, stdin_reason=stdin_reason)


def _stdin(command: Command, shell: Shell, pipe: Pipe) -> tuple[str | None, str | None]:
    """What the commit reads on stdin, or why the gate cannot know."""
    for redirection in reversed(command.redirections):
        if redirection.heredoc is not None:
            return redirection.heredoc.body, None
        if redirection.operator == "<" and redirection.descriptor in ("", "0"):
            path = shell.path(redirection.target)
            if path is None:
                return None, f"stdin is {redirection.target}, a path only the shell knows"
            return _read_known(path, shell, "<")
    if pipe.piped:
        if pipe.text is not None:
            return pipe.text, None
        return None, "the message is piped in (-F -), its text is produced at run time"
    return None, "the message is read from stdin (-F -) and nothing feeds it"


# --- the answers ---------------------------------------------------------------


def _top_level(directory: str | None) -> str | None:
    if directory is None:
        return None
    environment = {key: value for key, value in os.environ.items() if key not in GIT_ENVIRONMENT}
    try:
        completed = subprocess.run(["git", "-C", directory, "rev-parse", "--show-toplevel"],
                                   capture_output=True, text=True, env=environment)
    except OSError:
        return None
    return completed.stdout.strip() or None if completed.returncode == 0 else None


def _tree(call: CommitCall) -> tuple[str | None, str | None]:
    """The top level git will commit in — asked of git, with the walk's -C, --git-dir and
    --work-tree applied."""
    if call.problem is not None or call.work_directory is None:
        return None, call.problem or "the directory the commit runs in is unknown"
    arguments = ["git", "-C", call.work_directory]
    if call.git_dir:
        arguments.append(f"--git-dir={call.git_dir}")
    if call.work_tree:
        arguments.append(f"--work-tree={call.work_tree}")
    environment = {key: value for key, value in os.environ.items() if key not in GIT_ENVIRONMENT}
    try:
        completed = subprocess.run(arguments + ["rev-parse", "--show-toplevel"],
                                   capture_output=True, text=True, env=environment)
    except OSError as error:
        return None, f"git could not be run: {error}"
    if completed.returncode != 0 or not completed.stdout.strip():
        detail = completed.stderr.strip().splitlines()
        return None, f"{call.work_directory} is not in a git work tree ({detail[0] if detail else 'no answer'})"
    return completed.stdout.strip(), None


@dataclass
class MessageSources:
    """The message-bearing options of one `git commit` invocation."""

    messages: list[str] = field(default_factory=list)
    file: str | None = None
    template: str | None = None
    revision: str | None = None
    is_amend: bool = False
    is_no_edit: bool = False


def _message(call: CommitCall) -> tuple[str | None, str | None]:
    sources = _scan(call.arguments)
    work = call.work_directory
    if sources.messages:
        return PARAGRAPH_BREAK.join(_unwrap_cat_heredoc(m) for m in sources.messages), None
    if sources.file in STDIN_FILES:
        return (call.stdin, None) if call.stdin is not None else (None, call.stdin_reason)
    if sources.file is not None:
        return _from_file(sources.file, call, "-F")
    if sources.revision is not None:
        return _from_revision(sources.revision, work)
    if sources.template is not None:
        return _from_file(sources.template, call, "-t")
    if sources.is_amend:
        if sources.is_no_edit:
            return _from_revision("HEAD", work)
        return None, "--amend without --no-edit opens an editor"
    return None, "no message on the command line — git will open an editor"


def _unwrap_cat_heredoc(message: str) -> str:
    """`-m "$(cat <<'EOF' … EOF)"` is the heredoc's body, which the reader already has."""
    match = CAT_HEREDOC.match(message)
    return match.group(3) + "\n" if match else message


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
    if not argument.startswith("-") or argument == "-":
        return None, None
    for index, character in enumerate(argument[1:]):
        if character in VALUE_SHORT_OPTIONS:
            return f"-{character}", argument[index + 2:] or None
        if character in OPTIONAL_VALUE_SHORT_OPTIONS:
            return None, None  # the rest of the word is its value, -S<keyid>, -u<mode>
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


def _from_file(path: str, call: CommitCall, option: str) -> tuple[str | None, str | None]:
    full_path = call.shell.path(path, call.work_directory)
    if full_path is None:
        return None, f"the {option} message file {path} is a path only the shell knows"
    return _read_known(full_path, call.shell, option)


def _read_known(path: str, shell: Shell, option: str) -> tuple[str | None, str | None]:
    """A file the same command wrote is read from what it wrote, never from disk: on disk
    it does not exist yet, or holds a previous attempt's text."""
    if path in shell.written:
        body = shell.written[path]
        if body is None:
            return None, f"the {option} message file {path} is written by the same command at run time"
        return body, None
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            return handle.read(), None
    except OSError as error:
        return None, f"cannot read the {option} message file {path}: {error}"


def _from_revision(revision: str, work_dir: str | None) -> tuple[str | None, str | None]:
    if work_dir is None:
        return None, f"cannot read the message of {revision}: the directory is unknown"
    completed = subprocess.run(["git", "log", "-1", "--pretty=%B", revision],
                               cwd=work_dir, capture_output=True, text=True)
    if completed.returncode != 0:
        return None, f"cannot read the message of {revision}: {completed.stderr.strip()}"
    return completed.stdout, None


def _one_line(text: str | None) -> str:
    return " ".join((text or "").split())


def main() -> int:
    arguments = sys.argv[1:]
    if not arguments or arguments[0] != GATE_MODE:
        print(f"usage: {os.path.basename(sys.argv[0])} {GATE_MODE} <cwd> < command", file=sys.stderr)
        return 2
    cwd = arguments[1] if len(arguments) > 1 else "."
    command = sys.stdin.read()
    try:
        resolution = resolve(command, cwd)
    except Exception as error:  # the gate must get an answer, never a traceback
        print(f"unparsed\n\n\n{_one_line(f'{type(error).__name__}: {error}')}", end="")
        return 0
    if not resolution.is_commit:
        print("none\n\n\n", end="")
        return 0
    kind = "message" if resolution.message is not None else "unreadable"
    body = resolution.message if resolution.message is not None else resolution.reason
    print(f"{kind}\n{_one_line(resolution.tree)}\n{_one_line(resolution.problem)}\n{body or ''}", end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
