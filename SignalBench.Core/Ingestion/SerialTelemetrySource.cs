using System.IO.Ports;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Interfaces;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Ingestion;

public sealed class SerialTelemetrySource : IStreamingSource
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly StreamingPacketScanner? _scanner;
    private readonly MavlinkDecoder? _mavlinkDecoder;

    private SerialPort? _serialPort;
    private bool _isRunning;
    private Thread? _readThread;
    private FileStream? _rawLogStream;
    private long _frameErrorCount;
    private long _misalignmentCount;

    public event Action<DecodedPacket>? PacketReceived;
    public event Action<string>? ErrorReceived;
    public event Action<long, long>? StatsUpdated;

    public bool IsRecording { get; private set; }
    public string? RecordingFilePath { get; private set; }
    public long FrameErrorCount => _frameErrorCount;
    public long MisalignmentCount => _misalignmentCount;

    public SerialTelemetrySource(string portName, int baudRate, PacketSchema schema, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _portName = portName;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _scanner = new StreamingPacketScanner(schema);
    }

    public SerialTelemetrySource(string portName, int baudRate, MavlinkDecoder decoder, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _portName = portName;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _mavlinkDecoder = decoder;
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits);
            _serialPort.Handshake = Handshake.None;
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;
            _serialPort.ErrorReceived += SerialPort_ErrorReceived;
            _serialPort.Open();

            _isRunning = true;
            _readThread = new Thread(ReadLoop) { IsBackground = true, Name = $"SerialRead-{_portName}" };
            _readThread.Start();
        }
        catch (Exception ex)
        {
            _serialPort?.Dispose();
            _serialPort = null;
            ErrorReceived?.Invoke($"Failed to open port {_portName}: {ex.Message}");
            throw;
        }
    }

    private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        switch (e.EventType)
        {
            case SerialError.Frame:
                _frameErrorCount++;
                ErrorReceived?.Invoke($"Framing error detected (count: {_frameErrorCount})");
                StatsUpdated?.Invoke(_frameErrorCount, _misalignmentCount);
                break;
            case SerialError.Overrun:
                ErrorReceived?.Invoke($"Buffer overrun detected");
                break;
            case SerialError.RXOver:
                ErrorReceived?.Invoke($"Receive buffer overflow");
                break;
            case SerialError.TXFull:
                ErrorReceived?.Invoke($"Transmit buffer full");
                break;
            case SerialError.RXParity:
                _frameErrorCount++;
                ErrorReceived?.Invoke($"Parity error detected (count: {_frameErrorCount})");
                StatsUpdated?.Invoke(_frameErrorCount, _misalignmentCount);
                break;
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
        _isRunning = false;
        if (_readThread?.IsAlive == true)
        {
            _readThread.Join(1000);
        }

        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
        }
        catch { }
        finally
        {
            _serialPort?.Dispose();
            _serialPort = null;
            StopRecording();
        }
    }

    private void ReadLoop()
    {
        byte[] buffer = new byte[4096];
        while (_isRunning && _serialPort?.IsOpen == true)
        {
            try
            {
                int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    // Write raw data to recording file if recording is active
                    if (IsRecording && _rawLogStream != null)
                    {
                        _rawLogStream.Write(buffer, 0, bytesRead);
                        _rawLogStream.Flush();
                    }

                    if (_mavlinkDecoder != null)
                    {
                        var data = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                        _mavlinkDecoder.PushData(data);

                        while (_mavlinkDecoder.TryReadPacket(out var packet) && packet != null)
                        {
                            PacketReceived?.Invoke(packet with { Timestamp = DateTime.Now });
                        }
                    }
                    else if (_scanner != null)
                    {
                        var data = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);

                        var result = _scanner.PushData(data);

                        if (result.MisalignmentDetected)
                        {
                            _misalignmentCount++;
                            ErrorReceived?.Invoke($"Packet misalignment detected, resyncing (count: {_misalignmentCount})");
                            StatsUpdated?.Invoke(_frameErrorCount, _misalignmentCount);
                        }

                        foreach (var packet in result.Packets)
                        {
                            PacketReceived?.Invoke(packet with { Timestamp = DateTime.Now });
                        }
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    ErrorReceived?.Invoke($"Serial read error: {ex.Message}");
                    _isRunning = false;
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public IEnumerable<DecodedPacket> ReadPackets() => [];
    public void Seek(long position) { }
}
