#!/bin/sh
set -eu

# 2026-08-25-21ae: Next.js inlines a NEXT_PUBLIC_* variable at BUILD time — the
# build stage of this image's Dockerfile says so about the build revision, where
# it is correct. It is wrong for everything an operator sets per installation: an
# authority that needs a rebuild to change is not a setting. This writes what the
# environment says into the served static root before the server starts, so one
# image serves every installation and changing a setting is a restart.
#
# NOTHING SECRET GOES IN THIS DOCUMENT. It is served to any browser that asks, so
# it is public by construction. An OAuth public client's identifier and its
# authority are public values and belong here; a client secret, a token or a
# connection string never do — and there is no code path below that could tell
# the difference.
#
# AGENTSMITH_AUTH_AUTHORITY and AGENTSMITH_AUTH_AUDIENCE are deliberately the
# names the server already reads, so a compose file sets one value both services
# see and the two cannot drift apart.

# A variable rather than a constant, so the document's contract can be asserted
# without building an image. The default is the static root this image serves.
settings_file="${AGENTSMITH_RUNTIME_SETTINGS_FILE:-/app/public/runtime-settings.json}"

# What an operator typed is string CONTENT, not JSON: an unescaped quote or
# backslash ends the string early and the browser reads nothing at all.
json_string() {
    printf '%s' "${1:-}" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

mkdir -p "$(dirname "$settings_file")"

cat > "$settings_file" <<EOF
{
  "auth": {
    "authority": "$(json_string "${AGENTSMITH_AUTH_AUTHORITY:-}")",
    "clientId": "$(json_string "${AGENTSMITH_AUTH_CLIENT_ID:-}")",
    "audience": "$(json_string "${AGENTSMITH_AUTH_AUDIENCE:-}")",
    "scopes": "$(json_string "${AGENTSMITH_AUTH_SCOPES:-}")",
    "redirectPath": "$(json_string "${AGENTSMITH_AUTH_REDIRECT_PATH:-/signin-callback}")"
  }
}
EOF

exec "$@"
