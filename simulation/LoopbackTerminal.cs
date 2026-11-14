// Project-owned local UART bridge. No MCU/sample processing is performed here.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Antmicro.Renode.Backends.Terminals;
using Antmicro.Renode.Core;

namespace PhotoBench.Simulation
{
    public static class LoopbackTerminalExtensions
    {
        public static void CreateLoopbackTerminal(this Emulation emulation, int port, string name)
        {
            emulation.ExternalsManager.AddExternal(new LoopbackTerminal(port), name);
        }
    }

    public sealed class LoopbackTerminal : BackendTerminal, IDisposable
    {
        public LoopbackTerminal(int port)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(1);
            worker = new Thread(AcceptLoop);
            worker.IsBackground = true;
            worker.Start();
        }

        private void AcceptLoop()
        {
            while (!stopped)
            {
                TcpClient accepted;
                try { accepted = listener.AcceptTcpClient(); }
                catch (SocketException) { if (stopped) return; else continue; }
                catch (ObjectDisposedException) { return; }
                accepted.NoDelay = true;
                accepted.SendTimeout = 2000;
                lock (gate) { client = accepted; }
                try
                {
                    var stream = accepted.GetStream();
                    int value;
                    while (!stopped && (value = stream.ReadByte()) >= 0)
                    {
                        CallCharReceived((byte)value);
                    }
                }
                catch (Exception exception) when (exception is System.IO.IOException ||
                                                 exception is ObjectDisposedException) { }
                finally
                {
                    lock (gate) { if (client == accepted) client = null; }
                    accepted.Dispose();
                }
            }
        }

        public override void WriteChar(byte value)
        {
            lock (gate)
            {
                if (client == null) return;
                try { client.GetStream().WriteByte(value); }
                catch (Exception exception) when (exception is System.IO.IOException ||
                                                 exception is ObjectDisposedException) { }
            }
        }

        public void Dispose()
        {
            stopped = true;
            listener.Stop();
            lock (gate) { if (client != null) client.Dispose(); }
            worker.Join(500);
        }

        private readonly TcpListener listener;
        private readonly Thread worker;
        private readonly object gate = new object();
        private TcpClient client;
        private volatile bool stopped;
    }
}
