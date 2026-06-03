---
name: "autonomous-planner"
description: "Harness fixture autonomous planner. Minimal producer body — handler-chain shape only, not a real autonomous plan."
role: "producer"
output_schema: "plan"
activates_when: 'pipeline_name = "autonomous"'
version: "0.0.0-fixture"
---

Produce a plan for the autonomous run. Harness scripts the LLM response;
this body satisfies the loader's body-non-empty check.
