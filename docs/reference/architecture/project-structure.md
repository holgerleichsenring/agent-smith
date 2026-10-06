# Project Structure

The layout of the Agent Smith repository.

```
agent-smith/
├── .agentsmith/                    # The repository's own spec-first meta-files
│   ├── contexts/<name>/            # One directory per context (this repo has `default`)
│   │   ├── context.yaml            #   architecture, stack, phase status
│   │   └── principles.md           #   code quality rules
│   ├── specs/
│   │   ├── planned/                # Upcoming phase specs
│   │   ├── active/                 # The phase being implemented
│   │   └── done/                   # Completed phases
│   ├── decisions/                  # One decision YAML per phase
│   ├── memory/                     # Experiential memory (MEMORY.md index + one file per fact)
│   ├── runs/                       # Run records written by pipeline runs
│   ├── security/                   # Security-scan snapshots for trend analysis
│   └── *.schema.json               # Schemas for context, phase spec and decision files
│
├── src/
│   ├── backend/                    # The .NET solution's projects
│   │   ├── AgentSmith.Domain/                         # entities, value objects, exceptions
│   │   ├── AgentSmith.Contracts/                      # interfaces, commands, events, config models
│   │   ├── AgentSmith.Application/                    # pipeline executor, handlers, use cases
│   │   ├── AgentSmith.Infrastructure.Core/            # config loading, secrets, skill catalog
│   │   ├── AgentSmith.Infrastructure/                 # LLM clients, git/ticket providers, Redis, scanners
│   │   ├── AgentSmith.Infrastructure.Persistence/     # EF Core store and migrations
│   │   ├── AgentSmith.Infrastructure.Persistence.SqlServer/  # SQL Server migrations
│   │   ├── AgentSmith.Cli/                            # CLI entry point (+ Dockerfile)
│   │   ├── AgentSmith.Server/                         # long-running server (+ Dockerfile)
│   │   ├── AgentSmith.Sandbox.Wire/                   # step protocol shared with the sandbox agent
│   │   ├── AgentSmith.Sandbox.Agent/                  # executable injected into each sandbox (+ Dockerfile)
│   │   └── AgentSmith.SkillsPackaging/                # build-time skill catalog check
│   └── dashboard/                  # Next.js dashboard (pnpm, vitest; + Dockerfile)
│
├── tests/
│   ├── AgentSmith.Tests/           # main xUnit suite, including the frozen event fixtures
│   ├── AgentSmith.Sandbox.Agent.Tests/  # sandbox agent tests
│   ├── AgentSmith.PipelineHarness/ # drives real pipeline compositions against scripted LLMs
│   └── AgentSmith.Tests.Fixtures/  # sample repositories used by tests
│
├── config/                         # Configuration schema and examples
│   ├── agentsmith.schema.json      #   JSON schema for agentsmith.yml
│   ├── agentsmith.example.yml      #   annotated example configuration
│   ├── nuclei.yaml, spectral.yaml  #   api-scan tool settings (+ .example variants)
│   └── docs-writing-skill.md       #   writing guide for these docs
│
├── deploy/
│   ├── docker-compose.example.yml  # server, Redis, migrate job, dashboard
│   ├── k8s/                        # numbered manifests (1-namespace.yaml … 12-pvc-persistence.yaml) + examples/
│   ├── apply-k8s-secret.sh         # creates the Kubernetes secret
│   └── DOCKERHUB_DESCRIPTION.md
│
├── tools/                          # Repository tooling
│   ├── build-hub-event-types.mjs   #   dashboard event-type mirror check
│   ├── build-tokens.mjs            #   DESIGN.md → CSS tokens for website and docs
│   ├── freeze-event-fixtures.cs    #   one-shot seeder for event fixtures
│   ├── fetch-skills.sh             #   pulls the pinned skill catalog for local tests
│   └── …                           #   audit, prompt-bench and measurement scripts
│
├── docs/                           # This MkDocs site (docs/mkdocs.yml)
├── website/                        # Project website (Eleventy)
├── hooks/                          # Git hooks; enable with `git config core.hooksPath hooks`
├── .github/workflows/              # GitHub Actions: build, test and images; dashboard; docs; releases
│
├── AgentSmith.sln                  # Solution file
├── DESIGN.md                       # Design tokens, source for build-tokens.mjs
├── WORKER-MODE.md                  # Running a ticket with an external agent CLI answering the model calls
├── release-please-config.json      # Release automation
└── version.txt                     # Current version
```

## Solution projects

`AgentSmith.sln` contains fifteen projects:

| Project | Type | Description |
|---------|------|-------------|
| `AgentSmith.Domain` | Class library | Entities, value objects, exceptions |
| `AgentSmith.Contracts` | Class library | Interfaces, commands, events, config models |
| `AgentSmith.Application` | Class library | Handlers, pipeline executor, use cases |
| `AgentSmith.Infrastructure.Core` | Class library | Config loading, secrets, skill catalog, registries |
| `AgentSmith.Infrastructure` | Class library | LLM clients, git and ticket providers, Redis, scanners, output |
| `AgentSmith.Infrastructure.Persistence` | Class library | EF Core store (SQLite, PostgreSQL, MySQL, SQL Server) |
| `AgentSmith.Infrastructure.Persistence.SqlServer` | Class library | SQL Server migrations |
| `AgentSmith.Sandbox.Wire` | Class library | Sandbox step protocol |
| `AgentSmith.Sandbox.Agent` | Console app | Agent inside each sandbox |
| `AgentSmith.SkillsPackaging` | Console app | Build-time skill catalog check |
| `AgentSmith.Cli` | Console app | CLI entry point |
| `AgentSmith.Server` | Web app | Webhooks, queue, dashboard API, sandboxes |
| `AgentSmith.Tests` | Test project | Main test suite |
| `AgentSmith.Sandbox.Agent.Tests` | Test project | Sandbox agent tests |
| `AgentSmith.PipelineHarness` | Console app | Pipeline harness |

## Published images

| Image | Built from |
|-------|------------|
| `holgerleichsenring/agent-smith-server` | `src/backend/AgentSmith.Server/Dockerfile` |
| `holgerleichsenring/agent-smith-cli` | `src/backend/AgentSmith.Cli/Dockerfile` |
| `holgerleichsenring/agent-smith-sandbox-agent` | `src/backend/AgentSmith.Sandbox.Agent/Dockerfile` |
| `holgerleichsenring/agentsmith-dashboard` | `src/dashboard/Dockerfile` |

## Key files

| File | Purpose |
|------|---------|
| `src/backend/AgentSmith.Cli/Program.cs` | CLI verb definitions |
| `src/backend/AgentSmith.Cli/ConfigDiscovery.cs` | Where the CLI looks for `agentsmith.yml` |
| `src/backend/AgentSmith.Application/Services/ExecutePipelineUseCase.cs` | Top-level run orchestration |
| `src/backend/AgentSmith.Application/Services/PipelineExecutor.cs` | Runs the ordered command list |
| `src/backend/AgentSmith.Contracts/Commands/PipelinePresets*.cs` | The step list of every pipeline, one file per preset |
| `src/backend/AgentSmith.Cli/docker-entrypoint.sh` | CLI image entrypoint: fixes volume permissions, then drops to the `agentsmith` user |
