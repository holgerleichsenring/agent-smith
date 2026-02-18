# Phase 16: Jira Ticket Provider - Implementation Plan

## Goal
Add Jira as a ticket provider so Agent Smith can read issues, post comments,
and transition issues to Done/Closed via Jira REST API.

---

## Prerequisite
- Phase 10-14 completed

## Steps

### Step 1: Jira Ticket Provider
See: `prompts/phase16-jira.md`

Implement `JiraTicketProvider` using Jira REST API v3 via HttpClient.

### Step 2: Factory Integration
Update `TicketProviderFactory` to create `JiraTicketProvider` for type "jira".

### Step 3: Tests
- Factory test for "jira" type
- Unit tests with mocked HttpClient

### Step 4: Verify
```bash
dotnet build
dotnet test
```

---

## NuGet Packages

No new packages needed. Use `HttpClient` directly - Jira REST API is simple JSON.

---

## Definition of Done
- [ ] `JiraTicketProvider` implements `ITicketProvider`
- [ ] Fetches issue title, description, acceptance criteria
- [ ] Posts comments via REST API
- [ ] Transitions issue to Done/Closed
- [ ] `TicketProviderFactory` handles "jira" type
- [ ] Tests pass
- [ ] `dotnet build` + `dotnet test` clean
