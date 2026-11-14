namespace PhotoBench.Core.Transport;

public interface IByteTransport : IAsyncDisposable
{
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
}
