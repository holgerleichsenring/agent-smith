# Phase 31: OrphanJobDetector Redis Scan

## Requirements

After deploying Phase 30's OrphanJobDetector, a stale ConversationState from a pre-deployment
job was discovered blocking a Slack channel. The detector only checked in-memory tracked jobs
(`MessageBusListener._activeSubscriptions`), which are lost on dispatcher restart. Stale Redis
states from before the restart were never detected.

## Scope

### Modified files
- `ConversationStateManager.cs` — add `GetAllAsync()` scanning `conversation:*:*` keys
- `OrphanJobDetector.cs` — after checking tracked jobs, also scan all Redis states for untracked orphans
- `ConversationState.cs` — fix stale doc comment (TTL 2h → 45min)
- `OrphanJobDetectorTests.cs` — add tests for `GetAllAsync` (happy path + malformed JSON skip)

## Implementation

### ConversationStateManager.GetAllAsync
Scans Redis via `IServer.KeysAsync(pattern: "conversation:*:*")`, deserializes each value,
skips malformed entries with a warning log.

### OrphanJobDetector dual-path detection
1. Check in-memory tracked jobs (existing logic)
2. Scan all Redis conversation states via `GetAllAsync`, skip already-tracked jobs,
   apply same orphan criteria (`runtime > 10min AND inactivity > 5min`), clean up matches

## Result
- 290 tests (288 + 2 new), all passing
- Stale job `56181f6af53c` automatically detected and cleaned up within 60s of deployment
- Slack notification sent, channel unblocked
