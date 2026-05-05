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
}
