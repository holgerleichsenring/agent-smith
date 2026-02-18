# Phase 9: Model Registry - Implementierungsdetails

## Überblick
Das Model Registry bildet Task-Typen auf konkrete Model-Zuweisungen ab.
Statt überall dasselbe Model zu verwenden, wählt der Agent das passende Model
je nach Aufgabe: günstig für Discovery, leistungsstark für Coding.

---

## TaskType Enum (Contracts Layer)

```csharp
// Providers/TaskType.cs
public enum TaskType
{
    Scout,          // File discovery, codebase exploration (Haiku)
    Primary,        // Main coding tasks (Sonnet)
    Planning,       // Plan generation (Sonnet)
    Reasoning,      // Complex architecture decisions (Opus, optional)
    Summarization   // Context compaction (Haiku)
}
```

---

## ModelAssignment (Contracts Layer)

```csharp
// Configuration/ModelAssignment.cs
public class ModelAssignment
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 8192;
}
```

---

## ModelRegistryConfig (Contracts Layer)

```csharp
// Configuration/ModelRegistryConfig.cs
public class ModelRegistryConfig
{
    public ModelAssignment Scout { get; set; } = new()
    {
        Model = "claude-haiku-4-5-20251001",
        MaxTokens = 4096
    };
    public ModelAssignment Primary { get; set; } = new()
    {
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 8192
    };
    public ModelAssignment Planning { get; set; } = new()
    {
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 4096
    };
    public ModelAssignment? Reasoning { get; set; }
    public ModelAssignment Summarization { get; set; } = new()
    {
        Model = "claude-haiku-4-5-20251001",
        MaxTokens = 2048
    };
}
```

---

## IModelRegistry (Contracts Layer)

```csharp
// Providers/IModelRegistry.cs
public interface IModelRegistry
{
    ModelAssignment GetModel(TaskType taskType);
}
```

Einfaches Interface - gibt für einen TaskType die passende ModelAssignment zurück.

---

## ConfigBasedModelRegistry (Infrastructure Layer)

```csharp
// Providers/Agent/ConfigBasedModelRegistry.cs
```

### Implementierung
- Bekommt `ModelRegistryConfig` im Konstruktor
- Mappt `TaskType` auf die entsprechende Property in der Config
- Wenn `Reasoning` null ist und angefragt wird → Fallback auf `Primary`
- Logging bei Model-Auswahl (Debug-Level)

### Fallback-Strategie
```
TaskType.Scout          → config.Scout
TaskType.Primary        → config.Primary
TaskType.Planning       → config.Planning
TaskType.Reasoning      → config.Reasoning ?? config.Primary (Fallback)
TaskType.Summarization  → config.Summarization
```

---

## Integration in AgentConfig

```csharp
public class AgentConfig
{
    // ... existing properties ...
    public ModelRegistryConfig? Models { get; set; }  // nullable für Backward-Compat
}
```

Wenn `Models` null ist, wird das einzelne `Model`-Feld für ALLE TaskTypes verwendet.
Dies stellt sicher, dass bestehende agentsmith.yml-Dateien ohne Änderung funktionieren.

---

## Integration in ClaudeAgentProvider

Der Provider nutzt die Registry für:
1. `GeneratePlanAsync` → `TaskType.Planning`
2. `ExecutePlanAsync` → `TaskType.Primary` (für AgenticLoop)
3. Scout → `TaskType.Scout` (wenn Models konfiguriert)
4. CompactionConfig.SummaryModel wird von `TaskType.Summarization` abgelöst

---

## Config in agentsmith.yml

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514  # Fallback wenn models nicht gesetzt
  models:
    scout:
      model: claude-haiku-4-5-20251001
      max_tokens: 4096
    primary:
      model: claude-sonnet-4-20250514
      max_tokens: 8192
    planning:
      model: claude-sonnet-4-20250514
      max_tokens: 4096
    summarization:
      model: claude-haiku-4-5-20251001
      max_tokens: 2048
```
