using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class BinaryInjectorTests
{
    [Fact]
    public void Inject_CopiesProcessBinaryToTarget()
    {
        var target = Path.Combine(Path.GetTempPath(), $"inject-test-{Guid.NewGuid():N}");
        var injector = new BinaryInjector(NullLogger<BinaryInjector>.Instance);

        try
        {
            var exit = injector.Inject(target);
            exit.Should().Be(0);
            File.Exists(target).Should().BeTrue();
            new FileInfo(target).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(target)) File.Delete(target);
        }
    }

    [Fact]
    public void Inject_CreatesParentDirectoryIfMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"inject-dir-{Guid.NewGuid():N}");
        var target = Path.Combine(directory, "agent");
        var injector = new BinaryInjector(NullLogger<BinaryInjector>.Instance);

        try
        {
            injector.Inject(target);
            File.Exists(target).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    // 2026-10-16-8648: with a launcher in the carrier, the target path runs the launcher and both
    // agent builds sit beside it, so one command line starts on glibc and on musl images.
    [Fact]
    public void BinaryInjector_WithALauncher_PlacesLauncherGnuAgentAndMuslTree()
    {
        var carrier = Directory.CreateTempSubdirectory("carrier-").FullName;
        var shared = Path.Combine(Path.GetTempPath(), $"shared-{Guid.NewGuid():N}");
        try
        {
            var launcher = Path.Combine(carrier, "launcher");
            File.WriteAllText(launcher, "launcher");
            Directory.CreateDirectory(Path.Combine(carrier, "musl", "lib"));
            File.WriteAllText(Path.Combine(carrier, "musl", "agent"), "musl agent");
            File.WriteAllText(Path.Combine(carrier, "musl", "lib", "libstdc++.so.6"), "lib");

            new BinaryInjector(NullLogger<BinaryInjector>.Instance, launcher, Path.Combine(carrier, "musl"))
                .Inject(Path.Combine(shared, "agent"));

            File.ReadAllText(Path.Combine(shared, "agent")).Should().Be("launcher");
            new FileInfo(Path.Combine(shared, BinaryInjector.GnuAgentName)).Length
                .Should().Be(new FileInfo(Environment.ProcessPath!).Length);
            File.ReadAllText(Path.Combine(shared, "musl", "agent")).Should().Be("musl agent");
            File.Exists(Path.Combine(shared, "musl", "lib", "libstdc++.so.6")).Should().BeTrue();
            if (!OperatingSystem.IsWindows())
                File.GetUnixFileMode(Path.Combine(shared, "agent")).Should().HaveFlag(UnixFileMode.OtherExecute);
        }
        finally
        {
            Directory.Delete(carrier, recursive: true);
            if (Directory.Exists(shared)) Directory.Delete(shared, recursive: true);
        }
    }

    [Fact]
    public void BinaryInjector_WithoutALauncher_CopiesItselfAsBefore()
    {
        var shared = Path.Combine(Path.GetTempPath(), $"shared-{Guid.NewGuid():N}");
        try
        {
            new BinaryInjector(NullLogger<BinaryInjector>.Instance, Path.Combine(shared, "no-launcher"), "/no-musl")
                .Inject(Path.Combine(shared, "agent"));

            new FileInfo(Path.Combine(shared, "agent")).Length.Should().Be(new FileInfo(Environment.ProcessPath!).Length);
            File.Exists(Path.Combine(shared, BinaryInjector.GnuAgentName)).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(shared)) Directory.Delete(shared, recursive: true);
        }
    }
}
