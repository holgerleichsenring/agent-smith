using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

public sealed class ServerWritesScannerTests
{
    [Theory]
    [InlineData("await provider.AddLabelAsync(id, label, ct);")]
    [InlineData("await transitioner.TransitionAsync(id, a, b, ct);")]
    [InlineData("await links.LinkAsync(a, b, ct);")]
    [InlineData("await source.WriteFilesToBranchAsync(repo, branch, files, ct);")]
    public void Writes_AWriteMember_IsFound(string source) =>
        ServerWritesScanner.Writes(source).Should().BeTrue();

    [Fact]
    public void Writes_CreateAsync_CountsOnlyBesideITicketProvider()
    {
        ServerWritesScanner.Writes("ITicketProvider p; await p.CreateAsync(draft, ct);").Should().BeTrue();
        ServerWritesScanner.Writes("await scope.CreateAsync(options, ct);").Should().BeFalse();
    }

    [Fact]
    public void Writes_CloseAsync_CountsOnlyBesideIPullRequestCloser()
    {
        ServerWritesScanner.Writes("IPullRequestCloser c; await c.CloseAsync(pr, ct);").Should().BeTrue();
        ServerWritesScanner.Writes("await stream.CloseAsync();").Should().BeFalse();
    }

    [Fact]
    public void Writes_AReadCall_IsNotFound() =>
        ServerWritesScanner.Writes("var t = await provider.GetTicketAsync(id, ct);").Should().BeFalse();
}
