using AgentSmith.Application.Services.Surface;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services.Verification;
using FluentAssertions;

namespace AgentSmith.Tests.Surface;

/// <summary>
/// 2026-08-30-c6ec: a difference is evidence and the requirement is the finding — so every
/// id a difference cites has to exist in the standard this binary ships. A citation of an
/// id the shipped release does not carry decides nothing and cannot be looked up.
/// </summary>
public sealed class SurfaceRequirementIdTests
{
    [Theory]
    [InlineData(SurfaceDifferenceKind.UnexercisedOperation)]
    [InlineData(SurfaceDifferenceKind.UnsentAcceptedProperty)]
    [InlineData(SurfaceDifferenceKind.UnreadReturnedProperty)]
    public void Requirement_EveryDifferenceKindCites_AnEntryOfTheShippedCatalogue(
        SurfaceDifferenceKind kind)
    {
        var id = SurfaceRequirements.For(kind);

        Catalogue().Requirements.Should().Contain(r => r.Id == id,
            $"the id {id} paired with {kind} must resolve against the shipped release");
    }

    [Fact]
    public void Requirement_EachKind_CitesADifferentEntry()
    {
        var ids = Enum.GetValues<SurfaceDifferenceKind>().Select(SurfaceRequirements.For).ToList();

        ids.Should().OnlyHaveUniqueItems(
            "three differences that all cite one requirement are one difference with three names");
    }

    private static IVerificationCatalogue Catalogue() =>
        new EmbeddedVerificationCatalogue(new AsvsFlatExportParser());
}
