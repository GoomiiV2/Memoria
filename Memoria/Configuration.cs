using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Memoria;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public string PullSaveLocation { get; set; } = string.Empty;
    public string OBSUrl { get; set; } = string.Empty;
    public int DelayAfterPullEndToStopRec = 5;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
