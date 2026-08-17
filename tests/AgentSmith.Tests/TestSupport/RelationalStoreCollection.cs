namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// p0423/p0427/p0428: test classes that build a REAL relational store — a SQLite file with
/// the full migration set applied — share this collection so xUnit serializes them.
/// <para>
/// Applying every migration is the most expensive thing any test in this suite does, and
/// p0423 added several such tests at once (a CLI run must now record itself, so proving it
/// means creating a store). Run in parallel on a two-core CI runner they saturate the
/// thread pool, and the first casualty is not a database test at all: it is
/// <c>Broadcaster_OneStalledClient_DoesNotBlockOthers</c>, whose whole subject is a 50 ms
/// send timeout and whose continuation then waits behind the migration traffic.
/// </para>
/// <para>
/// Serializing them costs a few seconds of wall clock and removes a class of failure that
/// looks like a concurrency bug and is not one.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class RelationalStoreCollection
{
    public const string Name = "AgentSmithRelationalStore";
}
