using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class FindingHandlerCorrelatorTests
{
    private readonly FindingHandlerCorrelator _sut = new();

    private static RouteHandlerLocation Route(string method, string path, double conf = 0.9) =>
        new(method, path, "src/UserController.cs", 42, 60, "snippet", "dotnet", conf);

    private static ApiCodeContext CodeContext(params RouteHandlerLocation[] routes) =>
        new(routes, [], [], [], 1.0);

    private static NucleiResult Nuclei(params NucleiFinding[] findings) =>
        new(findings, 1, "");

    private static ZapResult Zap(params ZapFinding[] findings) =>
        new(findings, 1, "active");

    [Fact]
    public void Correlate_NucleiUrlMatchesRouteTemplate_AttachesHandler()
    {
        var ctx = CodeContext(Route("GET", "/api/users/{id}"));
        var n = Nuclei(new NucleiFinding("get-bola", "BOLA", "high", "https://api.example.com/api/users/42", null, null));

        var result = _sut.Correlate(n, null, ctx);

        result.Should().HaveCount(1);
        result[0].Handler.Should().NotBeNull();
        result[0].Handler!.Path.Should().Be("/api/users/{id}");
    }

    [Fact]
    public void Correlate_FindingUrlNotInRoutes_NoHandler()
    {
        var ctx = CodeContext(Route("GET", "/api/users/{id}"));
        var n = Nuclei(new NucleiFinding("misc-test", "Test", "low", "https://api.example.com/health", null, null));

        var result = _sut.Correlate(n, null, ctx);

        result.Should().HaveCount(1);
        result[0].Handler.Should().BeNull();
    }

    [Fact]
    public void Correlate_QueryStringStripped_StillMatches()
    {
        var ctx = CodeContext(Route("GET", "/api/users/{id}"));
        var n = Nuclei(new NucleiFinding("get-test", "Test", "medium", "https://api.example.com/api/users/42?include=details", null, null));

        var result = _sut.Correlate(n, null, ctx);

        result[0].Handler.Should().NotBeNull();
    }

    [Fact]
    public void Correlate_TrailingSlashNormalized_StillMatches()
    {
        var ctx = CodeContext(Route("GET", "/api/users"));
        var n = Nuclei(new NucleiFinding("get-test", "Test", "low", "https://api.example.com/api/users/", null, null));

        var result = _sut.Correlate(n, null, ctx);

        result[0].Handler.Should().NotBeNull();
    }

    [Fact]
    public void Correlate_RouteConfidenceBelowThreshold_NotMatched()
    {
        var ctx = CodeContext(Route("GET", "/api/users/{id}", conf: 0.3));
        var n = Nuclei(new NucleiFinding("get-test", "Test", "low", "https://api.example.com/api/users/42", null, null));

        var result = _sut.Correlate(n, null, ctx);

        result[0].Handler.Should().BeNull();
    }

    [Fact]
    public void Correlate_NoApiCodeContext_EmitsCorrelationsWithoutHandlers()
    {
        var n = Nuclei(new NucleiFinding("get-test", "Test", "low", "https://api.example.com/api/users/42", null, null));

        var result = _sut.Correlate(n, null, null);

        result.Should().HaveCount(1);
        result[0].Handler.Should().BeNull();
    }

    [Fact]
    public void Correlate_ZapFinding_AlsoCorrelated()
    {
        var ctx = CodeContext(Route("POST", "/api/orders"));
        var z = Zap(new ZapFinding("10001", "SQL Injection", "High", "Medium",
            "https://api.example.com/api/orders?id=1", "desc", null, null, null, 1));

        var result = _sut.Correlate(null, z, ctx);

        result.Should().HaveCount(1);
        result[0].FindingSource.Should().Be("zap");
        // ZAP method extraction is not implemented (method = "") — so any-method matches
        result[0].Handler.Should().NotBeNull();
    }

    [Fact]
    public void Correlate_NucleiPostMethodHint_RestrictsToPostRoute()
    {
        var getRoute = Route("GET", "/api/users/{id}");
        var postRoute = Route("POST", "/api/users/{id}");
        var ctx = CodeContext(getRoute, postRoute);
        var n = Nuclei(new NucleiFinding("post-bola", "Post BOLA", "high", "https://api.example.com/api/users/42", null, null));

        var result = _sut.Correlate(n, null, ctx);

        result[0].Handler!.Method.Should().Be("POST");
    }
}
