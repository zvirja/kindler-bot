using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Security;
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
    private readonly IChatAuthorization _chatAuthorization;

    public TelegramCommands(IMediator mediator, IChatAuthorization chatAuthorization)
    {
        _mediator = mediator;
        _chatAuthorization = chatAuthorization;
    }

    public IEnumerable<BotCommand> AvailableCommands { get; } = new BotCommand[]
    {
        new() { Command = Constants.HelpCmd, Description = "Get usage info" },
        new() { Command = Constants.ConfigureCmd, Description = "Configure my preferences" },
        new() { Command = Constants.GetConfigurationCmd, Description = "Get my preferences" },
    };

    public async Task DispatchUpdate(Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery!;

            if (callbackQuery.Data?.StartsWith(AuthorizeChatCallbackCmdRequest.CallbackDataPrefix, StringComparison.Ordinal) == true)
            {
                await _mediator.Send(new AuthorizeChatCallbackCmdRequest(callbackQuery), ct);
                return;
            }

            await _mediator.Send(new UnknownCmdRequest(update), ct);
            return;
        }

        if (update.Type != UpdateType.Message)
        {
            return;
        }

        var message = update.Message!;
        bool IsTextMessage(string msg) => message.Type == MessageType.Text && message.Text == msg;
        bool IsTextMessageStartWith(string prefix) => message.Type == MessageType.Text && message.Text?.StartsWith(prefix) == true;

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
            await _mediator.Send(new ConvertCmdRequest(message.Document!, message.Chat), ct);
            return;
        }

        if (await _chatAuthorization.IsAdminChat(update))
        {
            if (IsTextMessage(AuthorizeCmdRequest.CmdText))
            {
                await _mediator.Send(new AuthorizeCmdRequest(message.Chat), ct);
                return;
            }

            if (IsTextMessageStartWith(AuthorizeReviewCmdRequest.CmdPrefix))
            {
                await _mediator.Send(new AuthorizeReviewCmdRequest(message.Chat, message.Text!), ct);
                return;
            }
        }

        await _mediator.Send(new UnknownCmdRequest(update), ct);
    }
}
