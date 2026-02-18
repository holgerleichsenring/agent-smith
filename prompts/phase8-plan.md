# Phase 8: Context Compaction - Implementierungsplan

## Ziel
Verhindern, dass die Konversationshistorie unbegrenzt wächst. Nach N Iterationen
werden alte Tool-Results zusammengefasst und rohe Dateiinhalte entfernt. Ermöglicht
25+ Iterationen für komplexe Multi-File-Tasks.

---

## Vorbedingung
- Phase 7 abgeschlossen (TokenUsageTracker liefert Token-Counts für Trigger-Entscheidung)

## Schritte

### Schritt 1: CompactionConfig + IContextCompactor + FileReadTracker
Siehe: `prompts/phase8-compaction.md`

Config, Interface und Deduplizierung.
Projekt: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Schritt 2: ClaudeContextCompactor + Integration
Siehe: `prompts/phase8-file-tracking.md`

LLM-basierte Komprimierung via Haiku + Integration in AgenticLoop.
Projekt: `AgentSmith.Infrastructure/`

### Schritt 3: Tests + Verify

---

## Abhängigkeiten

```
Schritt 1 (Config + Interface + FileTracker)
    └── Schritt 2 (Compactor + Loop-Integration)
         └── Schritt 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 8)

Keine neuen Packages nötig.

---

## Definition of Done (Phase 8)
- [ ] `CompactionConfig` Klasse in Contracts
- [ ] `IContextCompactor` Interface in Contracts
- [ ] `ClaudeContextCompactor` Implementierung in Infrastructure (nutzt Haiku)
- [ ] `FileReadTracker` dedupliziert Datei-Reads
- [ ] `AgenticLoop` triggert Komprimierung basierend auf Iteration-Count ODER Token-Count
- [ ] Letzte N Iterationen bleiben vollständig erhalten
- [ ] Alle bestehenden Tests grün
- [ ] Neue Unit Tests für Compactor, FileTracker, Compaction-Trigger
- [ ] E2E-Test kann 15+ Iterationen ohne Context-Explosion
