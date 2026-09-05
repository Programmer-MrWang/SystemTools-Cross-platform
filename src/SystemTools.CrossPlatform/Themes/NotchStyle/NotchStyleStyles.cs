using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace SystemTools.CrossPlatform.Themes.NotchStyle;

public sealed class NotchStyleStyles : Styles
{
    private static readonly Uri ThemeResourceUri =
        new("avares://SystemTools.CrossPlatform/Themes/NotchStyle/Theme.axaml.txt");

    public NotchStyleStyles()
    {
        EnsureDefaultContextCanResolvePluginAssembly();

        using var stream = AssetLoader.Open(ThemeResourceUri);
        using var reader = new StreamReader(stream);
        if (AvaloniaRuntimeXamlLoader.Load(
                reader.ReadToEnd(),
                Assembly.GetExecutingAssembly(),
                uri: ThemeResourceUri) is not Styles styles)
        {
            throw new InvalidOperationException("The embedded Notch Style theme is not a Styles resource.");
        }

        Add(styles);
    }

    /// <summary>
    /// The host loads plugins in an isolated <see cref="AssemblyLoadContext"/>, while Avalonia's
    /// runtime XAML compiler resolves clr-namespace types by assembly name from the default load
    /// context only. This theme's runtime XAML references controls defined in this plugin assembly
    /// (NotchMaterialControl/NotchFrameControl/NotchClipControl), so loading it otherwise fails on
    /// every platform (Windows/Linux/macOS) with "Unable to resolve type ...". Registering a
    /// default-context Resolving hook that points the plugin assembly name back at this (isolated)
    /// assembly instance keeps a single copy of the assembly and lets the theme load everywhere.
    /// Hosts that load plugins into the default context already are left untouched.
    /// </summary>
    private static void EnsureDefaultContextCanResolvePluginAssembly()
    {
        var pluginName = typeof(NotchStyleStyles).Assembly.GetName().Name;
        if (pluginName is null ||
            AssemblyLoadContext.Default.Assemblies.Any(assembly =>
                string.Equals(assembly.GetName().Name, pluginName, StringComparison.Ordinal)))
        {
            return;
        }

        if (_defaultContextResolvingHookRegistered)
        {
            return;
        }

        _defaultContextResolvingHookRegistered = true;
        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
            string.Equals(assemblyName.Name, pluginName, StringComparison.Ordinal)
                ? typeof(NotchStyleStyles).Assembly
                : null;
    }

    private static bool _defaultContextResolvingHookRegistered;
}
