using System;
using System.Reflection;

namespace KindlerBot;

internal class BotVersion
{
    public static BotVersion Current { get; } = new();

    public Version FileVersion { get; }
    public string InfoVersion { get; }

    private BotVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        FileVersion = Version.Parse(assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
        InfoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    }
}
