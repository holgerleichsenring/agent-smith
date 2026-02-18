# Phase 4 - Pipeline Execution

## Ziel
Das System end-to-end lauffähig machen: User gibt `"fix #123 in payslip"` ein →
Intent wird erkannt → Config geladen → Pipeline gebaut → Commands sequentiell ausgeführt.

---

## Komponenten

| Schritt | Datei | Beschreibung |
|---------|-------|-------------|
| 1 | `phase4-intent-parser.md` | IntentParser: Regex-basiert, User Input → TicketId + ProjectName |
| 2 | `phase4-pipeline-executor.md` | PipelineExecutor: Command-Namen → Contexts bauen → Handler ausführen |
| 3 | `phase4-use-case.md` | ProcessTicketUseCase: Orchestriert den gesamten Flow |
| 4 | `phase4-di-wiring.md` | DI Registration in Infrastructure + Host Program.cs |
| 5 | Tests | IntentParser, PipelineExecutor, UseCase (mit Mocks) |

---

## Abhängigkeiten

- **Phase 1-3** müssen komplett sein (sind sie)
- IntentParser ist bewusst simpel gehalten (Regex statt LLM-Call)
  - Begründung: Einfachheit, keine API-Kosten, deterministisch
  - Später erweiterbar auf Claude-basiertes Parsing
- PipelineExecutor nutzt den bestehenden CommandExecutor
- ProcessTicketUseCase ist der zentrale Einstiegspunkt für Host/CLI

---

## Design-Entscheidungen

### IntentParser: Regex statt LLM
Laut architecture.md ist ein Claude-Call vorgesehen. Für Phase 4 implementieren wir
eine Regex-basierte Variante, die die gängigsten Patterns erkennt:
- `"fix #123 in payslip"` → TicketId(123), ProjectName(payslip)
- `"#34237 payslip"` → TicketId(34237), ProjectName(payslip)
- `"payslip #123"` → TicketId(123), ProjectName(payslip)

Ein LLM-basierter Parser kann später als Alternative registriert werden.

### PipelineExecutor: Context-Building
Die zentrale Herausforderung: Aus einem Command-Namen (String aus YAML Config)
den passenden ICommandContext bauen. Das erfordert ein Mapping:
- `"FetchTicketCommand"` → `FetchTicketContext` mit Daten aus Config + PipelineContext
- Jeder Command braucht unterschiedliche Inputs
- Lösung: Eine `CommandContextFactory` die das Mapping übernimmt

### ProcessTicketUseCase
Orchestriert den gesamten Flow:
1. Config laden
2. Intent parsen
3. Project Config finden
4. Pipeline Config finden
5. PipelineExecutor starten
