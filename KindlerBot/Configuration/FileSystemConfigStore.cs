using System.Collections.Generic;
using System.IO;
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

        public async ValueTask<string?> GetChatEmail(ChatId chatId)
        {
            Config config = await GetConfig();

            config.UserEmails.TryGetValue(chatId.ToString(), out var email);
            return email;
        }

        public async ValueTask SetChatEmail(ChatId chatId, string email)
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

        private string GetConfigPath() => string.IsNullOrEmpty(_deploymentConfig.ConfigStore) ? "config.json" : Path.Join(_deploymentConfig.ConfigStore, "config.json");

        private class Config
        {
            public Dictionary<string, string> UserEmails { get; set; } = new();
        }
    }
}
