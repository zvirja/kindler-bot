using System;
using System.Threading.Tasks;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using KindlerBot.Workflow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Controllers
{
    [Route("webhook")]
    public class TelegramWebhook : Controller
    {
        private readonly ILogger<TelegramWebhook> _logger;
        private readonly BotConfiguration _botConfiguration;

        public TelegramWebhook(IOptions<BotConfiguration> botConfiguration, ILogger<TelegramWebhook> logger)
        {
            _botConfiguration = botConfiguration.Value;
            _logger = logger;
        }

        [HttpPost("{signature}")]
        public async Task<IActionResult> HandleUpdate(string signature, [FromBody] Update update,
            [FromServices] ITelegramCommands updateDispatcher, [FromServices] IWorkflowManager workflowManager, [FromServices] ITelegramBotClient botClient)
        {
            if (!string.Equals(_botConfiguration.WebhookUrlSecret, signature, StringComparison.Ordinal))
            {
                return NotFound();
            }

            try
            {
                if (workflowManager.ResumeWorkflow(update))
                {
                    return Ok();
                }

                await updateDispatcher.DispatchUpdate(update, ct: default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle Telegram Update");
                if (update.TryGetChatId() is { } chatId)
                {
                    await botClient.SendTextMessageAsync(chatId, $"❗ Failed to handle update: {ex.Message}");
                }
            }

            return Ok();
        }
    }
}
