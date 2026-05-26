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
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD wget -qO- http://localhost:3000/ >/dev/null || exit 1
ENTRYPOINT ["node", "server.js"]
