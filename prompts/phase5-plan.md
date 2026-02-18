# Phase 5 - CLI & Docker

## Ziel
Agent Smith als echtes CLI-Tool und Docker-Image auslieferbar machen.
User soll `agentsmith "fix #123 in payslip"` ausführen können - lokal oder im Container.

---

## Komponenten

| Schritt | Datei | Beschreibung |
|---------|-------|-------------|
| 1 | `phase5-cli.md` | CLI mit System.CommandLine (Argumente, Optionen, --help) |
| 2 | `phase5-docker.md` | Dockerfile (Multi-Stage), .dockerignore, Docker Compose Beispiel |
| 3 | `phase5-smoke-test.md` | End-to-End Smoke Test (DI-Auflösung, CLI-Parsing) |
| 4 | Tests | CLI Argument Parsing, DI Integration |

---

## Design-Entscheidungen

### CLI: System.CommandLine statt handgeschriebenem Parsing
- `System.CommandLine` ist das offizielle .NET CLI Framework
- Gibt uns `--help`, `--version`, `--config`, Validierung gratis
- Minimal-Overhead, kein Over-Engineering

### Docker: Multi-Stage Build
- Stage 1: SDK zum Bauen
- Stage 2: Runtime-only Image (schlank)
- Config wird als Volume gemountet, nicht eingebaut
- Ziel: Image < 200MB

### Kein Over-Engineering
- Keine Sub-Commands (nur ein Root-Command)
- Keine interaktive Shell
- Keine Watch-Modes oder Daemon-Prozesse
- Einfach: Input rein → PR raus → Exit
