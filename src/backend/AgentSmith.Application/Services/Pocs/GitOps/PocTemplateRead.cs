using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: the app template as the branch head holds it — &lt;template_path&gt;/app/ listed with the bounded
/// recursive listing (a truncated one is refused: a template is a handful of manifests), its *.yaml and *.yml files read
/// in path order, every other file skipped.
/// </summary>
public sealed class PocTemplateRead
{
    public static string Folder(PocTargetConfig target) => $"{target.TemplatePath.TrimEnd('/')}/{PocManifestNames.AppFolder}";

    public async Task<(PocTemplate? Template, string? Refusal)> ReadAsync(
        ISourceProvider provider, BranchName branch, PocTargetConfig target, int maxFiles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(target);
        var folder = Folder(target);
        var listing = await provider.ListFilesUnderAsync(folder, branch, maxFiles, ct);
        if (listing.Truncated)
            return (null, $"the app template holds more than {maxFiles} files (pocs.gitops.max_folder_files): a template is a handful of manifests");
        if (listing.Files.Count == 0)
            return (null, $"{folder}/ holds no app template on `{branch.Value}`; run the target's Install (poc-target-install) first");
        var files = new List<PocTemplateFile>();
        var skipped = new List<string>();
        foreach (var path in listing.Files.Order(StringComparer.Ordinal))
        {
            var name = path[(folder.Length + 1)..];
            if (!IsYaml(name)) { skipped.Add(name); continue; }
            if (await provider.TryReadFileAsync(path, branch, ct) is not { } text)
                return (null, $"{path} was listed but could not be read on `{branch.Value}`");
            files.Add(new PocTemplateFile(name, text));
        }
        return (new PocTemplate(files, skipped), null);
    }

    private static bool IsYaml(string name) =>
        name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
}
