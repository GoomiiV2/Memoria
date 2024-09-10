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
    public string OBSHost { get; set; } = "127.0.0.1";
    public ushort OBSPort { get; set; } = 4455;
    public int DelayAfterPullEndToStopRec = 5;

    public bool RecInNormRaids { get; set; } = false;
    public bool RecInNormTrials { get; set; } = false;
    public bool RecInSavageRaids { get; set; } = true;
    public bool RecInExTrials { get; set; } = true;
    public bool RecInUltimates { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
