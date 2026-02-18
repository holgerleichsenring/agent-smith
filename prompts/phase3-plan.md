# Phase 3: Providers - Implementierungsplan

## Ziel
Echte Provider-Implementierungen + Factories.
Handler-Stubs aus Phase 2 werden mit echten Provider-Aufrufen verdrahtet.
Nach Phase 3: Die Pipeline kann echte Tickets holen, Repos auschecken und Code generieren.

---

## Vorbedingung
- Phase 2 abgeschlossen (alle Handler-Stubs, CommandExecutor funktioniert)

## Reihenfolge (laut Architecture)

Die Reihenfolge ist bewusst gewählt - jeder Provider kann isoliert getestet werden.

### Schritt 1: Provider Factories
Siehe: `prompts/phase3-factories.md`

Factories für alle drei Provider-Typen. Lösen den richtigen Provider anhand `config.Type` auf.
Projekt: `AgentSmith.Infrastructure/Factories/`

### Schritt 2: Ticket Providers
Siehe: `prompts/phase3-tickets.md`

Erster Provider: AzureDevOpsTicketProvider (mit Azure DevOps SDK).
Dann: GitHubTicketProvider (mit Octokit).
Optional Phase 3: JiraTicketProvider.
Plus: FetchTicketHandler von Stub → echte Implementierung.
Projekt: `AgentSmith.Infrastructure/Providers/Tickets/`

### Schritt 3: Source Providers
Siehe: `prompts/phase3-source.md`

LocalSourceProvider (Dateisystem + LibGit2Sharp).
Dann: GitHubSourceProvider (Octokit für PRs, LibGit2Sharp für Git).
Plus: CheckoutSourceHandler + CommitAndPRHandler von Stub → echt.
Projekt: `AgentSmith.Infrastructure/Providers/Source/`

### Schritt 4: Agent Provider (Agentic Loop)
Siehe: `prompts/phase3-agent.md`

ClaudeAgentProvider mit Anthropic SDK. Das Herzstück.
- Tool Definitions (read_file, write_file, list_files, run_command)
- Agentic Loop (send → tool calls → execute → send back → repeat)
- Plan Generation + Plan Execution
Plus: GeneratePlanHandler + AgenticExecuteHandler von Stub → echt.
Projekt: `AgentSmith.Infrastructure/Providers/Agent/`

### Schritt 5: Remaining Handlers verdrahten
AnalyzeCodeHandler + TestHandler von Stub → echt.
Diese brauchen keine Provider, nur Dateisystem/Process-Aufrufe.

### Schritt 6: Tests
- Unit Tests für jede Factory
- Unit Tests für jeden Provider (mit gemockten HTTP Clients)
- Integration Tests für LocalSourceProvider (echtes Dateisystem)

### Schritt 7: Verify
```bash
dotnet build
dotnet test
```

---

## NuGet Packages (Phase 3)

| Projekt | Package | Zweck |
|---------|---------|-------|
| AgentSmith.Infrastructure | Anthropic.SDK | Claude API |
| AgentSmith.Infrastructure | Octokit | GitHub API |
| AgentSmith.Infrastructure | LibGit2Sharp | Git Operations |
| AgentSmith.Infrastructure | Microsoft.TeamFoundationServer.Client | Azure DevOps API |
| AgentSmith.Infrastructure | Microsoft.Extensions.Logging.Abstractions | ILogger<T> |

---

## Abhängigkeiten

```
Schritt 1 (Factories)
    ├── Schritt 2 (Tickets) ← braucht TicketProviderFactory
    ├── Schritt 3 (Source) ← braucht SourceProviderFactory
    └── Schritt 4 (Agent) ← braucht AgentProviderFactory
         └── Schritt 5 (Remaining Handlers)
              └── Schritt 6 (Tests)
                   └── Schritt 7 (Verify)
```

Schritte 2, 3, 4 können theoretisch parallel, aber sequentiell ist sicherer wegen SDK-Konflikten.

---

## Definition of Done (Phase 3)
- [ ] Alle drei Factory-Implementierungen vorhanden
- [ ] AzureDevOpsTicketProvider + GitHubTicketProvider implementiert
- [ ] LocalSourceProvider + GitHubSourceProvider implementiert
- [ ] ClaudeAgentProvider mit Agentic Loop implementiert
- [ ] Alle Handler-Stubs durch echte Implementierungen ersetzt
- [ ] Unit Tests für Factories + Providers
- [ ] `dotnet build` + `dotnet test` fehlerfrei
- [ ] Alle Dateien halten sich an Coding Principles (20/120, Englisch)
