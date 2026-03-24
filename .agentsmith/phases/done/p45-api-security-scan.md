# Phase 45: API Security Scan Pipeline

## Goal

New pipeline preset `api-security-scan` that scans a running API instance against
its swagger.json / OpenAPI spec. Nuclei handles the mechanical scan via Docker
Spawner (p19a). Skills interpret Nuclei output and review the swagger spec for
structural issues that only a human reviewer (or LLM) would catch.

---

## Architecture Decisions (final)

- **Nuclei** as scan engine via Docker Spawner (`projectdiscovery/nuclei:latest`)
- **OWASP API Security Top 10 (2023)** as primary ruleset for skills
- Skills analyze Nuclei output + review swagger.json for structural problems
- Output via existing `IOutputStrategy` (SARIF/Markdown/Console) from p43c
- Cascading Commands + ConvergenceCheck exactly like `security-scan` (p43b)
- Ticketless pipeline — same pattern as security-scan

---

## CLI

```
agent-smith api-scan --swagger ./swagger.json --target https://api.example.com
agent-smith api-scan --swagger https://api.example.com/swagger/v1/swagger.json \
  --target https://api.example.com --output sarif
```

Parameters:
- `--swagger` path or URL to swagger.json / OpenAPI spec (required)
- `--target` base URL of the running API (required)
- `--output` `sarif` | `markdown` | `console` (default: `console`)
- `--project` project name from config (optional)

---

## Pipeline Preset

```
api-security-scan:
  - LoadSwaggerCommand
  - SpawnNucleiCommand
  - ApiSecurityTriageCommand
  - [ApiSkillRoundCommands]       ← cascading, inserted by Triage
  - ConvergenceCheckCommand       ← reuse existing
  - CompileDiscussionCommand      ← reuse existing
  - DeliverOutputCommand          ← reuse existing
```

7 base steps, expanding to 12+ after Triage inserts skill rounds.

---

## Skills (`config/skills/api-security/`)

### `api-security-principles.md`
Scope: OWASP API Security Top 10 (2023). Out of scope: DoS, race conditions
without evidence, source code analysis, infrastructure. Confidence < 7 → discard.

### `api-vuln-analyst.yaml` (Lead)
Evaluates Nuclei findings in API context. Maps each finding to an OWASP API
Top 10 category. Assigns severity (Critical/High/Medium/Low) with confidence score.
Triggers: nuclei-findings, vulnerability, exploit.

### `api-design-auditor.yaml`
Reviews swagger.json for structural problems:
- Endpoints without pagination (API4:2023 — Unrestricted Resource Consumption)
- Inconsistent auth across endpoints (API2:2023 — Broken Authentication)
- Resource IDs without ownership hints (API1:2023 — BOLA)
- Response schemas exposing internal fields (API3:2023 — Broken Object Property Level Authorization)
- URL parameters without constraints (API7:2023 — SSRF)
- Admin paths without elevated auth (API5:2023 — Broken Function Level Authorization)
- Naming leakage: /executeQuery, /runSql, aspNetVersion in parameters
- HTTP semantics errors: GET with side effects, RPC-style naming
- Auth tokens as query parameters instead of headers
- Error responses with stackTrace/exception fields
Triggers: swagger, openapi, schema, design.

### `auth-tester.yaml`
JWT validation, OAuth flows, API key exposure, missing auth on endpoints.
Bearer vs Cookie (SameSite/HttpOnly), OAuth without PKCE for public clients.
Triggers: auth, jwt, oauth, bearer, api-key.

### `false-positive-filter.yaml`
Nuclei-specific false positive filtering. Nuclei often reports theoretical
findings without evidence — discard those. Confidence threshold: 7/10.
Triggers: all (runs last).

---

## New Components

### Contracts

```csharp
// ISwaggerProvider — loads and parses OpenAPI spec
public interface ISwaggerProvider
{
    Task<SwaggerSpec> LoadAsync(string pathOrUrl, CancellationToken ct);
}

// Parsed OpenAPI spec
public sealed record SwaggerSpec(
    string Title,
    string Version,
    IReadOnlyList<ApiEndpoint> Endpoints,
    IReadOnlyList<SecurityScheme> SecuritySchemes,
    string RawJson);

public sealed record ApiEndpoint(
    string Method,
    string Path,
    string? OperationId,
    IReadOnlyList<ApiParameter> Parameters,
    bool RequiresAuth,
    string? RequestBodySchema,
    string? ResponseSchema);

public sealed record ApiParameter(
    string Name,
    string In,         // query | path | header | cookie
    string? Type,
    bool Required);

public sealed record SecurityScheme(
    string Name,
    string Type,       // apiKey | http | oauth2 | openIdConnect
    string? In,        // query | header | cookie
    string? Scheme);   // bearer | basic

// Nuclei scan result
public sealed record NucleiResult(
    IReadOnlyList<NucleiFinding> Findings,
    int DurationSeconds,
    string RawOutput);

public sealed record NucleiFinding(
    string TemplateId,
    string Name,
    string Severity,      // critical | high | medium | low | info
    string MatchedUrl,
    string? Description,
    string? Reference);
```

### CommandNames additions
- `LoadSwagger` = `"LoadSwaggerCommand"`
- `SpawnNuclei` = `"SpawnNucleiCommand"`
- `ApiSecurityTriage` = `"ApiSecurityTriageCommand"`
- `ApiSecuritySkillRound` = `"ApiSecuritySkillRoundCommand"`

### ContextKeys additions
- `SwaggerSpec` = `"SwaggerSpec"`
- `NucleiResult` = `"NucleiResult"`
- `ApiTarget` = `"ApiTarget"`
- `SwaggerPath` = `"SwaggerPath"`

---

## Handlers

### `LoadSwaggerHandler`
1. Read `SwaggerPath` from pipeline context (set by CLI)
2. Call `ISwaggerProvider.LoadAsync()` — handles local file or HTTP URL
3. Parse JSON into `SwaggerSpec` record
4. Store in pipeline as `ContextKeys.SwaggerSpec`
5. Log: endpoint count, auth scheme count, title/version

### `SpawnNucleiHandler`
1. Read `ApiTarget` and `SwaggerSpec` from pipeline context
2. Write swagger.json to temp file (Nuclei needs file path)
3. Spawn Nuclei container via existing Docker Spawner:
   ```
   docker run --rm -v /tmp/scan:/input projectdiscovery/nuclei:latest \
     -target {apiTarget} -jsonl -severity critical,high,medium \
     -tags api,owasp -exclude-tags dos
   ```
4. Parse JSON-lines output into `NucleiResult`
5. Store in pipeline as `ContextKeys.NucleiResult`
6. Log: finding count, duration, severity breakdown

### `ApiSecurityTriageHandler : TriageHandlerBase`
Same pattern as `SecurityTriageHandler` from p43b:
- `BuildUserPrompt()` includes swagger summary (endpoint count, auth schemes,
  parameter types) + Nuclei finding summary (severity counts)
- Selects from api-security skills based on content
- Inserts `ApiSecuritySkillRoundCommand` entries

### `ApiSkillRoundHandler : SkillRoundHandlerBase`
Same pattern as `SecuritySkillRoundHandler`:
- `BuildDomainSection()` includes swagger spec summary + Nuclei findings
- `SkillRoundCommandName` returns `"ApiSecuritySkillRoundCommand"`

---

## Infrastructure

### `SwaggerProvider : ISwaggerProvider`
- If path starts with `http://` or `https://`: HTTP GET with timeout
- Otherwise: read local file
- Parse JSON, extract endpoints/auth/parameters
- Validate it's a valid OpenAPI 2.0 or 3.x spec

### `NucleiSpawner`
Reuses the existing `IDockerJobSpawner` from p19a:
- Mount temp dir with swagger.json
- Run Nuclei with JSON-lines output
- Parse stdout line by line into `NucleiFinding` records
- Timeout: 5 minutes default (configurable)
- If Nuclei image not available: clear error with pull command suggestion

---

## DI Registration

```csharp
// Contracts
services.AddTransient<ISwaggerProvider, SwaggerProvider>();

// Handlers
services.AddTransient<ICommandHandler<LoadSwaggerContext>, LoadSwaggerHandler>();
services.AddTransient<ICommandHandler<SpawnNucleiContext>, SpawnNucleiHandler>();
services.AddTransient<ICommandHandler<ApiSecurityTriageContext>, ApiSecurityTriageHandler>();
services.AddTransient<ICommandHandler<ApiSecuritySkillRoundContext>, ApiSkillRoundHandler>();

// Context builders
AddBuilder<LoadSwaggerContextBuilder>(services, CommandNames.LoadSwagger);
AddBuilder<SpawnNucleiContextBuilder>(services, CommandNames.SpawnNuclei);
AddBuilder<ApiSecurityTriageContextBuilder>(services, CommandNames.ApiSecurityTriage);
AddBuilder<ApiSecuritySkillRoundContextBuilder>(services, CommandNames.ApiSecuritySkillRound);
```

---

## Config (agentsmith.yml)

```yaml
projects:
  my-api-security:
    source:
      type: GitHub
      url: https://github.com/org/my-api
      auth: token
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
      models:
        planning:
          model: claude-sonnet-4-20250514
          max_tokens: 4096
    pipeline: api-security-scan
    skills_path: skills/api-security
```

---

## Dependencies

- p19a (Docker Spawner) — for running Nuclei container
- p43b (security-scan pattern) — TriageHandlerBase, SkillRoundHandlerBase
- p43c (IOutputStrategy / SARIF) — DeliverOutputCommand
- p34 (Cascading Commands) — ConvergenceCheck pattern

---

## Definition of Done

- [ ] `agent-smith api-scan --swagger ./swagger.json --target https://...` runs locally
- [ ] Nuclei container spawns, scan runs, JSON output parsed into NucleiResult
- [ ] SwaggerProvider loads from local file and HTTP URL
- [ ] SwaggerSpec contains endpoints, auth schemes, parameters
- [ ] Triage selects skills based on swagger content + Nuclei findings
- [ ] Cascading SkillRounds + ConvergenceCheck work
- [ ] Output as Console, SARIF, Markdown via existing IOutputStrategy
- [ ] 5 skill files created with OWASP API Top 10 references
- [ ] Unit tests: SwaggerProvider, NucleiSpawner (mocked Docker), Triage
- [ ] Integration test: CLI end-to-end with local swagger.json (mocked Nuclei)
- [ ] `dotnet build` zero warnings, all tests green
