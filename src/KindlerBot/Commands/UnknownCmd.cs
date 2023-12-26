using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;

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
        if (request.Update.TryGetChatId() is { } chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, @"¯\_(ツ)_/¯ Unknown command", cancellationToken: cancellationToken);
        }
    }
}
