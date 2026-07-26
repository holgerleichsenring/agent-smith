---
name: feedback_unit_tests_bypass_di
description: "Unit tests with mocks bypass DI — composition-root bugs only surface when the real ServiceProvider builds the real handler against the real config loader"
metadata:
  type: feedback
status: proposed
---
Unit tests that construct a handler via `new MyHandler(mockA, mockB, configWithRealRegistries)` PROVE the handler's algorithm but say NOTHING about whether the production DI graph hands it the right config. Two production bugs slipped through that pattern:

1. **p0191 `get_artifact_credentials`** — `AgenticMasterHandler` injects `AgentSmithConfig` and reads `config.Registries`. Unit tests instantiated the handler with a populated config and passed. In production, `AddAgentSmithCore` registers `AgentSmithConfig.Empty()` as a default singleton and the Server composition root never overrode it. `config.Registries` was always `[]`. The tool was never useful. Nobody noticed because the master never called it.

2. **p0198 `SetupRegistryAuthHandler`** — same DI graph, same Empty placeholder, same empty `config.Registries`. Unit tests passed for the same reason. The bug surfaced only when the operator triggered a real production pipeline against the production deployment and saw "No `registries:` block in agentsmith.yml" in the log — even though the block was right there in the file.

**Why:** `services.AddSingleton<AgentSmithConfig>(_ => AgentSmithConfig.Empty())` is a placeholder that composition roots are *contracted* to override (comment in [[project-agent-smith-config-empty-placeholder]] says so). Server's `Program.cs` did NOT override. Last-binding-wins means whoever registers later, wins — and nobody did.

**How to apply:**
- For any handler that injects `AgentSmithConfig` or any other "loaded from operator YAML" model, the unit test is NOT sufficient. Add an integration test that:
  1. Builds a `ServiceCollection` exactly like the production composition root (Server's `Program.cs` for Server-side handlers, CLI's `ServiceProviderFactory` for CLI-side).
  2. Loads a fixture YAML through the real `IConfigurationLoader`.
  3. Resolves the handler via `provider.GetRequiredService<ICommandHandler<TContext>>()`.
  4. Asserts the handler sees the operator-set values.
- Pattern is captured in `tests/AgentSmith.Tests/Integration/AgentSmithConfigCompositionTests.cs`. Two paired tests: one documents the Empty placeholder (regression-guard against silent default changes), one proves the override pattern.
- The bigger ambition: `tests/AgentSmith.Tests/Integration/PipelineE2EHarness.cs` should be the real-composition end-to-end harness for every pipeline — see [[project-p0199-real-composition-harness]] for the plan.

**Smell test before declaring a fix done:** "would my test catch this bug if a colleague replaced the production DI registration with `_ => null!`?" If yes, the test is real. If no, it's a unit test pretending.
