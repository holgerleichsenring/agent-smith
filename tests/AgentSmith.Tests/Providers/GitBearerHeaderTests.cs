using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

/// <summary>2026-11-25-d0e2: real git reads a bearer credential's options — the header for the remote's host, none for another.</summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class GitBearerHeaderTests
{
    private const string Entra = "eyJ0eXAiOiJKV1QifQ.eyJhdWQiOiI0OTliIn0.c2lnbmF0dXJl";

    [Fact]
    public void GitCredential_Args_AreReadByGit_AndTheHeaderStaysOnTheRemotesHost()
    {
        var credential = new GitCredential(Entra, true, "https://dev.azure.com/");

        Header(credential, "https://dev.azure.com/o/p/_git/app").Should().Be($"AUTHORIZATION: bearer {Entra}");
        Header(credential, "https://example.com/other.git").Should().BeEmpty();
    }

    private static string Header(GitCredential credential, string url)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var option in credential.Args("credential.helper=")) start.ArgumentList.Add(option);
        foreach (var arg in new[] { "config", "--get-urlmatch", "http.extraheader", url }) start.ArgumentList.Add(arg);
        foreach (var (name, value) in credential.Env()!) start.Environment[name] = value;
        using var git = System.Diagnostics.Process.Start(start)!;
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }
}
