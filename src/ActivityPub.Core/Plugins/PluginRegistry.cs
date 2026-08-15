using System.ComponentModel;
using System.Reflection;

namespace ActivityPub.Core.Plugins;

/// <summary>
/// Plugin registry for managing ActivityPub extensions
/// </summary>
public class PluginRegistry
{
    private readonly Dictionary<string, IActivityPubPlugin> _plugins = new();
    
    /// <summary>
    /// Registers a plugin with the registry
    /// </summary>
    public void RegisterPlugin(IActivityPubPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));
            
        _plugins[plugin.Name] = plugin;
    }
    
    /// <summary>
    /// Gets all registered plugins
    /// </summary>
    public IEnumerable<IActivityPubPlugin> GetAllPlugins()
    {
        return _plugins.Values;
    }
    
    /// <summary>
    /// Gets a specific plugin by name
    /// </summary>
    public IActivityPubPlugin? GetPlugin(string name)
    {
        return _plugins.TryGetValue(name, out var plugin) ? plugin : null;
    }
    
    /// <summary>
    /// Unregisters a plugin from the registry
    /// </summary>
    public void UnregisterPlugin(string name)
    {
        _plugins.Remove(name);
    }
}