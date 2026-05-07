# p0121 source-context-diagnosis

## Symptom

In an `api-security-scan` run against a project configured with a remote GitHub source
(`source.type: GitHub`, `source.url: …`), the security agents produce findings that never
reference real source-code identifiers (controller names, route paths, handler methods).
Pre-sandbox-migration the same project's runs included those references.

## Where the chain breaks

1. **`TryCheckoutSourceHandler.CloneRemoteAsync`** ([file:67](../../src/AgentSmith.Application/Services/Handlers/TryCheckoutSourceHandler.cs#L67))
   calls `provider.CheckoutAsync(branch, ct)`. Post-p0117b that call is **metadata-only** —
   the provider returns `Repository(branch, remoteUrl)` whose `LocalPath` is the const
   `/work`. **No actual `git clone` runs** anywhere. Compare with
   `CheckoutSourceHandler.ExecuteAsync` ([file:49-52](../../src/AgentSmith.Application/Services/Handlers/CheckoutSourceHandler.cs#L49-L52))
   which then runs a `git clone` Step inside the sandbox to populate `/work`. The
   `Try`-variant skips that step.

2. **`SourcePath` is set to `/work`** ([file:78](../../src/AgentSmith.Application/Services/Handlers/TryCheckoutSourceHandler.cs#L78))
   — the sandbox path constant, which does not exist on the host filesystem.

3. **`ApiCodeContextHandler.TryResolveSourcePath`** ([file:55-59](../../src/AgentSmith.Application/Services/Handlers/ApiCodeContextHandler.cs#L55-L59))
   checks `Directory.Exists(sourcePath)` against the **host filesystem**. With
   `sourcePath = "/work"` that returns `false`. The handler exits early, sets
   `ApiSourceAvailable = false`, no `ApiCodeContext` is built, and the downstream
   skill prompts that depend on `EvidenceMode.AnalyzedFromSource` fall back to
   schema-only analysis — explaining the missing source-code references in the agents'
   output.

## Why the design split matters

`SourceFileEnumerator` ([file:1-13](../../src/AgentSmith.Infrastructure/Services/Security/SourceFileEnumerator.cs#L1-L13))
explicitly carries two enumeration modes by design:

- `EnumerateAsync(ISandboxFileReader, …)` — sandbox-routed, used by security-scan inside `/work`.
- `EnumerateSourceFiles(repoPath)` — host-disk, **used by api-scan code-aware analyzers
  reading the `--source-path` bind-mount on the host**.

`RouteMapper` / `AuthBootstrapExtractor` / `UploadHandlerExtractor` deliberately stay
on the host-disk path (their `File.ReadAllText` calls were left in place during p0117b —
the api-scan analyzers were intentionally *not* migrated). The breakage isn't in the
analyzers; it's that `TryCheckoutSourceHandler` no longer makes a host-disk path
available for them when the source is remote.

## Working configurations vs broken one

| Source shape | Behavior |
|---|---|
| `--source-path /abs/path` (CLI override) | Works. Host path passed through verbatim. |
| `source.type: local` + `source.path: …` | Works. Resolves to absolute host path. |
| `source.type: GitHub/GitLab/AzureRepos` (remote) | **Broken.** `SourcePath = /work`, host check fails. |

## Chosen fix

Smallest change that restores the design invariant: **`TryCheckoutSourceHandler` clones
the remote source server-side into a host tempdir** (via `Process.Start("git", "clone …")`)
and sets `SourcePath` to that tempdir. The sandbox is not involved — api-scan
analyzers read the host tempdir directly through their existing `EnumerateSourceFiles`
path.

Why server-side git is acceptable here even though p0117b removed server-side git ops:
p0117b's removal targeted the `AgenticExecute` / `Test` / `CommitAndPR` flow, where the
LLM writes to `/work` and server-side commits would commit an empty diff. `api-security-scan`
runs none of those steps — it reads the source, never modifies it. Host-disk read is
the documented intent of the api-scan analyzer split.

The clone is fail-soft: any error (network, missing git binary, auth failure) leaves
`SourcePath` unset and continues in passive schema-only mode, matching the existing
`WarnPassive` contract. Tempdir cleanup is left to OS process-exit / container teardown
(no per-run cleanup needed — agentsmith CLI is short-lived; spawned-job containers are
single-use; orchestrator pods recycle on rollout).

Touchpoints: one method body in `TryCheckoutSourceHandler` (`CloneRemoteAsync` rewrite,
~30 lines including helpers). No changes to `ApiCodeContextHandler`, the three extractors,
`SourceFileEnumerator`, `Repository`, or any other handler.

## What this does NOT fix

The bootstrap handlers (`LoadContext` / `LoadCodingPrinciples` / `LoadCodeMap`) in the
api-security-scan pipeline still read from the sandbox `/work`, which remains empty under
this fix. They are explicitly soft-fail per the preset comments — absence of
`.agentsmith/context.yaml` for the target project is normal and not a regression. If
those handlers need to read the cloned source in the future, that's a follow-up phase
(likely: Server-side dual-population, or migrate the api-scan path to also clone into
the sandbox).
