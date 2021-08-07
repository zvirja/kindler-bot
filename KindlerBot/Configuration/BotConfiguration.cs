#nullable disable warnings
using System;

namespace KindlerBot.Configuration
{
    public class BotConfiguration
    {
        public const string SectionName = "Bot";

        public string BotToken { get; set; }

        public string WebhookUrlSecret { get; set; }
    }

    public class DeploymentConfiguration
    {
        public const string SectionName = "Deployment";

        public Uri PublicUrl { get; set; }

        public string ConfigStore { get; set; }
    }
}
