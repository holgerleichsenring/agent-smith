using AgentSmith.Application.Services.Surface;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Surface;

/// <summary>
/// 2026-08-30-c6ec: reading the call-site report, and matching what it names against the
/// served description. Both are places where being wrong in the quiet direction — an
/// unreadable answer read as "calls nothing", a path template read as a different
/// operation — turns an exercised interface into a page of differences.
/// </summary>
public sealed class ClientUsageReadingTests
{
    [Fact]
    public void Reading_AnAnswerWithNoReadableObject_IsNotAClaimThatNothingIsCalled()
    {
        ClientUsageReportReader.Read("I could not find any calls, sorry.").Should().BeNull();
        ClientUsageReportReader.Read(null).Should().BeNull();
        ClientUsageReportReader.Read("{\"call_sites\": [").Should().BeNull();
    }

    [Fact]
    public void Reading_AnAnswerWrappedInProse_IsRead()
    {
        var reported = ClientUsageReportReader.Read(
            """
            Here is what I found:
            {"call_sites":[{"file":"src/orders.ts","operation":"POST /orders",
              "sends":["customerId"],"reads":["id"]}],
             "undecided":[{"file":"src/generated.ts","why":"the client is generated"}]}
            """);

        reported.Should().NotBeNull();
        reported!.CallSites.Should().ContainSingle()
            .Which.PropertiesSent.Should().Equal("customerId");
        reported.Undecided.Should().ContainSingle()
            .Which.Why.Should().Be("the client is generated");
    }

    [Fact]
    public void Reading_AnEmptyReport_IsAClaimTheReaderIsAllowedToMake()
    {
        var reported = ClientUsageReportReader.Read("""{"call_sites":[],"undecided":[]}""");

        reported.Should().NotBeNull();
        reported!.CallSites.Should().BeEmpty();
    }

    [Theory]
    [InlineData("GET /orders/{id}")]
    [InlineData("get /orders/:orderId")]
    [InlineData("GET /orders/${orderId}")]
    [InlineData("GET  /orders/{orderId}")]
    public void Matching_AClientsOwnPathTemplating_IsTheSameOperation(string asTheClientWritesIt)
    {
        var served = new ServedOperation("GET", "/orders/{id}", "getOrder", [], []);
        var usage = new ClientUsageReport(
            [new ClientCallSite("src/orders.ts", asTheClientWritesIt, [], [])],
            ClientExtractionAccount.Empty);

        new SurfaceDifferenceCalculator().Compute([served], usage)
            .Differences.Should().BeEmpty("the parameter's name is the client's own vocabulary");
    }

    [Fact]
    public void Matching_AClientAddressingTheOperationById_IsTheSameOperation()
    {
        var served = new ServedOperation("GET", "/orders/{id}", "getOrder", [], []);
        var usage = new ClientUsageReport(
            [new ClientCallSite("src/orders.ts", "getOrder", [], [])], ClientExtractionAccount.Empty);

        new SurfaceDifferenceCalculator().Compute([served], usage)
            .Differences.Should().BeEmpty();
    }
}
