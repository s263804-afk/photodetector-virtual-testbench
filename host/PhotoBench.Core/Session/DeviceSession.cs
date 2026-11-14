using PhotoBench.Core.Protocol;
using PhotoBench.Core.Transport;

namespace PhotoBench.Core.Session;

public sealed record SessionOptions(int ResponseTimeoutMs = 2000, int MaxAttempts = 3);
public sealed record TraceEvent(DateTimeOffset At, Guid SessionId, string Event,
                               ushort Sequence, int Attempt, string Detail);
public sealed class SessionInterruptedException(string reason) : IOException(reason)
{
    public string Outcome => "INTERRUPTED_INDETERMINATE";
}

public sealed class DeviceSession(IByteTransport transport, SessionOptions? options = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly FrameParser parser = new();
    private ushort nextSequence = 1;
    private bool opened;
    public Guid SessionId { get; } = Guid.NewGuid();
    public bool IsValid { get; private set; }
    public string Outcome { get; private set; } = "NOT_CONNECTED";
    public List<TraceEvent> Trace { get; } = [];
    public string FirmwareIdentity { get; private set; } = "";
    public SessionOptions Options { get; } = options ?? new();
    private void Log(string kind, ushort sequence, int attempt, string detail) =>
        Trace.Add(new(DateTimeOffset.UtcNow, SessionId, kind, sequence, attempt, detail));

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (opened) throw new InvalidOperationException("Create a new logical session to reconnect");
            if (Options.ResponseTimeoutMs < 1 || Options.MaxAttempts is < 1 or > 3)
                throw new ArgumentException("Timeout must be positive; attempts must be1..3");
            opened = true;
            await transport.ConnectAsync(cancellationToken);
            Frame hello = await ExchangeAsync(new(0, "HELLO", "-"), cancellationToken);
            if (hello.Command != "HELLO" || !hello.Arguments.Contains("protocol=1") ||
                !hello.Arguments.Contains("mode=VIRTUAL_FEED"))
                throw await InterruptAsync("INCOMPATIBLE_HELLO", 0);
            FirmwareIdentity = hello.Arguments;
            IsValid = true; Outcome = "HEALTHY"; Log("SESSION_OPEN", 0, 1, FirmwareIdentity);
        }
        catch
        {
            IsValid = false;
            await transport.DisposeAsync();
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task<Frame> ExecuteAsync(string command, string arguments = "-",
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsValid) throw new InvalidOperationException("Session invalid; reconnect and HELLO required");
            if (command == "HELLO") throw new ArgumentException("HELLO requires a new logical session");
            cancellationToken.ThrowIfCancellationRequested();
            Frame result = await ExchangeAsync(new(nextSequence, command, arguments), cancellationToken);
            nextSequence = nextSequence == ushort.MaxValue ? (ushort)1 : (ushort)(nextSequence + 1);
            return result;
        }
        finally { gate.Release(); }
    }

    private static string Expected(string command) => command switch
    {
        "HELLO" => "HELLO", "PING" => "PONG", "FEED" => "DATA", "STATUS" => "STATUS", _ => "OK"
    };

    private async Task<Frame> ExchangeAsync(Frame request, CancellationToken cancellationToken)
    {
        byte[] wire = FrameCodec.Encode(request); // Validation before bytes can be handed to transport.
        bool possibleTransmission = false;
        byte[] buffer = new byte[1024];
        try
        {
            for (int attempt = 1; attempt <= Options.MaxAttempts; ++attempt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Options.ResponseTimeoutMs);
                try
                {
                    Log(attempt == 1 ? "SEND" : "RETRY", request.Sequence, attempt, Convert.ToHexString(wire));
                    possibleTransmission = true;
                    await transport.WriteAsync(wire, timeout.Token);
                    while (true)
                    {
                        int count = await transport.ReadAsync(buffer, timeout.Token);
                        if (count == 0) throw new IOException("TRANSPORT_DISCONNECTED");
                        foreach (Frame reply in parser.Push(buffer.AsSpan(0, count)))
                        {
                            if (reply.Sequence != request.Sequence)
                            { Log("IGNORE_SEQUENCE", reply.Sequence, attempt, reply.Command); continue; }
                            if (reply.Command is "ERR_SESSION" or "ERR_SEQUENCE")
                                throw await InterruptAsync(reply.Command, request.Sequence);
                            if (reply.Command != Expected(request.Command) &&
                                reply.Command is not ("ERR_STATE" or "ERR_ARGUMENT" or "ERR_COMMAND"))
                            { Log("IGNORE_COMMAND", reply.Sequence, attempt, reply.Command); continue; }
                            Log("RESPONSE", reply.Sequence, attempt, reply.Command + ":" + reply.Arguments);
                            return reply;
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { Log("TIMEOUT", request.Sequence, attempt, "No matching response before deadline"); }
            }
            throw await InterruptAsync("RETRIES_EXHAUSTED", request.Sequence);
        }
        catch (SessionInterruptedException) { throw; }
        catch (OperationCanceledException) when (possibleTransmission)
        { throw await InterruptAsync("CANCELLED_AFTER_POSSIBLE_TRANSMISSION", request.Sequence); }
        catch (IOException ex)
        { throw await InterruptAsync("TRANSPORT_FAILURE:" + ex.Message, request.Sequence); }
    }

    private async Task<SessionInterruptedException> InterruptAsync(string reason, ushort sequence)
    {
        IsValid = false; Outcome = "INTERRUPTED_INDETERMINATE";
        Log("SESSION_INVALIDATED", sequence, 0, reason);
        await transport.DisposeAsync();
        return new(reason);
    }

    public async ValueTask DisposeAsync()
    {
        IsValid = false;
        if (Outcome == "HEALTHY") Outcome = "CLOSED";
        await transport.DisposeAsync();
    }
}
