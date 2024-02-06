using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace KindlerBot.Commands;

internal record AuthorizeChatCmdRequest(Chat ChatToAuthorize) : IRequest;

internal record AuthorizeChatCallbackCmdRequest(CallbackQuery CallbackQuery) : IRequest
{
    public const string CallbackDataPrefix = "authorize:";

    public static string BuildData(ApprovalReply reply, ChatId chatId, string? chatDescription)
    {
        return $"{CallbackDataPrefix}_{reply:G}_{chatId}_{Convert.ToBase64String(Encoding.UTF8.GetBytes(chatDescription ?? ""))}";
    }

    public static (ApprovalReply reply, ChatId chatId, string? chatDescription) ParseData(string data)
    {
        var parts = data.Split("_");
        if (!string.Equals(parts[0], CallbackDataPrefix))
            throw new ArgumentException("Wrong data", nameof(data));

        string? chatDescription = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
        if (string.IsNullOrEmpty(chatDescription))
            chatDescription = null;

        return (
            Enum.Parse<ApprovalReply>(parts[1]),
            new ChatId(parts[2]),
            chatDescription
        );
    }

    public enum ApprovalReply
    {
        Approve,
        Ignore
    }
}

internal class AuthorizeChatCmdHandler(ITelegramBotClient botClient, IConfigStore configStore)
    : IRequestHandler<AuthorizeChatCmdRequest>, IRequestHandler<AuthorizeChatCallbackCmdRequest>
{
    public async Task Handle(AuthorizeChatCmdRequest request, CancellationToken cancellationToken)
    {
        ChatId chatId = request.ChatToAuthorize;
        var chatDescription = $"{request.ChatToAuthorize.FirstName} {request.ChatToAuthorize.LastName}".Trim();
        if (string.IsNullOrEmpty(chatDescription))
        {
            chatDescription = request.ChatToAuthorize.Username;
        }

        var adminChatId = await configStore.GetAdminChatId();

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
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "Approve",
                    AuthorizeChatCallbackCmdRequest.BuildData(AuthorizeChatCallbackCmdRequest.ApprovalReply.Approve, chatId, chatDescription)),
                InlineKeyboardButton.WithCallbackData(
                    "Ignore",
                    AuthorizeChatCallbackCmdRequest.BuildData(AuthorizeChatCallbackCmdRequest.ApprovalReply.Ignore, chatId, chatDescription)),
            }
        );

        await botClient.SendTextMessageAsync(adminChatId, approvalMsg, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
    }

    public async Task Handle(AuthorizeChatCallbackCmdRequest request, CancellationToken cancellationToken)
    {
        string approvalStatusLine;

        (AuthorizeChatCallbackCmdRequest.ApprovalReply approvalReply, ChatId? chatId, string? chatDescription) =
            AuthorizeChatCallbackCmdRequest.ParseData(request.CallbackQuery.Data!);

        if (approvalReply == AuthorizeChatCallbackCmdRequest.ApprovalReply.Approve)
        {
            await configStore.AddAllowedChat(new AllowedChat(chatId, chatDescription));

            await botClient.SendTextMessageAsync(chatId, text: "Your bot usage was approved.\nPlease click here: /start", cancellationToken: cancellationToken);

            approvalStatusLine = "✅ Approved new user request!";
        }
        else
        {
            approvalStatusLine = "✖️ Ignored new user request!";
        }

        if (request.CallbackQuery.Message is { } approveMsg)
        {
            var approvalStatusMsg = $"""
                                     {approvalStatusLine}

                                     Chat ID: {chatId}
                                     Chat Info: {chatDescription}
                                     """;
            await botClient.EditMessageTextAsync(chatId: approveMsg.Chat, messageId: approveMsg.MessageId, text: approvalStatusMsg, cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(request.CallbackQuery.Id, cancellationToken: cancellationToken);
    }
}
