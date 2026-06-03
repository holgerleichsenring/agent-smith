---
name: "csharp-bootstrap"
description: "Harness fixture bootstrap producer for the csharp project language. Minimal shape — exercises BootstrapDispatch + BootstrapRound, not real bootstrap output."
role: "producer"
output_schema: "bootstrap"
activates_when: 'project_language = "csharp"'
version: "0.0.0-fixture"
---

Write the two onboarding files for the named component. Harness scripts the
LLM response; this body exists only to satisfy the loader's body-non-empty
check.
