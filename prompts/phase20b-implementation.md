# Phase 20b: Implementation Notes — Help Command & Graceful Unknown Input

## What was built

A `HelpHandler` that provides friendly responses for help requests, greetings, unknown inputs, and low-confidence parses. Plus a `ClarificationStateManager` for the "did you mean?" flow with Slack buttons.

## Why

Before this phase, unrecognized inputs were silently dropped or caused cryptic error messages. Users had no way to discover what Agent Smith can do. Now every interaction gets a response — help, greeting, clarification question, or a gentle "I didn't understand" with guidance.

## Key decisions

- **Help text is hardcoded** — the commands are part of the application, not user-configurable. No need for a config file.
- **Greeting detection in regex stage** — "hi", "hello", "hey", "hallo", etc. are caught before Haiku, costing zero AI tokens.
- **Clarification state in Redis** — when Haiku returns `confidence: low`, the suggested intent is stored via `ClarificationStateManager` with a TTL. When the user clicks "Yes, do it", the stored intent is replayed. This avoids a second AI call.
- **`SlackInteractionHandler` handles button callbacks** — dispatches `error:retry`, `error:abandon`, and clarification button actions.

## Files created

| File | Purpose |
|------|---------|
| `Handlers/HelpHandler.cs` | SendHelp, SendGreeting, SendUnknown, SendClarification |
| `Services/ClarificationStateManager.cs` | Redis-backed pending clarification state with TTL |
| `Models/PendingClarification.cs` | Record for stored clarification (suggestion, original text, userId) |

## Files modified

| File | Change |
|------|--------|
| `Adapters/SlackInteractionHandler.cs` | Button callback routing for clarification and error actions |
| `Extensions/ServiceCollectionExtensions.cs` | DI registration for HelpHandler, ClarificationStateManager |
| `Models/ChatIntent.cs` | Added GreetingIntent, ClarificationNeeded subtypes |
| `Services/ChatIntentParser.cs` | Added greeting regex patterns |
