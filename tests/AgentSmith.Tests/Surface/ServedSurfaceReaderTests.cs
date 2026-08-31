using AgentSmith.Application.Services.Surface;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Surface;

/// <summary>
/// 2026-08-30-c6ec: the served description reduced to what a client's usage can be
/// compared against. Every rule here exists to keep a difference refutable — a property
/// the comparison invents is one no reading of the client could ever answer for.
/// </summary>
public sealed class ServedSurfaceReaderTests
{
    [Fact]
    public void Served_ARequestBodyBehindAReference_StatesThePropertiesItAccepts()
    {
        var accepted = Operation("POST", "/orders").AcceptedProperties;

        accepted.Should().BeEquivalentTo(["customerId", "discountPercent"],
            "a body that names a component still declares what the operation accepts");
    }

    [Fact]
    public void Served_APathParameter_IsNotAnAcceptedProperty()
    {
        Operation("DELETE", "/orders/{id}").AcceptedProperties.Should().BeEmpty(
            "a client that calls the operation sends its path parameter by construction, so "
            + "comparing it against a call site could only manufacture a difference");
    }

    [Fact]
    public void Served_AnErrorResponse_IsNotAReturnedProperty()
    {
        var returned = Operation("POST", "/orders").ReturnedProperties;

        returned.Should().BeEquivalentTo(["id", "internalNote"]);
        returned.Should().NotContain("conflictReason",
            "only a success response describes what a client reads");
    }

    private static ServedOperation Operation(string method, string path) =>
        new ServedSurfaceReader().Read(OrdersSpec.Spec())
            .Single(o => o.Method == method && o.Path == path);
}
