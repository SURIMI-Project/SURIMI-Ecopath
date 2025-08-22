using EwEPlugin;

namespace Ecopath.EwE.Wrapper
{
    public interface IPluginManager
    {
        int LoadPlugins();
        ICollection<IPlugin> GetPlugins(Type t, cPluginAssembly? pa = null);
    }

}
