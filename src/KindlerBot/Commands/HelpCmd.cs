using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using MediatR;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Commands;

internal record HelpCmdRequest(Chat Chat) : IRequest;

internal class HelpCmdHandler : IRequestHandler<HelpCmdRequest>
{
    private readonly ITelegramBotClient _botClient;
    private readonly SmtpConfiguration _smtpConfig;

    public HelpCmdHandler(ITelegramBotClient botClient, IOptions<SmtpConfiguration> smtpConfig)
    {
        _botClient = botClient;
        _smtpConfig = smtpConfig.Value;
    }

    public async Task Handle(HelpCmdRequest request, CancellationToken cancellationToken)
    {
        var msg = $"Kindler v{BotVersion.Current.AppVersion} ({BotVersion.Current.GitSha})\n" +
                  $"Send me a book doc and I'll send it to your Kindle 🚀\n" +
                  $"\n" +
                  $"Make sure to add {_smtpConfig.FromEmail} to your list of allowed senders on Amazon website.";

        await _botClient.SendTextMessageAsync(request.Chat.Id, msg, cancellationToken: cancellationToken);
    }
}
