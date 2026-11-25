# Build context: repo root (matches docker-compose).
# Three-stage build → Next.js standalone runtime (~50MB).
# In-container tree mirrors the repo so dashboard's `../../tools/build-tokens.mjs`
# resolves identically to local dev.

# Stage 1: install
FROM node:20-alpine AS deps
WORKDIR /repo/src/dashboard
RUN corepack enable
COPY src/dashboard/package.json src/dashboard/pnpm-lock.yaml ./
# p0435: no fallback. --frozen-lockfile declares that the image installs exactly what
# the lockfile says; `|| pnpm install` guaranteed that declaration could never be
# enforced, so a drifted lockfile shipped whatever resolved that day. The lockfile is
# also required rather than globbed — absent, the build stops instead of installing
# unpinned around it.
RUN --mount=type=cache,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile

# Stage 2: build
FROM node:20-alpine AS build
WORKDIR /repo/src/dashboard
RUN corepack enable
# 2026-11-25-7f25a: Next.js evaluates rewrites() at BUILD time and writes the
# /api and /hub destinations into .next/routes-manifest.json. This build bakes the
# local-dev default; docker-entrypoint.sh rewrites both destinations from the
# container's AGENTSMITH_BACKEND_URL on every start, so no build arg sets the target.
COPY --from=deps /repo/src/dashboard/node_modules ./node_modules
COPY DESIGN.md /repo/DESIGN.md
COPY tools/build-tokens.mjs /repo/tools/build-tokens.mjs
# 2026-08-25-8c97: which build this bundle was made from. Next.js inlines a
# NEXT_PUBLIC_* variable at BUILD time, so the browser keeps the revision of the bundle it
# downloaded even after the pod that served it is gone — which is exactly the half of the
# comparison a stale tab has to be able to state. It must be set before the bundle is
# compiled, or the inlining has nothing to inline.
ARG AGENTSMITH_BUILD_REVISION
ARG AGENTSMITH_RELEASE_VERSION
ENV NEXT_PUBLIC_BUILD_REVISION=$AGENTSMITH_BUILD_REVISION
ENV NEXT_PUBLIC_RELEASE_VERSION=$AGENTSMITH_RELEASE_VERSION
COPY src/dashboard/ ./
RUN OPENAPI_SOURCE=./openapi.stub.json pnpm build

# Stage 3: runtime
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
COPY --from=build /repo/src/dashboard/.next/standalone ./
COPY --from=build /repo/src/dashboard/.next/static ./.next/static
COPY --from=build /repo/src/dashboard/public ./public
EXPOSE 3000
# BusyBox wget on Alpine resolves `localhost` IPv6-first and doesn't fall
# back to IPv4 when [::1]:3000 refuses; force 127.0.0.1 so the loopback
# probe always hits the v4 socket Next.js binds.
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:3000/ >/dev/null || exit 1
# 2026-08-25-21ae: the entrypoint writes the runtime settings document into the
# static root this server serves, then execs the server as PID 1's child. The
# server command stays a CMD so the entrypoint is what runs first and the
# document exists before the first browser can ask for it.
COPY --chmod=0755 src/dashboard/docker-entrypoint.sh /app/docker-entrypoint.sh
ENTRYPOINT ["/app/docker-entrypoint.sh"]
CMD ["node", "server.js"]
