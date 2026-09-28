namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>An identity's effective allow and deny bits on one security token.</summary>
public sealed record AzureDevOpsEffectivePermission(int Allow, int Deny)
{
    public bool Grants(int bit) => (Allow & bit) == bit && (Deny & bit) == 0;
}
