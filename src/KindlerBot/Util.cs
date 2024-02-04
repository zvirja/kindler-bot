using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KindlerBot;

internal static class Util
{
    public static ChatId? TryGetChatId(this Update update)
    {
        if (update.Type == UpdateType.Message)
            return update.Message!.Chat;

        if (update.Type == UpdateType.CallbackQuery)
            return update.CallbackQuery!.Message?.Chat;

        return null;
    }

    public static string? TryGetTextMessage(this Update update)
    {
        return update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text
            ? update.Message.Text
            : null;
    }
}
