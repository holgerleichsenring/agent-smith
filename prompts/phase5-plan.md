# Phase 5 - CLI & Docker

## Goal
Make Agent Smith deliverable as a real CLI tool and Docker image.
User should be able to run `agentsmith "fix #123 in todo-list"` - locally or in a container.

---

## Components

| Step | File | Description |
|------|------|-------------|
| 1 | `phase5-cli.md` | CLI with System.CommandLine (arguments, options, --help) |
| 2 | `phase5-docker.md` | Dockerfile (multi-stage), .dockerignore, Docker Compose example |
| 3 | `phase5-smoke-test.md` | End-to-end smoke test (DI resolution, CLI parsing) |
| 4 | Tests | CLI argument parsing, DI integration |

---

## Design Decisions

### CLI: System.CommandLine instead of hand-written parsing
- `System.CommandLine` is the official .NET CLI framework
- Gives us `--help`, `--version`, `--config`, validation for free
- Minimal overhead, no over-engineering

### Docker: Multi-Stage Build
- Stage 1: SDK for building
- Stage 2: Runtime-only image (lean)
- Config is mounted as a volume, not built in
- Target: Image < 200MB

### No Over-Engineering
- No sub-commands (only one root command)
- No interactive shell
- No watch modes or daemon processes
- Simple: Input in → PR out → Exit
