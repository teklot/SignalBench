using System.Reflection;

namespace SignalBench.Core;

public static class AppInfo
{
    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
        }
    }

    public static string Name => "SignalBench";
    public static string Copyright => "© 2026 TekLot";
    public static string Tagline => "A telemetry analysis workbench suitable for real test campaigns.";
    public static string Description => "The cleanest binary telemetry decoding desktop tool a CubeSat engineer can install and use within 5 minutes.";
}
