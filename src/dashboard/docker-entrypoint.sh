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

# 2026-11-25-7f25a: where /api/* and /hub/* are proxied to. next.config.ts declares
# the two rewrites, but `next build` resolves them and writes the destinations into
# .next/routes-manifest.json, and the standalone server reads that file at start —
# never next.config.ts. So the target is set here, in the file the server reads,
# from the environment of THIS start. Every start writes destination = backend +
# source again, so a restart with another value moves the target; nothing depends
# on what a previous start wrote. WebSocket upgrades on /hub (SignalR) go through
# the same rewrite, so they follow too. The image runs as root and /app is
# writable; an image that drops to another user must keep this file writable.
routes_manifest="${AGENTSMITH_ROUTES_MANIFEST_FILE:-/app/.next/routes-manifest.json}"
backend_url="${AGENTSMITH_BACKEND_URL:-http://localhost:8081}"

node -e '
const fs = require("node:fs");
const [manifestPath, backendUrl] = process.argv.slice(1);
const PROXIED = ["/api/:path*", "/hub/:path*"];
const fail = (message) => { console.error(`docker-entrypoint: ${message}`); process.exit(1); };

let backend;
try { backend = new URL(backendUrl); } catch { backend = null; }
if (!backend || !["http:", "https:"].includes(backend.protocol) || backend.search || backend.hash)
  fail(`AGENTSMITH_BACKEND_URL must be an http(s) URL without query or fragment, got "${backendUrl}"`);
// Next reads a colon in a rewrite destination as a parameter, so an IPv6 literal
// or a path with a colon would start fine and fail every proxied request.
if (backend.hostname.includes(":") || backend.pathname.includes(":"))
  fail(`AGENTSMITH_BACKEND_URL cannot carry a colon in its host or path (use a host name), got "${backendUrl}"`);
const base = (backend.origin + backend.pathname).replace(/\/+$/, "");

if (!fs.existsSync(manifestPath)) fail(`no routes manifest at ${manifestPath}`);
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
const rewrites = Array.isArray(manifest.rewrites)
  ? manifest.rewrites
  : [manifest.rewrites?.beforeFiles, manifest.rewrites?.afterFiles, manifest.rewrites?.fallback]
      .flatMap((group) => group ?? []);

for (const source of PROXIED) {
  const rules = rewrites.filter((rule) => rule.source === source);
  if (rules.length === 0) fail(`routes manifest ${manifestPath} has no rewrite for ${source}`);
  for (const rule of rules) rule.destination = base + source;
}
// Written beside the file and renamed over it, so a start killed mid-write leaves
// the previous manifest rather than half a document every later start fails on.
fs.writeFileSync(`${manifestPath}.tmp`, JSON.stringify(manifest));
fs.renameSync(`${manifestPath}.tmp`, manifestPath);
' "$routes_manifest" "$backend_url"

exec "$@"
