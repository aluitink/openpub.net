using System.Reflection;

namespace ActivityPub.Core.Plugins;

/// <summary>
/// Base interface for ActivityPub plugins
/// </summary>
public interface IActivityPubPlugin
{
    /// <summary>
    /// Gets the plugin name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the plugin version
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Initializes the plugin
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Gets the plugin metadata
    /// </summary>
    Dictionary<string, object> GetMetadata();
}