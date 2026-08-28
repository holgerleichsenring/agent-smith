# Delivery account eval

- model: `gpt-4.1`
- account prompt: `1d708432`
- generated: 2026-08-28T11:00:39.1099020+00:00
- fixtures: 5
- classes covered: absence, met, universal-across-windows, unmet, vacuous-conditional

**False negatives:** 1/4 (25 %) — met criteria the account refused.

**False positives:** 0/2 (0 %) — unmet criteria the account passed.

## absence-no-legacy-bus (absence)
- [x] truth=met, account=satisfied: No source file in the repository references the legacy bus library.
  - cited: LegacyBus
  - note: A search for 'LegacyBus' returned no results in the repository.

## met-explicit-publish-routes (met)
- [x] truth=met, account=satisfied: Each applicable host contains an explicit broker extension with conventional local routing disabled, explicit publish routes where the host publishes, and listener bindings where the host consumes.
  - cited: src/Messaging/Installer.cs
  - note: Both hosts define a broker extension, disable conventional routing, and configure publish/listener bindings as appropriate.
- [x] truth=unmet, account=not satisfied: Every publish route names its exchange through the shared name formatter rather than a literal string.
  - note: Publish routes use literal strings for exchange names rather than a formatter.

## universal-across-windows (universal-across-windows)
- [FN] truth=met, account=not satisfied: Each applicable host contains an explicit broker extension with conventional local routing disabled and topology appropriate to its publisher or consumer role.
  - note: Sample.Server configures publisher routing but Sample.Worker only disables local routing and uses the queue transport — it does not configure subscriptions or consumer topology corresponding to its role.

## unmet-missing-dead-letter (unmet)
- [x] truth=unmet, account=not satisfied: Every listening queue has configured dead-letter handling.
  - note: No evidence of dead-letter handling configuration was found in the branch.

## vacuous-conditional-topic-transport (vacuous-conditional)
- [x] truth=met, account=not applicable: Every topic-transport publisher and consumer configuration uses the required subscription shortening and websocket transport, where the topic transport was previously configured.
  - cited: Sample.Server@origin/main: the account searched 'topic-transport' exited 1
  - note: No topic-transport configuration was present in the base, so the criterion does not apply.
