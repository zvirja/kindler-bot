using System;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Interactivity;
using MediatR;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Commands;

internal record ConfigureCmdRequest(Chat Chat) : IRequest;
internal record GetConfigurationCmdRequest(Chat Chat) : IRequest;

internal class ConfigureCmdHandler: IRequestHandler<ConfigureCmdRequest>, IRequestHandler<GetConfigurationCmdRequest>
{
    private readonly IInteractionManager _interactionManager;
    private readonly IConfigStore _configManager;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<ConfigureCmdHandler> _logger;

    public ConfigureCmdHandler(IInteractionManager interactionManager, IConfigStore configManager, ITelegramBotClient botClient, ILogger<ConfigureCmdHandler> logger)
    {
        _interactionManager = interactionManager;
        _configManager = configManager;
        _botClient = botClient;
        _logger = logger;
    }

    public async Task Handle(GetConfigurationCmdRequest request, CancellationToken cancellationToken)
    {
        string chatEmail = await _configManager.GetChatEmail(request.Chat) ?? "<unconfigured>";
        var config = $"🛠 Configuration\nEmail: {chatEmail}";
        await _botClient.SendMessage(request.Chat, config, cancellationToken: cancellationToken);
    }

    public Task Handle(ConfigureCmdRequest request, CancellationToken cancellationToken)
    {
        // Run workflow asynchronously, so we handle telegram updates each time.
        _ = Task.Run(() => ConfigureWorkflow(request.Chat), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task ConfigureWorkflow(Chat chat)
    {
        try
        {
            await _botClient.SendMessage(chat, "Please enter your @kindle.com address");

            var mailReply = await _interactionManager.AwaitNextUpdate(chat);
            var mailAddress = mailReply.TryGetTextMessage();
            if (mailAddress == null || !mailAddress.Contains("@"))
            {
                await _botClient.SendMessage(chat, $"⚠ Wrong email address: {mailAddress}");
                return;
            }

            await _configManager.SetChatEmail(chat.Id, mailAddress);
            await _botClient.SendMessage(chat, $"✅ Successfully set email: {mailAddress}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure chat {id} ({first name}{last name})", chat.Id, chat.FirstName, chat.LastName);
            await _botClient.SendMessage(chat, $"⚠ Failed to configure: {ex.Message}");
        }
    }
}
