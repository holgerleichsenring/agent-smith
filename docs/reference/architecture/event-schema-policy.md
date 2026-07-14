# Event Schema Policy

Every record under `src/backend/AgentSmith.Contracts/Events/` is part of an
on-the-wire contract — written into Redis Streams today, eligible for
re-transport (Redis Streams replicas, Kafka, an HTTP fan-out, …) tomorrow.
Three rules govern its evolution. They are enforced by tests, not by review
discipline.

## Rule (a) — New field = optional with explicit default

A new field added to an existing record MUST be optional with an explicit
default value (typically `null` for reference types or a documented sentinel
for value types). Old JSON fixtures must deserialise on the new code without
modification.

```csharp
// before
public sealed record LlmCallStartedEvent(string RunId, string Model, ...);

// after — new fields appended as optional with defaults
public sealed record LlmCallStartedEvent(
    string RunId,
    string Model,
    ...,
    string? Phase = null,
    string? RepoName = null);
```

This rule is enforced by `EventSchemaCompatibilityTests` — it deserialises
every frozen JSON fixture under `tests/AgentSmith.Tests/Events/fixtures/events/`
against the current types and prints the offending field path on failure.

## Rule (b) — Deprecate via `[DeprecatedField]`, keep readable

A field that producers should stop emitting MUST be annotated with
`[DeprecatedField]`. Consumers stay tolerant of a missing value; producers
may stop emitting after a grace window. The field stays readable on
historical fixtures.

```csharp
public sealed record SomeEvent(
    string RunId,
    [DeprecatedField(reason: "superseded by RepoName", removeAfter: "2026-09")]
    string? LegacyKey = null,
    string? RepoName = null);
```

Removal of a `[DeprecatedField]` member is treated as a semantic break —
follow rule (c).

## Rule (c) — Semantic break = new record class with explicit version suffix

A field rename, a type change, or a meaning change is NOT an additive
mutation. It is a semantic break. Ship a new record with an explicit
`V{N}` suffix in its class name; keep the old record class on the
contract surface during the migration window.

```csharp
// existing record — frozen
public sealed record FooEvent(string RunId, string OldField, ...);

// semantic break — new record class
public sealed record FooEventV2(string RunId, string NewField, ...);
```

The dashboard and other consumers migrate from `FooEvent` to `FooEventV2`
on their own cadence. The old record is removed from the contract surface
only after consumers no longer reference it.

---

## Enforcement

- `DomainEventCoverageTests` — reflection-asserts that every public record
  under `Events/` implements `IDomainEvent`. A new record without the
  marker fails the build.
- `EventSchemaCompatibilityTests` — frozen JSON fixtures under
  `tests/AgentSmith.Tests/Events/fixtures/events/<tier>/<EventName>.json`
  deserialise against the current types. One fixture per record minimum.
- `tools/build-hub-event-types.mjs --check` — walks every record under
  `Events/` and fails when the TypeScript mirror in
  `src/dashboard/src/types/` is missing a record or stale on a record that
  no longer exists in C#.

The CI gate runs all three. The convention is the test; the test is the
convention.

## Seeding the fixture set

`tools/freeze-event-fixtures.cs` is a one-shot local seeder that emits a
minimal example fixture per record. Run it once when adding a new record;
**never** wire it into CI — regenerated samples drift with the code and
defeat the purpose of frozen fixtures.
