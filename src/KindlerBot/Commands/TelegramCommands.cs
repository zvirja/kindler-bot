using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KindlerBot.Commands;

internal class TelegramCommands : ITelegramCommands
{
    private static class Constants
    {
        public const string StartCmd = "/start";
        public const string HelpCmd = "/help";
        public const string ConfigureCmd = "/configure";
        public const string GetConfigurationCmd = "/getconfig";
    }

    private readonly IMediator _mediator;

    public TelegramCommands(IMediator mediator)
    {
        _mediator = mediator;
    }

    public IEnumerable<BotCommand> AvailableCommands { get; } = new BotCommand[]
    {
        new() { Command = Constants.HelpCmd, Description = "Get usage info" },
        new() { Command = Constants.ConfigureCmd, Description = "Configure my preferences" },
        new() { Command = Constants.GetConfigurationCmd, Description = "Get my preferences" },
    };

    public async Task DispatchUpdate(Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message)
        {
            return;
        }

        var message = update.Message;
        bool IsTextMessage(string msg) => message.Type == MessageType.Text && message.Text == msg;

        if (IsTextMessage(Constants.StartCmd) || IsTextMessage(Constants.HelpCmd))
        {
            await _mediator.Send(new HelpCmdRequest(message.Chat), ct);
            return;
        }

        if (IsTextMessage(Constants.ConfigureCmd))
        {
            await _mediator.Send(new ConfigureCmdRequest(message.Chat), ct);
            return;
        }

        if (IsTextMessage(Constants.GetConfigurationCmd))
        {
            await _mediator.Send(new GetConfigurationCmdRequest(message.Chat), ct);
            return;
        }

        if (message.Type == MessageType.Document)
        {
            await _mediator.Send(new ConvertCmdRequest(message.Document, message.Chat), ct);
            return;
        }

        await _mediator.Send(new UnknownCmdRequest(update), ct);
    }
}