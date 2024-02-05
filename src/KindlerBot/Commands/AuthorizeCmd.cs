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
        // Run workflow asynchronously, as approval can take really long time and we don't want Telegram to timeout.
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
                new[] { InlineKeyboardButton.WithCallbackData("Approve"), InlineKeyboardButton.WithCallbackData("Ignore") }
            );

            var approveMsg = await BotClient.SendTextMessageAsync(adminChatId, approvalMsg, replyMarkup: replyMarkup, cancellationToken: cancellationToken);

            var replyUpdate = await InteractionManager.AwaitNextUpdate(adminChatId);

            string approvalStatusLine;

            if (replyUpdate.CallbackQuery is { } callbackReply)
            {
                if (string.Equals(callbackReply.Data, "Approve", StringComparison.Ordinal))
                {
                    await ConfigStore.AddAllowedChat(new AllowedChat(chatId, chatDescription));

                    await BotClient.SendTextMessageAsync(chatId, text: "Your bot usage was approved.\nPlease click here: /start", cancellationToken: cancellationToken);

                    approvalStatusLine = "✅ Approved new user request!";
                }
                else
                {
                    approvalStatusLine = "❌ Ignored new user request!";
                }
            }
            else
            {
                approvalStatusLine = "⚠ Approval flow was interrupted!";
            }

            var approvalStatusMsg = $"""
                                     {approvalStatusLine}

                                     Chat ID: {chatId}
                                     Chat Info: {chatDescription}
                                     """;

            await BotClient.EditMessageTextAsync(chatId: approveMsg.Chat, messageId: approveMsg.MessageId, text: approvalStatusMsg, cancellationToken: cancellationToken);
        }
    }
}
