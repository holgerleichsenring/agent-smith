namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0197: temp directory holding a minimal fixture project. Dispose
/// removes the dir. Fixture content is inlined here — small, no separate
/// tests/fixtures/ tree to keep in sync.
/// </summary>
internal sealed class FixtureWorkdir : IAsyncDisposable
{
    public string Path { get; }

    private FixtureWorkdir(string path)
    {
        Path = path;
    }

    public static async Task<FixtureWorkdir> CreateConsoleProjectAsync(string projectName, string? extraPackage = null)
    {
        var dir = NewTempDir();
        var packageRef = extraPackage is null
            ? string.Empty
            : $"""
              <ItemGroup>
                <PackageReference Include="{extraPackage}" Version="13.0.3" />
              </ItemGroup>
              """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(dir, $"{projectName}.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              {packageRef}
            </Project>
            """);
        await File.WriteAllTextAsync(System.IO.Path.Combine(dir, "Program.cs"),
            "Console.WriteLine(\"hello\");\n");
        return new FixtureWorkdir(dir);
    }

    public static async Task<FixtureWorkdir> CreateXunitProjectAsync(string projectName)
    {
        var dir = NewTempDir();
        await File.WriteAllTextAsync(System.IO.Path.Combine(dir, $"{projectName}.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
                <PackageReference Include="xunit" Version="2.9.0" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(System.IO.Path.Combine(dir, "TrivialTests.cs"),
            """
            using Xunit;
            public class TrivialTests
            {
                [Fact]
                public void OnePlusOne_EqualsTwo() => Assert.Equal(2, 1 + 1);
            }
            """);
        return new FixtureWorkdir(dir);
    }

    public static async Task<FixtureWorkdir> CreatePrivateFeedConsoleProjectAsync(
        string projectName, string feedUrl, string token)
    {
        var fixture = await CreateConsoleProjectAsync(projectName);
        // Operator-style NuGet.Config: feed declared with cleartext password
        // populated from the env-supplied PAT. This is the exact shape the
        // agent (post-p0191) would produce on user-config level.
        await File.WriteAllTextAsync(System.IO.Path.Combine(fixture.Path, "NuGet.Config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                <add key="private" value="{feedUrl}" />
              </packageSources>
              <packageSourceCredentials>
                <private>
                  <add key="Username" value="any" />
                  <add key="ClearTextPassword" value="{token}" />
                </private>
              </packageSourceCredentials>
            </configuration>
            """);
        return fixture;
    }

    public static FixtureWorkdir CreatePackageJson(string name, string dependencies)
    {
        var dir = NewTempDir();
        File.WriteAllText(System.IO.Path.Combine(dir, "package.json"),
            $$"""
            {
              "name": "{{name}}",
              "version": "0.0.0",
              "private": true,
              "dependencies": { {{dependencies}} }
            }
            """);
        return new FixtureWorkdir(dir);
    }

    public static FixtureWorkdir CreateEmpty()
    {
        return new FixtureWorkdir(NewTempDir());
    }

    private static string NewTempDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"agentsmith-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch { /* test cleanup, best-effort */ }
        return ValueTask.CompletedTask;
    }
}
