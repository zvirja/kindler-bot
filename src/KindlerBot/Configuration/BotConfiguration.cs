#nullable disable warnings

namespace KindlerBot.Configuration;

public class BotConfiguration
{
    public const string SectionName = "Bot";

    public string BotToken { get; set; }

    public string? BotApiServer { get; set; }

    public string WebhookUrlSecret { get; set; }

    public bool EnableChatAuthorization { get; set; } = true;
}
