using AgentSmith.Application.Services.Specs;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>2026-10-13-8762s: the server composition wires the ratification, the filing stamp, the review's
/// offer, and hands the branch write the conversation's mock reader.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class MockRatificationCompositionTests
{
}
