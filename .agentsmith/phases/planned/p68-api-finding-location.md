# Phase 68: API Finding Location — ApiPath & SchemaName

## Goal

API scan findings show `swagger.json:0` as location, which is useless.
Add `ApiPath` and `SchemaName` to the Finding record so output shows
`POST /api/auth/login` or `OktaProcessInfoResponse` instead.

## Design

### 1. Finding record — add nullable fields

```csharp
public sealed record Finding(
    string Severity, string File, int StartLine, int? EndLine,
    string Title, string Description, int Confidence,
    string ReviewStatus = "not_reviewed",
    string? ApiPath = null,       // e.g. "POST /api/auth/login"
    string? SchemaName = null);   // e.g. "OktaProcessInfoResponse"
```

### 2. ParseGateFindings — extract from LLM JSON

Parse `apiPath` and `schemaName` from gate output JSON alongside existing fields.

### 3. LLM output instruction — include apiPath/schemaName

Update contributor and gate JSON templates in SkillRoundHandlerBase to
include the new fields.

### 4. Output strategies — format with API location

Use `ApiPath ?? SchemaName ?? $"{File}:{StartLine}"` as the location string
in Console, Markdown, and SARIF output.

## Files to Modify

- `src/AgentSmith.Contracts/Models/Finding.cs`
- `src/AgentSmith.Application/Services/Handlers/SkillRoundHandlerBase.cs`
- `src/AgentSmith.Infrastructure/Services/Output/ConsoleOutputStrategy.cs`
- `src/AgentSmith.Infrastructure/Services/Output/MarkdownOutputStrategy.cs`
- `src/AgentSmith.Infrastructure/Services/Output/SarifOutputStrategy.cs`

## Definition of Done

- [ ] Finding record has ApiPath and SchemaName (nullable, no breaking change)
- [ ] ParseGateFindings extracts apiPath/schemaName from LLM JSON
- [ ] LLM instructions include apiPath/schemaName in JSON template
- [ ] Console output: `[HIGH] POST /api/auth/login — Title` instead of `swagger.json:0`
- [ ] Markdown output: ApiPath/SchemaName column
- [ ] SARIF output: logicalLocation with apiPath
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] Security-scan pipeline unaffected (fields stay null)
