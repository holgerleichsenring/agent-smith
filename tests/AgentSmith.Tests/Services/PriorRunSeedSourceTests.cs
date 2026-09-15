using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-09-b26f: the resume seed is keyed on the project the run resolved,
/// and a run with no resolved project reads nothing at all — the alternative,
/// falling back to the ticket alone, is the defect this phase closed.
/// </summary>
public sealed class PriorRunSeedSourceTests
{
    private static PriorRunSeedSource Build(IPriorRunLedgerReader reader) =>
        new(reader, NullLogger<PriorRunSeedSource>.Instance);

    private static PipelineContext ContextWith(string? projectName)
    {
        var pipeline = new PipelineContext();
        if (projectName is not null) pipeline.Set(ContextKeys.ProjectName, projectName);
        return pipeline;
    }

    [Fact]
    public async Task SeedAsync_PassesTheResolvedProjectAlongsideTheTicket()
    {
        var reader = new Mock<IPriorRunLedgerReader>();
        reader.Setup(r => r.ReadLatestForTicketAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriorRunLedger?)null);

        await Build(reader.Object).SeedAsync(ContextWith("alpha"), "4471", CancellationToken.None);

        reader.Verify(r => r.ReadLatestForTicketAsync("alpha", "4471", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeedAsync_NoResolvedProject_ReadsNothing(string? projectName)
    {
        var reader = new Mock<IPriorRunLedgerReader>(MockBehavior.Strict);

        var seed = await Build(reader.Object)
            .SeedAsync(ContextWith(projectName), "4471", CancellationToken.None);

        seed.Should().BeEmpty();
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SeedAsync_ReadFails_EmptySeedNeverThrows()
    {
        var reader = new Mock<IPriorRunLedgerReader>();
        reader.Setup(r => r.ReadLatestForTicketAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no database channel"));

        var seed = await Build(reader.Object)
            .SeedAsync(ContextWith("alpha"), "4471", CancellationToken.None);

        seed.Should().BeEmpty("resume is an affordance, never a blocker");
    }
}
