# Multi-Repo Pipelines

One ticket, one run, N pull requests.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](../assets/lifecycle.svg)

Most Agent Smith deployments don't have one repo per project. A typical product looks like `server` (`dotnet/sdk:8.0`), `client` (`node:20`), `worker` (`dotnet/sdk:8.0`), `docs` (`alpine:3`). One ticket — "rotate the auth tokens" — touches all four. Multi-repo pipelines make that one run, not four.

## The shape

```yaml
projects:
  my-product:
    repos:
      - name: "server"
        url: "github.com/org/server"
      - name: "client"
        url: "github.com/org/client"
      - name: "worker"
        url: "github.com/org/worker"
      - name: "docs"
        url: "github.com/org/docs"
    ticket_provider: github
```

The full configuration reference is in [Multi-Repo Projects](../configuration/multi-repo.md).

## How a multi-repo run flows

1. **Enqueue.** A webhook or poll claim emits **exactly one `ClaimRequest` per ticket**, not one per repo. The legacy per-repo fan-out at the enqueue layer is gone (p0158a).

2. **Resolve.** `ExecutePipelineUseCase.ResolveRepos` returns the project's full `Repos` list by default. The CLI `--repo NAME` flag (or the consumer-side `SourceOverrideRepo` context key) narrows it to one match.

3. **Spawn sandboxes.** `PipelineSandboxCoordinator.EnsureSandboxesAsync` creates **one sandbox per repo, eagerly**, on the first sandbox-requiring command. Each sandbox runs the toolchain image resolved per-repo (via the layered chain in `SandboxSpecBuilder`).

4. **Checkout.** `CheckoutSourceHandler` clones every repo into its own sandbox at `/work` and creates `agentsmith/ticket-{N}` in each.

5. **Bootstrap per repo.** `BootstrapCheckHandler` probes each repo's `.agentsmith/context.yaml` and `coding-principles.md`. `LoadContextHandler`, `LoadCodingPrinciplesHandler`, `AnalyzeProjectHandler`, and `PublishProjectLanguageHandler` all iterate per repo.

6. **One plan, one agent conversation, N tool dispatches.** The orchestrator holds a single agentic loop. `FilesystemToolHost` routes file tool calls by **path prefix** — the first segment of a path identifies the repo (`server/src/Auth.cs` → server's sandbox). Unknown prefix throws with the known-repos list. The `run_command` tool requires an explicit `repo` argument on multi-repo runs.

7. **Open one PR per repo.** `CommitAndPRHandler` commits + pushes + opens one pull request per repo. Each PR body carries a `<!-- agentsmith:sibling-prs -->` marker so that `PrCrossLinkHandler` can patch it after every PR has been opened, inserting links to all sibling PRs.

8. **Resolve the ticket.** The ticket is written back with every PR link.

## Path-prefix dispatch

The orchestrator's tool surface is one logical filesystem, partitioned by repo name:

```
server/src/AgentSmith.Server/Program.cs        → Sandboxes["server"], /work/src/AgentSmith.Server/Program.cs
client/src/auth/login.ts                       → Sandboxes["client"], /work/src/auth/login.ts
docs/README.md                                 → Sandboxes["docs"],   /work/README.md
```

The first segment is the **repo key** as configured under `projects.{name}.repos[*].name`. The rest of the path is repo-internal. Single-repo projects short-circuit the router — paths are passed through unchanged.

## Branch coherence across repos

All repos use the same branch name: `agentsmith/ticket-{N}` (where `{N}` is the ticket id). Reviewers see the same branch across all sibling PRs, which makes the multi-repo change set legible at a glance. Linked PR bodies cross-reference each other after `PrCrossLink` runs.

## CLI ergonomics

```sh
# Run against every repo in the project (default).
agent-smith fix "#54 in my-product"

# Scope to a single repo.
agent-smith fix "#54 in my-product" --repo server

# Source override without --repo on a multi-repo project — fails fast:
agent-smith fix "#54" --source-url ./local-checkout
# → InvalidOperationException: project 'my-product' has multiple repos
#   (server, client, worker, docs); pick one with --repo NAME.
```

## Single-repo projects

Everything above still applies — N just equals 1. No multi-repo overhead is paid: the path-prefix router short-circuits, only one sandbox spawns, `PrCrossLink` is a no-op when fewer than 2 PRs were opened.

## See also

- [Configuration: multi-repo](../configuration/multi-repo.md)
- [Configuration: project resolution](../configuration/project-resolution.md)
- [Sandbox architecture](sandbox-architecture.md)
- [Ticket lifecycle](ticket-lifecycle.md)
