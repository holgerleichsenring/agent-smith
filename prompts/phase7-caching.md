# Phase 7 - Schritt 1: CacheConfig + TokenUsageTracker

## Ziel
Config-Klasse für Cache-Steuerung und ein Tracker der Token-Usage über alle
Iterationen kumuliert und Cache Hit Rates berechnet.
Projekt: `AgentSmith.Contracts/Configuration/`, `AgentSmith.Infrastructure/Providers/Agent/`

---

## CacheConfig

```
Datei: src/AgentSmith.Contracts/Configuration/CacheConfig.cs
```

- `bool Enabled` = true
- `string Strategy` = "automatic" (Werte: "automatic", "fine-grained", "none")

### Hinweise
- "automatic" → `PromptCacheType.AutomaticToolsAndSystem`
- "fine-grained" → `PromptCacheType.FineGrained` (für spätere manuelle Kontrolle)
- "none" → `PromptCacheType.None`

---

## AgentConfig Erweiterung

```
Datei: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```

Neues Property:
- `CacheConfig Cache { get; set; } = new();`

---

## TokenUsageSummary

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/TokenUsageSummary.cs
```

Sealed record:
```csharp
public sealed record TokenUsageSummary(
    int TotalInputTokens,
    int TotalOutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    int Iterations)
{
    public double CacheHitRate => (TotalInputTokens + CacheReadTokens) > 0
        ? (double)CacheReadTokens / (TotalInputTokens + CacheReadTokens)
        : 0.0;
}
```

---

## TokenUsageTracker

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/TokenUsageTracker.cs
```

**Verantwortung:** Akkumuliert Token-Usage über alle API-Calls einer Execution.

### Verhalten
1. `Track(MessageResponse response)` → extrahiert Usage, addiert zu Gesamt
2. `GetSummary()` → gibt `TokenUsageSummary` zurück
3. `LogSummary(ILogger logger)` → loggt Zusammenfassung am Ende

### Code-Skizze
```csharp
public sealed class TokenUsageTracker
{
    private int _totalInput, _totalOutput, _cacheCreate, _cacheRead, _iterations;

    public void Track(MessageResponse response)
    {
        var usage = response.Usage;
        _totalInput += usage.InputTokens;
        _totalOutput += usage.OutputTokens;
        _cacheCreate += usage.CacheCreationInputTokens;
        _cacheRead += usage.CacheReadInputTokens;
        _iterations++;
    }

    public TokenUsageSummary GetSummary() => new(
        _totalInput, _totalOutput, _cacheCreate, _cacheRead, _iterations);

    public void LogSummary(ILogger logger)
    {
        var summary = GetSummary();
        logger.LogInformation(
            "Token usage: {Input} input, {Output} output, " +
            "{CacheCreate} cache-create, {CacheRead} cache-read, " +
            "Cache hit rate: {Rate:P1}, Iterations: {Iter}",
            summary.TotalInputTokens, summary.TotalOutputTokens,
            summary.CacheCreationTokens, summary.CacheReadTokens,
            summary.CacheHitRate, summary.Iterations);
    }
}
```

## Tests

**TokenUsageTrackerTests:**
- `Track_AccumulatesTokens`
- `GetSummary_ReturnsCorrectTotals`
- `CacheHitRate_CalculatesCorrectly`
- `CacheHitRate_ZeroTokens_ReturnsZero`
