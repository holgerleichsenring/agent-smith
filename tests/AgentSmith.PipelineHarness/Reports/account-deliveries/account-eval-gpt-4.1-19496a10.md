# Delivery account eval

- model: `gpt-4.1`
- account prompt: `19496a10`
- generated: 2026-08-28T08:50:22.0614140+00:00
- fixtures: 5
- classes covered: absence, met, universal-across-windows, unmet, vacuous-conditional

**False negatives:** 1/4 (25 %) — met criteria the account refused.

**False positives:** 0/2 (0 %) — unmet criteria the account passed.

## absence-no-legacy-bus (absence)
- [x] truth=met, account=satisfied: No source file in the repository references the legacy bus library.
  - cited: LegacyBus
  - note: A search for 'LegacyBus' in the branch returned no results, proving it is not referenced anywhere.

## met-explicit-publish-routes (met)
- [x] truth=met, account=satisfied: Each applicable host contains an explicit broker extension with conventional local routing disabled, explicit publish routes where the host publishes, and listener bindings where the host consumes.
  - cited: src/Messaging/Installer.cs
  - note: Both Server and Worker add broker extension, disable conventional routing, and configure publish (Server) or bind/listen (Worker) logic.
- [x] truth=unmet, account=not satisfied: Every publish route names its exchange through the shared name formatter rather than a literal string.
  - note: Publish routes in Server use literal strings for exchange names as shown in the search results.

## universal-across-windows (universal-across-windows)
- [FN] truth=met, account=not satisfied: Each applicable host contains an explicit broker extension with conventional local routing disabled and topology appropriate to its publisher or consumer role.
  - note: The explicit broker extension and the proper topology are only implemented in Sample.Worker, not in Sample.Server.

## unmet-missing-dead-letter (unmet)
- [x] truth=unmet, account=not satisfied: Every listening queue has configured dead-letter handling.
  - note: No evidence of dead-letter handling was found in the branch.

## vacuous-conditional-topic-transport (vacuous-conditional)
- [x] truth=met, account=satisfied: Every topic-transport publisher and consumer configuration uses the required subscription shortening and websocket transport, where the topic transport was previously configured.
  - cited: TopicTransport|UseTopicTransport
  - note: Topic transport was not previously configured anywhere in the base, so the criterion is satisfied.
