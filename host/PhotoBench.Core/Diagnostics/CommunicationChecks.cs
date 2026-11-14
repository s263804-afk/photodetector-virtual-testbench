using PhotoBench.Core.Session;
using PhotoBench.Core.Simulation;
using PhotoBench.Core.Protocol;
using PhotoBench.Core.Transport;
using System.Text;
using System.Net.Sockets;

namespace PhotoBench.Core.Diagnostics;

public sealed record CheckResult(string Name, bool Passed, string Detail);
public sealed record CommunicationReport(CheckResult[] Checks, Guid[] SessionIds, TraceEvent[] Trace);
public sealed class CommunicationInterruptedException(CommunicationReport partial, Exception cause)
    : IOException(cause.GetType().Name + ": " + cause.Message, cause)
{
    public CommunicationReport Partial { get; } = partial;
    public string Outcome => InnerException is SessionInterruptedException ||
        (InnerException is OperationCanceledException && Partial.Trace.Any(x => x.Event == "SEND"))
        ? "INTERRUPTED_INDETERMINATE" : InnerException is OperationCanceledException ? "INTERRUPTED_CANCELLED" : "FAIL";
}

public static class CommunicationChecks
{
    public static async Task<CommunicationReport> RunAsync(string host, int port, RenodeRunner? simulator,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<CheckResult>(); var sessions = new List<DeviceSession>();
        Guid rawSession = Guid.NewGuid(); var rawTrace = new List<TraceEvent>();
        CommunicationReport Snapshot() => new(checks.ToArray(),
            (rawTrace.Count == 0 ? Array.Empty<Guid>() : new[] { rawSession }).Concat(sessions.Select(x => x.SessionId)).ToArray(),
            rawTrace.Concat(sessions.SelectMany(x => x.Trace)).ToArray());
        void Check(string name, bool pass, string detail) => checks.Add(new(name, pass, detail));
        DeviceSession New(IByteTransport transport, int timeout = 500)
        { var s = new DeviceSession(transport, new(timeout, 3)); sessions.Add(s); return s; }
        try
        {
            // This low-level path deliberately sends damaged bytes to the actual ARM UART.
            await using (var raw = new TcpTransport(host, port))
            {
                await raw.ConnectAsync(cancellationToken); var parser = new FrameParser();
                async Task<Frame> Exchange(Frame frame, bool fragment = false)
                {
                    byte[] bytes = FrameCodec.Encode(frame);
                    rawTrace.Add(new(DateTimeOffset.UtcNow, rawSession, "SEND", frame.Sequence, 1, Convert.ToHexString(bytes)));
                    if (fragment)
                    {
                        await raw.WriteAsync(bytes.AsMemory(0, 3), cancellationToken);
                        await Task.Delay(5, cancellationToken);
                        await raw.WriteAsync(bytes.AsMemory(3), cancellationToken);
                    }
                    else await raw.WriteAsync(bytes, cancellationToken);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(2000); byte[] buffer = new byte[1024];
                    while (true)
                    {
                        int n = await raw.ReadAsync(buffer, timeout.Token);
                        if (n == 0) throw new IOException("ARM UART disconnected");
                        Frame? reply = parser.Push(buffer.AsSpan(0, n)).FirstOrDefault(x => x.Sequence == frame.Sequence);
                        if (reply != null)
                        {
                            rawTrace.Add(new(DateTimeOffset.UtcNow, rawSession, "RESPONSE", reply.Sequence, 1, reply.Command + ":" + reply.Arguments));
                            return reply;
                        }
                    }
                }
                Check("FRAGMENTED_REQUEST", (await Exchange(new(0, "HELLO", "-"), true)).Command == "HELLO", "HELLO split across real TCP writes");
                await Exchange(new(1, "START", "-"));
                Frame data = await Exchange(new(2, "FEED", "124,869,4095"));
                Frame replay = await Exchange(new(2, "FEED", "124,869,4095"));
                Check("DUPLICATE_FEED", data == replay && data.Arguments == "0;124:124:0;869:496:0;4095:1696:2", data.Arguments);
                byte[] corrupt = FrameCodec.Encode(new(3, "PING", "-"));
                corrupt[^3] = corrupt[^3] == (byte)'A' ? (byte)'B' : (byte)'A';
                await raw.WriteAsync(corrupt, cancellationToken);
                rawTrace.Add(new(DateTimeOffset.UtcNow, rawSession, "SEND_GARBAGE", 3, 1, Convert.ToHexString(corrupt)));
                await raw.WriteAsync(Encoding.ASCII.GetBytes(new string('X', 600) + "\n"), cancellationToken);
                Frame pong = await Exchange(new(3, "PING", "-"));
                Frame status = await Exchange(new(4, "STATUS", "-"));
                var fields = ParseStatus(status.Arguments);
                Check("BAD_CRC_DROPPED", fields["crc_errors"] == "1" && pong.Command == "PONG", status.Arguments);
                Check("OVERFLOW_RECOVERY", fields["overflow_errors"] == "1" && fields["processed"] == "3", status.Arguments);
            }
            await using (var proxy = new FaultProxy(host, port, 1, 3))
            await using (var session = New(new TcpTransport(host, proxy.Port)))
            {
                await session.OpenAsync(cancellationToken); await session.ExecuteAsync("START", cancellationToken: cancellationToken);
                Frame data = await session.ExecuteAsync("FEED", "124,869,4095", cancellationToken);
                Frame status = await session.ExecuteAsync("STATUS", cancellationToken: cancellationToken);
                Check("LOST_FEED_REPLY_EXACTLY_ONCE", proxy.Dropped == 1 && ParseStatus(status.Arguments)["processed"] == "3" &&
                    data.Arguments == "0;124:124:0;869:496:0;4095:1696:2" && session.Trace.Any(x => x.Event == "RETRY"), status.Arguments);
                Check("FRAGMENTED_RESPONSE", session.IsValid, "Proxy emitted at most3 bytes per delayed write");
            }
            await using (var proxy = new FaultProxy(host, port, 3))
            await using (var session = New(new TcpTransport(host, proxy.Port), 150))
            {
                await session.OpenAsync(cancellationToken); await session.ExecuteAsync("START", cancellationToken: cancellationToken);
                bool interrupted = false;
                try { await session.ExecuteAsync("FEED", "124", cancellationToken); }
                catch (SessionInterruptedException) { interrupted = true; }
                Check("ALL_REPLIES_LOST_INDETERMINATE", interrupted && !session.IsValid && proxy.Dropped == 3 &&
                    session.Outcome == "INTERRUPTED_INDETERMINATE", "Three actual DATA replies discarded; no automatic reconnect/replay");
            }
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                await using var session = New(new CancelAfterFeedTransport(new TcpTransport(host, port), cancel));
                await session.OpenAsync(cancellationToken); await session.ExecuteAsync("START", cancellationToken: cancellationToken);
                bool interrupted = false;
                try { await session.ExecuteAsync("FEED", "124", cancel.Token); }
                catch (SessionInterruptedException) { interrupted = true; }
                Check("POST_SEND_CANCELLATION", interrupted && !session.IsValid, "Cancellation after real NetworkStream write completed");
            }
            if (simulator != null)
            {
                await using (var session = New(new TcpTransport(host, port)))
                {
                    await session.OpenAsync(cancellationToken); await session.ExecuteAsync("START", cancellationToken: cancellationToken);
                    await simulator.ResetAsync(cancellationToken);
                    bool interrupted = false;
                    try { await session.ExecuteAsync("PING", cancellationToken: cancellationToken); }
                    catch (SessionInterruptedException) { interrupted = true; }
                    Check("RESET_TCP_RETAINED", interrupted && !session.IsValid &&
                        session.Trace.Any(x => x.Detail == "ERR_SESSION"), "Firmware reset via private monitor; next command must return ERR_SESSION on retained TCP");
                }
                await using (var session = New(new TcpTransport(host, port)))
                {
                    await session.OpenAsync(cancellationToken);
                    Frame status = await session.ExecuteAsync("STATUS", cancellationToken: cancellationToken);
                    Check("RECONNECT_NEW_HELLO", ParseStatus(status.Arguments)["state"] == "STOPPED" && status.Sequence == 1,
                          "New logical session, HELLO then STATUS sequence1: " + status.Arguments);
                }
            }
            else Check("RESET_TCP_RETAINED", false, "UNVERIFIED: reset control unavailable for external simulator");
            await using (var proxy = new FaultProxy(host, port))
            await using (var session = New(new TcpTransport(host, proxy.Port)))
            {
                await session.OpenAsync(cancellationToken); proxy.Disconnect();
                bool interrupted = false;
                try { await session.ExecuteAsync("PING", cancellationToken: cancellationToken); }
                catch (SessionInterruptedException) { interrupted = true; }
                Check("TRANSPORT_DISCONNECT", interrupted && !session.IsValid, "Actual TCP peers closed mid-session");
            }
            return Snapshot();
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
            if (sessions.Count == 0 && rawTrace.Count > 0)
                rawTrace.Add(new(DateTimeOffset.UtcNow, rawSession, "SESSION_INVALIDATED", 0, 0, ex.GetType().Name + ":" + ex.Message));
            throw new CommunicationInterruptedException(Snapshot(), ex);
        }
    }

    public static Dictionary<string, string> ParseStatus(string text) =>
        text.Split(';').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1]);

    private sealed class CancelAfterFeedTransport(IByteTransport inner, CancellationTokenSource cancel) : IByteTransport
    {
        public ValueTask ConnectAsync(CancellationToken ct = default) => inner.ConnectAsync(ct);
        public ValueTask<int> ReadAsync(Memory<byte> bytes, CancellationToken ct = default) => inner.ReadAsync(bytes, ct);
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            await inner.WriteAsync(bytes, ct);
            if (Encoding.ASCII.GetString(bytes.Span).Contains("|FEED|", StringComparison.Ordinal)) cancel.Cancel();
        }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
