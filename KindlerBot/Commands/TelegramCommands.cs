using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KindlerBot.Commands
{
    internal class TelegramCommands : ITelegramCommands
    {
        private readonly IMediator _mediator;

        public TelegramCommands(IMediator mediator)
        {
            _mediator = mediator;
        }

        public IEnumerable<BotCommand> AvailableCommands { get; } = new BotCommand[]
        {
            new() { Command = "/help", Description = "Get usage info" },
            new() { Command = "/configure", Description = "Configure my preferences" },
            new() { Command = "/getconfig", Description = "Get my preferences" },
        };

        public async Task DispatchUpdate(Update update, CancellationToken ct)
        {
            if (update.Type != UpdateType.Message)
            {
                return;
            }

            var message = update.Message;

            if (IsTextMessage("/start") || IsTextMessage("/help"))
            {
                await _mediator.Send(new HelpCmdRequest(message.Chat), ct);
                return;
            }

            if (IsTextMessage("/configure"))
            {
                await _mediator.Send(new ConfigureCmdRequest(message.Chat), ct);
                return;
            }

            if (IsTextMessage("/getconfig"))
            {
                await _mediator.Send(new GetConfigurationCmdRequest(message.Chat), ct);
                return;
            }

            await _mediator.Send(new UnknownCmdRequest(update), ct);

            bool IsTextMessage(string msg) => message.Type == MessageType.Text && message.Text == msg;
        }
    }
}
