using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Commands
{
    internal record HelpCmdRequest(Chat Chat) : IRequest;

    internal class HelpCmdHandler : IRequestHandler<HelpCmdRequest>
    {
        private readonly ITelegramBotClient _botClient;

        public HelpCmdHandler(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public async Task<Unit> Handle(HelpCmdRequest request, CancellationToken cancellationToken)
        {
            var msg = $"Kindler {Util.ProductVersion}\n" +
                      $"Send me a book doc and I'll send it to your Kindle 🚀";

            await _botClient.SendTextMessageAsync(request.Chat.Id, msg, cancellationToken: cancellationToken);

            return Unit.Value;
        }
    }
}
