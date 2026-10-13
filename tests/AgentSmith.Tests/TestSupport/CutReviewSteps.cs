using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-9a59g: the deriver's review step over a given reviewer, publishing nowhere unless
/// a test hands it a publisher and a run context.</summary>
internal static class CutReviewSteps
{
    public static CutReviewStep Of(
        ISpecCutReviewer reviewer, IEventPublisher? events = null, IRunContextAccessor? runContext = null) =>
        new(reviewer, events ?? Mock.Of<IEventPublisher>(), runContext ?? Mock.Of<IRunContextAccessor>(),
            NullLogger<CutReviewStep>.Instance);
}
