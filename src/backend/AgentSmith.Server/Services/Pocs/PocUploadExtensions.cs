using AgentSmith.Server.Services.Pocs.Import;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>2026-11-23-933db: the PoC upload and import services, the run gate and the prefix findings.</summary>
internal static class PocUploadExtensions
{
    internal static IServiceCollection AddPocImports(this IServiceCollection services) =>
        services.AddSingleton<PocUploadBody>().AddScoped<PocUploadStore>().AddScoped<PocIdentity>().AddScoped<PocUploadAdmission>()
            .AddScoped<PocProjectProvisioner>().AddScoped<PocRunGate>().AddScoped<PocImportChoice>().AddScoped<PocImportLauncher>()
            .AddSingleton<PocPrefixFindings>().AddSingleton<PocSandboxFinding>()
            .AddSingleton<Revisions.PocOverruleRule>().AddScoped<Revisions.PocRevisionReader>().AddScoped<Revisions.PocOverruleLauncher>();
}
