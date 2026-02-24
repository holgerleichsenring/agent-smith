# Phase 22: CCS Auto-Bootstrap (Language-Agnostic Project Understanding)

## Goal

Agent Smith can work on any repository, not just .NET. When encountering a new
repo without a `.context.yaml`, the agent auto-generates one by detecting the
language, stack, and project structure.

## What is CCS?

CCS (Compact Context Specification) is a YAML format that captures project identity,
stack, architecture, and state in ~1,200 tokens instead of loading full documentation
(~50,000 tokens). See `template.context.yaml` and `context.schema.json`.

The `.context.yaml` lives in the repo root. Once generated, it is committed to the
repo and serves as the persistent project context for all future runs.

## Requirements

### Step 1: Language & Stack Detector (deterministic, zero LLM tokens)

`ProjectDetector` scans a repo root and returns a `DetectedProject` record:
language, runtime, package manager, build/test commands, frameworks, key files.

Detection rules by marker files:

**.NET:** `*.sln`, `*.csproj`, `global.json` → read csproj for PackageReferences,
TargetFramework. Build: `dotnet build`. Test: `dotnet test`.

**Python:** `pyproject.toml`, `setup.py`, `requirements.txt`, `Pipfile` →
detect poetry/pipenv/uv/hatch/pip. Test: pytest/tox/make test.

**TypeScript/JavaScript:** `package.json`, `tsconfig.json`, `deno.json` →
detect npm/pnpm/yarn/bun/deno. Framework detection (Next.js, Angular, Vite, etc.).
Test: vitest/jest/playwright/cypress.

**General:** CI detection (GitHub Actions, Azure DevOps, GitLab CI, Jenkins).
Infra detection (Docker, K8s, Terraform). README first 300 words.

### Step 2: Context Generator (one LLM call)

Takes `DetectedProject` + key file contents + directory tree. One call, ~3,000
input tokens, ~800 output tokens. Uses Haiku/Flash level model.

### Step 3: Validation & Commit

Validate generated YAML against `context.schema.json`. If invalid: retry once
with validation errors sent back to LLM. On approval: commit `.context.yaml`.

### Step 4: Build & Test Command Integration

Pipeline uses detected test/build commands instead of hardcoded `dotnet test`.

## Architecture

- `IProjectDetector` in Contracts, `ProjectDetector` in Infrastructure
- `DetectedProject` record in Contracts
- `IContextGenerator` in Contracts, `ContextGenerator` in Infrastructure (uses IAgentProvider)
- `IContextValidator` in Contracts, `ContextValidator` in Infrastructure
- `BootstrapProjectHandler` in Application (orchestrates detect → generate → validate → write)
- Detector does NOT use LLM. Generator uses exactly ONE cheap LLM call.

## Definition of Done

- [ ] Detecting .NET, Python, TypeScript projects correctly
- [ ] Generating valid `.context.yaml` for each
- [ ] Build and test commands correctly detected
- [ ] Pipeline uses detected commands instead of hardcoded ones
- [ ] Schema validation passes
- [ ] Unit tests for detector (mock file system)
- [ ] Integration test: run detector against Agent Smith's own repo
