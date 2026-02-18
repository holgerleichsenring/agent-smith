# Phase 9: Model Registry & Scout Pattern - Implementierungsplan

## Ziel
Task-spezifisches Model-Routing. Haiku für File-Discovery (Scout), Sonnet für
Coding (Primary), optional Opus für komplexe Architektur-Entscheidungen (Reasoning).
60-80% Kostenreduktion bei Discovery-Iterationen.

---

## Vorbedingung
- Phase 8 abgeschlossen (Multi-Model-Pattern bewiesen durch Haiku-Compaction)

## Schritte

### Schritt 1: ModelRegistryConfig + IModelRegistry + TaskType
Siehe: `prompts/phase9-model-registry.md`

Config, Interface und Enum für Task-basiertes Model-Routing.
Projekt: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Schritt 2: ScoutAgent + Integration
Siehe: `prompts/phase9-scout.md`

Haiku-basierte File-Discovery mit Read-Only-Tools, Integration in ClaudeAgentProvider.
Projekt: `AgentSmith.Infrastructure/`

### Schritt 3: Tests + Verify

---

## Abhängigkeiten

```
Schritt 1 (Config + Registry)
    └── Schritt 2 (Scout + Provider-Integration)
         └── Schritt 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 9)

Keine neuen Packages nötig.

---

## Kernentscheidungen

1. Scout ist ein Implementierungsdetail von ClaudeAgentProvider, kein neues Interface
2. `IAgentProvider`-Interface bleibt UNVERÄNDERT - alle Optimierungen sind intern
3. Backward-kompatibel: Wenn `Models` null ist, wird das einzelne `Model`-Feld verwendet
4. ScoutTools sind read-only (kein write_file, kein run_command) - Scout kann nichts kaputt machen

---

## Definition of Done (Phase 9)
- [ ] `ModelRegistryConfig` + `ModelAssignment` in Contracts
- [ ] `TaskType` Enum in Contracts
- [ ] `IModelRegistry` Interface in Contracts
- [ ] `ConfigBasedModelRegistry` in Infrastructure
- [ ] `ScoutAgent` in Infrastructure (Haiku, read-only tools)
- [ ] `ScoutResult` Record (RelevantFiles, ContextSummary, TokensUsed)
- [ ] `ToolDefinitions.ScoutTools` (nur read_file + list_files)
- [ ] ClaudeAgentProvider: Scout vor Primary laufen lassen (wenn konfiguriert)
- [ ] Alle bestehenden Tests grün
- [ ] Neue Unit Tests für Registry, Scout, TaskType
- [ ] E2E-Test zeigt Scout + Primary Model-Nutzung in Logs
