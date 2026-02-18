# Phase 6: Resilience - Retry mit Exponential Backoff - Implementierungsplan

## Ziel
Agent Smith überlebt Rate Limits (429) und transiente Fehler (500/502/503).
Grundvoraussetzung für alle weiteren Optimierungen (Caching, Compaction, Scout).

---

## Vorbedingung
- Phase 5 abgeschlossen (CLI funktionsfähig)
- Erster E2E-Test dokumentiert (run-log-001): Crash bei Iteration 6 durch Rate Limit

## Erkenntnisse aus SDK-Analyse

Anthropic.SDK 5.9.0 bietet:
- **Kein** eingebauten RetryInterceptor
- `AnthropicClient(string apiKey, HttpClient httpClient)` → HttpClient-Injection möglich
- `RateLimitsExceeded` Exception (fangbar)
- `RateLimits` Type auf Response (RequestsLimit, etc.)
- `Usage` mit InputTokens, OutputTokens, CacheCreationInputTokens, CacheReadInputTokens

Strategie: **Polly via `Microsoft.Extensions.Http.Resilience`** als DelegatingHandler auf dem HttpClient.

## Schritte

### Schritt 1: RetryConfig + Resilient HttpClient Factory
Siehe: `prompts/phase6-retry.md`

Neue Config-Klasse für Retry-Einstellungen und eine Factory die HttpClients mit
Polly-basiertem Retry-Handler erstellt.
Projekt: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Schritt 2: Integration in ClaudeAgentProvider + AgenticLoop
Siehe: `prompts/phase6-retry.md` (zweiter Teil)

Bestehende Klassen anpassen: Resilient Client nutzen, Token-Usage loggen.
Projekt: `AgentSmith.Infrastructure/`

### Schritt 3: Config + Tests + Verify
Config erweitern, Tests schreiben, E2E validieren.

---

## Abhängigkeiten

```
Schritt 1 (RetryConfig + Factory)
    └── Schritt 2 (Integration Provider + Loop)
         └── Schritt 3 (Config + Tests + Verify)
```

Strikt sequentiell.

---

## NuGet Packages (Phase 6)

| Projekt | Package | Zweck |
|---------|---------|-------|
| AgentSmith.Infrastructure | Microsoft.Extensions.Http.Resilience | Polly-basierter HttpClient Retry |
| AgentSmith.Infrastructure | Microsoft.Extensions.Http | HttpClientFactory |

---

## Definition of Done (Phase 6)
- [ ] `RetryConfig` Klasse in Contracts mit sinnvollen Defaults
- [ ] `AgentConfig.Retry` Property
- [ ] Resilient HttpClient mit Polly: Retry bei 429, 500, 502, 503, 504
- [ ] Exponential Backoff mit Jitter
- [ ] `ClaudeAgentProvider` nutzt resilienten Client für Plan + Execution
- [ ] `AgenticLoop` loggt Token-Usage pro Iteration
- [ ] YAML Config unterstützt `retry:` Sektion (backward-kompatibel)
- [ ] Alle bestehenden Tests grün
- [ ] Neue Unit Tests für Retry-Config
- [ ] E2E-Test überlebt Rate Limits
