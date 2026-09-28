# Event Schema Policy

Every record under `src/backend/AgentSmith.Contracts/Events/` is part of an
on-the-wire contract. Run events are written into Redis Streams, and the
dashboard reads them through a TypeScript mirror of the records. Three rules
govern how a record may change. Tests enforce them, not review discipline.

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
against the current types and names the offending fixture file on failure.

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
- `EventSchemaCompatibilityTests` — every frozen JSON fixture under
  `tests/AgentSmith.Tests/Events/fixtures/events/<tier>/<EventName>.json`
  must deserialise against the current types. The test covers the records
  that have a fixture; a record without one is not checked, so seed a
  fixture when you add a record.
- `tools/build-hub-event-types.mjs --check` (`pnpm gen:hub-events` in
  `src/dashboard`) — walks every record under `Events/` and fails when the
  TypeScript mirror (`src/dashboard/src/types/hub-events.ts` and
  `system-events.ts`) is missing a record or still carries one that no
  longer exists in C#.

CI runs all three: the two test classes with the .NET suite, the mirror
check in the dashboard workflow.

## Seeding the fixture set

`tools/freeze-event-fixtures.cs` is a local starting point for seeding a
fixture: adapt its example to the new record and write the serialised form
into the fixture directory. Run it once when adding a new record;
**never** wire it into CI — regenerated samples drift with the code and
defeat the purpose of frozen fixtures.
