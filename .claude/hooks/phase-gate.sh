#!/usr/bin/env bash
# The gate's old path, kept as a forwarder (2026-10-12-967af): a session snapshots its hook
# path at start, and a missing hook exits 127, which does not block. Removed in 967ah.
exec "$(dirname "$0")/spec-gate.sh" "$@"
