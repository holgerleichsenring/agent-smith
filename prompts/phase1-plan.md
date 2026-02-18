# Phase 1: Core Infrastructure - Implementierungsplan

## Ziel
Fundament des Projekts: Solution Structure, Domain Entities, alle Contracts (nur Interfaces), Config Loader.
Nach Phase 1 kompiliert die Solution, hat aber keine funktionale Logik.

---

## Vorbedingung
- .NET 8 SDK installiert
- Dieses Repo geklont

## Schritte

### Schritt 1: Solution & Projekte anlegen
Siehe: `prompts/phase1-solution-structure.md`

Erstelle die .NET Solution mit allen Projekten und korrekten Referenzen.
Ergebnis: `dotnet build` läuft durch (leer, aber fehlerfrei).

### Schritt 2: Domain Entities & Value Objects
Siehe: `prompts/phase1-domain.md`

Die Kerntypen des Systems. Reine Datenmodelle ohne Infrastruktur-Abhängigkeiten.
- Entities: `Ticket`, `Repository`, `Plan`, `CodeChange`, `CodeAnalysis`
- Value Objects: `TicketId`, `ProjectName`, `BranchName`, `FilePath`, `CommandResult`
- Exceptions: `AgentSmithException`, `TicketNotFoundException`, `ConfigurationException`

### Schritt 3: Contracts (Interfaces)
Siehe: `prompts/phase1-contracts.md`

Alle Interfaces die das System definieren. Keine Implementierung.
- Command Pattern (MediatR-Style): `ICommandContext`, `ICommandHandler<TContext>`, `ICommandExecutor`
- Shared State: `PipelineContext`, `ContextKeys`
- Providers: `ITicketProvider`, `ISourceProvider`, `IAgentProvider`
- Services: `IPipelineExecutor`, `IIntentParser`, `IConfigurationLoader`
- Factories: `ITicketProviderFactory`, `ISourceProviderFactory`, `IAgentProviderFactory`

### Schritt 4: Configuration
Siehe: `prompts/phase1-config.md`

YAML-basierte Konfiguration laden und als stark typisierte Objekte bereitstellen.
- Config Models: `AgentSmithConfig`, `ProjectConfig`, `SourceConfig`, `TicketConfig`, `AgentConfig`, `PipelineConfig`
- `YamlConfigurationLoader` implementierung (einzige Implementierung in Phase 1)
- Template `agentsmith.yml` als Beispiel

### Schritt 5: Verify
```bash
dotnet build
dotnet test  # Leere Test-Suite, aber muss durchlaufen
```

---

## Abhängigkeiten zwischen Schritten

```
Schritt 1 (Solution)
    └── Schritt 2 (Domain)
         └── Schritt 3 (Contracts) ← braucht Domain-Typen
              └── Schritt 4 (Config) ← braucht Contracts
                   └── Schritt 5 (Verify)
```

Strikt sequentiell. Jeder Schritt baut auf dem vorherigen auf.

---

## Projekt-Referenzen nach Phase 1

```
AgentSmith.Domain          → (keine Abhängigkeiten)
AgentSmith.Contracts       → AgentSmith.Domain
AgentSmith.Application     → AgentSmith.Contracts, AgentSmith.Domain
AgentSmith.Infrastructure  → AgentSmith.Contracts, AgentSmith.Domain
AgentSmith.Host            → AgentSmith.Application, AgentSmith.Infrastructure
AgentSmith.Tests           → AgentSmith.Domain, AgentSmith.Contracts, AgentSmith.Infrastructure
```

---

## NuGet Packages (Phase 1)

| Projekt | Package | Zweck |
|---------|---------|-------|
| AgentSmith.Infrastructure | YamlDotNet | YAML Config laden |
| AgentSmith.Host | Microsoft.Extensions.DependencyInjection | DI Container |
| AgentSmith.Host | Microsoft.Extensions.Logging.Console | Logging |
| AgentSmith.Tests | xunit | Test Framework |
| AgentSmith.Tests | xunit.runner.visualstudio | Test Runner |
| AgentSmith.Tests | Microsoft.NET.Test.Sdk | Test Infrastructure |
| AgentSmith.Tests | Moq | Mocking |
| AgentSmith.Tests | FluentAssertions | Readable Assertions |

---

## Definition of Done (Phase 1)
- [ ] Solution kompiliert fehlerfrei
- [ ] Alle Domain Entities mit Properties definiert
- [ ] Alle Value Objects als Records definiert
- [ ] Alle Interfaces in Contracts definiert
- [ ] Config Loader liest YAML und gibt typisierte Config zurück
- [ ] Beispiel `agentsmith.yml` vorhanden
- [ ] `coding-principles.md` vorhanden
- [ ] Mindestens 1 Unit Test (Config Loader)
- [ ] Alle Dateien halten sich an Coding Principles (20/120 Regel)
