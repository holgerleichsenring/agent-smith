---
name: feedback_verify_via_harness
description: "Verify behaviour with the test harness yourself; don't use the operator as a live QA tester"
metadata:
  type: feedback
status: proposed
---
Don't make the operator re-run the live agent to confirm a bug or a fix — that's treating them like a customer QA-ing a release they don't trust. Verify it yourself with the test suite.

**Why:** repeated live re-runs are slow, cost money, and offload verification that the harness can do deterministically.

**How to apply:** the repo has `RealCompositionHarness` (real ServiceProvider, same DI graph as Server.Program.cs) + `ScriptedChatClient` (`EnqueueText` / `EnqueueToolCall`) + stub ticket/source providers in `tests/AgentSmith.PipelineHarness/`. Preset E2E tests (`FixBugTests`, `AddFeatureTests`) script the LLM and run a preset end-to-end. To pin a bug: write a failing harness test that reproduces it (Category=PipelineHarness), watch it fail, fix, watch it pass. A missing test for a failure/edge path IS the proof the behaviour is unverified. Related: [[feedback_unit_tests_bypass_di]], [[feedback_finish_what_you_start]].
