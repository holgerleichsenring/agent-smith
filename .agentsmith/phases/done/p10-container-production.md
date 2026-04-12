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
Project: `AgentSmith.Cli/`, `AgentSmith.Application/`, `AgentSmith.Contracts/`

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


---

# Phase 10: Docker Production Setup - Implementation Details

## Overview
Production-ready container setup: non-root user, health check, environment-based
configuration, SSH key mount for git operations.

---

## Dockerfile Changes

### Non-root user
```dockerfile
RUN groupadd --gid 1000 agentsmith && \
    useradd --uid 1000 --gid agentsmith --create-home agentsmith && \
    mkdir -p /home/agentsmith/.ssh && \
    chown -R agentsmith:agentsmith /home/agentsmith

USER agentsmith
```

### Health check
```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD dotnet AgentSmith.Cli.dll --help || exit 1
```

### Temp directory for cloned repos
```dockerfile
RUN mkdir -p /tmp/agentsmith && chown agentsmith:agentsmith /tmp/agentsmith
```

---

## docker-compose.yml

```yaml
services:
  agentsmith:
    build: .
    restart: unless-stopped
    stdin_open: false          # No interactive mode in container
    env_file:
      - .env                   # All secrets from .env file
    environment:
      - GITHUB_TOKEN=${GITHUB_TOKEN}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
    volumes:
      - ./config:/app/config
      - ${SSH_KEY_PATH:-~/.ssh}:/home/agentsmith/.ssh:ro  # Read-only SSH keys
```

Key decisions:
- `stdin_open: false` - container runs headless
- `restart: unless-stopped` - survives Docker daemon restarts
- SSH keys mounted read-only
- Config directory mounted for easy editing

---

## .env.example

Template file with all possible environment variables and comments.
Users copy to `.env` and fill in their values.

---

## config/agentsmith.example.yml

Minimal quick-start configuration with comments explaining each section.


---

# Phase 10: Headless Mode - Implementation Details

## Overview
Container and CI environments cannot use interactive prompts. The `--headless` flag
makes Agent Smith fully autonomous by auto-approving plans without user confirmation.

---

## CLI Option (Host Layer)

Add `--headless` to System.CommandLine in `Program.cs`:

```csharp
var headlessOption = new Option<bool>(
    "--headless", "Run without interactive prompts (auto-approve plans)");
```

Pass the value to `ProcessTicketUseCase.ExecuteAsync()`.

---

## ContextKeys (Contracts Layer)

```csharp
// Commands/ContextKeys.cs
public const string Headless = "Headless";
```

---

## ProcessTicketUseCase (Application Layer)

Add `bool headless = false` parameter to `ExecuteAsync`.
Store in PipelineContext:

```csharp
pipeline.Set(ContextKeys.Headless, headless);
```

---

## ApprovalHandler (Application Layer)

Before prompting the user, check the headless flag:

```csharp
var headless = context.Pipeline.TryGet<bool>(ContextKeys.Headless, out var h) && h;
if (headless)
{
    logger.LogInformation("Headless mode: auto-approving plan");
    approved = true;
}
```

If headless, skip `Console.ReadLine()` and auto-approve.

---

## Security Consideration
Headless mode is explicitly opt-in via CLI flag. In non-headless mode,
the existing interactive approval flow remains unchanged.
