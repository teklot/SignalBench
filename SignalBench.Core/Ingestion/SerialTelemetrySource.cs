using System.IO.Ports;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Ingestion;

public class SerialTelemetrySource : IDisposable
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly PacketSchema _schema;
    private SerialPort? _serialPort;
    private bool _isRunning;
    private Thread? _readThread;
    private readonly StreamingPacketScanner _scanner;
    private FileStream? _rawLogStream;

    public event Action<DecodedPacket>? PacketReceived;
    public event Action<string>? ErrorReceived;

    public bool IsRecording { get; private set; }
    public string? RecordingFilePath { get; private set; }

    public SerialTelemetrySource(string portName, int baudRate, PacketSchema schema, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _portName = portName;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _schema = schema;
        _scanner = new StreamingPacketScanner(schema);
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
                    if (IsRecording && _rawLogStream != null)
                    {
                        _rawLogStream.Write(buffer, 0, bytesRead);
                        _rawLogStream.Flush();
                    }

                    var data = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                    
                    var packets = _scanner.PushData(data);
                    foreach (var packet in packets)
                    {
                        packet.Timestamp = DateTime.Now;
                        PacketReceived?.Invoke(packet);
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

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }
}
