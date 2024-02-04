using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using MediatR;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
        var msg = $"""
                   Kindler v{BotVersion.Current.AppVersion} ({BotVersion.Current.GitSha})
                   Send me a book file and I'll send it to your Kindle 🚀

                   Make sure to add {_smtpConfig.FromEmail} to your list of allowed senders on Amazon website.

                   To find your Kindle email address, visit the [Manage Your Content and Devices -> Preferences](https://www.amazon.com/hz/mycd/myx#/home/settings/pdoc) and navigate to *Personal Document Settings* section at the bottom.
                   """;

        await _botClient.SendTextMessageAsync(request.Chat.Id, msg, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
    }
}
