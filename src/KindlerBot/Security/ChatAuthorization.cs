using System.Threading.Tasks;
using KindlerBot.Configuration;
using Telegram.Bot.Types;

namespace KindlerBot.Security;

internal class ChatAuthorization : IChatAuthorization
{
    private IConfigStore ConfigStore { get; }

    public ChatAuthorization(IConfigStore configStore)
    {
        ConfigStore = configStore;
    }

    public async ValueTask<bool> IsAuthorized(Update update)
    {
        var chatId = update.TryGetChatId();
        if (chatId == null)
        {
            return false;
        }

        var allowedChatIds = await ConfigStore.GetAllowedChatIds();

        return allowedChatIds.Contains(chatId);
    }
}
