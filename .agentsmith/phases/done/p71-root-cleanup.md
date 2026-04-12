# Phase 71: Root Directory Cleanup

## Goal

Reduce root clutter from 20 entries to 13 by grouping deployment,
moving Dockerfiles to their projects, and removing generated directories.

## Scope

### Move Dockerfiles to their projects
- `Dockerfile` → `src/AgentSmith.Cli/Dockerfile`
- `Dockerfile.server` → `src/AgentSmith.Server/Dockerfile`
- `docker-entrypoint.sh` → `src/AgentSmith.Cli/docker-entrypoint.sh`

### Create deploy/ directory
- `docker-compose.yml` → `deploy/docker-compose.yml`
- `k8s/` → `deploy/k8s/`
- `apply-k8s-secret.sh` → `deploy/apply-k8s-secret.sh`
- `DOCKERHUB_DESCRIPTION.md` → `deploy/DOCKERHUB_DESCRIPTION.md`

### Move mkdocs.yml into docs/
- `mkdocs.yml` → `docs/mkdocs.yml`
- Update `docs_dir` inside mkdocs.yml

### Remove generated / stale directories
- `site/` — MkDocs build output → delete + .gitignore
- `agentsmith-output/` — scan output → delete + .gitignore
- `prompts/` — empty relic → delete

### Update references
- GitHub Actions workflows (Dockerfile paths, docker-compose path)
- docker-compose.yml build contexts (now relative to deploy/)
- docker-publish.yml Dockerfile references
- release.yml publish paths
- K8s manifests if they reference Dockerfiles

## Definition of Done

- [ ] Dockerfiles live next to their projects
- [ ] deploy/ contains docker-compose, k8s, scripts
- [ ] mkdocs.yml lives inside docs/
- [ ] site/, agentsmith-output/, prompts/ removed and gitignored
- [ ] CI/CD workflows updated for new paths
- [ ] docker-compose build contexts work from deploy/
- [ ] `dotnet build` succeeds
- [ ] All existing tests green
- [ ] Root has 13 entries or fewer
