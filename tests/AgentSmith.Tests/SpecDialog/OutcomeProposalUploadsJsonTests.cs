using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-04-96a0c: a stored proposal keeps what its turn read of each upload; an older row reads as none.</summary>
public sealed class OutcomeProposalUploadsJsonTests
{
    [Fact]
    public void OutcomeProposalJson_RoundTrip_KeepsUploads()
    {
        var proposal = new BugOutcome(new BugTicketDraft("The widget is lost", "It vanished.", null))
        {
            Uploads = [new UploadProvenance("reference:app", "set-a", ThisTurn: 6, Opened: 60, Files: 66)],
        };

        var back = OutcomeProposalJson.Read(OutcomeProposalJson.Write(proposal));

        back.Uploads.Should().Equal(proposal.Uploads);
        OutcomeProposalJson.Read(OutcomeProposalJson.Write(proposal with { Uploads = [] })).Uploads.Should().BeEmpty();
    }
}
