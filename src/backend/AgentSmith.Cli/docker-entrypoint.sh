#!/bin/bash
set -e

# Fix permissions on mounted volumes (host mounts are owned by root)
for dir in /output /tmp/agentsmith; do
    [ -d "$dir" ] && chown agentsmith:agentsmith "$dir" 2>/dev/null || true
done

exec gosu agentsmith dotnet AgentSmith.Cli.dll "$@"
