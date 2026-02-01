using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KindlerBot.IO;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace KindlerBot.Configuration;

internal class FileSystemConfigStore : FileSystemStoreBase<FileSystemConfigStore.Config>, IConfigStore
{
    public FileSystemConfigStore(IOptions<DeploymentConfiguration> deploymentConfig)
        : base("config.json", deploymentConfig)
    {
    }

    public async Task<string?> GetChatEmail(ChatId chatId)
    {
        Config config = await GetStoreData();

        config.UserEmails.TryGetValue(chatId.ToString(), out var email);
        return email;
    }

    public async Task<AllowedChat[]> GetAllowedChats()
    {
        var config = await GetStoreData();
        return config.AllowedChats.Select(x => new AllowedChat(new ChatId(x.ChatId), x.Description)).ToArray();
    }

    public async Task AddAllowedChat(AllowedChat chat)
    {
        await UpdateStoreData(config =>
        {
            var chatId = chat.ChatId.ToString();

            if (config.AllowedChats.Any(x => string.Equals(x.ChatId, chatId, StringComparison.Ordinal)))
            {
                return;
            }

            config.AllowedChats.Add(new Config.AllowedChat(ChatId: chatId, Description: chat.ChatDescription));
        });
    }

    public async Task RemoveAllowedChat(ChatId chatId)
    {
        await UpdateStoreData(config =>
        {
            config.AllowedChats.RemoveAll(x => x.ChatId == chatId);
        });
    }

    public async Task<ChatId?> GetAdminChatId()
    {
        var config = await GetStoreData();
        return config.AdminChatId != null ? new ChatId(config.AdminChatId) : null;
    }

    public async Task<string?> GetLastAppVersion()
    {
        var config = await GetStoreData();
        return config.LastVersion;
    }

    public async Task SetLastAppVersion(string version)
    {
        await UpdateStoreData(config =>
        {
            config.LastVersion = version;
        });
    }

    public async Task SetChatEmail(ChatId chatId, string email)
    {
        await UpdateStoreData(config =>
        {
            config.UserEmails[chatId.ToString()] = email;
        });
    }

    internal class Config
    {
        public Dictionary<string, string> UserEmails { get; set; } = new();

        public List<AllowedChat> AllowedChats { get; set; } = new();

        public string? LastVersion { get; set; }

        public string? AdminChatId { get; set; }

        public record AllowedChat(string ChatId, string? Description);
    }
}
