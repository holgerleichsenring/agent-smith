# Build context: repo root (matches docker-compose).
# Three-stage build → Next.js standalone runtime (~50MB).
# In-container tree mirrors the repo so dashboard's `../../tools/build-tokens.mjs`
# resolves identically to local dev.

# Stage 1: install
FROM node:20-alpine AS deps
WORKDIR /repo/src/dashboard
RUN corepack enable
COPY src/dashboard/package.json src/dashboard/pnpm-lock.yaml* ./
RUN --mount=type=cache,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile || pnpm install

# Stage 2: build
FROM node:20-alpine AS build
WORKDIR /repo/src/dashboard
RUN corepack enable
# p0169f-followup: Next.js evaluates rewrites() at BUILD time and bakes the
# destinations into the standalone bundle's server.js. The proxy target
# must be supplied as a build arg; runtime env-vars are too late.
# Default matches local dev; compose passes AGENTSMITH_BACKEND_URL=http://server:8081
# via build.args so the in-container Next.js server reaches the backend
# through compose DNS.
ARG AGENTSMITH_BACKEND_URL=http://localhost:8081
ENV AGENTSMITH_BACKEND_URL=$AGENTSMITH_BACKEND_URL
COPY --from=deps /repo/src/dashboard/node_modules ./node_modules
COPY DESIGN.md /repo/DESIGN.md
COPY tools/build-tokens.mjs /repo/tools/build-tokens.mjs
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
ENTRYPOINT ["node", "server.js"]
