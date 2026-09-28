# Autonomous Pipeline

There is no autonomous pipeline in this release. The idea was a run without a ticket that produces tickets: observe a project, agree on what matters most, and write improvement tickets for a person to accept or reject.

```
Regular:     Human writes ticket --> Agent executes
Autonomous:  Agent observes --> Agent writes tickets --> Human reviews
```

Nothing in the product runs that today: there is no `autonomous` master skill and no preset that declares one. Making it real means authoring that master and declaring a master-based preset for it, the way the other pipelines are built.

## What you see

- `agent-smith --help` lists an `autonomous` verb. Running it, with or without `--dry-run`, fails because no `autonomous` preset resolves.
- A project or trigger that names `pipeline: autonomous` has nothing to run; a ticket routed to it fails when it starts.

If you want the observation half today, the scan pipelines ([security scan](security-scan.md), [API scan](api-scan.md)) and the [knowledge base](../concepts/knowledge-base.md) cover a good part of it, and their findings can be turned into tickets by hand.
