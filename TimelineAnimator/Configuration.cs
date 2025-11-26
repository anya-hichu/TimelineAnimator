using Dalamud.Configuration;
using System;

namespace TimelineAnimator;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool OpenInGpose { get; set; } = true;
    public bool ShowTutorial { get; set; } = true;
    public bool ShowTooltips { get; set; } = true;
    public void Save()
    {
        Services.PluginInterface.SavePluginConfig(this);
    }
}
