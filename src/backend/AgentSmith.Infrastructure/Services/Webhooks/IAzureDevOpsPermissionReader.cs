namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Reads a named identity's EFFECTIVE permission in an Azure DevOps security namespace —
/// explicit grants, grants through group membership and grants inherited from parent tokens
/// alike. <paramref name="tokens"/> run from the most specific to the least; the first token
/// that carries an access control list answers. <c>null</c> when none does.
/// </summary>
public interface IAzureDevOpsPermissionReader
{
    Task<AzureDevOpsEffectivePermission?> ReadAsync(
        string organizationUrl, Guid securityNamespaceId, IReadOnlyList<string> tokens,
        Guid identityId, CancellationToken cancellationToken);
}
