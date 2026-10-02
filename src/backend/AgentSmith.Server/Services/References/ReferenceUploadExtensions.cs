namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the website upload's guards and its one store step. Registered beside the
/// spec-dialog services, whose own registration file is at its length limit.
/// </summary>
internal static class ReferenceUploadExtensions
{
    internal static IServiceCollection AddReferenceUploads(this IServiceCollection services)
    {
        services.AddSingleton<ReferenceUploadBody>();
        services.AddSingleton<ReferencePathRule>();
        services.AddSingleton<ReferenceIgnoreList>();
        services.AddSingleton<ReferenceFileTypes>();
        services.AddSingleton<ZipEntryChecksum>();
        services.AddSingleton<ReferenceZipReader>();
        services.AddSingleton<ReferenceSetValidator>();
        services.AddScoped<ReferenceSetUpload>();
        return services;
    }
}
