#!/usr/bin/env python3
"""Customer-fingerprint gate for the opensource agent-smith repo.

Scans STAGED content (default) or a commit RANGE (CI) for customer/target
fingerprints and fails when one appears in ADDED content — removing a
fingerprint is always allowed. Complements the PreToolUse guard
(agent-smith-no-customer-names.py), which only sees tool writes and so cannot
catch a pre-existing file being `git add`-ed or a fingerprint pushed straight
to a branch. (.gitignore already blocks the local-config pattern
config/agentsmith.*.yml.)

The fingerprint list is DELIBERATELY not in this repo — a committed list of
customer names would itself violate the "no customer names in any file" rule
(splitting them into fragments only hides the literal, not the name). The
patterns come from the OPERATOR's environment instead, in priority:

  1. env AGENTSMITH_CUSTOMER_FINGERPRINTS  — regexes, one per line or comma-separated
  2. env AGENTSMITH_CUSTOMER_FINGERPRINTS_FILE — path to a patterns file
  3. hooks/customer-fingerprints.txt        — gitignored local file (one regex per
                                              line; blank lines and #-comments ignored)

With no list configured the gate is INERT (exit 0 with a hint) — a fresh clone
never fails spuriously; the operator who knows their customer names owns the
list, locally and as a CI secret.

Enable the pre-commit hook with:  git config core.hooksPath hooks
CI passes the list via the AGENTSMITH_CUSTOMER_FINGERPRINTS env from a secret.
"""
import os
import re
import subprocess
import sys
from pathlib import Path

_LOCAL_LIST = Path(__file__).with_name("customer-fingerprints.txt")


def _load_patterns():
    inline = os.environ.get("AGENTSMITH_CUSTOMER_FINGERPRINTS", "")
    if inline.strip():
        raw = inline.replace(",", "\n")
    else:
        path = os.environ.get("AGENTSMITH_CUSTOMER_FINGERPRINTS_FILE")
        src = Path(path) if path else _LOCAL_LIST
        raw = src.read_text() if src.exists() else ""
    return [ln.strip() for ln in raw.splitlines()
            if ln.strip() and not ln.strip().startswith("#")]


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
    patterns = _load_patterns()
    if not patterns:
        sys.stderr.write(
            "customer-name-gate: no fingerprint list configured "
            "(set AGENTSMITH_CUSTOMER_FINGERPRINTS or hooks/customer-fingerprints.txt) "
            "- gate inert.\n")
        return 0
    pattern = re.compile("|".join(patterns), re.IGNORECASE)

    diff_args = ["--cached"]
    if len(argv) >= 3 and argv[1] == "--range":
        diff_args = [f"{argv[2]}...{argv[3]}"] if len(argv) >= 4 else [argv[2]]

    hits = [(p, pattern.search(line).group(0))
            for p, line in _added_lines(diff_args) if pattern.search(line)]
    if not hits:
        return 0

    sys.stderr.write("BLOCKED: staged content contains a customer fingerprint:\n")
    for path, token in hits:
        sys.stderr.write(f"  {path}: '{token}'\n")
    sys.stderr.write(
        "No customer names in the opensource repo - anonymize it "
        "(component-x / sample-app / reference deployment).\n")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
