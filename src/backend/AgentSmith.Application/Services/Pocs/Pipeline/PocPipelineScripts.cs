using System.Text;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: the job scripts of the build pipeline, filled for one host. Every input reaches them through an
/// environment variable; the guard every job runs first checks each against its pattern, the folder against the target's
/// regex-escaped root. A root holding anything but letters, digits, '.', '_', '-' and '/' is refused, because it is
/// written into a pattern and a quoted shell word.
/// </summary>
public sealed class PocPipelineScripts(PocPipelineResources resources, PocYamlFill fill)
{
    private static readonly Dictionary<string, string> NoBlocks = [];

    public IReadOnlyDictionary<string, string> For(PocPipelineValues values, PocHostShape host)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(host);
        var scalars = new Dictionary<string, string>
        {
            ["ROOT_REGEX"] = RootRegex(values.Root), ["CONTEXT"] = host.Context, ["IN"] = host.ImageIn,
            ["IMAGE"] = values.ImageRepository, ["REPO_URL"] = host.RepoUrl,
        };
        return new Dictionary<string, string>
        {
            ["GUARD"] = Script("guard.sh", scalars), ["BUILD"] = Script("build.sh", scalars),
            ["CHECKOUT"] = Script("checkout.sh", scalars, ("AUTH_ON", host.AuthOn), ("AUTH_OFF", host.AuthOff)),
            ["SCAN"] = Script("scan.sh", With(scalars, "OUT", host.ScanOut), ("TRIVY_FN", host.TrivyRun),
                ("REPORT_TPL", resources.Text("poc-report.tpl")), ("GATE_REGO", resources.Text("poc-gate.rego"))),
            ["PUSH"] = Script("push.sh", With(scalars, "OUT", host.PushOut), ("LOGIN", host.Login)),
        };
    }

    /// <summary>The root as an ERE fragment: '.' escaped, every other allowed character literal.</summary>
    private static string RootRegex(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/')))
            throw new ArgumentException($"pocs.targets[].root '{root}' holds a character the build pipeline cannot match safely; "
                + "use letters, digits, '.', '_', '-' and '/'");
        return new StringBuilder(root).Replace(".", "\\.").ToString();
    }

    private string Script(string name, Dictionary<string, string> scalars, params (string Name, string Block)[] blocks)
    {
        var map = blocks.ToDictionary(b => b.Name, b => b.Block);
        var text = fill.Fill(resources.Text(name), scalars, map.Count == 0 ? NoBlocks : map);
        return text.TrimEnd('\n');
    }

    private static Dictionary<string, string> With(Dictionary<string, string> scalars, string key, string value) =>
        new(scalars) { [key] = value };
}
