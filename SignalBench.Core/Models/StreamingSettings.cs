namespace SignalBench.Core.Models;

public class SerialSettings
{
    public string Port { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115200;
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public int RollingWindowSeconds { get; set; } = 10;
}

public class NetworkSettings
{
    public string Protocol { get; set; } = "UDP";
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5005;
    public int RollingWindowSeconds { get; set; } = 10;
}
