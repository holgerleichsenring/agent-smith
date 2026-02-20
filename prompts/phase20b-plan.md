# Phase 20b: Help Command & Graceful Unknown Input

## Goal

Every user who doesn't know what Agent Smith can do gets a clear, friendly answer.
Every unrecognised input is handled gracefully — no silent failures, no raw exceptions.

This phase is small but important for onboarding and day-to-day usability.

---

## Behaviour Overview

| Input | Response |
|---|---|
| `help` | Full capability overview |
| `?` | Full capability overview |
| `what can you do` | Full capability overview |
| `hi`, `hello` | Short greeting + capability hint |
| Truly unknown input | "I didn't understand that" + short help |
| Low-confidence Haiku parse | Clarification question with buttons |

---

## Help Message Format (Slack)

```
🤖 *Agent Smith — here's what I can do:*

*Fix a ticket*
  `fix #58`                        → finds the project automatically
  `fix #58 in agent-smith-test`    → uses the specified project
  `fix ticket #58`                 → same as above

*List tickets*
  `list tickets`                   → lists open tickets (single project)
  `list tickets in agent-smith-test`

*Create a ticket*
  `create ticket "Add logging" in agent-smith-test`

*Help*
  `help` or `?`                    → shows this message

_I also understand free-form text — just describe what you need._
```

Plain text fallback (for platforms without markdown):

```
Agent Smith — here's what I can do:

Fix a ticket:
  fix #58
  fix #58 in agent-smith-test

List tickets:
  list tickets in agent-smith-test

Create a ticket:
  create ticket "Add logging" in agent-smith-test

Type "help" anytime to see this message.
```

---

## Unknown Input Message

When `IntentEngine` returns `UnknownIntent`:

```
🤷 I didn't understand: "your input here"

Type `help` to see what I can do.
```

Short, non-judgmental, actionable.

---

## Greeting Detection

Regex stage catches common greetings before Haiku:

```
hi / hello / hey / hallo / guten tag / good morning / ...
```

Response:
```
👋 Hey! I'm Agent Smith — an autonomous coding agent.
Type `help` to see what I can do.
```

---

## Clarification Message (low confidence from Haiku)

When `IntentEngine` returns `ClarificationNeeded`:

```
🤔 Did you mean: *fix ticket #58 in agent-smith-test*?

[✅ Yes, do it]   [📋 Show help]
```

- "Yes, do it" → proceeds as if the interpreted intent was typed directly
- "Show help" → sends the full help message

The clarification message uses Slack interactive blocks with `block_id: clarification`.
The Dispatcher handles the button callback and routes accordingly.

---

## New Classes

### HelpHandler

```csharp
public sealed class HelpHandler(IPlatformAdapter adapter)
{
    public Task SendHelpAsync(string channelId, CancellationToken cancellationToken = default);
    public Task SendGreetingAsync(string channelId, CancellationToken cancellationToken = default);
    public Task SendUnknownAsync(string channelId, string originalInput, CancellationToken cancellationToken = default);
    public Task SendClarificationAsync(string channelId, string suggestion, string originalInput, CancellationToken cancellationToken = default);
}
```

### Greeting patterns (added to Regex stage in Phase 20a)

```csharp
private static readonly Regex GreetingPattern = new(
    @"^\s*(hi|hello|hey|hallo|howdy|good\s+(morning|afternoon|evening)|guten\s+tag)\s*[!.]?\s*$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

---

## Dispatcher Integration

In `Program.cs`, the switch on `ResolvedIntent` handles the new cases:

```csharp
case HelpIntent:
    await helpHandler.SendHelpAsync(channelId, ct);
    break;

case GreetingIntent:
    await helpHandler.SendGreetingAsync(channelId, ct);
    break;

case UnknownIntent u:
    await helpHandler.SendUnknownAsync(channelId, u.OriginalInput, ct);
    break;

case ClarificationNeeded c:
    await helpHandler.SendClarificationAsync(channelId, c.Suggestion, c.OriginalInput, ct);
    // Store pending clarification in ConversationStateManager
    await stateManager.SetPendingClarificationAsync(platform, channelId, c, ct);
    break;
```

### Clarification Button Callback

When the user clicks "Yes, do it":
1. Dispatcher receives the button interaction
2. Looks up `PendingClarification` from `ConversationStateManager`
3. Re-runs `HandleFixAsync` (or whichever intent was suggested)
4. Clears the pending clarification state

When the user clicks "Show help":
1. Dispatcher calls `helpHandler.SendHelpAsync`
2. Clears the pending clarification state

---

## ConversationState Extension

Add optional `PendingClarification` to `ConversationState`:

```csharp
public record PendingClarification(
    string SuggestedCommandType,
    string TicketNumber,
    string Project,
    string OriginalInput);
```

Stored in Redis alongside the existing conversation state.
TTL: same as conversation state (2 hours).
Cleared after any button click.

---

## Phase 20b Steps

| Step | File | Description |
|------|------|-------------|
| 20b-1 | `phase20b-help-handler.md` | HelpHandler: SendHelp, SendGreeting, SendUnknown, SendClarification |
| 20b-2 | `phase20b-greeting-regex.md` | Add greeting + help patterns to Regex stage |
| 20b-3 | `phase20b-clarification-state.md` | PendingClarification in ConversationState + Redis |
| 20b-4 | `phase20b-dispatcher-wiring.md` | Wire HelpHandler into Dispatcher Program.cs |

---

## Constraints & Notes

- Help message content is hardcoded in `HelpHandler` — not config-driven
  (the commands are part of the application, not user-configurable)
- Greeting detection lives in the Regex stage (Stage 1) — zero AI cost
- The clarification flow adds a new interaction type alongside the existing
  yes/no question flow — same button callback endpoint, different `block_id` prefix
- `GreetingIntent` is a new `ResolvedIntent` subtype added in this phase

---

## Success Criteria

- [ ] `help` → full capability message in Slack
- [ ] `?` → full capability message in Slack
- [ ] `hi` → short greeting message
- [ ] Truly unknown input → "I didn't understand" + hint to type `help`
- [ ] Low-confidence parse → clarification message with two buttons
- [ ] "Yes, do it" button → intent executes correctly
- [ ] "Show help" button → full help message
- [ ] No AI call for `help`, `?`, greetings, or unknown inputs

---

## Dependencies

- Phase 20a complete (IntentEngine, ResolvedIntent types, ClarificationNeeded)
- `ConversationStateManager` extended with `SetPendingClarificationAsync`
- `IPlatformAdapter` unchanged (uses existing `SendMessageAsync` and block-based methods)