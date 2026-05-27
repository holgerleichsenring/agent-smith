# Dashboard

The agent-smith dashboard is a Next.js 15 web UI that ships alongside the
backend in the same monorepo. It is read-only — every two-way interaction
(approvals, comments, retries) continues to live in chat or pull-request
review.

## What ships in the bootstrap (p0169-pre)

A single landing page proving the toolchain works end-to-end:

- Next.js 15 + TypeScript strict + Tailwind v4 + shadcn-compatible setup
- OpenAPI-driven TypeScript types (regenerated at build time)
- Multi-stage Docker image (~50 MB runtime)
- Compose service `dashboard` behind the `dashboard` profile

Real Job-Viewer UI arrives in p0169a.

## Run it locally

```
cd src/dashboard
pnpm install
pnpm dev
```

Open http://localhost:3000 — you should see a placeholder landing page
styled with the Smith-green primary colour and Inter typography. Both come
from `DESIGN.md` via `tools/build-tokens.mjs`.

## Run it via docker compose

The dashboard service is gated behind the `dashboard` profile so existing
operators are not surprised by a new port:

```
docker compose -f deploy/docker-compose.yml --profile dashboard up
```

The dashboard reaches the backend over compose-internal DNS at
`http://server:8081` (`AGENT_SMITH_API_URL`). Host port defaults to 3000;
override with `DASHBOARD_PORT=…`.

## Disable it

Either:

1. Omit the `--profile dashboard` flag (default behaviour — the service
   stays inactive).
2. Or remove the `dashboard:` block from your local `docker-compose.yml`.

## Authentication

Out of scope for the dashboard itself. Operators stand up an auth proxy
(nginx + oauth2-proxy, Cloudflare Access, identity-aware proxy, …) in front
of port 3000. The dashboard expects same-origin or reverse-proxied API
access.

## Where to point a reverse proxy

```
proxy_pass http://dashboard:3000/;
```

The backend (`/api/*`) can sit on the same origin or on a sibling subdomain;
when on a different origin, set `AGENTSMITH_DASHBOARD_ORIGIN` on the backend
so CORS allows the dashboard's URL (introduced in p0169a).

## Tokens

The dashboard reads `DESIGN.md` via `tools/build-tokens.mjs --target
tailwind`, which emits `src/dashboard/src/styles/tokens.json`. `tailwind.config.ts`
imports that file at build time. Single source of truth, same as docs and
the marketing site.

## Type generation

```
OPENAPI_SOURCE=http://localhost:8081/api/openapi.json pnpm gen:types
```

Defaults to `./openapi.stub.json` (a small placeholder so the bootstrap
build is hermetic). Once p0169a's backend ships, point `OPENAPI_SOURCE` at
the live `/api/openapi.json` endpoint.
