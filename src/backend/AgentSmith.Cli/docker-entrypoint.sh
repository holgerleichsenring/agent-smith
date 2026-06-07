#!/bin/bash
set -e

# Fix permissions on mounted volumes (host mounts / fresh named volumes are
# owned by root). /var/lib/agentsmith holds the relational-persistence SQLite
# file (p0246) — `database migrate` and the server both write it as the
# unprivileged agentsmith user, so the mount point must be agentsmith-owned.
for dir in /output /tmp/agentsmith /var/lib/agentsmith; do
    [ -d "$dir" ] && chown agentsmith:agentsmith "$dir" 2>/dev/null || true
done

exec gosu agentsmith dotnet AgentSmith.Cli.dll "$@"
