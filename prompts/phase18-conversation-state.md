# Phase 18 – Step 5: ConversationStateManager

## Goal

Track which K8s job is currently running for each chat channel.
Enables the dispatcher to route Redis Stream messages back to the correct
channel, and to prevent duplicate jobs from being spawned.

---

## Files

- `src/AgentSmith.Dispatcher/Models/ConversationState.cs`
- `src/AgentSmith.Dispatcher/Services/ConversationStateManager.cs`

---

## ConversationState (Model)

```csharp
namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Tracks an active agent job linked to a specific chat channel.
/// Stored in Redis with TTL = 2 hours.
/// Key: conversation:{platform}:{channelId}
/// </summary>
public sealed record ConversationState
{
    public required string JobId { get; init; }
    public required string ChannelId { get; init; }
    public required string UserId { get; init; }
    public required string Platform { get; init; }
    public required string Project { get; init; }
    public required int TicketId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// The questionId currently waiting for an answer, if any.
    /// Null when no question is pending.
    /// </summary>
    public string? PendingQuestionId { get; init; }

    public ConversationState WithPendingQuestion(string questionId) =>
        this with { PendingQuestionId = questionId };

    public ConversationState ClearPendingQuestion() =>
        this with { PendingQuestionId = null };
}
```

---

## ConversationStateManager

```csharp
using System.Text.Json;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Manages active conversation state in Redis.
/// Maps a chat channel to its currently running K8s job.
///
/// Key schema:
///   conversation:{platform}:{channelId}  → JSON(ConversationState)
///   job-index:{jobId}                    → conversation key (reverse lookup)
///
/// TTL: 2 hours (auto-cleanup after job ends or times out).
/// </summary>
public sealed class ConversationStateManager(
    IConnectionMultiplexer redis,
    ILogger<ConversationStateManager> logger)
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Persists a new conversation state for the given channel.
    /// Overwrites any existing state (one active job per channel).
    /// </summary>
    public async Task SetAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(state.Platform, state.ChannelId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await db.StringSetAsync(key, json, StateTtl);
        logger.LogInformation(
            "Stored conversation state for channel {ChannelId} on {Platform}: job {JobId}",
            state.ChannelId, state.Platform, state.JobId);
    }

    /// <summary>
    /// Retrieves the active conversation state for a channel.
    /// Returns null if no active job exists.
    /// </summary>
    public async Task<ConversationState?> GetAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(platform, channelId);
        var json = await db.StringGetAsync(key);
        if (json.IsNullOrEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for channel {ChannelId}", channelId);
            return null;
        }
    }

    /// <summary>
    /// Reverse lookup: finds a channel's state by job ID.
    /// Uses a secondary index key stored alongside the primary state.
    /// </summary>
    public async Task<ConversationState?> GetByJobIdAsync(string jobId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var channelKey = await db.StringGetAsync(JobIndexKey(jobId));
        if (channelKey.IsNullOrEmpty) return null;
        var json = await db.StringGetAsync((string)channelKey!);
        if (json.IsNullOrEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for job {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Stores a secondary index: jobId → channel key.
    /// Must be called alongside SetAsync to enable GetByJobIdAsync.
    /// </summary>
    public async Task IndexJobAsync(ConversationState state,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var channelKey = BuildKey(state.Platform, state.ChannelId);
        await db.StringSetAsync(JobIndexKey(state.JobId), channelKey.ToString(), StateTtl);
    }

    /// <summary>Updates the pending question on an existing state.</summary>
    public async Task SetPendingQuestionAsync(string platform, string channelId,
        string questionId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning(
                "Cannot set pending question: no state for channel {ChannelId}", channelId);
            return;
        }
        await SetAsync(existing.WithPendingQuestion(questionId), cancellationToken);
    }

    /// <summary>Clears the pending question from an existing state.</summary>
    public async Task ClearPendingQuestionAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null) return;
        await SetAsync(existing.ClearPendingQuestion(), cancellationToken);
    }

    /// <summary>
    /// Removes all state for a channel after the job completes.
    /// Also removes the job-id index entry.
    /// </summary>
    public async Task RemoveAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(platform, channelId).ToString());
        if (existing is not null)
            await db.KeyDeleteAsync(JobIndexKey(existing.JobId).ToString());
        logger.LogInformation(
            "Removed conversation state for channel {ChannelId} on {Platform}",
            channelId, platform);
    }

    // --- Helpers ---

    private static RedisKey BuildKey(string platform, string channelId) =>
        $"conversation:{platform.ToLowerInvariant()}:{channelId}";

    private static RedisKey JobIndexKey(string jobId) =>
        $"job-index:{jobId}";
}
```

---

## Redis Key Schema

| Key | Value | TTL | Purpose |
|-----|-------|-----|---------|
| `conversation:{platform}:{channelId}` | `JSON(ConversationState)` | 2h | Primary state lookup by channel |
| `job-index:{jobId}` | conversation key string | 2h | Reverse lookup: job → channel |

---

## Usage in Program.cs

```csharp
// After spawning a job:
var state = new ConversationState
{
    JobId = jobId,
    ChannelId = intent.ChannelId,
    UserId = intent.UserId,
    Platform = intent.Platform,
    Project = intent.Project,
    TicketId = intent.TicketId,
    StartedAt = DateTimeOffset.UtcNow
};
await stateManager.SetAsync(state);
await stateManager.IndexJobAsync(state);

// When a Redis bus message arrives (in MessageBusListener):
var state = await stateManager.GetByJobIdAsync(message.JobId);
// route to state.Platform adapter, post to state.ChannelId

// When a Slack button is clicked:
var state = await stateManager.GetAsync("slack", channelId);
// check state.PendingQuestionId matches
await messageBus.PublishAnswerAsync(state.JobId, questionId, answer);
await stateManager.ClearPendingQuestionAsync("slack", channelId);

// After job Done or Error:
await stateManager.RemoveAsync(state.Platform, state.ChannelId);
```

---

## DI Registration

```csharp
builder.Services.AddSingleton<ConversationStateManager>();
```

---

## Design Notes

- **One job per channel**: `SetAsync` overwrites existing state. Before spawning, check `GetAsync` and reject if a job is already running.
- **No locking needed**: Redis `SET` is atomic. Race conditions between two simultaneous messages in the same channel are acceptable (last write wins, both would reject on the "already running" check).
- **TTL as safety net**: If a job crashes without publishing Done/Error, the state expires automatically after 2 hours, allowing new jobs to start.
- **Secondary index**: `job-index:{jobId}` lets the `MessageBusListener` route bus messages back to channels without scanning all keys.

---

## Definition of Done

- [ ] `ConversationState` record compiles with all required properties
- [ ] `ConversationStateManager` reads/writes/deletes from Redis correctly
- [ ] Secondary index is stored alongside primary state
- [ ] Pending question can be set and cleared atomically
- [ ] TTL is applied on every write
- [ ] `dotnet build` clean