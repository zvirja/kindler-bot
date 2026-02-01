using System.Reflection;

namespace KindlerBot;

internal class BotVersion
{
    public static BotVersion Current { get; } = new();

    public string InfoVersion { get; }

    private BotVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        InfoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    }
}
