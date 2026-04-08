# Phase 23 (Update): Multi-Repo Support

> Ursprüngliches Konzept aus p26 ist architektonisch korrekt und aktuell.
> Dieses Dokument präzisiert und ergänzt es auf Basis von Phase 58 (Dialogue).

---

## Ziel

Agent Smith bearbeitet ein Ticket das mehrere Repositories betrifft.
Ein Ticket → mehrere PRs → koordinierter Dialogue mit dem Menschen → alles verlinkt.

---

## Was sich seit dem ursprünglichen Entwurf geändert hat

Das ursprüngliche p23-Konzept ist strukturell richtig. Zwei Dinge kommen hinzu:

1. **Dialogue-Integration (Phase 58):** Zwischen Primary und Consumer steht
   jetzt ein typisierter Approval-Schritt — kein blindes Weiterschalten.

2. **Monorepo als Sonderfall:** Ein Monorepo ist Multi-Repo ohne Checkout-Wechsel.
   Dieselbe Architektur, aber `CheckoutSource` läuft einmal und der Agent
   bekommt unterschiedliche Working-Directories innerhalb desselben Repos.

---

## Config (aktuell, backward-compatible)

```yaml
# Bestehend — bleibt unverändert, kein Bruch:
projects:
  my-api:
    ticket_provider: github
    source_provider: github
    repo: owner/my-api

# Neu — optional:
project-groups:
  platform-update:
    description: "Shared contract + consuming services"
    confirm_at_start: true          # Bestätigung bevor Primary startet
    confirm_between_repos: true     # Bestätigung nach Primary, vor Consumer
    repos:
      - project: my-api
        role: primary
      - project: service-a
        role: consumer
        depends_on: []              # startet parallel zu service-b
      - project: service-b
        role: consumer
        depends_on: []
    strategy: sequential            # oder: parallel, dependency-order

# Monorepo-Variante:
project-groups:
  monorepo-update:
    monorepo: true                  # NEU: kein separater Checkout pro Repo
    root_repo: my-monorepo
    repos:
      - path: packages/api
        role: primary
      - path: packages/service-a
        role: consumer
        depends_on: [packages/api]
      - path: packages/service-b
        role: consumer
        depends_on: [packages/api]
    strategy: dependency-order
    confirm_between_repos: false    # In Monorepos oft nicht nötig
```

---

## Ablauf mit Dialogue

```
Schritt 1: Intent-Parsing erkennt Ticket gehört zu project-group "platform-update"

Schritt 2 (wenn confirm_at_start=true):
  DialogQuestion (Confirmation):
  "Ticket #123 betrifft 3 Repos: my-api (primary), service-a, service-b.
   Soll ich mit my-api beginnen?"
  [✅ Ja]  [⚙️ Nur my-api bearbeiten]  [❌ Abbrechen]

Schritt 3: Primary-Pipeline läuft vollständig durch
  → PR #42 in my-api erstellt
  → Diff extrahiert (interface changes, new endpoints)

Schritt 4 (wenn confirm_between_repos=true):
  DialogQuestion (Approval):
  "my-api abgeschlossen. PR #42 erstellt.
   Änderungen: +180 Zeilen, 2 neue Interfaces, 1 geändertes DTO.
   Soll ich jetzt service-a und service-b anpassen?"
  [✅ Alle anpassen]
  [1️⃣ Nur service-a]
  [2️⃣ Nur service-b]
  [❌ Stopp]
  💬 Optionaler Kommentar

Schritt 5: Consumer-Pipelines mit Cross-Repo-Context
  System-Prompt-Erweiterung für jeden Consumer:
  ---
  ## Cross-Repo Context
  The following changes were made in `my-api` (PR #42) as part of this ticket:

  {git diff --stat}
  {interface changes as extracted text}

  Adapt this service to be compatible with these changes.
  Preserve existing behavior — only update what breaks due to the API change.
  ---

Schritt 6: Ticket-Writeback
  "Agent Smith abgeschlossen:
   - my-api: PR #42 (primary changes)
   - service-a: PR #67 (consumer adaptation)
   - service-b: PR #68 (consumer adaptation)"
```

---

## Architektur

### MultiRepoPipelineExecutor

```csharp
// AgentSmith.Application/Services/MultiRepoPipelineExecutor.cs

public sealed class MultiRepoPipelineExecutor(
    IPipelineExecutor singleRepoExecutor,
    IPlatformAdapter adapter,
    IDialogueTransport dialogue,
    IDialogueTrail trail,
    ILogger<MultiRepoPipelineExecutor> logger)
{
    public async Task<MultiRepoResult> ExecuteAsync(
        ProjectGroupConfig group,
        TicketId ticketId,
        PipelineContext sharedContext,
        CancellationToken ct)
    {
        var results = new List<RepoResult>();

        // Optional: Bestätigung vor Start
        if (group.ConfirmAtStart)
        {
            var start = await AskStartConfirmationAsync(group, sharedContext, ct);
            if (!start) return MultiRepoResult.Cancelled("Abgebrochen vor Start");
        }

        // Primary
        var primary = group.Repos.Single(r => r.Role == RepoRole.Primary);
        var primaryResult = await singleRepoExecutor.ExecuteAsync(
            primary.Project, sharedContext, ct);
        results.Add(new RepoResult(primary, primaryResult));

        if (!primaryResult.Success)
            return MultiRepoResult.FailedOnPrimary(primaryResult);

        // Cross-Repo-Context extrahieren
        var crossContext = ExtractCrossRepoContext(primaryResult);

        // Optional: Bestätigung vor Consumers
        if (group.ConfirmBetweenRepos)
        {
            var proceed = await AskConsumerConfirmationAsync(
                group, primaryResult, sharedContext, ct);
            if (!proceed.ShouldContinue)
                return MultiRepoResult.PartialSuccess(results, proceed.SelectedRepos);
        }

        // Consumers (sequential oder parallel je nach strategy)
        var consumers = group.Repos.Where(r => r.Role == RepoRole.Consumer);
        foreach (var consumer in ResolveOrder(consumers, group.Strategy))
        {
            var consumerContext = sharedContext.WithCrossRepoContext(crossContext);
            var consumerResult = await singleRepoExecutor.ExecuteAsync(
                consumer.Project, consumerContext, ct);
            results.Add(new RepoResult(consumer, consumerResult));
        }

        return MultiRepoResult.Success(results);
    }
}
```

### CrossRepoContext

```csharp
public sealed record CrossRepoContext(
    string SourceRepo,
    string PrUrl,
    string DiffSummary,         // git diff --stat output
    string InterfaceChanges,    // LLM-extrahiert: neue/geänderte public APIs
    IReadOnlyList<string> LinkedPrUrls);
```

`InterfaceChanges` wird nach dem Primary-PR via einmaligem Haiku-Call extrahiert:

```
Extrahiere alle public API-Änderungen aus diesem Git-Diff.
Fokus auf: neue Interfaces, geänderte Methodensignaturen, neue DTOs, breaking changes.
Kompakt, maximal 500 Zeichen. Für AI-Agenten-Context optimiert.
```

### Monorepo-Modus

Bei `monorepo: true`:
- `CheckoutSource` läuft einmal für `root_repo`
- `AgenticExecute` bekommt `WorkingDirectory` = `{repoRoot}/{path}`
- Kein separates Checkout/PR pro Consumer — ein PR für alle Änderungen
  (oder separate Commits auf demselben Branch, je nach Config)

```yaml
# Monorepo: ein PR, mehrere Commits
monorepo_pr_strategy: single_pr    # oder: per_package
```

---

## Ticket-Writeback (Multi-Repo)

```csharp
// Bestehender ITicketWriteback bekommt Multi-PR-Support:
public sealed record TicketWritebackResult(
    TicketId TicketId,
    IReadOnlyList<PrReference> PullRequests,  // NEU: Liste statt einzelner PR
    string Summary);

public sealed record PrReference(
    string Repo,
    string PrUrl,
    RepoRole Role);       // Primary oder Consumer
```

Kommentar im Ticket:
```
Agent Smith — Abgeschlossen ✅

PRs erstellt:
- [my-api #42](url) — Primary Changes
- [service-a #67](url) — Consumer Adaptation
- [service-b #68](url) — Consumer Adaptation

Dialogue Trail: 2 Fragen, 2 menschliche Antworten
Kosten: $1.24 gesamt (my-api: $0.67, service-a: $0.31, service-b: $0.26)
```

---

## Definition of Done

- [ ] `ProjectGroupConfig` mit `ConfirmAtStart`, `ConfirmBetweenRepos`, `MonorepoMode`
- [ ] `MultiRepoPipelineExecutor` — sequential + parallel strategy
- [ ] `CrossRepoContext` — Extraktion via Haiku nach Primary-PR
- [ ] Dialogue-Integration: Approval vor Primary (optional) und vor Consumers
- [ ] Monorepo-Modus: ein Checkout, mehrere Working-Directories
- [ ] Ticket-Writeback: alle PRs aufgelistet, Kosten pro Repo
- [ ] Bestehende Single-Repo-Pipelines: zero Regression
- [ ] Unit Tests: Config-Parsing, CrossRepoContext-Extraktion, Strategy-Ordering
- [ ] Integration Test: 2 Repos, sequential, mit Dialogue-Mock

---

## Abhängigkeiten

```
Phase 58 (Dialogue) — Schritt 1+2 müssen fertig sein
Phase 22 (CCS Bootstrap) — Project-Detection für Consumer-Repos
```
