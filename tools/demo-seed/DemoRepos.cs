using System.Diagnostics;

namespace DemoSeed;

/// <summary>
/// The demo's two local repositories (type: local in demo-config.yml). A ticket branch carries the
/// approved series the way filing leaves it: a manifest under .agentsmith/series and one YAML per spec.
/// </summary>
internal static class DemoRepos
{
    public static string Root(string demoRoot) => Path.Combine(demoRoot, "repos");

    public static void Create(string demoRoot)
    {
        Init(Path.Combine(Root(demoRoot), "shop-api"), new Dictionary<string, string>
        {
            ["README.md"] = "# shop-api\n\nPricing, cart and order endpoints of the acme sample shop.\n",
            ["src/Shop.Api/Pricing/PriceCalculator.cs"] = "namespace Shop.Api.Pricing;\n\npublic sealed class PriceCalculator { }\n",
            ["src/Shop.Api/Pricing/PriceList.cs"] = "namespace Shop.Api.Pricing;\n\npublic sealed class PriceList { }\n",
            ["src/Shop.Api/Cart/CartTotals.cs"] = "namespace Shop.Api.Cart;\n\npublic sealed class CartTotals { }\n",
            [".agentsmith/contexts/default/context.yaml"] = "name: shop-api\nstack: dotnet\n",
        });
        Init(Path.Combine(Root(demoRoot), "shop-web"), new Dictionary<string, string>
        {
            ["README.md"] = "# shop-web\n\nThe storefront of the acme sample shop.\n",
            ["src/cart/CartPage.tsx"] = "export function CartPage() { return null; }\n",
            [".agentsmith/contexts/default/context.yaml"] = "name: shop-web\nstack: node\n",
        });
    }

    /// <summary>Commits a ticket's series on agent-smith/{ticket} and returns the commit sha.</summary>
    public static string WriteSeries(string demoRoot, string repo, string ticketId, string seriesBase, string goal,
        IReadOnlyList<Story.DemoSpec> specs, string approvedBy, string conversation, DateTimeOffset approvedAt)
    {
        var dir = Path.Combine(Root(demoRoot), repo);
        var branch = $"agent-smith/{ticketId}";
        Git(dir, "checkout", "-q", "-B", branch, "main");
        var at = approvedAt.ToString("O");
        Write(dir, $".agentsmith/series/{seriesBase}.yaml",
            $"ticket: github-{ticketId}\ngoal: \"{goal}\"\nspecs:\n{string.Join("\n", specs.Select(s => $"- {s.Id}"))}\n"
            + $"revisions:\n- number: 1\n  cause: initial\n  at: {at}\n"
            + $"approved_at: {at}\napproved_in_conversation: {conversation}\napproved_by: {approvedBy}\n");
        foreach (var spec in specs)
            Write(dir, $".agentsmith/specs/planned/{spec.Id}-{spec.Label}.yaml", Story.SpecYaml(spec, ticketId));
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", $"specs: approved series {seriesBase} for #{ticketId}");
        var sha = Git(dir, "rev-parse", "HEAD").Trim();
        Git(dir, "checkout", "-q", "main");
        return sha;
    }

    private static void Init(string dir, IReadOnlyDictionary<string, string> files)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q", "-b", "main");
        Git(dir, "config", "user.name", "Sam Sample");
        Git(dir, "config", "user.email", "sam@example.com");
        foreach (var (path, content) in files) Write(dir, path, content);
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "initial import");
    }

    private static void Write(string dir, string path, string content)
    {
        var full = Path.Combine(dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static string Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', args)} in {dir}: {error}");
        return output;
    }
}
