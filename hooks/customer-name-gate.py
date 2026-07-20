#!/usr/bin/env python3
"""Customer-fingerprint gate for the opensource agent-smith repo.

Scans STAGED content (default) or a commit RANGE (CI) for customer/target
fingerprints and fails when one is found in ADDED content — removing a
fingerprint is always allowed. Mirrors the PreToolUse guard
(agent-smith-no-customer-names.py), which only sees tool writes and so cannot
catch a pre-existing file being `git add`-ed or a fingerprint pushed straight
to a branch. (.gitignore already blocks the local-config pattern
config/agentsmith.*.yml.)

Enable the pre-commit hook with:
    git config core.hooksPath hooks

The fingerprint list is assembled from FRAGMENTS so this guard file does not
itself contain the very names it blocks (the repo rule: no customer names in
any file). The joined strings only exist at runtime.
"""
import re
import subprocess
import sys

# Each entry is split so the literal fingerprint never appears in this source.
_FRAGMENTS = [
    "auth" + "port",
    "rhe" + "nus",
    "copy" + "rhe",
    "tree" + "validator",
    "client" + "api" + "generator",
    "database" + "migrator",
    "aad" + r"[-_]dev",
    r"\b" + "rh" + r"s\b",
    "rh" + r"s[.\-/_]",
]
PATTERN = re.compile("|".join(_FRAGMENTS), re.IGNORECASE)


def _added_lines(diff_args):
    diff = subprocess.run(
        ["git", "diff", "--no-color", "-U0", *diff_args],
        capture_output=True, text=True).stdout
    path = "?"
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            path = line[6:]
        elif line.startswith("+") and not line.startswith("+++"):
            yield path, line[1:]


def main(argv):
    diff_args = ["--cached"]
    if len(argv) >= 3 and argv[1] == "--range":
        diff_args = [f"{argv[2]}...{argv[3]}"] if len(argv) >= 4 else [argv[2]]

    hits = [(p, line, PATTERN.search(line).group(0))
            for p, line in _added_lines(diff_args) if PATTERN.search(line)]
    if not hits:
        return 0

    sys.stderr.write("BLOCKED: staged content contains a customer fingerprint:\n")
    for path, _line, token in hits:
        sys.stderr.write(f"  {path}: '{token}'\n")
    sys.stderr.write(
        "No customer names in the opensource repo — anonymize it "
        "(component-x / sample-app / reference deployment).\n")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
