# Phase 3 - Step 1: Provider Factories

## Goal
Implement factories that instantiate the correct provider based on the `Type` field in the config.
Project: `AgentSmith.Infrastructure/Factories/`

---

## TicketProviderFactory
```
File: src/AgentSmith.Infrastructure/Factories/TicketProviderFactory.cs
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

**Behavior:**
- Switch on `config.Type` (case-insensitive)
- Instantiates the matching provider with the config values
- Secrets (token etc.) are read from environment variables
- Unknown type → `ConfigurationException`

---

## SourceProviderFactory
```
File: src/AgentSmith.Infrastructure/Factories/SourceProviderFactory.cs
```

**Behavior:**
- `"local"` → `LocalSourceProvider(config.Path)`
- `"github"` → `GitHubSourceProvider(config.Url, token)`
- `"gitlab"` → `throw new NotSupportedException(...)` (Phase 3 scope)
- `"azurerepos"` → `throw new NotSupportedException(...)` (Phase 3 scope)

---

## AgentProviderFactory
```
File: src/AgentSmith.Infrastructure/Factories/AgentProviderFactory.cs
```

**Behavior:**
- `"claude"` → `ClaudeAgentProvider(apiKey, config.Model)`
- `"openai"` → `throw new NotSupportedException(...)` (Phase 3 scope)

---

## Secrets Resolution

The factories read API keys from the DI container.
For this, a `SecretsProvider` class is registered that wraps environment variables.

```
File: src/AgentSmith.Infrastructure/Configuration/SecretsProvider.cs
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

## Notes
- Factories as `sealed` classes.
- `IServiceProvider` via constructor injection for access to logger, secrets, etc.
- Unimplemented providers throw `NotSupportedException` with a clear message.
- Factories are registered as singletons.
