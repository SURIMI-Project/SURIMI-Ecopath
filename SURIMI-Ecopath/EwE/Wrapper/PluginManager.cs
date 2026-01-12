using EwECore;
using EwECore.Plugins;

namespace Ecopath.EwE.Wrapper
{
    public class PluginManager : IPluginManager
    {
        private readonly cPluginManager m_pluginManager;

        public PluginManager(cPluginManager pluginManager)
        {
            m_pluginManager = pluginManager;
        }

        public PluginManager()
        {
            m_pluginManager = new cPluginManager(); // Us this only in UnitTests
        }

        public ICollection<IPlugin> GetPlugins(Type t, cPluginAssembly? pa = null) => m_pluginManager.GetPlugins(t, pa);

        public int LoadPlugins() => m_pluginManager.LoadPlugins();
    }
}
