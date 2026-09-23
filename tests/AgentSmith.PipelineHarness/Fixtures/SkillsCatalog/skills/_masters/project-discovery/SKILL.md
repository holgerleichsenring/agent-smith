---
name: "project-discovery"
description: "Harness fixture discovery producer. Minimal shape — exercises BootstrapDiscover's round, not real component enumeration."
role: "producer"
output_schema: "discovery"
activates_when: 'pipeline_name = "init-project"'
version: "0.0.0-fixture"
---

Enumerate this repository's independently-deployable components. The harness
scripts the LLM response; this body exists only to satisfy the loader's
body-non-empty check.
