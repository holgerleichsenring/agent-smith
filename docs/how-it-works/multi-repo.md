# Multi-repo

Most products I've worked on span two or three repos. A "fix the auth flow" ticket touches the API, the client, and probably the worker that handles refresh tokens. That's why Agent Smith does one ticket → one run → N pull requests instead of one ticket per repo.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](../assets/lifecycle.svg)

## What "multi-repo" looks like in config

One entry under `projects:` with more than one repo in its `repos:` list. See [Repos: multi-repo](../connect-your-stuff/repos-multi.md) for the worked TodoList example.

## What the framework does

**One ClaimRequest per ticket.** The enqueue layer doesn't fan out per-repo. Multi-repo projects produce exactly one entry on the Redis job queue per ticket. See [Ticket lifecycle](../reference/concepts/ticket-lifecycle.md) for the claim machinery.

**N sandboxes, eager.** `PipelineSandboxCoordinator.EnsureSandboxesAsync` creates all the sandboxes before the first sandbox-requiring command runs: one per repo, or one per toolchain image when a repo's contexts need different ones. Contexts of one repo that share an image share one sandbox, sized to the largest resource request among them. Predictable timing beats lazy per-repo creation; the cost (a sandbox for a docs repo that the bug-fix doesn't end up touching) is small.

**Path-prefix routing.** The agent's tool surface looks like one filesystem partitioned by repo:

```
todolist-api/src/Auth.cs            → Sandboxes["todolist-api"]   reads /work/src/Auth.cs
todolist-web/src/auth/login.ts      → Sandboxes["todolist-web"]   reads /work/src/auth/login.ts
todolist-docs/auth.md               → Sandboxes["todolist-docs"]  reads /work/auth.md
```

The first segment of every file path is the repo's name in the project. Unknown prefix throws with the known-repos list. The `run_command` tool requires an explicit `repo` argument on multi-repo runs — paths inside a shell command don't carry a prefix the framework can parse.

**One agent conversation, not N.** The run holds one specification across all repos and one agent conversation. The system prompt names the repos in scope. The agent decides where to make changes based on the specification.

**Per-repo bootstrap.** Each repo carries its own contexts, one directory per component under `.agentsmith/contexts/<name>/`, each with a `context.yaml` (stack, toolchain image) and a `principles.md` (the rules for that component). The handlers iterate per repo via `ContextKeys.Sandboxes` + `ContextKeys.Repos`. The `init-project` pipeline writes these files per repo and opens one bootstrap PR per repo, cross-linked.

**One PR per repo, cross-linked.** `CommitAndPRHandler` iterates the per-repo `Configs` list. Each PR body carries a `<!-- agentsmith:sibling-prs -->` marker. After every PR has been opened, `PrCrossLinkHandler` PATCHes each PR body and inserts links to the siblings:

```
<!-- agentsmith:sibling-prs -->
Sibling pull requests:
- todolist-api    https://dev.azure.com/.../pullrequest/4471
- todolist-worker https://dev.azure.com/.../pullrequest/4472
- todolist-web    https://dev.azure.com/.../pullrequest/4473
- todolist-docs   (no changes — skipped)
```

**Branch coherence.** Every repo's branch is named `agent-smith/{ticket}`. Reviewers see the same branch name on every sibling PR.

## Single-repo as the N=1 case

Single-repo projects use the same code paths. `Sandboxes` is a dict with one entry. The path-prefix router short-circuits (one entry → pass through). `PrCrossLinkHandler` is a no-op when fewer than 2 PRs were opened. No special-casing, on purpose: two code paths for one and for many repos is two code paths that drift apart.

## Mixed toolchains across repos

When the repos in a project use different languages, each sandbox gets its own image. Each context declares its stack in its `context.yaml`:

```yaml
# todolist-api: .agentsmith/contexts/api/context.yaml
stack:
  lang: C#
  image: mcr.microsoft.com/dotnet/sdk:9.0

# todolist-web: .agentsmith/contexts/web/context.yaml
stack:
  lang: TypeScript
```

`SandboxImageChain` picks one image per sandbox, most authoritative first: the project's `sandbox.toolchain_image`, then its per-language `sandbox.images` map, then the `stack.image` the context names (only if it comes from a registry the installation trusts; otherwise it's skipped with a warning), then the built-in image for the language, and last a generic image that carries git and no language toolchain. The run log names the link that decided and why. Landing on the generic image is logged as a warning, because a sandbox without a toolchain can't build or test, and that's better read in the log than discovered as a command-not-found halfway through the run.

Commands run in the sandbox of the repo they're addressed to, so `dotnet test` runs in the .NET sandbox and `npm test` in the Node one.

## CLI ergonomics

To scope a CLI run to one repo of a multi-repo project:

```bash
agent-smith code --ticket 54 --project azuredevops-todolist --repo todolist-api
```

Useful for testing the per-repo bootstrap before turning on the full multi-repo flow, or when you know a ticket only needs changes in one repo.

## See also

- [Repos: multi-repo](../connect-your-stuff/repos-multi.md) — the configuration walk-through.
- [Methodology](methodology.md) — what the specification, review and verification do across repos.
- [Lifecycle](lifecycle.md) — the full ticket-in to ticket-back flow.
