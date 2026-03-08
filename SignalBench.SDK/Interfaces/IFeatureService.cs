using System.Threading.Tasks;

namespace SignalBench.SDK.Interfaces;

public enum LicenseStatus
{
    Free,
    Pro,
    Expired,
    Invalid
}

public interface IFeatureService
{
    /// <summary>
    /// Checks if a specific feature (e.g., "Pro.3DView") is enabled for the current license.
    /// </summary>
    bool IsFeatureEnabled(string featureId);

    /// <summary>
    /// Gets the current license status.
    /// </summary>
    LicenseStatus CurrentStatus { get; }

    /// <summary>
    /// Validates a license key (e.g., against an online server or local encrypted file).
    /// </summary>
    Task<LicenseStatus> ValidateLicenseAsync(string licenseKey);
}
