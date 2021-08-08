using System;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Interactivity;
using MediatR;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Commands
{
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

        public async Task<Unit> Handle(GetConfigurationCmdRequest request, CancellationToken cancellationToken)
        {
            string chatEmail = await _configManager.GetChatEmail(request.Chat) ?? "<unconfigured>";
            var config = $"🛠 Configuration\nEmail: {chatEmail}";
            await _botClient.SendTextMessageAsync(request.Chat, config, cancellationToken: cancellationToken);

            return Unit.Value;
        }

        public Task<Unit> Handle(ConfigureCmdRequest request, CancellationToken cancellationToken)
        {
            // Run workflow asynchronously, so we handle telegram updates each time.
            _ = Task.Run(() => ConfigureWorkflow(request.Chat), CancellationToken.None);

            return Unit.Task;
        }

        private async Task ConfigureWorkflow(Chat chat)
        {
            try
            {
                await _botClient.SendTextMessageAsync(chat, "Please enter your @kindle.com address");

                var mailReply = await _interactionManager.AwaitNextUpdate(chat);
                var mailAddress = mailReply.TryGetTextMessage();
                if (mailAddress == null || !mailAddress.EndsWith("@kindle.com"))
                {
                    await _botClient.SendTextMessageAsync(chat, $"⚠ Wrong email address: {mailAddress}");
                    return;
                }

                await _configManager.SetChatEmail(chat.Id, mailAddress);
                await _botClient.SendTextMessageAsync(chat, $"✅ Successfully set email: {mailAddress}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure chat {id} ({first name}{last name})", chat.Id, chat.FirstName, chat.LastName);
                await _botClient.SendTextMessageAsync(chat, $"⚠ Failed to configure: {ex.Message}");
            }
        }
    }
}
