# Phase 69: Project Rename — Host → Cli, Dispatcher → Server

## Goal

Rename the two top-level projects to reflect their actual roles:

- `AgentSmith.Host` → `AgentSmith.Cli` (CLI entry point, commands, pipeline execution)
- `AgentSmith.Dispatcher` → `AgentSmith.Server` (Slack gateway, Redis bus, job spawning, webhook listener)

## Motivation

The current names are misleading:

- "Host" suggests ASP.NET Generic Host or a hosting layer, but it's the CLI tool.
- "Dispatcher" sounds like an internal pattern (event dispatcher), but it's the long-running server process.

## Scope

### Per project rename

1. Rename directory `src/AgentSmith.Host/` → `src/AgentSmith.Cli/`
2. Rename `.csproj` file and update `<RootNamespace>` / `<AssemblyName>`
3. Update `namespace` declarations in all `.cs` files
4. Update all `<ProjectReference>` entries in other `.csproj` files
5. Update `using` directives across the solution
6. Update `docker-compose.yml`, `Dockerfile`, Kustomize manifests
7. Update CI/CD workflows (GitHub Actions)
8. Update documentation references

Repeat for `AgentSmith.Dispatcher` → `AgentSmith.Server`.

### Cross-cutting

- Update solution file (`.sln`)
- Update any hardcoded assembly name references (e.g. in logging, Docker entrypoints)
- Update `.agentsmith/context.yaml` layer descriptions
- Grep for stale references

### CI/CD & Release

- GitHub Actions workflows: build paths, artifact names, Docker image references
- Dockerfile(s): `COPY`, `ENTRYPOINT`, binary paths
- Docker Hub: image names/tags (if `agentsmith-host` or `agentsmith-dispatcher` are published)
- Release-Please config: component names, changelog references
- Kustomize overlays & Helm values: container names, image references

### Documentation & Website

- `docs/` (MkDocs): architecture pages, code examples, layer diagrams referencing Host/Dispatcher
- `website/` (Vercel): any project name references, architecture visuals
- `README.md`: installation/build instructions, project structure overview
- Phase files in `.agentsmith/phases/` that reference old project names

## Risks

- Binary/image name changes may affect deployment scripts and K8s manifests
- Docker Hub image tags may need updating
- External links or bookmarks to old Docker image names will break

## Definition of Done

- [x] `AgentSmith.Host` renamed to `AgentSmith.Cli` (directory, csproj, namespaces)
- [x] `AgentSmith.Dispatcher` renamed to `AgentSmith.Server` (directory, csproj, namespaces)
- [x] Solution file updated
- [x] All ProjectReference and using directives updated
- [x] Docker, K8s, CI/CD manifests updated
- [x] MkDocs site (docs/) updated — architecture pages, code examples
- [ ] Website (Vercel) updated — project references, visuals
- [x] README.md updated
- [ ] All existing tests green
