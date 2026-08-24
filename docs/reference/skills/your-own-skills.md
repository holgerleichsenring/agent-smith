# Shipping Your Own Skills

You want your own masters, your own house rules, your own patterns — and you
still want the release note "agent-smith 0.134.1 runs skills catalog 4.6.0" to
mean something.

An **overlay** gives you both. Your directory is layered ON TOP OF the catalog
your `skills.source` resolved, instead of replacing it. Every master you did not
write still comes from the pinned catalog, and the pin still governs that half.

```yaml
# agentsmith.yml
skills:
  source: default          # or embedded, or url, or path — any of them
  version: v4.6.0
  overlay: /srv/skills-overlay
```

`overlay` is orthogonal to `source`. Pin a release, run the catalog embedded in
the binary, or mount a directory — the overlay layers onto whichever one you
chose.

## What your overlay directory looks like

Exactly like a catalog: a root with a `skills/` subdirectory. Only the files you
actually want to add or change need to be in it.

```
/srv/skills-overlay/
  skills/
    _masters/
      coding-agent-master/SKILL.md    # replaces the pinned one
      house-review-master/SKILL.md    # a master the catalog does not ship
  references/
    house-conventions.md              # cited by your masters as {{ref:...}}
```

The overlay must exist and must contain `skills/` — the same two checks a
mounted catalog gets. A misconfigured overlay fails the resolve loudly rather
than quietly running the bare base, because a run that shipped without your
skills and said nothing would be worse than a run that did not start.

## What layering means, file by file

Resolution copies the resolved base catalog and then copies your overlay over
it, so the rule is **per file**:

- A path only the base has — you get the base's file. This is how every master
  the binary asks for by name keeps working.
- A path only the overlay has — you get your file. New masters, new references,
  new patterns.
- A path both have — **your file wins, completely.** Replacing a master is
  allowed and is the point of building an overlay at all.

There is no merging *inside* a file. `concept-vocabulary.yaml` is the case that
matters: to add a concept you ship a whole `skills/concept-vocabulary.yaml` that
contains the official entries plus yours. Half a vocabulary file replaces the
whole vocabulary.

## What a run tells you

The layered root is a third directory next to `skills.cache_dir` (same path plus
`-overlay`); neither the base nor your overlay is ever written to. It is
rebuilt when the base version, the base's file set or your overlay's file set
changes, and re-used otherwise — so editing a skill takes effect on the next
run, and an untouched overlay costs nothing.

The run's Load-catalog step names both halves:

```
catalog v4.6.0 + overlay 6f2b91c4ad07: 74 concepts, 12 skills, 15 masters
```

The version stays the base version — it is never rewritten into a composite —
and the fingerprint identifies the overlay file set the run actually loaded.
The same phrase names the catalog in any refusal that has to blame it.

## Validating what you wrote

The catalog you did not write is covered by the pin. The files you wrote are
yours, and the existing verb checks them:

```bash
agentsmith validate-concepts --skills-path /var/lib/agentsmith/skills-overlay/skills
```

Point it at the **layered** root's `skills/` directory, not at your overlay
alone — that way your `activates_when` expressions resolve against the official
concept vocabulary you did not ship.

It checks: SKILL.md frontmatter, the declared role, a non-empty description and
its length cap, the `output_schema` value, and every `activates_when` expression
against the concept vocabulary. It does **not** check that your master's prose
is any good, that it agrees with the pinned masters it runs alongside, or that
replacing a master was a sensible thing to do. Those stay yours.
