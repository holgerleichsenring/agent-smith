# init-project trigger path audit (p0133)

Recorded during p0133 implementation, 2026-05-11.

## Q1 — Slack-modal / chat-text init path (p0029)

**Status: functional and wired.**

Chain (Slack ingress → JobRequest):

- `ChatIntentParser.cs:41-43` defines `InitPattern` = `^init(?:ialize)?\s+(\S+)$`; `TryParseInit()` (lines 115-126) emits `InitProjectIntent`.
- `SlackMessageDispatcher.cs:13-58` injects `InitProjectIntentHandler` and routes `InitProjectIntent` to `initHandler.HandleAsync()` (line 56-57).
- `ModalCommandType.cs:16` includes `InitProject`.
- `SlackModalSubmissionHandler.cs:84-86` routes `ModalCommandType.InitProject` to the same handler via `ModalIntentFactory.CreateInitIntent()`.
- `SlackModalBuilder.cs:100` parses the `"init_project"` modal command value.
- `InitProjectIntentHandler.cs:37-45` builds a `JobRequest { PipelineOverride = "init-project" }` (no TicketId field) and spawns the job.
- DI: `ServiceCollectionExtensions.cs:149` registers `InitProjectIntentHandler` as scoped.

Both Slack entry-points (modal button + chat `init {project}` message) coexist with the label-triggered path added in p0133. They publish no TicketId into the pipeline context; InitCommitHandler's new lifecycle branch is a `TryGet<TicketId>` no-op for these paths.

## Q2 — Whitelist on pipeline name "init-project"

**Status: no whitelist; clean routing.**

- `WebhookTriggerConfig.PipelineFromLabel` is a plain `Dictionary<string, string>`.
- The four webhook resolvers (`GitHubIssueWebhookHandler.cs:107-118`, `GitLabIssueWebhookHandler`, `AzureDevOpsWorkItemWebhookHandler`, `Jira*`) return a pipeline name from the dict without filtering its value.
- `PipelinePresets.cs:178` registers `"init-project"` in the preset map.
- `ClaimPreChecker.cs:18-19` calls `PipelinePresets.TryResolve(name)` which returns non-null for `"init-project"` (line 190-191).
- Pollers go through `PipelineResolver.Resolve()` which applies the same dict-lookup shape.

A `pipeline_from_label: { "agent-smith:init": "init-project" }` mapping therefore routes cleanly from webhook ingestion through claim → spawn without intermediate rejection.

## Q3 — FetchTicket behavior on null TicketId

**Status: Fail-louds; null-tolerance NOT free.**

- `FetchTicketContextBuilder.cs:14` does `pipeline.Get<TicketId>(ContextKeys.TicketId)`. `PipelineContext.Get<T>` throws `KeyNotFoundException` when the key is missing (`PipelineContext.cs:48`).
- `FetchTicketHandler.cs` would dereference `context.TicketId` if it ran — but the builder fails before that.

Today this is harmless: `FetchTicket` only appears in presets that always carry a ticket (FixBug, FixNoTest, AddFeature, MadDiscussion, SecurityScan). Adding it to InitProject would break the Slack/CLI init paths (no TicketId published).

**Design consequence for p0133**: InitProject preset does NOT gain FetchTicket. Instead, `InitCommitHandler` reads `ContextKeys.TicketId` via `pipeline.TryGet` directly — present for label-triggered init, absent for Slack/CLI — and finalizes the ticket lifecycle through the new `TicketLifecycle` helper using id-only operations (`UpdateStatusAsync` + `TransitionToAsync` / `CloseTicketAsync`). No full Ticket fetch is required for the lifecycle transition.
