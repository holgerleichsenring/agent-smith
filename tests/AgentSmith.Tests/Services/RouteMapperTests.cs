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

    // ASP.NET style: class-level [Route("api/masterdata")] + method-level [HttpGet("user")].
    // Pre-DotNetRouteExtractor this was unmatched because the regex saw only "user" while
    // swagger reported "/api/Masterdata/user" — the gap behind Drop 2 from the AuthPort run.
    [Fact]
    public void DotNet_CombinesClassRouteWithMethodRoute()
    {
        File.WriteAllText(Path.Combine(_temp, "MasterdataController.cs"), """
            [Route("api/masterdata")]
            [ApiController]
            public class MasterdataController : ControllerBase
            {
                [HttpGet("user")]
                public IActionResult Filter() => Ok();
            }
            """);
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/masterdata/user")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("dotnet");
        routes[0].Confidence.Should().Be(1.0);
    }

    // [Route("api/[controller]")] token replacement — the [controller] placeholder
    // must resolve to the controller name minus the "Controller" suffix to match
    // ASP.NET's conventional binding behavior.
    [Fact]
    public void DotNet_ResolvesControllerToken()
    {
        File.WriteAllText(Path.Combine(_temp, "TrafficTypeController.cs"), """
            [Route("api/[controller]")]
            public class TrafficTypeController : ControllerBase
            {
                [HttpGet("{id}")]
                public IActionResult Get(int id) => Ok();
            }
            """);
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/TrafficType/{id}")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("dotnet");
        routes[0].Confidence.Should().Be(1.0);
    }

    // Method-level [Route("subpath")] paired with a verb-attribute that has no inline
    // path. Both forms are real ASP.NET — the extractor pairs them by proximity.
    [Fact]
    public void DotNet_PairsMethodRouteWithVerbAttribute()
    {
        File.WriteAllText(Path.Combine(_temp, "UserAssignmentController.cs"), """
            [Route("api/userassignment")]
            public class UserAssignmentController : ControllerBase
            {
                [HttpGet]
                [Route("user/{userId}")]
                public IActionResult ByUser(string userId) => Ok();
            }
            """);
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/userassignment/user/{userId}")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Confidence.Should().Be(1.0);
    }

    // Minimal-API style still works alongside the controller-based extractor.
    [Fact]
    public void DotNet_MinimalApiMapStillWorks()
    {
        File.WriteAllText(Path.Combine(_temp, "Program.cs"),
            "app.MapGet(\"/api/health\", () => \"ok\");");
        var routes = _mapper.MapRoutes([Endpoint("GET", "/api/health")], _temp);
        routes.Should().HaveCount(1);
        routes[0].Framework.Should().Be("dotnet");
    }
}
