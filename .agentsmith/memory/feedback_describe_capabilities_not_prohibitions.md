---
name: feedback_describe_capabilities_not_prohibitions
description: "Skill/prompt text describes what the agent CAN do, not what to avoid; state the capability and stop, trust the agent for the rest"
metadata:
  type: feedback
status: proposed
---
I wrote a coding-master skill bullet in prohibition style ("Writing a program to inspect an API by runtime reflection is the expensive last resort, not the opening move…"). The user rejected it: "warum schreibst du da reflection rein? … Wir beschreiben was möglich ist nicht was verhindert werden soll. … wenn du etwas aus dem internet lesen willst, kannst du das mit web_fetch. den rest kann und muss der selbst machen."

**Why:** the project's prompt philosophy is capability-descriptive, not defensive. Enumerating anti-patterns to avoid is noise and negative framing; a capable agent works out the "how" from the stated capability.

**How to apply:** skill/prompt text states the tool + capability plainly ("To read anything from the internet — docs, changelog, source, a ticket URL — use `web_fetch`.") and stops. Do NOT add "never do X", "the last resort is Y", or injection/safety prohibitions inline unless they're a real, named policy. The final text became exactly two lines: what's possible, nothing about what to avoid. Related: [[feedback_phase_specs_terse]], [[feedback_code_quality_bar]].
