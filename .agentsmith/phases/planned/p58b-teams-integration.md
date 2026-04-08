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

- [ ] `TeamsAdapter` implements `IPlatformAdapter` completely
- [ ] Adaptive Cards for all 5 QuestionTypes
- [ ] JWT verification via Microsoft OpenID endpoint
- [ ] `MapTeamsEndpoints()` registered
- [ ] Old `AskQuestion` method removed from all adapters
- [ ] Integration test with Teams Bot Framework Emulator
- [ ] All existing tests green
- [ ] `dotnet build` zero warnings
