using System.Threading.Tasks;
using SignalBench.SDK.Interfaces;

namespace SignalBench.Core.Services;

public class DefaultFeatureService : IFeatureService
{
    public LicenseStatus CurrentStatus => LicenseStatus.Free;

    public bool IsFeatureEnabled(string featureId)
    {
        // For the open-source Core, only non-Pro features are allowed.
        // Pro features must start with "Pro." to be identified.
        if (featureId.StartsWith("Pro.", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Standard features are always enabled.
        return true;
    }

    public Task<LicenseStatus> ValidateLicenseAsync(string licenseKey)
    {
        // The default service doesn't know how to validate real keys.
        // It always returns Free status for safety.
        return Task.FromResult(LicenseStatus.Free);
    }
}
