# Static client-vs-server surface diffing — survey, 2026-08-30

Background for `2026-08-30-c6ec`. Kept out of the spec because a market claim is
unfalsifiable by any test and ages badly in a file that is never revised.

- The declared-versus-exercised comparison is productised only for schema-typed
  graph interfaces (unused fields over a window, per-field last-used, per-client
  usage tables).
- Everywhere else the technique is derived from production traffic — passive
  capture, spec inference, shadow/zombie endpoint detection. That needs
  production and misses the rarely-walked path.
- Adjacent research is web/role debloating ("accessible but unnecessary") and
  OAuth over-scoping, where the same shape is documented: granted permissions
  are visible, exercised ones are not.
- No prior art was found for STATIC extraction of client call sites diffed
  against a served surface. That is an absence-of-evidence result from one
  research pass across several phrasings, not a proof.

Design consequence, and the only part that belongs in the spec: first-party
client source is where the INTENT lives, which is why this is derived statically
rather than from traffic.
