using AgentSmith.Contracts.Models;

namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: the inline ticket describing the sample project's seeded bug. Kept
/// next to the runner (not in the tarball) so the ticket text and the sample's
/// PriceCalculator/tests stay reviewable side by side in this assembly.
/// </summary>
internal static class DemoTicket
{
    public static InlineTicket Create() => new(
        Title: "Bulk discount is not applied to orders of exactly 100.00",
        Description:
            "PriceCalculator.Total (src/Sample/PriceCalculator.cs) applies the 10% bulk "
            + "discount only to orders strictly above 100.00. The business rule — pinned by "
            + "tests/Sample.Tests/PriceCalculatorTests.cs — says orders of exactly 100.00 "
            + "qualify too. Fix the boundary condition so the failing test passes; "
            + "change nothing else.",
        ReproSteps:
            "dotnet test tests/Sample.Tests/Sample.Tests.csproj — "
            + "Total_ExactlyAtBulkThreshold_GetsDiscount fails: expected 90.00, got 100.00.");
}
