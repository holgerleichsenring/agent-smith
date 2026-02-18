# Phase 9: Scout Agent - Implementierungsdetails

## Überblick
Der Scout Agent ist eine leichtgewichtige, Haiku-basierte File-Discovery-Phase,
die VOR der eigentlichen Coding-Phase läuft. Er identifiziert relevante Dateien
und sammelt Kontext, damit der teurere Primary-Agent (Sonnet) direkt mit den
richtigen Dateien arbeiten kann.

---

## ScoutResult Record (Infrastructure Layer)

```csharp
// Providers/Agent/ScoutResult.cs
public sealed record ScoutResult(
    IReadOnlyList<string> RelevantFiles,
    string ContextSummary,
    int TokensUsed);
```

---

## ScoutAgent (Infrastructure Layer)

```csharp
// Providers/Agent/ScoutAgent.cs
```

### Konstruktor
```csharp
public sealed class ScoutAgent(
    AnthropicClient client,
    string model,
    int maxTokens,
    ILogger logger)
```

### Methode
```csharp
public async Task<ScoutResult> DiscoverAsync(
    Plan plan,
    string repositoryPath,
    CancellationToken cancellationToken = default)
```

### Algorithmus
1. Erstellt eigenen ToolExecutor mit NUR read-only Tools
2. Führt eine kurze Agentic Loop (max 5 Iterationen)
3. System-Prompt instruiert: "Explore the codebase, identify relevant files"
4. User-Prompt enthält: Plan-Summary, Plan-Steps, Repository-Pfad
5. Sammelt alle gelesenen Datei-Pfade via FileReadTracker
6. Extrahiert die finale Text-Antwort als ContextSummary
7. Gibt ScoutResult zurück

### System-Prompt
```
You are a codebase scout. Your job is to explore the repository and identify
all files relevant to the implementation plan below.

Instructions:
- Use list_files to understand the project structure
- Use read_file to examine files that might be relevant
- Focus on files that will need to be created or modified
- Also examine related files (imports, dependencies, tests)
- When done, provide a brief summary of what you found and which files are relevant

Do NOT modify any files. You are read-only.
```

---

## ScoutTools (ToolDefinitions Extension)

```csharp
// Addition to ToolDefinitions.cs
public static List<Tool> ScoutTools => new()
{
    // read_file - same as existing
    // list_files - same as existing
    // NO write_file
    // NO run_command
};
```

Scout bekommt nur 2 der 4 Tools. Kein Schreibzugriff, keine Command-Ausführung.

---

## Integration in ClaudeAgentProvider.ExecutePlanAsync

### Ablauf mit Scout
```
1. Wenn Models konfiguriert UND Scout-Model gesetzt:
   a. ScoutAgent.DiscoverAsync(plan, repoPath)
   b. Scout-Ergebnis (RelevantFiles + ContextSummary) wird dem User-Prompt hinzugefügt
   c. Primary-Agent startet mit vorgeladenem Kontext

2. Wenn kein Scout konfiguriert:
   a. Verhalten wie bisher (Primary macht alles)
```

### Erweiterter User-Prompt für Primary
```
Execute the following implementation plan in repository at: {repoPath}
Branch: {branch}

## Scout Results
The following files have been identified as relevant:
{relevantFiles}

Scout Summary: {contextSummary}

## Plan
...
```

### Vorteil
- Scout (Haiku): ~$1/MT Input, 5 Iterationen File-Discovery → ~$0.01
- Ohne Scout: Primary (Sonnet) macht File-Discovery selbst → ~$0.10 für die gleichen Iterationen
- 10x Kostenreduktion für die Discovery-Phase
- Primary startet direkt mit relevantem Kontext → weniger Iterationen insgesamt

---

## Backward-Kompatibilität
- `AgentConfig.Models` ist nullable
- Wenn null → kein Scout, kein Registry, alles wie in Phase 8
- Bestehende agentsmith.yml funktioniert ohne Änderung
- Scout-Phase wird nur aktiviert wenn explizit konfiguriert
