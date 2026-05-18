using AgentSmith.Application.Services.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// p0140a: ADO area paths come from operators in two forms (\ native, / safer for YAML).
/// The normalizer must treat both identically and the prefix matcher must respect path-
/// segment boundaries so siblings like ContosoMain\Billing and ContosoMain\BillingOther
/// don't collide.
/// </summary>
public sealed class AreaPathNormalizerTests
{
    private static bool Prefix(string parent, string child) => AreaPathNormalizer.IsPrefix(parent, child);

    [Fact]
    public void ForwardSlashAndBackslash_ResolveIdentically()
    {
        Prefix("ContosoMain/Billing", "ContosoMain\\Billing\\Invoicing").Should().BeTrue();
        Prefix("ContosoMain\\Billing", "ContosoMain/Billing/Invoicing").Should().BeTrue();
    }

    [Fact]
    public void IsPrefix_ExactMatch_ReturnsTrue()
    {
        Prefix("ContosoMain\\Billing", "ContosoMain\\Billing").Should().BeTrue();
    }

    [Fact]
    public void IsPrefix_SiblingPath_ReturnsFalse()
    {
        Prefix("ContosoMain\\Billing", "ContosoMain\\BillingOther").Should().BeFalse();
    }
}
