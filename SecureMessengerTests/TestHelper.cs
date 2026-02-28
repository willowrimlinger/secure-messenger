using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SecureMessenger.Tests;
internal static class TestHelpers
{
    public static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static bool Wait(ManualResetEventSlim evt, int ms = 3000) => evt.Wait(ms);
}