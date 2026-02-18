# Phase 9: Model Registry & Scout Pattern - Implementation Plan

## Goal
Task-specific model routing. Haiku for file discovery (Scout), Sonnet for
coding (Primary), optionally Opus for complex architecture decisions (Reasoning).
60-80% cost reduction for discovery iterations.

---

## Prerequisite
- Phase 8 completed (Multi-model pattern proven through Haiku compaction)

## Steps

### Step 1: ModelRegistryConfig + IModelRegistry + TaskType
See: `prompts/phase9-model-registry.md`

Config, interface, and enum for task-based model routing.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: ScoutAgent + Integration
See: `prompts/phase9-scout.md`

Haiku-based file discovery with read-only tools, integration into ClaudeAgentProvider.
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (Config + Registry)
    └── Step 2 (Scout + Provider Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 9)

No new packages needed.

---

## Key Decisions

1. Scout is an implementation detail of ClaudeAgentProvider, not a new interface
2. `IAgentProvider` interface remains UNCHANGED - all optimizations are internal
3. Backward-compatible: If `Models` is null, the single `Model` field is used
4. ScoutTools are read-only (no write_file, no run_command) - Scout cannot break anything

---

## Definition of Done (Phase 9)
- [ ] `ModelRegistryConfig` + `ModelAssignment` in Contracts
- [ ] `TaskType` Enum in Contracts
- [ ] `IModelRegistry` interface in Contracts
- [ ] `ConfigBasedModelRegistry` in Infrastructure
- [ ] `ScoutAgent` in Infrastructure (Haiku, read-only tools)
- [ ] `ScoutResult` Record (RelevantFiles, ContextSummary, TokensUsed)
- [ ] `ToolDefinitions.ScoutTools` (only read_file + list_files)
- [ ] ClaudeAgentProvider: Run Scout before Primary (when configured)
- [ ] All existing tests green
- [ ] New unit tests for Registry, Scout, TaskType
- [ ] E2E test shows Scout + Primary model usage in logs
