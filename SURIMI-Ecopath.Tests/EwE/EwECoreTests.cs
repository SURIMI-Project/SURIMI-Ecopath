using System.Reflection;
using System.Runtime.Loader;
using Ecopath.EwE.Wrapper;
using Xunit;

namespace Ecopath.Tests.EwE;

public class EwECoreTests
{
    [Fact]
    public void Constructor_LoadsBridgeAssemblyWithoutInitializingEngine()
    {
        // Arrange
        const string assemblyName = "Eii.Ecopath.BridgePlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        var expectedAssemblyPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "BridgePlugin", "Eii.Ecopath.BridgePlugin.dll");

        // Act
        IEwECore core = new Ecopath.EwE.Wrapper.EwECore();

        // Assert
        var assembly = Assembly.Load(assemblyName);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(assembly));
        Assert.Equal(expectedAssemblyPath, assembly.Location);
        Assert.NotNull(assembly.GetType("EwEBridge.Ecospace.cEcospaceBridgePlugin", throwOnError: true));
        Assert.Throws<InvalidOperationException>(() => core.EcopathDataStructures);
        Assert.Throws<InvalidOperationException>(() => core.PluginManager);
    }
}
