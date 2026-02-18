# Phase 6 - Schritt 1 & 2: Retry mit Exponential Backoff

## Ziel
Alle Anthropic API-Aufrufe überleben transiente Fehler und Rate Limits automatisch.
Projekt: `AgentSmith.Contracts/Configuration/`, `AgentSmith.Infrastructure/Providers/Agent/`

---

## RetryConfig

```
Datei: src/AgentSmith.Contracts/Configuration/RetryConfig.cs
```

Einfache Config-Klasse mit sinnvollen Defaults:

- `int MaxRetries` = 5
- `int InitialDelayMs` = 2000 (2 Sekunden)
- `double BackoffMultiplier` = 2.0
- `int MaxDelayMs` = 60000 (1 Minute)

### Hinweise
- Kein `UseJitter` Property nötig - Polly hat Jitter eingebaut
- Defaults sind konservativ genug für Tier 1 (30k ITPM)

---

## AgentConfig Erweiterung

```
Datei: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```

Neues Property:
- `RetryConfig Retry { get; set; } = new();`

Backward-kompatibel: Default-Konstruktor liefert sinnvolle Werte.

---

## ResilientHttpClientFactory

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/ResilientHttpClientFactory.cs
```

**Verantwortung:** Erstellt HttpClient-Instanzen mit Polly Retry-Policy.

### Verhalten
1. Erstellt `HttpClient` mit `SocketsHttpHandler` als Basis
2. Fügt Polly `RetryPolicy` als `DelegatingHandler` hinzu
3. Retry bei HTTP Status: 429, 500, 502, 503, 504
4. Exponential Backoff: `initialDelay * Math.Pow(backoffMultiplier, retryAttempt)`
5. Jitter: ±25% auf jeden Delay (verhindert Thundering Herd)
6. Loggt jeden Retry-Versuch mit Wartezeit

### Code-Skizze

```csharp
public sealed class ResilientHttpClientFactory(
    RetryConfig config,
    ILogger<ResilientHttpClientFactory> logger)
{
    public HttpClient Create()
    {
        var retryPolicy = CreateRetryPolicy();
        var handler = new PolicyHttpMessageHandler(retryPolicy)
        {
            InnerHandler = new SocketsHttpHandler()
        };
        return new HttpClient(handler);
    }

    private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => IsTransientOrRateLimit(r.StatusCode))
            .WaitAndRetryAsync(
                config.MaxRetries,
                retryAttempt => CalculateDelay(retryAttempt),
                onRetry: (outcome, delay, retryCount, _) =>
                    logger.LogWarning(
                        "Retry {Count}/{Max} after {Delay}ms (HTTP {Status})",
                        retryCount, config.MaxRetries, delay.TotalMilliseconds,
                        outcome.Result?.StatusCode));
    }
}
```

### Hinweise
- `PolicyHttpMessageHandler` kommt aus `Microsoft.Extensions.Http.Polly`
- Alternative: Direkt `Polly` nutzen ohne Microsoft.Extensions.Http
- Prüfen ob `Microsoft.Extensions.Http.Resilience` oder `Polly` besser passt
- Der HttpClient wird an `new AnthropicClient(apiKey, httpClient)` übergeben

---

## ClaudeAgentProvider Anpassung

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

### Änderungen

**Constructor:** Erweitern um `RetryConfig retryConfig`

```csharp
public sealed class ClaudeAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    ILogger<ClaudeAgentProvider> logger) : IAgentProvider
```

**Client-Erstellung:** Statt `new AnthropicClient(apiKey)`:

```csharp
private AnthropicClient CreateClient()
{
    var factory = new ResilientHttpClientFactory(retryConfig, ...);
    var httpClient = factory.Create();
    return new AnthropicClient(apiKey, httpClient);
}
```

**Beide Methoden** (`GeneratePlanAsync`, `ExecutePlanAsync`) nutzen `CreateClient()`.

---

## AgenticLoop Anpassung

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

### Änderungen

Nach jedem API-Call: Token-Usage loggen.

```csharp
private void LogUsage(MessageResponse response, int iteration)
{
    var usage = response.Usage;
    logger.LogDebug(
        "Iteration {Iter}: Input={Input}, Output={Output}, CacheCreate={CacheCreate}, CacheRead={CacheRead}",
        iteration,
        usage.InputTokens,
        usage.OutputTokens,
        usage.CacheCreationInputTokens ?? 0,
        usage.CacheReadInputTokens ?? 0);
}
```

### Hinweise
- Noch kein TokenUsageTracker (kommt Phase 7)
- Erstmal nur Logging pro Iteration für Observability
- `Usage.CacheCreationInputTokens` und `CacheReadInputTokens` sind `int?`

---

## AgentProviderFactory Anpassung

```
Datei: src/AgentSmith.Infrastructure/Factories/AgentProviderFactory.cs
```

`CreateClaude` reicht `config.Retry` durch:

```csharp
private ClaudeAgentProvider CreateClaude(AgentConfig config)
{
    var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
    return new ClaudeAgentProvider(
        apiKey, config.Model, config.Retry,
        loggerFactory.CreateLogger<ClaudeAgentProvider>());
}
```

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
```

---

## Tests

**RetryConfigTests:**
- `Defaults_AreReasonable` - Default-Werte prüfen
- `YamlDeserialization_Works` - Config aus YAML laden

**ResilientHttpClientFactoryTests:**
- `Create_ReturnsHttpClient` - Nicht null
- `RetryPolicy_Retries429` - Mock HTTP, prüfe dass 429 geretried wird

**AgentProviderFactoryTests:**
- Bestehende Tests anpassen für neuen Constructor-Parameter

**DiRegistrationTests:**
- Alle bestehenden Tests müssen weiter grün sein
