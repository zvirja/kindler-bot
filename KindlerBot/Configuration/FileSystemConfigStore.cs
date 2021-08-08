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

namespace KindlerBot.Configuration
{
    internal class FileSystemConfigStore : IConfigStore
    {
        private readonly DeploymentConfiguration _deploymentConfig;
        private readonly SemaphoreSlim _storeLock = new(1, 1);

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

        public async Task<ChatId[]> GetAllowedChatIds()
        {
            var config = await GetConfig();
            return config.AllowedChatIds.Select(x => new ChatId(x)).ToArray();
        }

        public async Task SetChatEmail(ChatId chatId, string email)
        {
            await _storeLock.WaitAsync();
            try
            {
                Config config = await GetConfig();
                config.UserEmails[chatId.ToString()] = email;
                await StoreConfig(config);
            }
            finally
            {
                _storeLock.Release();
            }
        }

        private async Task<Config> GetConfig()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
                return new();

            var fileContents = await File.ReadAllTextAsync(configPath);
            return JsonConvert.DeserializeObject<Config>(fileContents);
        }


        private async Task StoreConfig(Config config)
        {
            await File.WriteAllTextAsync(GetConfigPath(), JsonConvert.SerializeObject(config));
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
            public string[] AllowedChatIds { get; set; } = Array.Empty<string>();
        }
    }
}
