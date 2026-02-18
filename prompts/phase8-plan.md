# Phase 8: Context Compaction - Implementation Plan

## Goal
Prevent the conversation history from growing unboundedly. After N iterations,
old tool results are summarized and raw file contents are removed. Enables
25+ iterations for complex multi-file tasks.

---

## Prerequisite
- Phase 7 completed (TokenUsageTracker provides token counts for trigger decisions)

## Steps

### Step 1: CompactionConfig + IContextCompactor + FileReadTracker
See: `prompts/phase8-compaction.md`

Config, interface, and deduplication.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: ClaudeContextCompactor + Integration
See: `prompts/phase8-file-tracking.md`

LLM-based compression via Haiku + integration into AgenticLoop.
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (Config + Interface + FileTracker)
    └── Step 2 (Compactor + Loop Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 8)

No new packages needed.

---

## Definition of Done (Phase 8)
- [ ] `CompactionConfig` class in Contracts
- [ ] `IContextCompactor` interface in Contracts
- [ ] `ClaudeContextCompactor` implementation in Infrastructure (uses Haiku)
- [ ] `FileReadTracker` deduplicates file reads
- [ ] `AgenticLoop` triggers compaction based on iteration count OR token count
- [ ] Last N iterations remain fully preserved
- [ ] All existing tests green
- [ ] New unit tests for Compactor, FileTracker, Compaction trigger
- [ ] E2E test can run 15+ iterations without context explosion
