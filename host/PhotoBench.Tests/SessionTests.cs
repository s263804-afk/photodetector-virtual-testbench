using System.Threading.Channels;
using PhotoBench.Core.Protocol;
using PhotoBench.Core.Session;
using PhotoBench.Core.Transport;

namespace PhotoBench.Tests;

// Unit-level byte transport only; real ARM integration is a separate required test layer.
internal sealed class ScriptTransport : IByteTransport
{
    private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
    private byte[] remaining = []; private int offset;
    public List<byte[]> Writes { get; } = [];
    public bool Closed { get; private set; }
    public Action<Frame>? OnRequest { get; set; }
    public Action? OnWriteStarted { get; set; }
    public void Queue(params byte[][] chunks) { foreach (byte[] chunk in chunks) input.Writer.TryWrite(chunk); }
    public void Respond(Frame frame) => Queue(FrameCodec.Encode(frame));
    public ValueTask ConnectAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        Writes.Add(bytes.ToArray()); OnWriteStarted?.Invoke(); ct.ThrowIfCancellationRequested();
        Frame request = new FrameParser().Push(bytes.Span).Single();
        if (request.Command == "HELLO") Respond(new(0, "HELLO", "fw=1.0.0;protocol=1;mode=VIRTUAL_FEED"));
        else OnRequest?.Invoke(request);
        return ValueTask.CompletedTask;
    }
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (offset == remaining.Length)
        {
            remaining = await input.Reader.ReadAsync(ct); offset = 0;
            if (remaining.Length == 0) return 0;
        }
        int n = Math.Min(buffer.Length, remaining.Length - offset);
        remaining.AsMemory(offset, n).CopyTo(buffer); offset += n; return n;
    }
    public ValueTask DisposeAsync() { Closed = true; return ValueTask.CompletedTask; }
}

public sealed class SessionTests
{
    private static DeviceSession New(ScriptTransport transport) => new(transport, new(20, 3));

    [Fact]
    public async Task RetryReusesExactBytesAndAcceptsFragmentedMatchingReply()
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        int attempts = 0;
        wire.OnRequest = f => {
            if (++attempts != 2) return;
            byte[] wrong = FrameCodec.Encode(new(19, "PONG", "-"));
            byte[] good = FrameCodec.Encode(new(f.Sequence, "PONG", "-"));
            wire.Queue(wrong, good[..3], good[3..]);
        };
        Assert.Equal("PONG", (await session.ExecuteAsync("PING")).Command);
        Assert.Equal(3, wire.Writes.Count); Assert.Equal(wire.Writes[1], wire.Writes[2]);
        Assert.True(session.IsValid); Assert.Contains(session.Trace, x => x.Event == "RETRY");
    }

    [Fact]
    public async Task ThreeLostRepliesCloseSessionWithoutCrossSessionReplay()
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        var error = await Assert.ThrowsAsync<SessionInterruptedException>(() => session.ExecuteAsync("FEED", "124"));
        Assert.Equal("INTERRUPTED_INDETERMINATE", error.Outcome);
        Assert.Equal(4, wire.Writes.Count); Assert.False(session.IsValid); Assert.True(wire.Closed);
        Assert.Equal(wire.Writes[1], wire.Writes[2]); Assert.Equal(wire.Writes[1], wire.Writes[3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("PING"));
    }

    [Fact]
    public async Task CancellationBeforeWritePreservesSessionAndSequence()
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExecuteAsync("PING", cancellationToken: cancel.Token));
        Assert.True(session.IsValid); Assert.Single(wire.Writes);
        wire.OnRequest = f => { Assert.Equal(1, f.Sequence); wire.Respond(new(f.Sequence, "PONG", "-")); };
        await session.ExecuteAsync("PING");
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task CancellationDuringOrAfterWriteIsIndeterminate(bool duringWrite)
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        using var cancel = new CancellationTokenSource();
        if (duringWrite) wire.OnWriteStarted = cancel.Cancel;
        else wire.OnRequest = _ => cancel.Cancel();
        await Assert.ThrowsAsync<SessionInterruptedException>(() => session.ExecuteAsync("START", cancellationToken: cancel.Token));
        Assert.False(session.IsValid); Assert.True(wire.Closed);
        Assert.Equal("INTERRUPTED_INDETERMINATE", session.Outcome);
    }

    [Theory]
    [InlineData("ERR_SESSION")] [InlineData("ERR_SEQUENCE")]
    public async Task ResetOrSequenceErrorInvalidatesSession(string error)
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        wire.OnRequest = f => wire.Respond(new(f.Sequence, error, "-"));
        await Assert.ThrowsAsync<SessionInterruptedException>(() => session.ExecuteAsync("PING"));
        Assert.False(session.IsValid); Assert.Contains(session.Trace, x => x.Detail.Contains(error));
    }

    [Fact]
    public async Task SemanticErrorConsumesSequenceButKeepsSession()
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        wire.OnRequest = f => wire.Respond(new(f.Sequence, f.Sequence == 1 ? "ERR_STATE" : "PONG", "-"));
        Assert.Equal("ERR_STATE", (await session.ExecuteAsync("FEED", "124")).Command);
        Assert.Equal(2, (await session.ExecuteAsync("PING")).Sequence); Assert.True(session.IsValid);
    }

    [Fact]
    public async Task DisconnectAbortsAndBadCrcReplyIsIgnored()
    {
        var wire = new ScriptTransport(); await using var session = New(wire); await session.OpenAsync();
        wire.OnRequest = f => {
            byte[] bad = FrameCodec.Encode(new(f.Sequence, "PONG", "-")); bad[2] ^= 1;
            wire.Queue(bad, []);
        };
        await Assert.ThrowsAsync<SessionInterruptedException>(() => session.ExecuteAsync("PING"));
        Assert.False(session.IsValid); Assert.True(wire.Closed);
    }

    [Fact]
    public void DefaultsUseTwoSecondsAndThreeTotalAttempts()
    {
        var options = new SessionOptions(); Assert.Equal(2000, options.ResponseTimeoutMs); Assert.Equal(3, options.MaxAttempts);
    }
}
