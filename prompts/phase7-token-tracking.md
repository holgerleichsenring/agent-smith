# Phase 7 - Schritt 2: Prompt Caching aktivieren

## Ziel
`PromptCaching = AutomaticToolsAndSystem` auf allen API-Calls setzen und den
System-Prompt für optimales Caching umstrukturieren.
Projekt: `AgentSmith.Infrastructure/Providers/Agent/`

---

## AgenticLoop Anpassung

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

### Änderungen

1. Constructor: `CacheConfig cacheConfig` und `TokenUsageTracker tracker` als Parameter
2. In `SendRequestAsync`, `PromptCaching` setzen:
   ```csharp
   PromptCaching = ResolveCacheType(cacheConfig)
   ```
3. Nach jedem API-Call: `tracker.Track(response)`
4. Am Ende von `RunAsync`: `tracker.LogSummary(logger)`
5. Bestehende `LogTokenUsage` Methode bleibt für Detail-Logging pro Iteration

### ResolveCacheType Helper
```csharp
private static PromptCacheType ResolveCacheType(CacheConfig config)
{
    if (!config.Enabled) return PromptCacheType.None;
    return config.Strategy.ToLowerInvariant() switch
    {
        "automatic" => PromptCacheType.AutomaticToolsAndSystem,
        "fine-grained" => PromptCacheType.FineGrained,
        _ => PromptCacheType.None
    };
}
```

---

## ClaudeAgentProvider Anpassung

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

### Änderungen

1. System-Prompt Umstrukturierung für optimalen Cache-Prefix:
   - **Erster SystemMessage**: Coding Principles (groß, statisch pro Execution)
   - **Zweiter SystemMessage**: Execution Instructions (klein, variiert)
   - Grund: Cache ist prefix-basiert. Der längste stabile Prefix wird gecacht.

2. In `GeneratePlanAsync`: `PromptCaching` setzen
   ```csharp
   PromptCaching = ResolveCacheType(cacheConfig)
   ```

3. In `ExecutePlanAsync`: `TokenUsageTracker` erstellen, an `AgenticLoop` übergeben

4. Constructor: `CacheConfig` aus `AgentConfig` beziehen (via `retryConfig` Pattern)

### System-Prompt Aufbau (optimiert für Caching)
```
SystemMessage[0]: Coding Principles      ← ~1.5k tokens, GECACHT nach 1. Call
SystemMessage[1]: Task Instructions       ← ~400 tokens, GECACHT nach 1. Call
Tool Definitions (4 Tools)               ← ~800 tokens, GECACHT automatisch
```

Ab Iteration 2: ~2.7k Tokens werden aus Cache gelesen statt gezählt.
Bei 30k ITPM Limit (Tier 1) spart das ~9% pro Iteration.

---

## AgentProviderFactory Anpassung

`CreateClaude` reicht `config.Cache` durch (wie bei `config.Retry`).

---

## Config Beispiel

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514
  retry:
    max_retries: 5
    initial_delay_ms: 2000
    backoff_multiplier: 2.0
    max_delay_ms: 60000
  cache:
    enabled: true
    strategy: automatic
```
