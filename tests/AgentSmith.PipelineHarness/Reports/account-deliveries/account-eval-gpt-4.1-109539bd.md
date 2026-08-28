# Delivery account eval

- model: `gpt-4.1`
- account prompt: `109539bd`
- generated: 2026-08-28T11:46:39.8547940+00:00
- fixtures: 5
- classes covered: absence, met, universal-across-windows, unmet, vacuous-conditional

**False negatives:** 0/4 (0 %) — met criteria the account refused.

**False positives:** 1/2 (50 %) — unmet criteria the account passed.

## absence-no-legacy-bus (absence)
- [x] truth=met, account=satisfied: No source file in the repository references the legacy bus library.
  - cited: LegacyBus
  - note: A full-repository search for 'LegacyBus' found no occurrences in the branch.

## met-explicit-publish-routes (met)
- [x] truth=met, account=satisfied: Each applicable host contains an explicit broker extension with conventional local routing disabled, explicit publish routes where the host publishes, and listener bindings where the host consumes.
  - cited: src/Messaging/Installer.cs
  - note: Both hosts define explicit broker extensions; Sample.Server has publish routes, Sample.Worker has bindings and a listener.
- [FP] truth=unmet, account=satisfied: Every publish route names its exchange through the shared name formatter rather than a literal string.
  - cited: Publish\s*\([^)]*"[^"]+"
  - note: Search found no use of publish routes with literal exchange names in either repository.

## universal-across-windows (universal-across-windows)
- [x] truth=met, account=satisfied: Each applicable host contains an explicit broker extension with conventional local routing disabled and topology appropriate to its publisher or consumer role.
  - cited: src/Messaging/Installer.cs
  - note: Installer.cs defines an explicit broker extension, disables conventional local routing, and configures queue topology.

## unmet-missing-dead-letter (unmet)
- [x] truth=unmet, account=not satisfied: Every listening queue has configured dead-letter handling.
  - note: No evidence of dead-letter handling configuration was found in the repository.

## vacuous-conditional-topic-transport (vacuous-conditional)
- [x] truth=met, account=not applicable: Every topic-transport publisher and consumer configuration uses the required subscription shortening and websocket transport, where the topic transport was previously configured.
  - cited: Sample.Server@origin/main: the account searched 'topic-transport' exited 1
  - note: The base contains no topic-transport configuration, so the requirement does not apply.
