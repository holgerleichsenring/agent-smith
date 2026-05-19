using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// Test-builder shortcuts for the LLM-JSON parser stack. Each method returns
/// the parser composed with a no-op telemetry sink + NullLogger so test
/// arrangements stay terse. Production code wires these through DI.
/// </summary>
internal static class TolerantJsonParserFactory
{
    internal static TolerantJsonParser CreateTolerant() =>
        new(new NoOpTelemetry(), NullLogger<TolerantJsonParser>.Instance);

    internal static ObservationParser CreateObservation() =>
        new(CreateTolerant(), new ObservationNormalizer());

    internal static GateObservationParser CreateGate() =>
        new(CreateTolerant());

    internal static PlanParser CreatePlan() =>
        new(CreateTolerant());

    internal static ConsolidationResponseParser CreateConsolidation() =>
        new(CreateTolerant());

    internal static ConvergenceResultParser CreateConvergence() =>
        new(CreateTolerant());

    internal static WikiUpdateParser CreateWikiUpdate() =>
        new(CreateTolerant());

    private sealed class NoOpTelemetry : ITolerantParseTelemetry
    {
        public void Record(TolerantRecoveryKind kind, string detail) { }
    }
}
