using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Security;

internal interface IChatApprovalRequestsStore
{
    Task<IReadOnlyCollection<ChatApprovalRequest>> GetAllApprovalRequests();

    Task<ChatApprovalRequest?> GetApprovalRequest(ChatId chatId);

    Task AddApprovalRequest(ChatApprovalRequest approvalRequest);

    Task RemoveRequest(ChatId chatId);

    Task CleanObsoleteRequests(TimeSpan expiration);
}
