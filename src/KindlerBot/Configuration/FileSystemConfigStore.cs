using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace KindlerBot.Configuration;

internal class FileSystemConfigStore : IConfigStore
{
    private readonly DeploymentConfiguration _deploymentConfig;
    private readonly SemaphoreSlim _storeLock = new(1, 1);

    private HashSet<ChatId>? AllowedChatIdsCached { get; set; }

    public FileSystemConfigStore(IOptions<DeploymentConfiguration> deploymentConfig)
    {
        _deploymentConfig = deploymentConfig.Value;
    }

    public async Task<string?> GetChatEmail(ChatId chatId)
    {
        Config config = await GetConfig();

        config.UserEmails.TryGetValue(chatId.ToString(), out var email);
        return email;
    }

    public async ValueTask<HashSet<ChatId>> GetAllowedChatIds()
    {
        if (AllowedChatIdsCached != null)
        {
            return AllowedChatIdsCached;
        }

        var allowedChats = (await GetAllowedChats()).Select(x => x.ChatId).ToHashSet();

        var adminChat = await GetAdminChatId();
        if (adminChat is not null)
        {
            allowedChats.Add(adminChat);
        }

        return AllowedChatIdsCached = allowedChats;
    }

    public async Task<AllowedChat[]> GetAllowedChats()
    {
        var config = await GetConfig();
        return config.AllowedChats.Select(x => new AllowedChat(new ChatId(x.ChatId), x.Description)).ToArray();
    }

    public async Task AddAllowedChat(AllowedChat chat)
    {
        await StoreConfig(config =>
        {
            var chatId = chat.ChatId.ToString();

            if (config.AllowedChats.Any(x => string.Equals(x.ChatId, chatId, StringComparison.Ordinal)))
            {
                return;
            }

            config.AllowedChats.Add(new Config.AllowedChat(ChatId: chatId, Description: chat.Description));
        });

        AllowedChatIdsCached = null;
    }

    public async Task<ChatId?> GetAdminChatId()
    {
        var config = await GetConfig();
        return config.AdminChatId != null ? new ChatId(config.AdminChatId) : null;
    }

    public async Task<Version?> GetLastAppVersion()
    {
        var config = await GetConfig();
        if (config.LastVersion is { } lastVersion)
            return Version.Parse(lastVersion);

        return null;
    }

    public async Task SetLastAppVersion(Version version)
    {
        await StoreConfig(config =>
        {
            config.LastVersion = version.ToString();
        });
    }

    public async Task SetChatEmail(ChatId chatId, string email)
    {
        await StoreConfig(config =>
        {
            config.UserEmails[chatId.ToString()] = email;
        });
    }

    private async Task<Config> GetConfig()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
            return new();

        var fileContents = await File.ReadAllTextAsync(configPath);
        return JsonConvert.DeserializeObject<Config>(fileContents)!;
    }


    private async Task StoreConfig(Action<Config> modifyConfig)
    {
        await _storeLock.WaitAsync();
        try
        {
            var config = await GetConfig();
            modifyConfig(config);
            await File.WriteAllTextAsync(GetConfigPath(), JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private string GetConfigPath()
    {
        return string.IsNullOrEmpty(_deploymentConfig.ConfigStore)
            ? Path.Join(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), "config.json")
            : Path.Join(_deploymentConfig.ConfigStore, "config.json");
    }

    private class Config
    {
        public Dictionary<string, string> UserEmails { get; set; } = new();

        public List<AllowedChat> AllowedChats { get; set; } = new();

        public string? LastVersion { get; set; }

        public string? AdminChatId { get; set; }

        public record AllowedChat(string ChatId, string? Description);
    }
}
