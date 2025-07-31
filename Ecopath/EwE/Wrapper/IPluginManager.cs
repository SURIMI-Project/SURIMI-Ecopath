using EwEPlugin;

namespace Ecopath.EwE.Wrapper
{
    public interface IPluginManager
    {
        int LoadPlugins();
        ICollection<IPlugin> GetPlugins(Type t, cPluginAssembly pa = null);
        //ICollection<IPlugin> GetPlugins(string strName, cPluginAssembly pa = null);
        //ICollection<cPluginAssembly> PluginAssemblies { get; }
        //event cPluginManager.AssemblyAddedHandler AssemblyAdded;
        //event cPluginManager.AssemblyRemovedHandler AssemblyRemoved;
        //event cPluginManager.PluginExceptionHandler PluginException;
        // Add more members as needed for your application
    }

}
