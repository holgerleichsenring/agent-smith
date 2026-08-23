#!/usr/bin/env python3
"""p0505 — merge the two measurement passes and emit the pinned TSV.

Called by tools/measure-data-toolchain.sh; not a standalone entry point. The
fixture hash it writes must stay byte-identical to the one
DataToolchainFixtureHash recomputes in the offline test, so the two
implementations share one rule: skip the generated names, sort relative paths
ordinally, hash "<relpath>\\n<sha256-of-bytes>\\n" per file, sha256 the lot.
"""
import hashlib
import os
import sys
from datetime import datetime, timezone

SKIP_DIRS = {"target", "dbt_packages", "logs", ".git"}
SKIP_FILES = {".user.yml", "package-lock.yml"}

COLUMNS = ["shape", "variant", "command", "exit_code", "network", "verdict",
           "tool_version", "image", "first_line"]

CLEAN = "clean"
SYNTAX_DEFECT = "yaml-syntax"


def verdict_for(exits: dict) -> str:
    """Classify one (shape, command) pair from its exit codes across variants.

    Recomputed byte-for-byte by the offline test — the exit codes are the
    measurement, this is the only part of the table that can be re-derived
    without the toolchain, so it is the only part the test may police.
    """
    if exits.get(CLEAN, 1) != 0:
        return "broken-on-clean"
    reds = {v for v, code in exits.items() if v != CLEAN and code != 0}
    if reds - {SYNTAX_DEFECT}:
        return "declarable"
    if reds:
        return "linter"
    return "no-defect-detected"


def fixture_hash(root: str) -> str:
    entries = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS)
        for name in filenames:
            if name in SKIP_FILES:
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace(os.sep, "/")
            with open(full, "rb") as handle:
                entries.append((rel, hashlib.sha256(handle.read()).hexdigest()))
    buffer = "".join(f"{rel}\n{digest}\n" for rel, digest in sorted(entries))
    return hashlib.sha256(buffer.encode("utf-8")).hexdigest()


def read_rows(path: str) -> dict:
    rows = {}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.rstrip("\n")
            if not line:
                continue
            shape, variant, command, exit_code, tool_version, first_line = line.split("\t", 5)
            rows[(shape, variant, command)] = (exit_code, tool_version, first_line)
    return rows


def main() -> int:
    work = os.environ["WORK"]
    fixtures = os.environ["FIXTURES"]
    image = os.environ["IMAGE_TAG"]
    networked = read_rows(os.path.join(work, "rows-net.tsv"))
    isolated = read_rows(os.path.join(work, "rows-nonet.tsv"))

    lines = [
        f"# p0505 measured commands — recorded {datetime.now(timezone.utc):%Y-%m-%d}",
        f"# image: {image} (FROM {os.environ['BASE_IMAGE']})",
        f"# install: {os.environ['INSTALL_LINE']}",
        "# every command ran in a fresh copy of the variant under a temp directory,",
        "# with no workspace host, token or profile beyond the fixture's own profiles.yml",
        "# network: yes = the exit code differed when the same command ran with --network none",
    ]
    for shape in sorted(os.listdir(fixtures)):
        shape_dir = os.path.join(fixtures, shape)
        if os.path.isdir(shape_dir):
            lines.append(f"# fixture-hash\t{shape}\t{fixture_hash(shape_dir)}")
    lines.append("\t".join(COLUMNS))

    exits_by_pair = {}
    for (shape, variant, command), (exit_code, _, _) in networked.items():
        exits_by_pair.setdefault((shape, command), {})[variant] = int(exit_code)
    verdicts = {pair: verdict_for(exits) for pair, exits in exits_by_pair.items()}

    for key in sorted(networked):
        shape, variant, command = key
        exit_code, tool_version, first_line = networked[key]
        offline = isolated.get(key)
        network = "yes" if offline is None or offline[0] != exit_code else "no"
        lines.append("\t".join([shape, variant, command, exit_code, network,
                                verdicts[(shape, command)], tool_version, image,
                                first_line or "-"]))

    out = os.environ["OUT"]
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
