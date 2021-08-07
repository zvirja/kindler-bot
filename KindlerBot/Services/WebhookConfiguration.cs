using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace KindlerBot.Services
{
    internal class WebhookConfiguration: IHostedService
    {
        private readonly ITelegramBotClient _telegramBotClient;
        private readonly ILogger<WebhookConfiguration> _logger;
        private BotConfiguration _botConfiguration;

        public WebhookConfiguration(ITelegramBotClient telegramBotClient, IOptions<BotConfiguration> botConfiguration, ILogger<WebhookConfiguration> logger)
        {
            _telegramBotClient = telegramBotClient;
            _logger = logger;
            _botConfiguration = botConfiguration.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Uri url = new(_botConfiguration.PublicUrl, $"webhook/{_botConfiguration.WebhookUrlSecret}");
            await _telegramBotClient.SetWebhookAsync(url.ToString(), cancellationToken: cancellationToken);

            _logger.LogInformation("Registered webhook url");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _telegramBotClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Unregistered webhook url");
        }
    }
}
