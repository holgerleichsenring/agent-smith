# Phase 29: Init Project Command

## Goal

Add an "Init Project" command to Agent Smith — available via Slack modal and chat.
When triggered, it checks out a repo, runs BootstrapProject (generates context.yaml,
code-map.yaml, coding-principles.md in `.agentsmith/`), and creates a PR with the result.

## Background

Phase 22 introduced auto-bootstrap (ProjectDetector + ContextGenerator) and Phase 28
created the `.agentsmith/` directory structure. Currently, bootstrap only runs as part
of the fix-ticket pipeline. Users need a dedicated command to initialize a project's
`.agentsmith/` directory without having to fix a ticket first.

## Requirements

### Application Layer
1. **InitCommitHandler** — new handler that commits `.agentsmith/` files and creates a PR
   - Branch: `agentsmith/init`
   - Commit message: `chore: initialize .agentsmith/ directory`
   - PR title: `Initialize .agentsmith/ directory`
   - No ticket dependency (unlike CommitAndPRHandler)
2. **ProcessTicketUseCase** — detect `init {project}` pattern, run `init-project` pipeline
3. **CommandContextFactory** — handle init mode for CheckoutSource (branch=agentsmith/init)
   and InitCommitCommand
4. **PipelineExecutor** — dispatch InitCommitCommand
5. **Host CLI** — add `--pipeline` option to support pipeline override

### Pipeline Configuration
6. **init-project pipeline** in agentsmith.yml:
   - CheckoutSourceCommand
   - BootstrapProjectCommand
   - InitCommitCommand

### Dispatcher Layer (Slack + Chat)
7. **IJobSpawner** — generalize from FixTicketIntent to generic JobRequest record
   - JobRequest: InputCommand, Project, ChannelId, UserId, Platform, PipelineOverride
   - Update KubernetesJobSpawner and DockerJobSpawner
8. **InitProjectIntent** — new ChatIntent subtype
9. **InitProjectIntentHandler** — spawns job with `PipelineOverride = "init-project"`
10. **FixTicketIntentHandler** — create JobRequest from FixTicketIntent (breaking change)
11. **ChatIntentParser** — regex for `init {project}` / `initialize {project}`
12. **SlackModalBuilder** — add "Init Project" to command dropdown
13. **ModalCommandType** — add InitProject enum value
14. **SlackModalSubmissionHandler** — route InitProject to handler
15. **SlackMessageDispatcher** — route InitProjectIntent from chat messages
16. **DI registration** — register InitProjectIntentHandler

### Tests
17. Update existing tests (SlackModalSubmissionHandlerTests) for JobRequest
18. Verify ChatIntentParser handles init patterns

## Non-Goals
- No new tests for InitProjectIntentHandler itself (would need full integration setup)
- No changes to BootstrapProjectHandler (it already works)
- No multi-repo support (that's Phase 23)

## File Summary

| Action | Files |
|--------|-------|
| New (3) | InitCommitContext.cs, InitCommitHandler.cs, InitProjectIntentHandler.cs |
| New (1) | JobRequest.cs |
| Modify (12) | ContextKeys, CommandContextFactory, PipelineExecutor, ProcessTicketUseCase, Program.cs, IJobSpawner, DockerJobSpawner, KubernetesJobSpawner, ChatIntent, ModalCommandType, SlackModalBuilder, SlackModalSubmissionHandler, SlackMessageDispatcher, ChatIntentParser, FixTicketIntentHandler |
| Modify DI (2) | Application + Dispatcher ServiceCollectionExtensions |
| Config (2) | agentsmith.yml, agentsmith.example.yml |
| Tests (1) | SlackModalSubmissionHandlerTests |
