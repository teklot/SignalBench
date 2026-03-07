using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using SignalBench.SDK.Interfaces;

namespace SignalBench.Core.Services;

public class PluginLoader(ILogger<PluginLoader> logger)
{
    private readonly List<IPlugin> _loadedPlugins = [];

    public IEnumerable<IPlugin> Plugins => _loadedPlugins;

    public void LoadPlugins(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            logger.LogInformation("Plugin directory '{Path}' does not exist. Skipping.", pluginDirectory);
            return;
        }

        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll");
        foreach (var file in pluginFiles)
        {
            try
            {
                LoadPlugin(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load plugin from '{Path}'.", file);
            }
        }
    }

    private void LoadPlugin(string path)
    {
        var loadContext = new PluginLoadContext(path);
        var assembly = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));

        foreach (var type in assembly.GetTypes())
        {
            if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is IPlugin plugin)
                {
                    plugin.Initialize();
                    _loadedPlugins.Add(plugin);
                    logger.LogInformation("Loaded plugin: {Name} v{Version}", plugin.Name, plugin.Version);
                }
            }
        }
    }

    private class PluginLoadContext(string pluginPath) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
