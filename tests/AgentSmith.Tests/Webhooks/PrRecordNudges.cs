using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>The record nudge the admissions and handlers under test share; without a store, a
/// delivery that has to find its ticket through the record finds none.</summary>
internal static class PrRecordNudges
{
    public static PrRecordNudge Of(IPrTicketOwner? owners = null, IReworkNudges? nudges = null, IServiceScopeFactory? scopes = null) =>
        new(owners ?? Mock.Of<IPrTicketOwner>(), nudges ?? Mock.Of<IReworkNudges>(),
            scopes ?? new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PrRecordNudge>.Instance);
}
