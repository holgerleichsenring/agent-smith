# syntax=docker/dockerfile:1
# 2026-10-01-283de: the browser sandbox image — a TOOLCHAIN image, not a carrier. The server's
# spawners inject the agent binary from agent-smith-sandbox-agent into it like into any other
# toolchain image (it needs only glibc, which the Playwright Ubuntu base has), and the agent runs
# `node /opt/agentsmith/render.mjs <request.json>` for render_reference. The server and the repo
# sandboxes never run a browser; this image is the only place one exists.
#
# Pinned: the Playwright base by version AND digest, the playwright-core package to the same
# version through a lockfile — Playwright drives exactly the Chromium build its base ships.
FROM mcr.microsoft.com/playwright:v1.63.0-noble@sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27

WORKDIR /opt/agentsmith
COPY src/sandbox-images/browser/package.json src/sandbox-images/browser/package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts && npm cache clean --force
COPY src/sandbox-images/browser/render.mjs src/sandbox-images/browser/egress.mjs \
     src/sandbox-images/browser/site.mjs src/sandbox-images/browser/shots.mjs ./

# Build identity, like every leg of the publish matrix stamps it.
ARG AGENTSMITH_BUILD_REVISION
ARG AGENTSMITH_RELEASE_VERSION
ENV AGENTSMITH_BUILD_REVISION=$AGENTSMITH_BUILD_REVISION
ENV AGENTSMITH_RELEASE_VERSION=$AGENTSMITH_RELEASE_VERSION
# The spawner overrides the entrypoint with the injected agent; standalone, the image renders.
WORKDIR /work
ENTRYPOINT []
CMD ["node", "/opt/agentsmith/render.mjs"]
