using System.Net.Sockets;

namespace PhotoBench.Core.Transport;

public sealed class TcpTransport(string host, int port) : IByteTransport
{
    private readonly TcpClient client = new() { NoDelay = true };
    private NetworkStream? stream;
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        await client.ConnectAsync(host, port, cancellationToken);
        stream = client.GetStream();
    }
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        (stream ?? throw new InvalidOperationException("Not connected")).ReadAsync(buffer, cancellationToken);
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
        (stream ?? throw new InvalidOperationException("Not connected")).WriteAsync(bytes, cancellationToken);
    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
}
