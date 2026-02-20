# Phase 18 – Step 6: Slack Adapter

## Goal

Implement the Slack platform adapter that translates generic dispatcher actions
into Slack Web API calls. No Slack SDK — raw HTTP only.

---

## Files

### `src/AgentSmith.Dispatcher/Adapters/IPlatformAdapter.cs`

Common contract for all chat platform adapters:

```csharp
namespace AgentSmith.Dispatcher.Adapters;

public interface IPlatformAdapter
{
    string Platform { get; }

    Task SendMessageAsync(string channelId, string text,
        CancellationToken cancellationToken = default);

    Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken = default);

    Task<string> AskQuestionAsync(string channelId, string questionId, string text,
        CancellationToken cancellationToken = default);

    Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken = default);

    Task SendErrorAsync(string channelId, string text,
        CancellationToken cancellationToken = default);

    Task UpdateQuestionAnsweredAsync(string channelId, string messageId, string questionText,
        string answer, CancellationToken cancellationToken = default);
}
```

---

### `src/AgentSmith.Dispatcher/Adapters/SlackAdapter.cs`

- `Platform` returns `"slack"`
- All API calls POST JSON to `https://slack.com/api/{method}`
- Bearer token from `SlackAdapterOptions.BotToken`
- No SDK dependency — uses `HttpClient` directly

#### `SendMessageAsync`
Posts plain text via `chat.postMessage`.

#### `SendProgressAsync`
Posts a progress bar message:
```
:gear: *[3/9]* `AnalyzeCodeCommand`
`[███░░░░░░░]` 3/9
```
On the final step, uses `:white_check_mark:` emoji instead of `:gear:`.

Progress bar formula: fill `█` for completed steps, `░` for remaining, bar length = 10.

#### `AskQuestionAsync`
Posts a Block Kit message with two buttons (Yes / No):

```json
{
  "channel": "...",
  "text": ":thought_balloon: *Question text*",
  "blocks": [
    {
      "type": "section",
      "text": { "type": "mrkdwn", "text": ":thought_balloon: *Question text*" }
    },
    {
      "type": "actions",
      "block_id": "{questionId}",
      "elements": [
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "Yes :white_check_mark:" },
          "style": "primary",
          "value": "yes",
          "action_id": "{questionId}:yes"
        },
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "No :x:" },
          "style": "danger",
          "value": "no",
          "action_id": "{questionId}:no"
        }
      ]
    }
  ]
}
```

Returns the message timestamp (`ts`) from the Slack response — used later
to update/replace the message when the user answers.

#### `SendDoneAsync`
Posts a completion message:
```
:rocket: *Done!* {summary}
:link: <{prUrl}|View Pull Request>
```
If `prUrl` is null, omits the link line.

#### `SendErrorAsync`
Posts an error message with the stack in a code block:
```
:x: *Agent Smith encountered an error:*
```{errorText}```
```

#### `UpdateQuestionAnsweredAsync`
Calls `chat.update` to replace the button message with the answer:
```
:thought_balloon: *{questionText}*
:white_check_mark: Answered: *yes*
```
Removes all blocks (no more buttons). This is called after the user clicks a button.

---

### `SlackAdapterOptions`

```csharp
public sealed class SlackAdapterOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
}
```

Populated from environment variables in `Program.cs`:

```csharp
builder.Services.AddSingleton(new SlackAdapterOptions
{
    BotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? string.Empty,
    SigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty
});
builder.Services.AddSingleton<SlackAdapter>();
builder.Services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());
```

---

## Slack Signature Verification

All incoming Slack requests are verified using HMAC-SHA256 before processing.

Slack sends two headers:
- `X-Slack-Request-Timestamp` — Unix timestamp (reject if > 5 minutes old)
- `X-Slack-Signature` — `v0=<hex(HMAC-SHA256(signingSecret, "v0:{timestamp}:{body}"))>`

Verification steps:
1. Read raw body (must happen before `ctx.Request.Body` is consumed by middleware)
2. Check timestamp age (prevent replay attacks)
3. Compute HMAC-SHA256 with `SLACK_SIGNING_SECRET`
4. Compare computed signature to `X-Slack-Signature` (constant-time comparison)

If `SLACK_SIGNING_SECRET` is empty (local dev), skip verification and return `true`.

```csharp
static async Task<bool> VerifySlackSignatureAsync(HttpRequest request, string signingSecret)
{
    if (string.IsNullOrEmpty(signingSecret)) return true;

    if (!request.Headers.TryGetValue("X-Slack-Request-Timestamp", out var tsValue)
        || !long.TryParse(tsValue, out var timestamp))
        return false;

    var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp;
    if (Math.Abs(age) > 300) return false; // replay attack protection

    // body must be buffered before calling this method
    var body = (string)request.HttpContext.Items["rawBody"]!;
    var sigBase = $"v0:{timestamp}:{body}";

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sigBase));
    var computed = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

    var received = request.Headers["X-Slack-Signature"].ToString();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(computed),
        Encoding.UTF8.GetBytes(received));
}
```

---

## Endpoints in `Program.cs`

### `POST /slack/events`

Handles Slack Events API payloads:

1. Verify signature
2. Parse JSON body
3. If `type == "url_verification"` → return `{ challenge }` immediately
4. If `type != "event_callback"` → return 200 OK
5. If `event.type` is neither `"message"` nor `"app_mention"` → return 200 OK
6. If `event.bot_id` is set → skip (ignore bot messages)
7. Strip bot mention prefix: `<@BOTID>` at start of text (for `app_mention` events)
8. Fire-and-forget: call `HandleSlackMessageAsync` in a background task
9. Return 200 OK immediately (Slack requires a response within 3 seconds)

**Strip mention helper:**
```csharp
static string StripMention(string text)
{
    var stripped = Regex.Replace(text.Trim(), @"^<@[A-Z0-9]+>\s*", string.Empty);
    return stripped.Trim();
}
```

### `POST /slack/interact`

Handles interactive component payloads (button clicks):

1. Verify signature
2. Read form body: `payload=<url-encoded JSON>`
3. Parse `payload` JSON
4. Check `type == "block_actions"`
5. Extract: `user.id`, `channel.id`, `actions[0].action_id`, `actions[0].value`
6. Parse `action_id` → `"{questionId}:{answer}"` (split on last `:`)
7. Fire-and-forget: call `HandleSlackInteractionAsync`
8. Return 200 OK immediately (removes Slack's loading spinner)

---

## Message Handlers

### `HandleSlackMessageAsync`

```
parse intent
→ FixTicketIntent → HandleFixTicketAsync
→ ListTicketsIntent → HandleListTicketsAsync
→ CreateTicketIntent → HandleCreateTicketAsync
→ UnknownIntent → send help message
```

### `HandleFixTicketAsync`

1. Check if there's already an active job for this channel (via `ConversationStateManager`)
   - If yes: post "already running" message and return
2. Post `:rocket: Starting Agent Smith for ticket #N in project...`
3. `JobSpawner.SpawnAsync(intent)` → get `jobId`
4. Create `ConversationState` and persist via `stateManager.SetAsync` + `IndexJobAsync`
5. `MessageBusListener.TrackJobAsync(jobId)`

### `HandleListTicketsAsync`

1. Load config via `IConfigurationLoader`
2. Resolve project config
3. Create ticket provider via `ITicketProviderFactory`
4. Call `ListOpenAsync()`
5. Format and post up to 20 tickets

### `HandleCreateTicketAsync`

1. Load config, resolve project
2. Create ticket via `ITicketProviderFactory` + `CreateAsync(title, description)`
3. Post confirmation with the new ticket ID and a ready-to-use `fix #N in project` command

### `HandleSlackInteractionAsync`

1. Resolve `ConversationState` for the channel
2. Validate `PendingQuestionId == questionId`
3. `messageBus.PublishAnswerAsync(jobId, questionId, answer)`
4. `adapter.UpdateQuestionAnsweredAsync(...)` to remove buttons from the message
5. `stateManager.ClearPendingQuestionAsync(...)`

---

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `SLACK_BOT_TOKEN` | Bot User OAuth Token (`xoxb-...`) |
| `SLACK_SIGNING_SECRET` | Signing secret for request verification |

Both are optional for local development (no verification, no Slack posts).

---

## Definition of Done

- [ ] `SlackAdapter` implements `IPlatformAdapter`
- [ ] `IPlatformAdapter` registered in DI (both concrete and interface)
- [ ] `POST /slack/events` handles messages + app mentions
- [ ] `POST /slack/interact` handles button clicks
- [ ] Signature verification works with real Slack credentials
- [ ] Empty `SLACK_SIGNING_SECRET` skips verification (local dev)
- [ ] Bot messages are ignored (no echo loops)
- [ ] Progress bar renders correctly in Slack
- [ ] Question buttons appear and disappear after answer