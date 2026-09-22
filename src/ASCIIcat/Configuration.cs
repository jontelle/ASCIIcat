using ASCIIcat.Core;
using Dalamud.Configuration;

namespace ASCIIcat;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int RecentMessageCapacity { get; set; } = 200;
    public int RecentMessageMinutes { get; set; } = 15;
    public int ChatByteWarningThreshold { get; set; } = 500;
    public bool ShowInvisibleCharacters { get; set; }
    public bool KeepWindowOpenAfterCopy { get; set; } = true;
    public int AutomaticSendDelayMilliseconds { get; set; } = 1200;
    public ChatDestination AutomaticSendDestination { get; set; } = ChatDestination.Echo;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
