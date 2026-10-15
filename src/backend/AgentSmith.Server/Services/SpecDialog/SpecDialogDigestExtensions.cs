using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-10-14-dbceo: what keeps a design turn and folds a long conversation — scoped for the session
/// repository's unit of work, which both read and write through.</summary>
internal static class SpecDialogDigestExtensions
{
    internal static IServiceCollection AddSpecDialogDigest(this IServiceCollection services) =>
        services.AddScoped<SpecDialogTurnKeeper>().AddScoped<SpecDialogDigestWriter>();
}
