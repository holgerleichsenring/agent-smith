#!/usr/bin/env bash
# RETIRED (p0349): this script used to regenerate the k8s ConfigMap from a full
# config/agentsmith.yml. Since p0349 the ConfigMap carries ONLY the bootstrap
# slice (persistence + secrets) — the server ignores everything else in the
# mounted file, and copying a full config here would leak agents/trackers/
# projects into a ConfigMap where they are dead weight (and possibly sensitive).
#
# The script deliberately does nothing rather than extract the slice in bash:
# the bootstrap slice is a dozen hand-maintained lines in 3-configmap.yaml.
set -euo pipefail

cat >&2 <<'EOF'
regenerate-configmap.sh is retired (p0349: configuration lives in the DB).

What to do instead:
  * Bootstrap (persistence + secrets):
      edit deploy/k8s/3-configmap.yaml BY HAND — it holds only the
      `persistence:` and `secrets:` sections, the only parts the server
      reads from the mounted file.
  * Everything else (agents, trackers, repos, projects, triggers, settings):
      configure in the Config Studio (dashboard), or seed an empty DB once:
        agentsmith config import <your-agentsmith.yml>
      Export the DB back to YAML for backup/DR:
        agentsmith config export --output agentsmith.yml
EOF
exit 1
