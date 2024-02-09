using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Configuration;

public record AllowedChat(ChatId ChatId, string? ChatDescription);

internal interface IConfigStore
{
    public Task SetChatEmail(ChatId chatId, string email);

    public Task<string?> GetChatEmail(ChatId chatId);

    public Task<AllowedChat[]> GetAllowedChats();

    public Task AddAllowedChat(AllowedChat chat);

    public Task RemoveAllowedChat(ChatId chatId);

    public Task<ChatId?> GetAdminChatId();

    public Task<Version?> GetLastAppVersion();

    public Task SetLastAppVersion(Version version);
}
