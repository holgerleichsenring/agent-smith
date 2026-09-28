using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class AzureDevOpsEffectivePermissionTests
{
    [Theory]
    [InlineData(4, 0, true)]
    [InlineData(6, 0, true)]
    [InlineData(2, 0, false)]
    [InlineData(4, 4, false)]
    public void Grants_NeedsTheBitAllowedAndNotDenied(int allow, int deny, bool granted) =>
        new AzureDevOpsEffectivePermission(allow, deny).Grants(4).Should().Be(granted);
}
