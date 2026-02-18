# Phase 10: Container Production-Ready - Implementation Plan

## Goal
`docker compose up` with a `.env` file and done. No source code checkout required.
A `--headless` mode that skips interactive approval prompts for container/CI usage.

---

## Prerequisite
- Phase 9 completed (Model Registry & Scout)

## Steps

### Step 1: Headless Mode
See: `prompts/phase10-headless.md`

CLI option `--headless` that auto-approves plans without interactive stdin.
Project: `AgentSmith.Host/`, `AgentSmith.Application/`, `AgentSmith.Contracts/`

### Step 2: Docker Production Setup
See: `prompts/phase10-docker.md`

Dockerfile with non-root user, health check. Docker Compose with restart policy,
`.env` file support, SSH key mount.
Project: `Dockerfile`, `docker-compose.yml`, `.env.example`, `config/agentsmith.example.yml`

### Step 3: Verify

---

## Dependencies

```
Step 1 (Headless Mode)
    └── Step 2 (Docker Production Setup)
         └── Step 3 (Verify)
```

---

## NuGet Packages (Phase 10)

No new packages required.

---

## Key Decisions

1. `--headless` mode instead of interactive stdin in container (containers without TTY cannot do `Console.ReadLine()`)
2. ApprovalHandler respects headless flag from PipelineContext (auto-approve)
3. Non-root user `agentsmith` in Docker for security
4. SSH keys mounted read-only from host
5. All secrets via environment variables, referenced in `.env` file

---

## Definition of Done (Phase 10)
- [ ] `--headless` CLI option in Program.cs
- [ ] `ContextKeys.Headless` constant
- [ ] ApprovalHandler auto-approves in headless mode
- [ ] ProcessTicketUseCase accepts `headless` parameter
- [ ] Dockerfile: non-root user, health check, temp directory
- [ ] docker-compose.yml: restart policy, env_file, SSH mount, stdin_open: false
- [ ] `.env.example` template with all environment variables
- [ ] `config/agentsmith.example.yml` quick-start config template
- [ ] `.env` added to `.gitignore`
- [ ] All existing tests green
