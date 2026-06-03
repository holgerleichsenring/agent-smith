---
name: "autonomous-investigator"
description: "Harness fixture autonomous investigator. Minimal investigator body — handler-chain shape only, not a real observation."
role: "investigator"
investigator_mode: "verify_hint"
category: "outputs"
output_schema: "observation"
activates_when: 'pipeline_name = "autonomous"'
version: "0.0.0-fixture"
---

Investigate the trigger context for the autonomous run. Harness scripts
the LLM response; this body satisfies the loader's body-non-empty check.
