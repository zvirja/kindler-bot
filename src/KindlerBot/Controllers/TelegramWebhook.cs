using System;
using System.Threading.Tasks;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using KindlerBot.Interactivity;
using KindlerBot.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Controllers;

public class TelegramWebhook : Controller
{
    private readonly IChatAuthorization _chatAuthorization;
    private readonly ILogger<TelegramWebhook> _logger;
    private readonly BotConfiguration _botConfig;

    public TelegramWebhook(IOptions<BotConfiguration> botConfiguration, IChatAuthorization chatAuthorization, ILogger<TelegramWebhook> logger)
    {
        _botConfig = botConfiguration.Value;
        _chatAuthorization = chatAuthorization;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleUpdate(string signature, [FromBody] Update update,
        [FromServices] ITelegramCommands updateDispatcher, [FromServices] IInteractionManager interactionManager, [FromServices] ITelegramBotClient botClient)
    {
        if (!string.Equals(_botConfig.WebhookUrlSecret, signature, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected webhook request with wrong signature. Actual: {actual}, Expected: {expected}", signature, _botConfig.WebhookUrlSecret);
            return NotFound();
        }

        if (!await _chatAuthorization.IsAuthorized(update))
        {
            _logger.LogWarning("Received message from non-authorized chat {chat id}", update.TryGetChatId());
            await _chatAuthorization.TrackUnauthorized(update);
            return Ok();
        }

        try
        {
            if (interactionManager.ResumeInteraction(update))
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
                await botClient.SendMessage(chatId, $"❌ Failed to handle update: {ex.Message}");
            }
        }

        return Ok();
    }
}
