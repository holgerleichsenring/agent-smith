# Skill Manager

The **skill-manager** pipeline has been removed. It is not one of the shipped presets, the name does not resolve, and no CLI command starts it. A configuration or label rule that names `skill-manager` fails when a run is routed to it.

It was removed together with the triage and skill-round machinery it was built on. Bringing it back would mean authoring a `skill-manager` master skill and declaring a preset around an `AgenticMaster` step, the way the other pipelines work.

## Managing skills today

Skills ship as a versioned catalog. Agent Smith embeds a pinned release and can pull another one:

- [Skills Catalog](../../how-it-works/skills-catalog.md) explains how the catalog is pinned, pulled and overridden.
- [Your own skills](../skills/your-own-skills.md) covers writing and loading skills of your own.
- `agent-smith skills pull` downloads a catalog release tarball.

For the pipelines that exist, see [Pipelines](index.md).
