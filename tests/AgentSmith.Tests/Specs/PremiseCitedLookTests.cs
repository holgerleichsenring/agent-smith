using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-04-6768a: what the series-base replay needs from a look — the pattern a search ran, and the
/// one look the admission chose for a false finding.
/// </summary>
public sealed class PremiseCitedLookTests
{
    [Fact]
    public async Task RepositorySearchTool_Search_RecordsItsPattern()
    {
        var look = Over(new CountingSandbox(exitCode: 1));

        await new RepositorySearchTool(look, NullLogger.Instance).SearchRepository(Repo, "Empty|Payload", "src");

        look.Evidence.Looks.Single().Pattern.Should().Be("Empty|Payload", "never read back out of the prose 'what'");
    }

    [Fact]
    public async Task PremiseCheckAdmission_ProvenFinding_CarriesTheCitedLook()
    {
        var look = Over(new CountingSandbox(exitCode: 0));
        await new RepositorySearchTool(look, NullLogger.Instance).SearchRepository(Repo, "first");
        await new RepositorySearchTool(look, NullLogger.Instance).SearchRepository(Repo, "second");
        var draft = PremiseCheckTestDoubles.Draft();

        var check = new PremiseCheckAdmission(NullLogger.Instance).Admit(draft, SpecPremises.For(draft),
            [new PremiseFinding("OrderHandler validates the payload before dispatch", "no-longer-holds", "gone", "X9, L2, L1")],
            look);

        check.False.Single().CitedId.Should().Be("L2", "the first cited look that ran, the one whose line it carries");
        check.False.Single().Evidence.Should().StartWith("[L2]");
    }
}
