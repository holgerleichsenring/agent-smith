---
name: feedback_spawner_model
description: "The CLI is a dev tool only. Spawned K8s/Docker containers must be per-tool/per-language runtime images, not the CLI itself."
metadata:
  type: feedback
status: proposed
---
The CLI (`AgentSmith.Cli`) is **exclusively for console actions** — developer/operator invocations from a laptop. It is NOT a runtime that gets spawned in K8s.

When the pipeline needs to do work that requires a specific toolchain (dotnet SDK, java SDK, npm, python, security scanners like nuclei), the model is: **spawn a per-tool runtime container**, run the single tool, capture output, exit. Pattern reference: how the CLI's nuclei tool spawner works in the security-scan flow.

**The wrong model** (which I built in p0113a, and which had to be reverted/feature-flagged):
- "Spawn the CLI image as a queue worker that runs the whole pipeline" — wrong because (a) the CLI isn't deployed to K8s, (b) it conflates orchestration with toolchain execution.

**The right model**:
- Server pod orchestrates the pipeline in-process — LLM calls, git ops, code analysis, file edits all stay in Server.
- Specific steps that need a toolchain (TestCommand, build steps, security scanners) spawn an ephemeral runtime container with just that toolchain.
- Server pod stays lightweight — no SDKs.

**Why:** AAD-DEV's K8s only deploys the Server image. The CLI image has never been built/pushed/deployed to a registry the cluster can reach. The original spec premise ("queue path should mirror Slack-intent's IJobSpawner usage") was itself faulty — the Slack-intent path was never actually exercised in production either, so its design wasn't validated.

**How to apply:**
- When a spec or task says "spawn a CLI container", challenge that premise BEFORE implementing. Ask: is the CLI deployed where it would need to be? What does the runtime container need to actually contain? Is this orchestration work or toolchain work?
- For toolchain-execution steps, model after the existing tool-spawner pattern (e.g. nuclei) — single-purpose runtime image, narrow inputs, narrow outputs.
- Don't add CLI subcommands intended to be invoked inside K8s Jobs. CLI subcommands are for humans at terminals.
