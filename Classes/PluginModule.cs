using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wraedar;

public abstract class PluginModule {
    protected Plugin Plugin { get; }
    protected Settings Settings => Plugin.Settings;

    protected PluginModule(Plugin plugin) {
        Plugin = plugin;
    }

}
