using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public sealed class SecretsProviderTests
{
    private readonly SecretsProvider _secrets = new();

    [Fact]
    public void GetRequired_EnvVarExists_ReturnsValue()
    {
        var key = $"TEST_SECRET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "secret-value");

        try
        {
            var value = _secrets.GetRequired(key);
            value.Should().Be("secret-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GetRequired_EnvVarMissing_ThrowsConfigurationException()
    {
        var key = $"MISSING_{Guid.NewGuid():N}";

        var act = () => _secrets.GetRequired(key);

        act.Should().Throw<ConfigurationException>()
            .WithMessage($"*{key}*");
    }

    [Fact]
    public void GetOptional_EnvVarExists_ReturnsValue()
    {
        var key = $"TEST_OPT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "opt-value");

        try
        {
            var value = _secrets.GetOptional(key);
            value.Should().Be("opt-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GetOptional_EnvVarMissing_ReturnsNull()
    {
        var key = $"MISSING_{Guid.NewGuid():N}";

        var value = _secrets.GetOptional(key);

        value.Should().BeNull();
    }
}
