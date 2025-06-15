using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KindlerBot.Commands;

internal record UnknownCmdRequest(Update Update) : IRequest;

internal class UnknownCmdHandler : IRequestHandler<UnknownCmdRequest>
{
    private readonly ITelegramBotClient _botClient;

    public UnknownCmdHandler(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task Handle(UnknownCmdRequest request, CancellationToken cancellationToken)
    {
        Update update = request.Update;

        if (update.TryGetChatId() is { } chatId)
        {
            string message = update.Type switch
            {
                UpdateType.Message => $@"¯\_(ツ)_/¯ Unknown {update.Message!.Type} message command",
                UpdateType.CallbackQuery => $@"¯\_(ツ)_/¯ Unknown callback command: {update.CallbackQuery!.Data}",
                _ => $@"¯\_(ツ)_/¯ Unknown command. Update type: {update.Type:G}"
            };

            await _botClient.SendMessage(chatId, message, cancellationToken: cancellationToken);
        }
    }
}
