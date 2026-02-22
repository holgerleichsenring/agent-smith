# Phase 27: Structured Command UI (Slack Modals & Teams Adaptive Cards)

## Goal

Replace free-text command input with structured, guided UI. Instead of typing
"fix #58 in agent-smith-test" (error-prone), users get dropdown menus with
autocomplete for commands, projects, and tickets.

## Current Problem

Free-text parsing via IntentEngine (regex + Haiku fallback) frequently fails:
typos, wrong project names, ambiguous input → clarification round-trips waste time.

## New Flow

```
User types: /fix (or /agentsmith)
  → Dispatcher opens a Modal with dropdowns
  → User selects from validated options — no typos possible
  → Dispatcher receives structured data — no parsing needed
```

## Requirements

### Step 1: Slash Command → Modal

Register `/fix` and `/agentsmith` as Slack slash commands. On receive, respond
with `views.open` containing:

- **Command dropdown** (static_select): Fix ticket, List tickets, Create ticket, Fix PR comments
- **Project dropdown** (external_select): populated from `agentsmith.yml`
- **Ticket dropdown** (external_select): dynamically populated from ticket provider

### Step 2: Dynamic Options Endpoint

New `POST /slack/options` endpoint on Dispatcher:

- **project_select**: Load all projects from config, filter by search query
- **ticket_select**: Query project's TicketProvider for open tickets matching search.
  Cache results for 60 seconds (avoid hammering APIs on every keystroke).

### Step 3: Modal Submission Handler

On `view_submission`, extract structured values and route directly — no IntentEngine
needed for modal submissions.

```
command=fix, project=agent-smith-test, ticket=58
  → Direct routing to HandleFixAsync(project, ticket, channelId)
```

### Step 4: Conditional Fields

Update modal dynamically via `views.update` on `block_actions` events:
- "List tickets" → hide ticket dropdown
- "Fix ticket" → show ticket dropdown
- "Create ticket" → show title + description text inputs
- "Fix PR comments" → show PR dropdown instead

### Step 5: Teams Adaptive Cards

Same concept, different API. Teams uses `Input.ChoiceSet` with `Data.Query`
for dynamic data. Implement via existing `IPlatformAdapter` pattern:
- `SlackPlatformAdapter` → Block Kit modals
- `TeamsPlatformAdapter` → Adaptive Cards
- Both produce same structured command data downstream

## Architecture

- New endpoint: `POST /slack/options` (Options Load URL)
- New handler: `ModalCommandHandler` in Dispatcher
- `IModalBuilder` interface for platform-agnostic modal construction
- `SlackModalBuilder`, `TeamsModalBuilder` implementations
- `ICachedTicketSearch` with 60s TTL, keyed by project+query
- IntentEngine stays for backward compatibility (free-text still works)

## Free-Text Fallback

Free-text input continues to work exactly as before. The modal is an ADDITIONAL
input method, not a replacement.

## Definition of Done

- [ ] Slash command `/fix` opens modal in Slack
- [ ] Slash command `/agentsmith` opens general modal in Slack
- [ ] Project dropdown populated from `agentsmith.yml`
- [ ] Ticket dropdown populated dynamically from ticket provider
- [ ] Ticket search with typeahead filtering
- [ ] Conditional fields based on selected command
- [ ] Modal submission routes correctly without IntentEngine
- [ ] Teams Adaptive Card equivalent
- [ ] Free-text input still works (no regression)
- [ ] Ticket search results cached (60s TTL)
- [ ] Unit tests for modal builder
- [ ] Unit tests for options handler
- [ ] Integration test: full modal flow in Slack
