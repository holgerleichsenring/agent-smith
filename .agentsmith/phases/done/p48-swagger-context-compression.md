# Phase 48: Swagger Context Compression

## Problem

The api-security-scan pipeline passes `spec.RawJson` (99,619 chars) into every
skill LLM call. With 12 LLM calls per run, this produces ~613k input tokens —
15x cost increase ($1.96 vs $0.13) with no proportional quality gain.

The raw swagger.json contains massive redundancy:
- Every endpoint declares `text/plain`, `application/json`, `text/json` variants
- `$ref` chains repeat the same schema definitions
- Auto-generated descriptions and operation IDs add noise
- Response schemas repeat identical error models on every endpoint

## Solution

Replace `spec.RawJson` in `BuildDomainSection()` with a compressed, structured
representation built from `SwaggerSpec`'s already-parsed data. No new pipeline
step needed — the compression happens in `ApiSkillRoundHandler.BuildDomainSection()`.

## Architecture Decision

- **No new pipeline command** — this is a formatting concern inside the handler
- Use the structured data already in `SwaggerSpec`: Endpoints, Parameters,
  RequestBodySchema, ResponseSchema, SecuritySchemes
- Deduplicate schemas: extract unique `definitions`/`components.schemas` once,
  reference by name in endpoint listing
- Strip content-type duplicates (keep only one per response code)
- Keep field names, types, enums, constraints — these are what skills analyze

## Compressed Format

```
## API: {Title} {Version}

### Security Schemes
- {Name}: {Type} ({Scheme})

### Schemas (deduplicated)
{SchemaName}:
  {fieldName}: {type} {format?} {enum?} {nullable?} {constraints?}

### Endpoints
{METHOD} {Path} [auth: {yes/no}]
  Params: {name} ({in}, {type}, {required})
  Request: {SchemaRef or inline}
  Responses:
    {code}: {SchemaRef or inline}
```

Target: ~10-15k chars instead of 99k (85% reduction).

## Implementation

### Changes to `ApiSkillRoundHandler.BuildDomainSection()`

1. Replace `spec.RawJson` dump with `CompressSwaggerSpec(spec)` call
2. New private method `CompressSwaggerSpec(SwaggerSpec spec) -> string`:
   - Parse `spec.RawJson` once to extract `definitions`/`components.schemas`
   - Deduplicate and flatten schema definitions (field name + type + constraints)
   - Format endpoints with schema references instead of full inline JSON
   - Strip content-type variants (keep first match per response code)
3. Spectral + Nuclei findings sections stay unchanged

### Files to Modify

- `src/AgentSmith.Application/Services/Handlers/ApiSkillRoundHandler.cs`

### Files to Create

- Tests for the compression logic

## Definition of Done

- [ ] `BuildDomainSection()` produces compressed swagger context
- [ ] Schemas deduplicated and listed once
- [ ] Content-type duplicates removed
- [ ] Field names, types, enums, constraints preserved
- [ ] Output < 20k chars for 33-endpoint API (was 99k)
- [ ] Unit tests for compression
- [ ] `dotnet build` + `dotnet test` clean
- [ ] Smoke test confirms same quality findings at lower cost

## Dependencies

- p47 (Spectral + schema analysis) — the handler being modified
