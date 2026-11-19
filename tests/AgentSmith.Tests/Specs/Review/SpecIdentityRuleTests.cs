using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.CutRevisionFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762g: a rewrite keeps the spec's id, requires, repos and applies_to, or it is no rewrite.</summary>
public sealed class SpecIdentityRuleTests
{
    [Theory]
    [InlineData("requires: [2026-10-14-1234a]", "requires: [2026-10-14-9999a]")]
    [InlineData("applies_to: \"backend (export)\"", "applies_to: \"backend (import)\"")]
    [InlineData("applies_to: \"backend (export)\"", "applies_to: \"backend (export)\"\nrepos: [shop]")]
    public void SpecIdentityRule_ChangedRequiresReposOrAppliesTo_DoesNotHold(string from, string to)
    {
        SpecIdentityRule.Holds(Draft(), Draft(With(from, to))).Should().BeFalse();
        SpecIdentityRule.Holds(Draft(), Draft(With("one file.\"", "one dated file.\""))).Should().BeTrue();
    }
}
