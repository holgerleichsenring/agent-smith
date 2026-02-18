# Agent Smith - Prompts Index

Dieses Verzeichnis ist die Single Source of Truth für Architektur, Planung und Coding Standards.
Jede Änderung am Design wird hier reflektiert.

---

## Übersicht

| Datei | Inhalt | Status |
|-------|--------|--------|
| `architecture.md` | Gesamtarchitektur, Business Model, Tech Stack | Stabil |
| `coding-principles.md` | Code-Qualitätsregeln (wird vom Agent geladen) | Stabil |
| `phase1-plan.md` | Phase 1 Übersicht und Abhängigkeiten | Erledigt |
| `phase1-solution-structure.md` | Schritt 1: .NET Solution anlegen | Erledigt |
| `phase1-domain.md` | Schritt 2: Domain Entities & Value Objects | Erledigt |
| `phase1-contracts.md` | Schritt 3: MediatR-Style Command Pattern & Interfaces | Erledigt |
| `phase1-config.md` | Schritt 4: YAML Config Loader | Erledigt |
| `phase2-plan.md` | Phase 2 Übersicht: Commands + Executor | Erledigt |
| `phase2-executor.md` | CommandExecutor Implementierung | Erledigt |
| `phase2-contexts.md` | Alle 9 Command Context Records | Erledigt |
| `phase2-handlers.md` | Alle 9 Command Handler Stubs + DI Registration | Erledigt |
| `phase3-plan.md` | Phase 3 Übersicht: Providers + Factories | Erledigt |
| `phase3-factories.md` | Provider Factories (Ticket, Source, Agent) | Erledigt |
| `phase3-tickets.md` | AzureDevOps + GitHub Ticket Providers | Erledigt |
| `phase3-source.md` | Local + GitHub Source Providers | Erledigt |
| `phase3-agent.md` | Claude Agent Provider (Plan + Execution) | Erledigt |
| `phase3-agentic-loop.md` | Agentic Loop Detail (Tools, Loop, Security) | Erledigt |
| `phase4-plan.md` | Phase 4 Übersicht: Pipeline Execution | Erledigt |
| `phase4-intent-parser.md` | RegexIntentParser (User Input → Intent) | Erledigt |
| `phase4-pipeline-executor.md` | PipelineExecutor + CommandContextFactory | Erledigt |
| `phase4-use-case.md` | ProcessTicketUseCase (Orchestrierung) | Erledigt |
| `phase4-di-wiring.md` | DI Registration + Host Program.cs | Erledigt |
| `phase5-plan.md` | Phase 5 Übersicht: CLI & Docker | Erledigt |
| `phase5-cli.md` | System.CommandLine CLI (--help, --config, --dry-run) | Erledigt |
| `phase5-docker.md` | Multi-Stage Dockerfile + Docker Compose | Erledigt |
| `phase5-smoke-test.md` | DI Integration Test + CLI Smoke Test | Erledigt |

---

## Phasen-Übersicht

- **Phase 1: Core Infrastructure** ← Erledigt
- **Phase 2: Free Commands (Stubs)** ← Erledigt
- **Phase 3: Providers** ← Erledigt
- **Phase 4: Pipeline Execution** ← Erledigt
- **Phase 5: CLI & Docker** ← Erledigt
- Phase 6: Pro Features (Private Repo)

---

## Konventionen

- Prompts sind auf Deutsch (Projektsprache), Code ist auf Englisch.
- Jede Phase hat einen `phase{N}-plan.md` als Einstiegspunkt.
- Einzelschritte sind in `phase{N}-{thema}.md` aufgeteilt.
- Änderungen am Design → `architecture.md` aktualisieren.
- Änderungen an Code-Regeln → `coding-principles.md` aktualisieren.
