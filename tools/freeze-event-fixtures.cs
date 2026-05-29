// p0173e — one-shot local seeder for the frozen JSON fixture set under
// tests/AgentSmith.Tests/Events/fixtures/events/. NEVER wired into CI — the
// purpose of frozen fixtures is to fail when the schema drifts; regenerating
// the fixture set on every build defeats that.
//
// Run manually when you add a new event record:
//
//   dotnet run --project tools/freeze-event-fixtures.cs -- <EventName>
//
// or open and instantiate the record + write its serialised form yourself.
// This file is a starting point — adapt the example below to the record you
// are seeding. The shape it emits is the same envelope EventEnvelopeSerializer
// produces in production: {"t":<typeCode>,"p":{...payload...}}.

#if FREEZE_EVENT_FIXTURES_RUN
using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;

var fixturesRoot = Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
    "tests", "AgentSmith.Tests", "Events", "fixtures", "events");

// Example: seed a RunStartedEvent fixture.
var sample = new RunStartedEvent(
    RunId: "2026-05-20T10-15-30-1a2b",
    Trigger: "github-webhook",
    Pipeline: "fix-bug",
    Repos: new[] { "sample-repo" },
    StartedAt: DateTimeOffset.Parse("2026-05-20T10:15:30Z"));

var envelope = EventEnvelopeSerializer.Serialize(sample);
var indented = JsonSerializer.Serialize(
    JsonSerializer.Deserialize<JsonElement>(envelope),
    new JsonSerializerOptions { WriteIndented = true });

var target = Path.Combine(fixturesRoot, "L1Run", "RunStartedEvent.json");
File.WriteAllText(target, indented);
Console.WriteLine($"wrote {target}");
#endif
