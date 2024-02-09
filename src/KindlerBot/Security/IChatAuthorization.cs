using System.Collections.Generic;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using Telegram.Bot.Types;

namespace KindlerBot.Security;

public interface IChatAuthorization
{
    ValueTask<bool> IsAuthorized(Update update);

    ValueTask<bool> IsAuthorized(ChatId chatId);

    ValueTask<bool> IsAdminChat(Update update);

    Task<IReadOnlyCollection<AllowedChat>> GetAllAllowedChats();

    Task TrackUnauthorized(Update update);

    Task<IReadOnlyCollection<ChatApprovalRequest>> GetAllChatApprovalRequests();

    Task<ChatApprovalRequest?> GetChatApprovalRequest(ChatId chatId);

    Task AuthorizeChat(ChatId chatId, string? chatDescription);

    Task RevokeChatAuthorization(ChatId chatId);
}
