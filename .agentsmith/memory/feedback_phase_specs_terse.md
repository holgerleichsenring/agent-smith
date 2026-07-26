---
name: feedback_phase_specs_terse
description: "Phase YAMLs must be terse — no prose walls in goal, scope, or decisions"
metadata:
  type: feedback
status: proposed
---
Phase specs are not prose; keep them tight.

- **Goal**: a few lines max, dense, no paragraph-style elaboration. Each line carries weight.
- **scope.in / out**: bullets or numbered short items, not multi-paragraph blocks per item.
- **decisions**: one tight line per decision with "— alternatives: …" appended, not multi-paragraph reasoning.
- **steps**: one imperative line per action; no narrative.
- **tests / done**: pure verifiable list, no parenthetical commentary.

**Why:** the user explicitly called out a previous spec (p0155 first draft) as too verbose — "phases sind keine prosa gerede. kurz und knapp." Long specs hide intent behind ceremony, slow review, and drift from the verifiable shape the methodology expects.

**How to apply:** when writing or editing any `phases/planned/*.yaml`, default to short. If a decision needs three paragraphs of reasoning, that reasoning belongs in `decisions.md` (with [[reference]] from the phase) or in a [[notes]] file, not the phase spec. Mirror the tone of recent good specs in the same repo, but cut prose further wherever possible. The reference style in this repo is p0154-ish but tighter.
