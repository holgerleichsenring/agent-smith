# Phase 20a: Intent Engine — Regex + Haiku + Project Resolution

## Goal

Replace the current `RegexIntentParser` with a robust two-stage Intent Engine
that understands free-form natural language input while keeping costs minimal
and behaviour deterministic for known patterns.

A third stage handles project resolution without AI — purely by querying
configured ticket providers.

---

## Design Principles

- **Free first:** Regex catches ~90% of real inputs at zero cost
- **Cheap second:** Claude Haiku only for inputs that don't match Regex
- **Deterministic third:** Project lookup is explicit code, never AI
- **Always structured output:** Every stage produces the same JSON shape
- **Graceful degradation:** Unknown intent → helpful response, never silent failure

---

## Output Schema (all stages produce this)

```json
{
  "commandType": "fix | list | create | help | unknown",
  "ticketNumber": "58",
  "project": "agent-smith-test",
  "title": "",
  "confidence": "high | low"
}
```

- `commandType`: what the user wants to do
- `ticketNumber`: numeric ticket ID (empty string if not applicable)
- `project`: project name from config (empty string if not specified)
- `title`: ticket title for `create` command (empty string otherwise)
- `confidence`: `high` = certain, `low` = Haiku guessed, may need clarification

---

## Stage 1: Regex Pre-Filter (zero cost)

Handles all common patterns directly. Runs before any AI call.

### Patterns

```
fix #58                          → { commandType:"fix", ticket:"58", project:"" }
fix #58 in agent-smith-test      → { commandType:"fix", ticket:"58", project:"agent-smith-test" }
fix ticket #58                   → { commandType:"fix", ticket:"58", project:"" }
fix ticket #58 in xyz            → { commandType:"fix", ticket:"58", project:"xyz" }
ticket #58                       → { commandType:"fix", ticket:"58", project:"" }

list tickets                     → { commandType:"list", project:"" }
list tickets in agent-smith-test → { commandType:"list", project:"agent-smith-test" }
list                             → { commandType:"list", project:"" }

create ticket "Add logging" in xyz        → { commandType:"create", title:"Add logging", project:"xyz" }
create "Add logging" in xyz               → { commandType:"create", title:"Add logging", project:"xyz" }

help                             → { commandType:"help" }
?                                → { commandType:"help" }
what can you do                  → { commandType:"help" }
```

All regex matching is case-insensitive and trims surrounding whitespace.

---

## Stage 2: Claude Haiku (cheap, only when Regex fails)

Called only when Stage 1 returns no match.

### Model
- `claude-haiku-4-5-20251001` — cheapest, fastest
- System prompt is small and prompt-cached → near-zero cost on repeated calls
- Max tokens: 256 (JSON response only, no prose)

### System Prompt (cached)

```
You are an intent classifier for a coding agent bot called Agent Smith.
Extract the user's intent from their message and return ONLY a JSON object.

Valid commandTypes: fix, list, create, help, unknown

Rules:
- "fix", "implement", "solve", "work on", "handle" → commandType: "fix"
- "list", "show", "what tickets", "open issues" → commandType: "list"
- "create", "add", "new ticket", "open a ticket" → commandType: "create"
- Greetings, capability questions → commandType: "help"
- Anything else → commandType: "unknown"
- Extract ticket numbers from #N or "ticket N" or "issue N"
- Extract project names after "in" or "for" or "on"
- If uncertain, set confidence: "low"

Always respond with valid JSON only. No explanation. No markdown.
Schema: { "commandType": string, "ticketNumber": string, "project": string, "title": string, "confidence": string }
```

### Behaviour on `confidence: low`

The Dispatcher asks the user for clarification before proceeding:
```
🤔 I think you want to [fix ticket #58], but I'm not sure.
Did you mean that? [Yes ✅] [No, show help 📋]
```

---

## Stage 3: Project Resolution (deterministic, no AI)

Runs after Stage 1 or 2 when `project` is empty string.

### Algorithm

```
1. Load all projects from agentsmith.yml
2. If only ONE project configured → use it directly (no lookup needed)
3. If multiple projects:
   a. Query each project's ticket provider for ticket #{ticketNumber} IN PARALLEL
   b. Collect results:
      - 0 matches → "Ticket #58 not found in any configured project"
      - 1 match   → use that project, proceed
      - 2+ matches → "Ticket #58 found in multiple projects: [a, b] — which one?"
                     User picks via Slack buttons
```

### Cost
Zero AI tokens. Pure API calls to ticket providers (Azure DevOps, GitHub, Jira, GitLab).
Parallel execution keeps latency low even with many projects.

### Edge Cases

| Situation | Behaviour |
|---|---|
| Ticket not found anywhere | Error message, no job spawned |
| Found in 1 project | Auto-resolved, proceed silently |
| Found in 2+ projects | Ask user to pick via buttons |
| Only 1 project configured | Skip lookup, use it directly |
| Ticket provider unreachable | Fail gracefully, report error |

---

## New Class: IntentEngine

Replaces `RegexIntentParser` and `ChatIntentParser` with a unified engine:

```csharp
public sealed class IntentEngine(
    IHaikuIntentParser haikuParser,
    IProjectResolver projectResolver,
    IConfigurationLoader configLoader,
    ILogger<IntentEngine> logger)
{
    public async Task<ResolvedIntent> ParseAsync(
        string input,
        string platform,
        string channelId,
        string userId,
        CancellationToken cancellationToken = default);
}
```

Returns a `ResolvedIntent` — a discriminated-union-style record:

```csharp
public abstract record ResolvedIntent;
public record FixIntent(string TicketNumber, string Project, string UserId, string ChannelId, string Platform) : ResolvedIntent;
public record ListIntent(string Project, string UserId, string ChannelId, string Platform) : ResolvedIntent;
public record CreateIntent(string Title, string Project, string UserId, string ChannelId, string Platform) : ResolvedIntent;
public record HelpIntent : ResolvedIntent;
public record ClarificationNeeded(string Suggestion, string OriginalInput) : ResolvedIntent;
public record UnknownIntent(string OriginalInput) : ResolvedIntent;
```

---

## Integration in Dispatcher

`IntentEngine` replaces the current `ChatIntentParser` in `Program.cs`.
The Dispatcher switch-cases on `ResolvedIntent` type:

```csharp
var intent = await intentEngine.ParseAsync(text, platform, channelId, userId, ct);

switch (intent)
{
    case FixIntent fix:        await HandleFixAsync(fix); break;
    case ListIntent list:      await HandleListAsync(list); break;
    case CreateIntent create:  await HandleCreateAsync(create); break;
    case HelpIntent:           await HandleHelpAsync(channelId); break;
    case ClarificationNeeded c: await HandleClarificationAsync(c, channelId); break;
    case UnknownIntent u:      await HandleUnknownAsync(u, channelId); break;
}
```

---

## Phase 20a Steps

| Step | File | Description |
|------|------|-------------|
| 20a-1 | `phase20a-regex-stage.md` | RegexIntentStage: pattern matching, ResolvedIntent records |
| 20a-2 | `phase20a-haiku-stage.md` | HaikuIntentParser: system prompt, JSON parsing, confidence |
| 20a-3 | `phase20a-project-resolver.md` | ProjectResolver: parallel ticket lookup, disambiguation |
| 20a-4 | `phase20a-intent-engine.md` | IntentEngine: orchestrates all three stages |
| 20a-5 | `phase20a-dispatcher-wiring.md` | Replace ChatIntentParser in Dispatcher Program.cs |

---

## Constraints & Notes

- Stage 2 (Haiku) is only called when Stage 1 fails — typical sessions cost near zero
- Project resolution queries ticket providers — ensure providers handle "not found" gracefully
- The old `RegexIntentParser` in `AgentSmith.Application` is kept for the CLI use case
- `IntentEngine` lives in `AgentSmith.Dispatcher` — it is chat-specific logic
- Token usage from Haiku calls is tracked in the existing `TokenUsageTracker`

---

## Success Criteria

- [ ] "fix #58" resolves correctly (single project configured)
- [ ] "fix #58" with multiple projects triggers parallel lookup
- [ ] "fix ticket #58 in agent-smith-test" skips lookup, uses given project
- [ ] "please fix the login bug, ticket 58" → Haiku parses correctly
- [ ] "what can you do?" → HelpIntent
- [ ] Ambiguous input with low confidence → clarification message with buttons
- [ ] Truly unknown input → UnknownIntent with helpful fallback message
- [ ] No AI call for standard "fix #N in project" input
- [ ] Token cost for Haiku calls is tracked and logged

---

## Dependencies

- Phase 20b (Help Command) can be implemented in parallel
- Phase 20c (Error UX) depends on the new ResolvedIntent types
- `claude-haiku-4-5-20251001` model available via existing ClaudeAgentProvider infrastructure
- All ticket providers must implement a lightweight `TicketExistsAsync(ticketId)` or
  `GetTicketAsync` method (already exists via `FetchTicketHandler`)