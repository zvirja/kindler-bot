using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Configuration
{
    internal interface IConfigStore
    {
        public Task SetChatEmail(ChatId chatId, string email);

        public Task<string?> GetChatEmail(ChatId chatId);

        public Task<ChatId[]> GetAllowedChatIds();

        public Task<ChatId?> GetAdminChatId();

        public Task<Version?> GetLastAppVersion();
        public Task SetLastAppVersion(Version version);
    }
}
