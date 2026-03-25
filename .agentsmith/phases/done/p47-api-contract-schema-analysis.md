# Phase 47: API Contract & Schema Analysis

## Goal

Two extensions for `api-security-scan`:

1. **Spectral** as a second static analyzer alongside Nuclei — validates swagger.json
   against structural and security rules
2. **Full schema analysis** in the `api-design-auditor` skill — the semantic layer
   no tool can reach

## Architecture Decisions (not up for discussion)

- Spectral runs as Docker container via IContainerRunner, same pattern as Nuclei
  Image: `stoplight/spectral:6`
  Ruleset: OWASP Spectral Ruleset, mounted as `.spectral.yaml` file
- Spectral analyzes swagger.json **statically** — no running target required
- Nuclei keeps current approach: endpoints extracted from swagger → targets.txt
  (NOT `-openapi` flag — verified it doesn't work as documented)
- Nuclei tags: `api,auth,token,cors,ssl` (NOT exposure,misconfig — caused timeout)
- No `-insecure` flag on Nuclei (doesn't exist, TLS handled automatically)
- `api-design-auditor` skill fully rewritten with schema analysis
- Spectral findings feed into the same skill pipeline as Nuclei findings

## Spectral Integration

### SpawnSpectralCommand — new pipeline step

```
LoadSwaggerCommand
SpawnNucleiCommand
SpawnSpectralCommand    ← NEW, after Nuclei
LoadSkillsCommand
ApiSecurityTriageCommand
...
```

### SpectralSpawner

Same pattern as `NucleiSpawner`. Mounts swagger.json and .spectral.yaml,
starts container, parses JSON output.

```bash
docker run --rm \
  -v /tmp/swagger.json:/tmp/swagger.json \
  -v /path/to/.spectral.yaml:/.spectral.yaml \
  stoplight/spectral:6 \
  lint /tmp/swagger.json \
  --ruleset /.spectral.yaml \
  --format json
```

### .spectral.yaml (mounted ruleset)

```yaml
extends:
  - "@stoplight/spectral-owasp"
rules:
  # Additional custom rules can be added here
```

Stored in `config/spectral/.spectral.yaml` in the repo.

### SpectralResult record

```csharp
public sealed record SpectralFinding(
    string Code,
    string Message,
    string Path,
    string Severity,
    int Line);

public sealed record SpectralResult(
    IReadOnlyList<SpectralFinding> Findings,
    int ErrorCount,
    int WarnCount,
    int DurationSeconds);
```

Stored in `PipelineContext` under `ContextKeys.SpectralResult`.

## api-design-auditor Skill — Full Rewrite

### Checks

**Category 1: Sensitive Data in Response Schemas**
- Response schemas combining multiple sensitive fields in a single response
  (Real example: `passwordCreationUrl` + `qrCode` + `passcode` + `pdf`
  all in `OktaProcessInfoResponse`)
- Fields that should never appear in API responses:
  `password`, `passwordHash`, `secret`, `privateKey`, `internalId`,
  `exceptionMessage`, `stackTrace`, `correlationId`
- Binary data (`format: byte`) in JSON responses instead of dedicated binary endpoints
- `nullable: true` on security-relevant fields without justification

**Category 2: Enum Opacity**
- Enums defined as integers only without descriptive names
  (Real example: `enum: [0, 100, 110, 120, 130...]` with 31 values and no names)
- Prevents meaningful validation and hides business logic

**Category 3: REST Semantics**
- Verb endpoints instead of resources: `/reset-mfa`, `/deactivate`, `/remind`
- PUT used for non-idempotent operations
  (Real example: `PUT /api/blue-collar-worker/pdf/id` with GET semantics)
- POST endpoints returning 200 instead of 201 for create operations
- GET endpoints whose summary describes actions rather than retrieval

**Category 4: Route Consistency**
- Inconsistent sub-resource structures
- Mixed path conventions: kebab-case vs camelCase in same API
- Duplicate concepts with inconsistent paths
- Tag/path mismatch

**Category 5: Missing Constraints**
- Collection endpoints without PageSize maximum constraint
- Array request bodies without `maxItems`
- String fields without `maxLength`
- URL-type parameters without format restrictions

**Category 6: Spectral Findings Interpretation**
- Evaluate and contextualize Spectral output from `SpectralResult`
- Link Spectral findings to API domain and business impact
- Identify false positives from Spectral

## New Components

**Contracts:**
- `SpectralResult` record + `SpectralFinding` record
- `ContextKeys.SpectralResult`
- `CommandNames.SpawnSpectral`

**Infrastructure:**
- `SpectralSpawner` — same pattern as `NucleiSpawner`

**Application:**
- `SpawnSpectralHandler`
- `SpawnSpectralContextBuilder`

**Config:**
- `config/spectral/.spectral.yaml` — OWASP ruleset config

**Skills:**
- `config/skills/api-security/api-design-auditor.yaml` — full replacement

## Files to Create

- `src/AgentSmith.Contracts/Models/SpectralResult.cs`
- `src/AgentSmith.Infrastructure/Services/Spectral/SpectralSpawner.cs`
- `src/AgentSmith.Application/Services/Handlers/SpawnSpectralHandler.cs`
- `config/spectral/.spectral.yaml`
- Tests: SpectralSpawner, SpawnSpectralHandler

## Files to Modify

- `src/AgentSmith.Contracts/Commands/CommandNames.cs` — add SpawnSpectral
- `src/AgentSmith.Contracts/Commands/ContextKeys.cs` — add SpectralResult
- `src/AgentSmith.Contracts/Commands/PipelinePresets.cs` — insert SpawnSpectral
- `config/skills/api-security/api-design-auditor.yaml` — full replacement
- DI registration + context builder wiring

## Definition of Done

- [ ] Spectral runs against swagger.json with OWASP ruleset
- [ ] `SpectralResult` available in PipelineContext for skills
- [ ] `api-design-auditor` detects sensitive data bundling in response schemas
- [ ] `api-design-auditor` detects enum opacity (integer-only enums)
- [ ] `api-design-auditor` evaluates and contextualizes Spectral output
- [ ] Unit tests: SpectralSpawner (mocked Docker), SpawnSpectralHandler
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- p45 (api-security-scan) — base pipeline
- p19a (Docker Spawner) — for Spectral container
