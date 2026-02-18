# Phase 5 - Schritt 2: Docker

## Ziel
Multi-Stage Dockerfile für schlankes Runtime-Image.
Projekt Root: `Dockerfile`, `.dockerignore`

---

## Dockerfile

```
Datei: Dockerfile (Projekt-Root)
```

**Stage 1: Build**
- Base: `mcr.microsoft.com/dotnet/sdk:8.0`
- Copy Solution + alle csproj (für Restore-Caching)
- `dotnet restore`
- Copy Rest + `dotnet publish -c Release`

**Stage 2: Runtime**
- Base: `mcr.microsoft.com/dotnet/runtime:8.0`
- Copy published output
- Copy `config/` als Default-Config
- ENTRYPOINT: `dotnet AgentSmith.Host.dll`

---

## .dockerignore

```
Datei: .dockerignore (Projekt-Root)
```

Ausschließen:
- `bin/`, `obj/`, `.git/`, `node_modules/`
- `*.md` (außer config/coding-principles.md)
- `.vs/`, `.idea/`, `*.user`
- `tests/` (nicht im Runtime-Image)

---

## Docker Compose (Beispiel)

```
Datei: docker-compose.yml (Projekt-Root)
```

Für lokale Entwicklung / Demo:
```yaml
services:
  agentsmith:
    build: .
    environment:
      - GITHUB_TOKEN=${GITHUB_TOKEN}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - AZURE_DEVOPS_TOKEN=${AZURE_DEVOPS_TOKEN}
    volumes:
      - ./config:/app/config
      - ~/.ssh:/root/.ssh:ro
```

---

## Image-Größe

Ziel: < 200MB
- Runtime Base: ~85MB
- Published App: ~30-50MB (Self-Contained wäre ~80MB, aber Framework-Dependent reicht)
- Gesamt: ~120-150MB

---

## Testen

```bash
# Bauen
docker build -t agentsmith .

# Hilfe anzeigen
docker run --rm agentsmith --help

# Dry-Run
docker run --rm \
  -v $(pwd)/config:/app/config \
  agentsmith --dry-run "fix #123 in payslip"

# Echt
docker run --rm \
  -e GITHUB_TOKEN=ghp_xxx \
  -e ANTHROPIC_API_KEY=sk-xxx \
  -v $(pwd)/config:/app/config \
  agentsmith "fix #123 in payslip"
```
