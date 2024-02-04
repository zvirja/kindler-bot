using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Commands;

public interface ITelegramCommands
{
    IEnumerable<BotCommand> AvailableCommands { get; }

    Task DispatchAuthorizedUpdate(Update update, CancellationToken ct);

    Task DispatchNonAuthorizedUpdate(Update update, CancellationToken ct);
}
