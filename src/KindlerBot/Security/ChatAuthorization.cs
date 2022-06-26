using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using Telegram.Bot.Types;

namespace KindlerBot.Security;

internal class ChatAuthorization : IChatAuthorization
{
    private readonly IConfigStore _configStore;

    private HashSet<ChatId>? AuthorizedChatIds { get; set; }

    public ChatAuthorization(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async ValueTask<bool> IsAuthorized(Update update)
    {
        AuthorizedChatIds ??= (await _configStore.GetAllowedChatIds()).ToHashSet();

        if (AuthorizedChatIds.Count == 0)
        {
            return true;
        }

        var chatId = update.TryGetChatId();
        if (chatId == null)
        {
            return false;
        }

        return AuthorizedChatIds.Contains(chatId);
    }
}
