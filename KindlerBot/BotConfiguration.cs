#nullable disable warnings
using System;

namespace KindlerBot
{
    public class BotConfiguration
    {
        public const string SectionName = "BotConfiguration";

        public string BotToken { get; set; }
        public Uri PublicUrl { get; set; }

        public string WebhookUrlSecret { get; set; }
    }
}
