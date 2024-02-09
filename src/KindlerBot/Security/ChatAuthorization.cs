using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KindlerBot.Security;

internal class ChatAuthorization : IChatAuthorization
{
    private IConfigStore ConfigStore { get; }
    private IChatApprovalRequestsStore ChatApprovalRequestsStore { get; }
    private ILogger<ChatAuthorization> Logger { get; }

    private HashSet<ChatId>? AuthorizedChatIdsCached { get; set; }

    public ChatAuthorization(IConfigStore configStore, IChatApprovalRequestsStore chatApprovalRequestsStore, ILogger<ChatAuthorization> logger)
    {
        ConfigStore = configStore;
        ChatApprovalRequestsStore = chatApprovalRequestsStore;
        Logger = logger;
    }

    public async ValueTask<bool> IsAuthorized(Update update)
    {
        var chatId = update.TryGetChatId();
        if (chatId == null)
        {
            return false;
        }

        return await IsAuthorized(chatId);
    }

    public async ValueTask<bool> IsAuthorized(ChatId chatId)
    {
        if (AuthorizedChatIdsCached == null)
        {
            var allowedChatIds = (await ConfigStore.GetAllowedChats()).Select(x => x.ChatId).ToHashSet();

            var adminChatId = await ConfigStore.GetAdminChatId();
            if (adminChatId is not null)
            {
                allowedChatIds.Add(adminChatId);
            }

            AuthorizedChatIdsCached = allowedChatIds;
        }

        return AuthorizedChatIdsCached.Contains(chatId);
    }

    public async ValueTask<bool> IsAdminChat(Update update)
    {
        var chatId = update.TryGetChatId();
        if (chatId == null)
        {
            return false;
        }

        var adminChatId = await ConfigStore.GetAdminChatId();
        if (adminChatId == null)
        {
            return false;
        }

        return chatId == adminChatId;
    }

    public async Task<IReadOnlyCollection<AllowedChat>> GetAllAllowedChats()
    {
        return await ConfigStore.GetAllowedChats();
    }

    public async Task TrackUnauthorized(Update update)
    {
        // First request should be of text type, as that's /start command.
        // If it's not - just ignore such a request.
        // Idea is to track them roughly, we don't care much about precision here.
        if (update.Type != UpdateType.Message)
        {
            return;
        }

        var chat = update.Message!.Chat;

        ChatId chatId = chat;
        var chatDescription = $"{chat.FirstName} {chat.LastName}".Trim();
        if (string.IsNullOrEmpty(chatDescription))
        {
            chatDescription = chat.Username;
        }

        await ChatApprovalRequestsStore.AddApprovalRequest(new ChatApprovalRequest(chatId, chatDescription, DateTimeOffset.Now));
    }

    public async Task<IReadOnlyCollection<ChatApprovalRequest>> GetAllChatApprovalRequests()
    {
        return await ChatApprovalRequestsStore.GetAllApprovalRequests();
    }

    public async Task<ChatApprovalRequest?> GetChatApprovalRequest(ChatId chatId)
    {
        return await ChatApprovalRequestsStore.GetApprovalRequest(chatId);
    }

    public async Task AuthorizeChat(ChatId chatId, string? chatDescription)
    {
        await ConfigStore.AddAllowedChat(new AllowedChat(chatId, chatDescription));
        await ChatApprovalRequestsStore.RemoveRequest(chatId);

        AuthorizedChatIdsCached = null;

        Logger.LogInformation("Authorized chat. Chat ID: {chatId}", chatId);
    }

    public async Task RevokeChatAuthorization(ChatId chatId)
    {
        await ConfigStore.RemoveAllowedChat(chatId);
        await ChatApprovalRequestsStore.RemoveRequest(chatId);

        AuthorizedChatIdsCached = null;

        Logger.LogInformation("Revoked chat authorization. Chat ID: {chatId}", chatId);
    }
}
