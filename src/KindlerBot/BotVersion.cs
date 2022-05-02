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

            var gitShaAttr = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "GitSha")?.Value;
            GitSha = string.IsNullOrEmpty(gitShaAttr) ? "<unknown>" : gitShaAttr[..7];
        }
    }
}
