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

**One spec, one repository.** The run holds one series of specs across all repos, and every spec names exactly one repository: the one it changes. Work that needs a change in the API and in the client is cut as two specs, one per repository, in the order they have to land. The master of each spec still sees every repo through the same path-prefixed tools, so it can read the client while it changes the API. When the derivation can't put each part of the ticket into one repository, it hands the ticket back with "the work could not be divided between the repositories" rather than guess; you reply with where each part belongs, or split the ticket, and move it back to a trigger status.

**Per-repo bootstrap.** Each repo carries its own contexts, one directory per component under `.agentsmith/contexts/<name>/`, each with a `context.yaml` (stack, toolchain image) and a `principles.md` (the rules for that component). The handlers iterate per repo via `ContextKeys.Sandboxes` + `ContextKeys.Repos`. The `init-project` pipeline writes these files per repo and opens one bootstrap PR per repo, cross-linked.

## One series across repositories

The specs of a ticket share one series id, and each repository holds its own part of the series: the specs that name it, plus a manifest under the same base id. For a ticket cut into a database change in `todolist-api` and a screen in `todolist-web`:

```
todolist-api (branch agent-smith/54)
└── .agentsmith/
    ├── series/2026-10-15-3f2a.yaml
    └── specs/planned/2026-10-15-3f2aa-todo-due-date-column.yaml   (+ .md)

todolist-web (branch agent-smith/54)
└── .agentsmith/
    ├── series/2026-10-15-3f2a.yaml
    └── specs/planned/2026-10-15-3f2ab-todo-due-date-picker.yaml   (+ .md)
```

Every manifest lists all members of the series and the repositories it spans, so the next run reads the parts back as one set, in id order. A revision of the series is written to every member repository in one go, all stamped with the same revision time. When a spec has run, it moves to `specs/done/` in its own repository only, together with its `state.done` line in that repository's context.

When that write fails in one repository (a push refused, say), it is rolled back in the others and the run stops with the reason, so the series stays on its previous revision everywhere. If a run still finds the repositories carrying different revisions, nothing is derived and nothing is built. The ticket is handed back with "the series is out of step between its repositories", naming the repositories that lag. Two ways out: a comment whose first line is `cut this specification again` plus a move back to a trigger status, which cuts the series again over every repository and keeps the specs that already ran; or a new approval of the specification in the design conversation, filed again.

## Pull requests per repository

**A spec PR in every series repository.** At the spec commit, each repository the series touches gets its own draft PR on the ticket branch. A repository whose specs gain code later has one PR, which carries the specs and the code. A repository whose specs end up changing nothing keeps its spec PR anyway, as one of the run's PRs, because its executed specs land there.

**Cross-linked.** `CommitAndPRHandler` iterates the per-repo `Configs` list, and each PR body carries a `<!-- agentsmith:sibling-prs -->` marker. After every PR has been opened, `PrCrossLinkHandler` replaces the marker in each body with links to the siblings:

```
### Related PRs
- todolist-api: https://dev.azure.com/.../pullrequest/4471
- todolist-worker: https://dev.azure.com/.../pullrequest/4472
- todolist-web: https://dev.azure.com/.../pullrequest/4473
- todolist-docs: (no changes)
```

A repo that was in scope but that no spec named and nothing changed shows as `(no changes)`.

**Branch coherence.** Every repo's branch is named `agent-smith/{ticket}`. Reviewers see the same branch name on every sibling PR.

**Reference sets.** The uploaded websites an approval cites are written once, into the repository of the series' earliest spec.

## Memory and decisions per repository

Each repository keeps its own [experiential memory](../reference/concepts/memory.md) and its own decision files. The master's prompt carries the memory index of every repository in the run, labelled by repository, and `recall` searches all of them. `remember` and `log_decision` name the repository a fact or a decision is about, and it is written there and nowhere else. A memory can only go into a repository whose code changes in this run: a change of nothing but memory opens no pull request, so it would never reach git, and the tool says so instead of writing.

## Single-repo as the N=1 case

Single-repo projects use the same code paths. `Sandboxes` is a dict with one entry. The path-prefix router short-circuits (one entry → pass through). `PrCrossLinkHandler` is a no-op when fewer than 2 PRs were opened. A one-repository series is one manifest and its specs in that repository. No special-casing, on purpose: two code paths for one and for many repos is two code paths that drift apart.

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

Commands run in the sandbox of the repo they're addressed to, so `dotnet test` runs in the .NET sandbox and `npm test` in the Node one. Before `VerifySpec`, every sandbox of one repository is brought to the union of their work; two sandboxes that changed the same file differently make verify red, naming the file.

## CLI ergonomics

To scope a CLI run to one repo of a multi-repo project:

```bash
agent-smith code --ticket 54 --project azuredevops-todolist --repo todolist-api
```

Useful for testing the per-repo bootstrap before turning on the full multi-repo flow, or when you know a ticket only needs changes in one repo.

## See also

- [Repos: multi-repo](../connect-your-stuff/repos-multi.md) — the configuration walk-through.
- [The code pipeline](../reference/pipelines/fix-and-feature.md#the-specification) — the series layout and the hand-back cases.
- [Methodology](methodology.md) — what the specification, review and verification do across repos.
- [Lifecycle](lifecycle.md) — the full ticket-in to ticket-back flow.
