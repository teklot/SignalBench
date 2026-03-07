using System.Net;
using System.Net.Sockets;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Interfaces;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Ingestion;

public enum NetworkProtocol
{
    Tcp,
    Udp
}

public sealed class NetworkTelemetrySource(string ipAddress, int port, PacketSchema schema, NetworkProtocol protocol = NetworkProtocol.Tcp) : IStreamingSource
{
    private readonly StreamingPacketScanner _scanner = new(schema);
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

    public void Start()
    {
        if (_receiveTask != null) return;

        try
        {
            if (protocol == NetworkProtocol.Tcp)
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ipAddress, port);
                _networkStream = _tcpClient.GetStream();
                _networkStream.ReadTimeout = 1000;
                
                _cancellationSource = new CancellationTokenSource();
                _receiveTask = Task.Run(() => TcpReceiveLoop(_cancellationSource.Token));
                
                ErrorReceived?.Invoke($"TCP client connected to {ipAddress}:{port}");
            }
            else
            {
                if (IPAddress.TryParse(ipAddress, out var localAddress))
                {
                    var localEndPoint = new IPEndPoint(localAddress, port);
                    _udpClient = new UdpClient(localEndPoint);
                    ErrorReceived?.Invoke($"UDP listener started on {ipAddress}:{port}");
                }
                else
                {
                    _udpClient = new UdpClient(port);
                    ErrorReceived?.Invoke($"UDP listener started on all interfaces, port {port}");
                }

                _udpClient.Client.ReceiveTimeout = 1000;
                
                _cancellationSource = new CancellationTokenSource();
                _receiveTask = Task.Run(() => UdpReceiveLoop(_cancellationSource.Token));
            }
        }
        catch (Exception ex)
        {
            Stop();
            if (protocol == NetworkProtocol.Tcp)
                ErrorReceived?.Invoke($"Failed to connect to {ipAddress}:{port}: {ex.Message}");
            else
                ErrorReceived?.Invoke($"Failed to start UDP listener on port {port}: {ex.Message}");
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
            _packetCount++;
            PacketReceived?.Invoke(packet with { Timestamp = DateTime.Now });
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
            if (protocol == NetworkProtocol.Tcp)
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

        if (protocol == NetworkProtocol.Tcp)
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

    public IEnumerable<DecodedPacket> ReadPackets() => [];
    public void Seek(long position) { }
}
