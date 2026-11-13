using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-10-14-dbceo: what keeps a design turn and folds a long conversation — scoped for the session
/// repository's unit of work, which both read and write through. 2026-11-04-96a0b: and what names an upload stored
/// while the turn ran, and what keeps the thread a turn ended with — 2026-11-12-92e9a: and writes it as it grows.</summary>
internal static class SpecDialogDigestExtensions
{
    internal static IServiceCollection AddSpecDialogDigest(this IServiceCollection services) =>
        services.AddScoped<SpecDialogTurnKeeper>().AddScoped<SpecDialogDigestWriter>().AddScoped<SpecDialogUploadArrivals>()
            .AddScoped<SpecDialogThreadStore>().AddSingleton<DialogThreadCodec>().AddScoped<SpecDialogThreadWriteLink>();
}
