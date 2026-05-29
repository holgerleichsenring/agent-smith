using AgentSmith.Application.Services.Loop;
using FluentAssertions;

namespace AgentSmith.Tests.SubAgents;

public sealed class SubAgentNameValidatorTests
{
    private readonly SubAgentNameValidator _sut = new();

    [Theory]
    [InlineData("agent1")]
    [InlineData("AGENT42")]
    [InlineData("Agent007")]
    public void SubAgentNameValidator_GenericRegex_Rejected_AgentDigits(string name)
        => _sut.IsValid(name).Should().BeFalse();

    [Theory]
    [InlineData("sub1")]
    [InlineData("SUB99")]
    public void SubAgentNameValidator_GenericRegex_Rejected_SubDigits(string name)
        => _sut.IsValid(name).Should().BeFalse();

    [Theory]
    [InlineData("child1")]
    [InlineData("Child2")]
    public void SubAgentNameValidator_GenericRegex_Rejected_ChildDigits(string name)
        => _sut.IsValid(name).Should().BeFalse();

    [Theory]
    [InlineData("worker")]
    [InlineData("helper")]
    [InlineData("runner")]
    [InlineData("executor")]
    [InlineData("processor")]
    public void SubAgentNameValidator_SingleWordGenerics_Rejected_Worker_Helper_Runner(string name)
        => _sut.IsValid(name).Should().BeFalse();

    [Theory]
    [InlineData("ContextMapInvestigator")]
    [InlineData("UploadHandlerAuditor")]
    [InlineData("SecuritySurfaceScanner")]
    public void SubAgentNameValidator_DescriptiveName_Accepted_ContextMapInvestigator(string name)
        => _sut.IsValid(name).Should().BeTrue();
}
