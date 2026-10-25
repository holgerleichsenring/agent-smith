using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxBusCredentialModeReaderTests
{
    [Theory]
    [InlineData(null, SandboxBusCredentialMode.PerJob)]
    [InlineData("", SandboxBusCredentialMode.PerJob)]
    [InlineData("per-job", SandboxBusCredentialMode.PerJob)]
    [InlineData("shared", SandboxBusCredentialMode.Shared)]
    [InlineData(" Shared ", SandboxBusCredentialMode.Shared)]
    [InlineData("sharde", SandboxBusCredentialMode.PerJob)]
    public void SandboxBusCredentialModeReader_ReadsBusCredential(string? value, SandboxBusCredentialMode expected) =>
        SandboxBusCredentialModeReader.Read(value).Mode.Should().Be(expected);
}
