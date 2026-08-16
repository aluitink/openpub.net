using ActivityPub.Core.Plugins;
using Xunit;

namespace ActivityPub.Tests.Plugins;

public class PluginRegistryTests
{
    [Fact]
    public void RegisterPlugin_Adds_Plugin_To_Registry()
    {
        var registry = new PluginRegistry();
        var plugin = new TestPlugin();

        registry.RegisterPlugin(plugin);

        Assert.Single(registry.GetAllPlugins());
    }

    [Fact]
    public void RegisterPlugin_Duplicate_Name_Overwrites()
    {
        var registry = new PluginRegistry();
        var plugin1 = new TestPlugin { Name = "Test1" };
        var plugin2 = new TestPlugin { Name = "Test1" };

        registry.RegisterPlugin(plugin1);
        registry.RegisterPlugin(plugin2);

        var plugins = registry.GetAllPlugins().ToList();
        Assert.Single(plugins);
        Assert.Equal("Test1", plugins[0].Name);
    }

    [Fact]
    public void GetPlugin_Retrieves_Registered_Plugin()
    {
        var registry = new PluginRegistry();
        var plugin = new TestPlugin { Name = "TestPlugin" };
        registry.RegisterPlugin(plugin);

        var result = registry.GetPlugin("TestPlugin");

        Assert.Equal(plugin, result);
    }

    [Fact]
    public void GetPlugin_Returns_Null_For_Nonexistent_Plugin()
    {
        var registry = new PluginRegistry();

        var result = registry.GetPlugin("Nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void UnregisterPlugin_Removes_Plugin()
    {
        var registry = new PluginRegistry();
        var plugin = new TestPlugin { Name = "TestPlugin" };
        registry.RegisterPlugin(plugin);

        registry.UnregisterPlugin("TestPlugin");

        Assert.Empty(registry.GetAllPlugins());
    }

    [Fact]
    public void UnregisterPlugin_Does_Nothing_For_Nonexistent_Plugin()
    {
        var registry = new PluginRegistry();

        registry.UnregisterPlugin("Nonexistent");

        Assert.Empty(registry.GetAllPlugins());
    }

    [Fact]
    public void GetAllPlugins_Returns_All_Registered_Plugins()
    {
        var registry = new PluginRegistry();
        var plugin1 = new TestPlugin { Name = "Plugin1" };
        var plugin2 = new TestPlugin { Name = "Plugin2" };
        var plugin3 = new TestPlugin { Name = "Plugin3" };
        registry.RegisterPlugin(plugin1);
        registry.RegisterPlugin(plugin2);
        registry.RegisterPlugin(plugin3);

        var plugins = registry.GetAllPlugins().ToList();

        Assert.Equal(3, plugins.Count);
        Assert.Contains(plugin1, plugins);
        Assert.Contains(plugin2, plugins);
        Assert.Contains(plugin3, plugins);
    }

    [Fact]
    public void RegisterPlugin_Throws_ArgumentNullException_For_Null_Plugin()
    {
        var registry = new PluginRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.RegisterPlugin(null!));
    }
}

public class TestPlugin : IActivityPubPlugin
{
    public string Name { get; set; } = "Test";
    public string Version { get; set; } = "1.0.0";

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Dictionary<string, object> GetMetadata()
    {
        return new Dictionary<string, object>
        {
            { "test", true }
        };
    }
}
