using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Security.Code;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class RouteMapperTests : IDisposable
{
    private readonly string _temp;
    private readonly RouteMapper _mapper = new(NullLogger<RouteMapper>.Instance);

    public RouteMapperTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "rm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose() { try { Directory.Delete(_temp, true); } catch { } }

    private static ApiEndpoint Endpoint(string method, string path) =>
        new(method, path, null, [], false, null, null);

    [Fact]
    public void DotNet_MapsHttpAttributes()
    {
        File.WriteAllText(Path.Combine(_temp, "OrdersController.cs"),
            "[HttpGet(\"/api/orders/{id}\")]\npublic IActionResult Get(int id) { }");
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/orders/{id}")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("dotnet");
        routes[0].Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Express_MapsRouterMethods()
    {
        File.WriteAllText(Path.Combine(_temp, "users.js"),
            "router.get('/api/users/:id', (req, res) => res.json({}))");
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/users/{id}")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("express");
    }

    [Fact]
    public void FastApi_MapsDecorators()
    {
        File.WriteAllText(Path.Combine(_temp, "main.py"),
            "@app.post(\"/api/items\")\nasync def create(): pass");
        var routes = _mapper.MapRoutes([Endpoint("POST", "/api/items")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("fastapi");
    }

    [Fact]
    public void Spring_MapsRequestMapping()
    {
        File.WriteAllText(Path.Combine(_temp, "OrderController.java"),
            "@DeleteMapping(\"/api/orders/{id}\")\npublic void delete(@PathVariable int id) { }");
        var routes = _mapper.MapRoutes([Endpoint("DELETE", "/api/orders/{id}")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("spring");
    }

    [Fact]
    public void Unmatched_LowConfidenceEntry()
    {
        // Path exists in source but verb differs from swagger — entry kept at confidence 0.5
        // so downstream skills can filter (decision: Confidence < 0.5 → no finding).
        File.WriteAllText(Path.Combine(_temp, "Ctrl.cs"),
            "[HttpPut(\"/api/orders/{id}\")]\npublic IActionResult Put(int id) { }");
        var routes = _mapper.MapRoutes([Endpoint("DELETE", "/api/orders/{id}")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Confidence.Should().Be(0.5);
    }

    [Fact]
    public void NoMatch_NotInList()
    {
        File.WriteAllText(Path.Combine(_temp, "App.cs"),
            "[HttpGet(\"/internal/health\")]\npublic IActionResult H() { }");
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/users/{id}")], _temp);
        routes.Should().BeEmpty();
    }
}
