# AgentSmith.Contracts

Cross-layer contracts: interfaces, DTOs, configuration records, event
record families. Depended on by every other AgentSmith.* project.

## Event records

`Events/` carries the typed message contracts that flow across the bus —
`RunEvent` family (per-run stream) and `SystemEvent` family (pre-run
activity stream). Every record implements `IDomainEvent` and is mirrored
in TypeScript under `src/dashboard/src/types/`.

Schema evolution follows the three rules in
[`docs/event-schema-policy.md`](../../../docs/event-schema-policy.md) —
additive-optional fields, `[DeprecatedField]` for retired channels,
explicit versioned record for semantic breaks. The rules are enforced by
the compatibility test suite and the TypeScript drift detector; they are
not a review-time convention.
