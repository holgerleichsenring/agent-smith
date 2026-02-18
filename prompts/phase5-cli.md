# Phase 5 - Schritt 1: CLI mit System.CommandLine

## Ziel
Echtes CLI-Interface mit Argument Parsing, --help, --config Option.
Projekt: `AgentSmith.Host/`

---

## NuGet Package

```
System.CommandLine --version 2.0.0-beta4.22272.1
```

(Letzte stabile Beta, weit verbreitet, wird auch von .NET-Team empfohlen)

---

## CLI Design

```
agentsmith <input> [options]

Arguments:
  <input>    Ticket reference and project, e.g. "fix #123 in payslip"

Options:
  --config <path>    Path to configuration file [default: config/agentsmith.yml]
  --dry-run          Parse intent and show plan, but don't execute
  --verbose          Enable verbose logging
  --version          Show version information
  --help             Show help
```

---

## Program.cs Umbau

```
Datei: src/AgentSmith.Host/Program.cs
```

Statt dem einfachen `args[0]`-Parsing wird `System.CommandLine` verwendet:

1. Root Command mit Argument `<input>` und Optionen
2. Handler: DI-Container bauen → ProcessTicketUseCase aufrufen
3. `--dry-run`: Nur IntentParser + Config-Lookup, kein Pipeline-Execute
4. `--verbose`: LogLevel auf Debug setzen
5. Exit Code: 0 = Erfolg, 1 = Fehler

**Struktur:**

```csharp
var inputArg = new Argument<string>("input", "Ticket reference and project");
var configOption = new Option<string>("--config", () => "config/agentsmith.yml", "Config file path");
var dryRunOption = new Option<bool>("--dry-run", "Parse only, don't execute");
var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

var rootCommand = new RootCommand("Agent Smith - AI Coding Agent")
{
    inputArg, configOption, dryRunOption, verboseOption
};

rootCommand.SetHandler(async (input, config, dryRun, verbose) =>
{
    // Build DI, configure logging, run use case
}, inputArg, configOption, dryRunOption, verboseOption);

return await rootCommand.InvokeAsync(args);
```

---

## Dry-Run Modus

Bei `--dry-run`:
1. Config laden
2. Intent parsen
3. Project + Pipeline finden
4. Ausgabe: "Would run pipeline 'fix-bug' for project 'payslip', ticket #123"
5. Pipeline-Commands auflisten
6. Exit 0

Kein API-Call, kein Checkout, kein PR.

---

## Tests

**CliTests:**
- `ParseArgs_ValidInput_ReturnsZeroExitCode` (schwer ohne echte Providers)
- Besser: Unit-Tests für die DI-Auflösung

**DI Integration Test:**
- `AllServices_Resolvable_FromContainer` - Baut den vollen DI-Container, resolved alle Services
