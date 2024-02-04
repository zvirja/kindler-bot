using System;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Interactivity;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace KindlerBot.Commands;

internal record AuthorizeCmdRequest(Chat ChatToAuthorize) : IRequest;

internal class AuthorizeCmdHandler : IRequestHandler<AuthorizeCmdRequest>
{
    private ITelegramBotClient BotClient { get; }

    private IConfigStore ConfigStore { get; }

    private IInteractionManager InteractionManager { get; }

    public AuthorizeCmdHandler(ITelegramBotClient botClient, IConfigStore configStore, IInteractionManager interactionManager)
    {
        BotClient = botClient;
        ConfigStore = configStore;
        InteractionManager = interactionManager;
    }

    public Task Handle(AuthorizeCmdRequest request, CancellationToken cancellationToken)
    {
        // Run workflow asynchronously, so we handle telegram updates each time.
        _ = Task.Run(DoHandle, CancellationToken.None);

        return Task.CompletedTask;

        async Task DoHandle()
        {
            ChatId chatId = request.ChatToAuthorize;
            var chatDescription = $"{request.ChatToAuthorize.FirstName} {request.ChatToAuthorize.LastName}".Trim();
            if (string.IsNullOrEmpty(chatDescription))
            {
                chatDescription = request.ChatToAuthorize.Username;
            }

            var adminChatId = await ConfigStore.GetAdminChatId();

            // If admin chat is not configured, just return and do nothing. As we simply cannot proceed.
            if (adminChatId == null)
            {
                return;
            }

            var approvalMsg = $"""
                               ❓ Please review and approve new user

                               Chat ID: {chatId}
                               Chat Info: {chatDescription}
                               """;

            var replyMarkup = new InlineKeyboardMarkup(
                new[] { InlineKeyboardButton.WithCallbackData("Approve"), InlineKeyboardButton.WithCallbackData("Reject") }
            );

            await BotClient.SendTextMessageAsync(adminChatId, approvalMsg, replyMarkup: replyMarkup, cancellationToken: cancellationToken);

            var replyUpdate = await InteractionManager.AwaitNextUpdate(adminChatId);
            if (replyUpdate.Type != UpdateType.CallbackQuery)
            {
                return;
            }

            string approvalStatusLine;

            if (string.Equals(replyUpdate.CallbackQuery!.Data, "Approve", StringComparison.Ordinal))
            {
                await ConfigStore.AddAllowedChat(new AllowedChat(chatId, chatDescription));

                await BotClient.SendTextMessageAsync(chatId, text: "Your bot usage was approved.\nPlease click here: /start", cancellationToken: cancellationToken);

                approvalStatusLine = "✅ Approved new user request!";
            }
            else
            {
                approvalStatusLine = "❌ Rejected new user request!";
            }

            var approvalStatusMsg = $"""
                               {approvalStatusLine}

                               Chat ID: {chatId}
                               Chat Info: {chatDescription}
                               """;

            if (replyUpdate.CallbackQuery.Message is {} replyMsg)
            {
                await BotClient.EditMessageTextAsync(chatId: replyMsg.Chat, messageId: replyMsg.MessageId, text: approvalStatusMsg, cancellationToken: cancellationToken);
            }
        }
    }
}
