# Phase 58b: Microsoft Teams Integration

## Goal

Full Teams adapter implementing `IPlatformAdapter` with Adaptive Cards
for all 5 `QuestionType` variants from p58.

## Scope

- Azure Bot Service registration + JWT verification (Microsoft OpenID endpoint)
- `TeamsAdapter` implementing `IPlatformAdapter` fully
- `TeamsCardBuilder` — deterministic Adaptive Card generation per QuestionType:
  - Confirmation: two Action.Submit buttons (Yes/No)
  - Choice: N Action.Submit buttons
  - Approval: Approve/Reject + Input.Text for comment
  - FreeText: Input.Text + Submit
  - Info: TextBlock + Acknowledge
- `MapTeamsEndpoints()` in ASP.NET minimal API
- Remove `[Obsolete]` from `AskQuestion` and delete the old method
  (all adapters migrated to `AskTypedQuestionAsync` at this point)

## Prerequisites

- p58 (Interactive Dialogue) — fully implemented
- Teams environment available for testing

## Definition of Done

- [x] `TeamsAdapter` implements `IPlatformAdapter` completely
- [x] Adaptive Cards for all 5 QuestionTypes (TeamsCardBuilder)
- [x] JWT verification via Microsoft OpenID endpoint (TeamsJwtValidator)
- [x] `MapTeamsEndpoints()` registered (/api/teams/messages)
- [x] Old `AskQuestion` method removed from IPlatformAdapter + all adapters
- [x] MessageBusListener migrated to AskTypedQuestionAsync
- [ ] Integration test with Teams Bot Framework Emulator (requires Teams env)
- [x] All existing tests green (864 tests)
- [x] `dotnet build` zero new warnings
