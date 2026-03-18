# Phase 43a: CI/CD — Docker Build & Docker Hub Publishing

## Goal

Every push to `main` triggers a GitHub Action that builds Docker images and
publishes them to Docker Hub. Two images: `holgerleichsenring/agent-smith`
(CLI/Runner) and `holgerleichsenring/agent-smith-dispatcher` (Server).

This makes Agent Smith consumable without cloning — customers pull directly.

---

## Why This Matters

- GitLab CI, Azure Pipelines, K8s manifests reference published images
- `docker-compose.yml` works out of the box for new customers
- Enables CLI use case: `docker run holgerleichsenring/agent-smith security-scan ...`

---

## Dockerfile Optimization

**Host (`Dockerfile`)**: Stays on `mcr.microsoft.com/dotnet/sdk:8.0` for runtime
because the agentic loop runs `dotnet build/test` on cloned repositories. Added
`libgit2-dev` alongside git. Added `Infrastructure.Core` csproj to restore layer.

**Dispatcher (`Dockerfile.dispatcher`)**: Already on `aspnet:8.0` (~250MB).
Added `Infrastructure.Core` csproj to restore layer.

Both Dockerfiles are multi-arch compatible (no platform pinning).

---

## GitHub Action: `.github/workflows/docker-publish.yml`

### Triggers

```yaml
on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:
    branches: [main]
```

- Push to `main` → publish `latest` tag
- Git tag `v*` (e.g. `v1.2.0`) → publish semver tag + `latest`
- Pull request → build only, no push (verify Dockerfile)

### Workflow Steps

1. `actions/checkout@v4`
2. `docker/setup-qemu-action` — ARM64 emulation
3. `docker/setup-buildx-action` — multi-platform builder
4. `docker/login-action` — DockerHub credentials from GitHub Secrets
5. `docker/metadata-action` — extract tags + labels
6. Run `dotnet test` — fail fast before build
7. `docker/build-push-action` — build + push `agent-smith` image
8. `docker/build-push-action` — build + push `agent-smith-dispatcher` image
9. `peter-evans/dockerhub-description` — update DockerHub README

### Matrix: Two Images

```yaml
strategy:
  matrix:
    include:
      - image: agent-smith
        dockerfile: Dockerfile
      - image: agent-smith-dispatcher
        dockerfile: Dockerfile.dispatcher
```

Docker namespace as workflow variable:

```yaml
env:
  DOCKER_NAMESPACE: holgerleichsenring
```

### Versioning Strategy

| Trigger | Tags |
|---------|------|
| Push to `main` | `latest` |
| Tag `v1.2.0` | `1.2.0`, `1.2`, `latest` |
| Pull request | build only, no push |

### Multi-Arch

Platforms: `linux/amd64`, `linux/arm64`

Uses QEMU + Buildx. ARM64 for Apple Silicon dev machines and ARM-based K8s nodes.

---

## DockerHub Description

File: `DOCKERHUB_DESCRIPTION.md` in repo root.

Content:
- Short description (what Agent Smith does)
- Quickstart: `docker run`, `docker-compose up`
- Environment variables reference
- Links to GitHub repo, docs

Updated on every push to `main` via `peter-evans/dockerhub-description`.

---

## Secrets (GitHub Repository Settings)

| Secret | Description |
|--------|-------------|
| `DOCKERHUB_USERNAME` | DockerHub username |
| `DOCKERHUB_TOKEN` | DockerHub access token (not password) |

Document in repo README and in this phase doc.

---

## Files to Create

- `.github/workflows/docker-publish.yml` — GitHub Action
- `DOCKERHUB_DESCRIPTION.md` — DockerHub repo description

## Files to Modify

- `Dockerfile` — switch runtime to `aspnet:8.0` + git install
- `Dockerfile.dispatcher` — verify already on `aspnet:8.0` (it is)

---

## Definition of Done

- [ ] Push to `main` → both images appear on Docker Hub as `latest`
- [ ] Git tag `v*` → semver tags published for both images
- [ ] PR → build-only, no push
- [ ] Multi-arch: amd64 + arm64
- [ ] Dockerfile optimized: `aspnet:8.0` + git, ~250MB image size
- [ ] DockerHub description updated on push
- [ ] Tests run in CI before build (fail fast)
- [ ] Secrets documented
- [ ] `DOCKER_NAMESPACE` as workflow variable, not hardcoded
