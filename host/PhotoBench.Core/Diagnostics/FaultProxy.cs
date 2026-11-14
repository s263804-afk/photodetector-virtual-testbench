using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PhotoBench.Core.Diagnostics;

/// <summary>A real loopback byte proxy: it changes transport delivery, never firmware results.</summary>
public sealed class FaultProxy : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    private TcpClient? downstream, upstream;
    public int Port { get; }
    public int Dropped { get; private set; }

    public FaultProxy(string host, int port, int dropDataReplies = 0, int fragmentSize = 0)
    {
        listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        worker = Task.Run(async () => {
            try
            {
                downstream = await listener.AcceptTcpClientAsync(stop.Token);
                upstream = new TcpClient { NoDelay = true };
                downstream.NoDelay = true;
                await upstream.ConnectAsync(host, port, stop.Token);
                Task toDevice = downstream.GetStream().CopyToAsync(upstream.GetStream(), stop.Token);
                Task toHost = RelayRepliesAsync(dropDataReplies, fragmentSize);
                await Task.WhenAny(toDevice, toHost);
                stop.Cancel(); upstream.Dispose(); downstream.Dispose();
                await Task.WhenAll(toDevice, toHost);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
        });
    }

    private async Task RelayRepliesAsync(int dropDataReplies, int fragmentSize)
    {
        byte[] buffer = new byte[1024]; var line = new List<byte>(512);
        while (true)
        {
            int count = await upstream!.GetStream().ReadAsync(buffer, stop.Token);
            if (count == 0) return;
            for (int i = 0; i < count; ++i)
            {
                byte value = buffer[i];
                line.Add(value);
                if (value != (byte)'\n' && line.Count < 4096) continue;
                byte[] wire = line.ToArray(); line.Clear();
                if (Encoding.ASCII.GetString(wire).Contains("|DATA|", StringComparison.Ordinal) && Dropped < dropDataReplies)
                { ++Dropped; continue; }
                int size = fragmentSize <= 0 ? wire.Length : fragmentSize;
                for (int start = 0; start < wire.Length; start += size)
                {
                    await downstream!.GetStream().WriteAsync(wire.AsMemory(start, Math.Min(size, wire.Length - start)), stop.Token);
                    if (fragmentSize > 0) await Task.Delay(1, stop.Token);
                }
            }
        }
    }
    public void Disconnect() { downstream?.Dispose(); upstream?.Dispose(); }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop(); Disconnect(); await worker; stop.Dispose();
    }
}
