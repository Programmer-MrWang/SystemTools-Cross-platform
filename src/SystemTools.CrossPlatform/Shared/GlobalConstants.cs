using SystemTools.CrossPlatform.ConfigHandlers;

namespace SystemTools.CrossPlatform.Shared;

public static class GlobalConstants
{
    public static string? PluginConfigFolder { get; set; }

    public static MainConfigHandler? MainConfig { get; set; }

    public static class Information
    {
        public static string PluginFolder { get; set; } = string.Empty;

        public static string PluginVersion { get; set; } = "???";
    }

    public static bool ShowChangelogOnOpen { get; set; } = false;
}
