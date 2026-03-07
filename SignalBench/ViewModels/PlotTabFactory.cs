using SignalBench.Core.Data;
using SignalBench.SDK.Interfaces;
using System.IO;

namespace SignalBench.ViewModels;

public class PlotTabFactory : ITabFactory
{
    public string TabTypeId => "SignalBench.Plot";
    public string DisplayName => "New Plot";
    public string Icon => "ChartLine";

    public ITabViewModel CreateTab()
    {
        // Default to InMemory for new tabs via factory
        var store = new InMemoryDataStore();
        return new PlotViewModel("Untitled Plot", store);
    }
}
