# What the catalogue direction was decided against — 2026-08-30

Background for `2026-08-30-03e1`. Kept out of the spec because it is measurement
and comparison, not a constraint on the build.

## The human comparison

One reviewer read one repository — a backend service of roughly twenty
controllers — and reported five issues. Held against the checked-in standard:

| finding | covered |
|---|---|
| authorization decided by role, never by object | yes, squarely |
| the API description is served in every environment | yes |
| an identity helper's claim lookup falls back to a different claim type | adjacent at best |
| a single configured object id grants administrative rights | adjacent; a weakness id fits better than a requirement |
| a security-shaped configuration flag that no code reads | not covered by any standard |

Two of five squarely. The three that fail are the ones a checklist structurally
cannot reach: a logic flaw inside a helper, a trust anchor in a configuration
line, and a setting that does nothing.

## The live re-run

Six scans of the same repository, three before this batch and three after, same
clone, same agent, same context. The enumeration machinery was present in the
second set but not pinned, so the master was never told to use it.

- findings: 25 / 26 / 37 before, 25 / 27 / 26 after
- of the reviewer's five: **zero** appeared in any of the six
- base prompt: 25,345 characters before, 44,750 after

The finding sets are the same shape either way: the scanners' git-history
secrets plus the master's dismissals. Nothing in the batch moved detection,
which is the whole reason to ask what the catalogue is for.

## The volume, stated correctly

The lens selects **71 entries across the six stations** at the level floor —
twelve per station, eleven for one — and the run caps at five entry groups, so
**355 answers** at the cap. An earlier draft of the spec said 71 *per station*
and was wrong by a factor of six; the argument for inverting the direction
survives the correction but is less dramatic than that number made it look.
