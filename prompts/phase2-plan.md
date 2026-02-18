# Phase 2: Free Commands (Stubs) - Implementierungsplan

## Ziel
Alle Free Command Contexts + Handler als Stubs implementieren.
Handler haben echte Signaturen, Logging und Error Handling, aber TODO-Bodies für die Provider-Aufrufe.
Zusätzlich: `CommandExecutor` als echte Implementierung (das Herzstück der DI-Auflösung).

Nach Phase 2: Pipeline ist "verdrahtet" - Commands laufen durch, tun aber noch nichts Echtes.

---

## Vorbedingung
- Phase 1 abgeschlossen (Solution kompiliert, Contracts definiert)

## Schritte

### Schritt 1: CommandExecutor Implementierung
Siehe: `prompts/phase2-executor.md`

Die zentrale Klasse die `ICommandHandler<TContext>` per DI auflöst.
Einzige nicht-Stub Implementierung in Phase 2.
Projekt: `AgentSmith.Application`

### Schritt 2: Command Contexts
Siehe: `prompts/phase2-contexts.md`

Alle 9 Free Command Context Records definieren.
Projekt: `AgentSmith.Application/Commands/Contexts/`

### Schritt 3: Command Handlers (Stubs)
Siehe: `prompts/phase2-handlers.md`

Alle 9 Free Command Handler mit:
- Echtem Constructor (DI-fähig, nimmt Factories/Providers)
- Logging (ILogger)
- Guard Clauses
- TODO im Body für die echte Logik
- Schreibt Dummy-Daten in PipelineContext
Projekt: `AgentSmith.Application/Commands/Handlers/`

### Schritt 4: DI Registration
`ServiceCollectionExtensions` in Application für Handler-Registrierung.

### Schritt 5: Tests
- CommandExecutor Tests (mit Mock-Handlern)
- Mindestens 1 Handler-Stub Test (Logging, Guard Clauses)

### Schritt 6: Verify
```bash
dotnet build
dotnet test
```

---

## Abhängigkeiten

```
Schritt 1 (CommandExecutor)
    └── Schritt 2 (Contexts) ← können parallel zu 1
         └── Schritt 3 (Handlers) ← braucht Contexts + Executor-Interface
              └── Schritt 4 (DI Registration)
                   └── Schritt 5 (Tests)
                        └── Schritt 6 (Verify)
```

Schritt 1 und 2 können parallel, der Rest sequentiell.

---

## NuGet Packages (Phase 2)

| Projekt | Package | Zweck |
|---------|---------|-------|
| AgentSmith.Application | Microsoft.Extensions.Logging.Abstractions | ILogger<T> |
| AgentSmith.Application | Microsoft.Extensions.DependencyInjection.Abstractions | IServiceCollection Extensions |

---

## Definition of Done (Phase 2)
- [ ] CommandExecutor löst Handler per DI auf und ruft ExecuteAsync
- [ ] Alle 9 Free Command Contexts definiert
- [ ] Alle 9 Free Command Handlers als Stubs implementiert
- [ ] Jeder Handler loggt Start/Ende und hat Guard Clauses
- [ ] DI Registration für alle Handler vorhanden
- [ ] CommandExecutor Tests grün
- [ ] `dotnet build` + `dotnet test` fehlerfrei
- [ ] Alle Dateien halten sich an Coding Principles (20/120, Englisch)
