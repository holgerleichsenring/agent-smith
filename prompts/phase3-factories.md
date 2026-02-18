# Phase 3 - Schritt 1: Provider Factories

## Ziel
Factories implementieren die anhand des `Type`-Feldes in der Config den richtigen Provider instanziieren.
Projekt: `AgentSmith.Infrastructure/Factories/`

---

## TicketProviderFactory
```
Datei: src/AgentSmith.Infrastructure/Factories/TicketProviderFactory.cs
```

```csharp
public sealed class TicketProviderFactory(IServiceProvider serviceProvider)
    : ITicketProviderFactory
{
    public ITicketProvider Create(TicketConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "azuredevops" => CreateAzureDevOps(config),
            "github" => CreateGitHub(config),
            "jira" => throw new NotSupportedException("Jira provider not yet implemented"),
            _ => throw new ConfigurationException($"Unknown ticket provider: {config.Type}")
        };
    }
}
```

**Verhalten:**
- Switch auf `config.Type` (case-insensitive)
- Instanziiert den passenden Provider mit den Config-Werten
- Secrets (Token etc.) werden aus Environment Variables gelesen
- Unbekannter Typ → `ConfigurationException`

---

## SourceProviderFactory
```
Datei: src/AgentSmith.Infrastructure/Factories/SourceProviderFactory.cs
```

**Verhalten:**
- `"local"` → `LocalSourceProvider(config.Path)`
- `"github"` → `GitHubSourceProvider(config.Url, token)`
- `"gitlab"` → `throw new NotSupportedException(...)` (Phase 3 Scope)
- `"azurerepos"` → `throw new NotSupportedException(...)` (Phase 3 Scope)

---

## AgentProviderFactory
```
Datei: src/AgentSmith.Infrastructure/Factories/AgentProviderFactory.cs
```

**Verhalten:**
- `"claude"` → `ClaudeAgentProvider(apiKey, config.Model)`
- `"openai"` → `throw new NotSupportedException(...)` (Phase 3 Scope)

---

## Secrets-Auflösung

Die Factories lesen API Keys aus dem DI Container.
Dafür wird eine `SecretsProvider` Klasse registriert die Environment Variables kapselt.

```
Datei: src/AgentSmith.Infrastructure/Configuration/SecretsProvider.cs
```

```csharp
public sealed class SecretsProvider
{
    public string GetRequired(string envVarName)
    {
        return Environment.GetEnvironmentVariable(envVarName)
            ?? throw new ConfigurationException(
                $"Required environment variable '{envVarName}' is not set.");
    }

    public string? GetOptional(string envVarName)
    {
        return Environment.GetEnvironmentVariable(envVarName);
    }
}
```

---

## Hinweise
- Factories als `sealed` Klassen.
- `IServiceProvider` per Constructor Injection für Zugriff auf Logger, Secrets etc.
- Nicht implementierte Provider werfen `NotSupportedException` mit klarer Meldung.
- Factories werden als Singleton registriert.
