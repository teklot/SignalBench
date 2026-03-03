using System.Net;
using System.Net.Sockets;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Ingestion;

public enum NetworkProtocol
{
    Tcp,
    Udp
}

public class NetworkTelemetrySource : IDisposable
{
    private readonly NetworkProtocol _protocol;
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly PacketSchema _schema;
    private readonly StreamingPacketScanner _scanner;
    private TcpClient? _tcpClient;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationSource;
    private Task? _receiveTask;
    private NetworkStream? _networkStream;
    private FileStream? _rawLogStream;
    private long _packetCount;
    private long _errorCount;

    public event Action<DecodedPacket>? PacketReceived;
    public event Action<string>? ErrorReceived;
    public event Action<long, long>? StatsUpdated;

    public bool IsRecording { get; private set; }
    public string? RecordingFilePath { get; private set; }
    public long PacketCount => _packetCount;
    public long ErrorCount => _errorCount;

    public NetworkTelemetrySource(string ipAddress, int port, PacketSchema schema, NetworkProtocol protocol = NetworkProtocol.Tcp)
    {
        _ipAddress = ipAddress;
        _port = port;
        _protocol = protocol;
        _schema = schema;
        _scanner = new StreamingPacketScanner(schema);
    }

    public void Start()
    {
        if (_receiveTask != null) return;

        try
        {
            if (_protocol == NetworkProtocol.Tcp)
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(_ipAddress, _port);
                _networkStream = _tcpClient.GetStream();
                _networkStream.ReadTimeout = 1000;
                
                _cancellationSource = new CancellationTokenSource();
                _receiveTask = Task.Run(() => TcpReceiveLoop(_cancellationSource.Token));
                
                ErrorReceived?.Invoke($"TCP client connected to {_ipAddress}:{_port}");
            }
            else
            {
                if (IPAddress.TryParse(_ipAddress, out var localAddress))
                {
                    var localEndPoint = new IPEndPoint(localAddress, _port);
                    _udpClient = new UdpClient(localEndPoint);
                    ErrorReceived?.Invoke($"UDP listener started on {_ipAddress}:{_port}");
                }
                else
                {
                    _udpClient = new UdpClient(_port);
                    ErrorReceived?.Invoke($"UDP listener started on all interfaces, port {_port}");
                }

                _udpClient.Client.ReceiveTimeout = 1000;
                
                _cancellationSource = new CancellationTokenSource();
                _receiveTask = Task.Run(() => UdpReceiveLoop(_cancellationSource.Token));
            }
        }
        catch (Exception ex)
        {
            Stop();
            if (_protocol == NetworkProtocol.Tcp)
                ErrorReceived?.Invoke($"Failed to connect to {_ipAddress}:{_port}: {ex.Message}");
            else
                ErrorReceived?.Invoke($"Failed to start UDP listener on port {_port}: {ex.Message}");
            throw;
        }
    }

    private async Task TcpReceiveLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        
        while (!cancellationToken.IsCancellationRequested && _tcpClient?.Connected == true)
        {
            try
            {
                if (_networkStream == null) break;
                
                int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead > 0)
                {
                    ProcessReceivedData(buffer, bytesRead);
                }
                else
                {
                    await Task.Delay(1, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                ErrorReceived?.Invoke("TCP connection closed by remote host");
                break;
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke($"TCP receive error: {ex.Message}");
            }
        }
    }

    private async Task UdpReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                ProcessReceivedData(result.Buffer, result.Buffer.Length);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }
                ErrorReceived?.Invoke($"UDP socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke($"UDP receive error: {ex.Message}");
            }
        }
    }

    private void ProcessReceivedData(byte[] data, int length)
    {
        // Write raw data to recording file if recording is active
        if (IsRecording && _rawLogStream != null)
        {
            _rawLogStream.Write(data, 0, length);
            _rawLogStream.Flush();
        }

        var receiveBuffer = new byte[length];
        Buffer.BlockCopy(data, 0, receiveBuffer, 0, length);

        var scanResult = _scanner.PushData(receiveBuffer);

        if (scanResult.MisalignmentDetected)
        {
            _errorCount++;
            ErrorReceived?.Invoke($"Packet misalignment detected, resyncing");
            StatsUpdated?.Invoke(_packetCount, _errorCount);
        }

        foreach (var packet in scanResult.Packets)
        {
            packet.Timestamp = DateTime.Now;
            _packetCount++;
            PacketReceived?.Invoke(packet);
        }

        if (_packetCount % 100 == 0)
        {
            StatsUpdated?.Invoke(_packetCount, _errorCount);
        }
    }

    public void StartRecording(string filePath)
    {
        try
        {
            _rawLogStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            RecordingFilePath = filePath;
            IsRecording = true;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke($"Failed to start recording: {ex.Message}");
        }
    }

    public void StopRecording()
    {
        IsRecording = false;
        _rawLogStream?.Dispose();
        _rawLogStream = null;
    }

    public void Stop()
    {
        _cancellationSource?.Cancel();

        try
        {
            if (_protocol == NetworkProtocol.Tcp)
            {
                _networkStream?.Close();
                _tcpClient?.Close();
            }
            else
            {
                _udpClient?.Close();
            }
        }
        catch { }

        if (_protocol == NetworkProtocol.Tcp)
        {
            _networkStream?.Dispose();
            _tcpClient?.Dispose();
        }
        else
        {
            _udpClient?.Dispose();
        }

        _networkStream = null;
        _tcpClient = null;
        _udpClient = null;

        if (_receiveTask != null)
        {
            try
            {
                _receiveTask.Wait(1000);
            }
            catch { }
        }

        _receiveTask = null;
        _cancellationSource?.Dispose();
        _cancellationSource = null;

        StopRecording();
    }

    public void Dispose()
    {
        Stop();
    }
}
