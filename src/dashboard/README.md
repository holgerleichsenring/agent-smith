# agent-smith dashboard

Next.js 15 + TypeScript + Tailwind v4 dashboard for agent-smith.
Lives alongside `src/backend/` in the monorepo (see [[p0162]]).

## Quick start

```
pnpm install
pnpm dev               # http://localhost:3000
```

## Scripts

- `pnpm dev` — Next dev server
- `pnpm build` — generates tokens + types, then `next build`
- `pnpm test` — Vitest unit tests
- `pnpm lint` — ESLint
- `pnpm gen:types` — regenerate `src/types/api.ts` from `OPENAPI_SOURCE`
  (URL or path; falls back to `./openapi.stub.json`)
- `pnpm gen:tokens` — regenerate `src/styles/tokens.json` from `DESIGN.md`

## Design tokens

The dashboard reads `DESIGN.md` via `tools/build-tokens.mjs`. The Tailwind
config and `globals.css` pull from the generated `src/styles/tokens.json`.

## Docker

```
docker build -f src/dashboard/Dockerfile -t agentsmith-dashboard .
docker compose -f deploy/docker-compose.yml --profile dashboard up
```
