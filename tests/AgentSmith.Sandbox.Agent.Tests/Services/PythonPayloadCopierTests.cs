using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// p0357: the carrier's relocatable CPython payload is copied next to the injected
/// agent binary — exec bits preserved, symlinks re-created as links (a byte-copied
/// python3 -> python3.12 link would break the interpreter), absent payload a no-op.
/// </summary>
public sealed class PythonPayloadCopierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pypayload-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void CopyIfPresent_PayloadPresent_CopiedWithExecBitsAndSymlinks()
    {
        var source = Path.Combine(_root, "python");
        var bin = Path.Combine(source, "bin");
        Directory.CreateDirectory(bin);
        var interpreter = Path.Combine(bin, "python3.12");
        File.WriteAllText(interpreter, "#!/fake");
        File.SetUnixFileMode(interpreter,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        File.CreateSymbolicLink(Path.Combine(bin, "python3"), "python3.12");
        Directory.CreateDirectory(Path.Combine(source, "lib"));
        File.WriteAllText(Path.Combine(source, "lib", "libpython.so"), "lib");

        var target = Path.Combine(_root, "shared", "python");
        new PythonPayloadCopier(NullLogger<PythonPayloadCopier>.Instance)
            .CopyIfPresent(source, target);

        var copiedInterpreter = Path.Combine(target, "bin", "python3.12");
        File.Exists(copiedInterpreter).Should().BeTrue();
        File.GetUnixFileMode(copiedInterpreter).Should().HaveFlag(UnixFileMode.UserExecute);
        var link = new FileInfo(Path.Combine(target, "bin", "python3"));
        link.LinkTarget.Should().Be("python3.12", "symlinks are re-created as links, not byte copies");
        File.Exists(Path.Combine(target, "lib", "libpython.so")).Should().BeTrue();
    }

    [Fact]
    public void CopyIfPresent_NoPayload_NoOp()
    {
        var target = Path.Combine(_root, "shared", "python");

        new PythonPayloadCopier(NullLogger<PythonPayloadCopier>.Instance)
            .CopyIfPresent(Path.Combine(_root, "missing"), target);

        Directory.Exists(target).Should().BeFalse("an absent payload must not create anything or throw");
    }
}
