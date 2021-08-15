using System;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using MediatR;
using Telegram.Bot;

namespace KindlerBot.Commands
{
    internal record UpdateNotification(BotVersion NewVersion) : INotification;

    internal class UpdateNotificationHandler : INotificationHandler<UpdateNotification>
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IConfigStore _configStore;

        public UpdateNotificationHandler(ITelegramBotClient botClient, IConfigStore configStore)
        {
            _botClient = botClient;
            _configStore = configStore;
        }

        public async Task Handle(UpdateNotification notification, CancellationToken cancellationToken)
        {
            var adminChatId = await _configStore.GetAdminChatId();
            if (adminChatId == null)
                return;

            var msg = $"🎈 Updated to v{notification.NewVersion.AppVersion} ({notification.NewVersion.GitSha})";
            await _botClient.SendTextMessageAsync(adminChatId, msg, cancellationToken: cancellationToken);
        }
    }
}
