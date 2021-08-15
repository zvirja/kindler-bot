using System;
using System.Linq;
using System.Reflection;

namespace KindlerBot
{
    internal class BotVersion
    {
        public static BotVersion Current { get; } = new();

        public Version AppVersion { get; }
        public string GitSha { get; }

        private BotVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();

            AppVersion = Version.Parse(assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
            GitSha = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "GitSha")?.Value![..7] ?? "<unknown>";
        }
    }
}
