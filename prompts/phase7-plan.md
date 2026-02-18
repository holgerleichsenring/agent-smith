# Phase 7: Prompt Caching - Implementierungsplan

## Ziel
Anthropic Prompt Caching aktivieren, damit System-Prompt, Tool-Definitionen und Coding
Principles server-seitig gecacht werden. Gecachte Tokens zählen NICHT gegen das ITPM
Rate Limit - der größte einzelne Hebel für unsere Rate-Limit-Probleme.

---

## Vorbedingung
- Phase 6 abgeschlossen (Retry-Logik vorhanden als Safety Net bei Cache-Misses)

## SDK-Erkenntnisse

Anthropic.SDK 5.9.0 bietet:
- `MessageParameters.PromptCaching` Property (Typ: `PromptCacheType`)
  - `None` = 0 (kein Caching)
  - `FineGrained` = 1 (manuell per `CacheControl` auf SystemMessages/Content)
  - `AutomaticToolsAndSystem` = 2 (automatisch für alle System-Messages und Tools)
- `SystemMessage(text, cacheControl)` Constructor
- `CacheControl { Type = CacheControlType.ephemeral, TTL = null }` (5 Min Default)
- `Usage.CacheCreationInputTokens` / `Usage.CacheReadInputTokens` auf Response

## Schritte

### Schritt 1: CacheConfig + TokenUsageTracker
Siehe: `prompts/phase7-caching.md`

Neue Config-Klasse und Token-Tracking für Observability.
Projekt: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Schritt 2: Prompt Caching aktivieren
Siehe: `prompts/phase7-token-tracking.md`

`PromptCaching = PromptCacheType.AutomaticToolsAndSystem` auf alle API-Calls setzen.
System-Prompt umstrukturieren für optimalen Cache-Prefix.
Projekt: `AgentSmith.Infrastructure/`

### Schritt 3: Tests + Verify

---

## Abhängigkeiten

```
Schritt 1 (CacheConfig + Tracker)
    └── Schritt 2 (Caching aktivieren)
         └── Schritt 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 7)

Keine neuen Packages nötig. Alles im Anthropic.SDK 5.9.0 enthalten.

---

## Definition of Done (Phase 7)
- [ ] `CacheConfig` Klasse in Contracts
- [ ] `AgentConfig.Cache` Property
- [ ] `PromptCaching = AutomaticToolsAndSystem` auf allen API-Calls (wenn enabled)
- [ ] System-Prompt optimal strukturiert (Coding Principles zuerst = längster stabiler Prefix)
- [ ] `TokenUsageTracker` loggt kumulative Usage inkl. Cache-Metriken
- [ ] Cache Hit Rate in Logs sichtbar
- [ ] Alle bestehenden Tests grün
- [ ] Neue Unit Tests für TokenUsageTracker
- [ ] E2E zeigt Cache Hits ab Iteration 2
