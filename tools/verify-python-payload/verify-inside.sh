#!/bin/bash
# p0357/p0358: runs INSIDE an unmodified toolchain image. Asserts the injected
# payload is complete and python3 actually executes — the same PATH mechanics
# the agent's ProcessRunner applies (prepend <agentdir>/python/bin).
set -euo pipefail
label="${1:-unknown-image}"

fail() { echo "[$label] FAIL: $*" >&2; exit 1; }

[ -x /shared/agent ] || fail "/shared/agent missing or not executable"
[ -d /shared/python/bin ] || fail "/shared/python/bin missing"

link_target="$(readlink /shared/python/bin/python3 || true)"
[ "$link_target" = "python3.12" ] \
  || fail "python3 symlink broken (got '$link_target', want 'python3.12')"

export PATH="/shared/python/bin:$PATH"

command -v python3 >/dev/null || fail "python3 not on PATH"

# Interpreter runs + stdlib imports (json/re/pathlib are what codemods lean on).
out="$(python3 -c 'import sys, json, re, pathlib; print("hello from", sys.version.split()[0])')" \
  || fail "python3 execution failed"
case "$out" in
  "hello from 3.12."*) ;;
  *) fail "unexpected python output: $out" ;;
esac

# A real mini-codemod: rewrite a file in place, the workload we ship python for.
tmp="$(mktemp -d)"
printf 'using MediatR;\n' > "$tmp/Program.cs"
python3 - "$tmp/Program.cs" <<'PY'
import pathlib, sys
p = pathlib.Path(sys.argv[1])
p.write_text(p.read_text().replace("MediatR", "Mediator"))
PY
grep -q "using Mediator;" "$tmp/Program.cs" || fail "codemod rewrite did not stick"

echo "[$label] OK: $out — payload complete, symlinks intact, codemod works"
