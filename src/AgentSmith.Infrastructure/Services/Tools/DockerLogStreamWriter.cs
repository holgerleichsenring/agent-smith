using System.Text;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Tools;

/// <summary>
/// Stream adapter that buffers Docker container stderr output
/// and forwards complete lines to the logger.
/// </summary>
internal sealed class DockerLogStreamWriter(
    ILogger logger, string toolName) : Stream
{
    private readonly StringBuilder _buffer = new();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Append(Encoding.UTF8.GetString(buffer, offset, count));
        while (_buffer.ToString().Contains('\n'))
        {
            var content = _buffer.ToString();
            var idx = content.IndexOf('\n');
            var line = content[..idx].TrimEnd('\r');
            _buffer.Remove(0, idx + 1);
            if (!string.IsNullOrWhiteSpace(line))
                logger.LogInformation("[{Tool}] {Line}", toolName, line);
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => 0;
    public override long Position { get => 0; set { } }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => 0;
    public override long Seek(long offset, SeekOrigin origin) => 0;
    public override void SetLength(long value) { }
}
