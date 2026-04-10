using System.Net.Sockets;

namespace SignalBench.Core.Flux.Transport;

public sealed class UdpTransport(string target) : ITransport
{
    private UdpClient? _client;
    private readonly string _host = target.Split(':')[0];
    private readonly int _port = int.Parse(target.Split(':')[1]);

    public bool IsConnected => _client != null;

    public void Connect()
    {
        _client = new UdpClient();
    }

    public void Send(byte[] data)
    {
        if (_client == null) throw new InvalidOperationException("Not connected");
        var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(_host), _port);
        _client.Send(data, data.Length, endpoint);
    }

    public void Dispose() => _client?.Dispose();
}
