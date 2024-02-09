using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Security;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace KindlerBot.Commands;

internal record AuthorizeCmdRequest(ChatId AdminChatId) : IRequest
{
    public const string CmdText = "/authorize";
}

internal record AuthorizeReviewCmdRequest(ChatId AdminChatId, string CmdText) : IRequest
{
    public const string CmdPrefix = "/authorize/review/";

    public static string BuildData(ChatId chatId)
    {
        return $"{CmdPrefix}{chatId}";
    }

    public static ChatId ParseData(string data)
    {
        if (!data.StartsWith(CmdPrefix))
            throw new ArgumentException("Wrong data", nameof(data));

        data = data[CmdPrefix.Length..];

        return new ChatId(data);
    }
}

internal record AuthorizeChatCallbackCmdRequest(CallbackQuery CallbackQuery) : IRequest
{
    public const string CallbackDataPrefix = "/authorize/callback/";

    public static string BuildData(ApprovalReply reply, ChatId chatId, string? chatDescription)
    {
        return $"{CallbackDataPrefix}{reply:G}_{chatId}_{Convert.ToBase64String(Encoding.UTF8.GetBytes(chatDescription ?? ""))}";
    }

    public static (ApprovalReply reply, ChatId chatId, string? chatDescription) ParseData(string data)
    {
        if (!data.StartsWith(CallbackDataPrefix))
            throw new ArgumentException("Wrong data", nameof(data));

        data = data[CallbackDataPrefix.Length..];

        var parts = data.Split("_");

        string? chatDescription = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
        if (string.IsNullOrEmpty(chatDescription))
            chatDescription = null;

        return (
            Enum.Parse<ApprovalReply>(parts[0]),
            new ChatId(parts[1]),
            chatDescription
        );
    }

    public enum ApprovalReply
    {
        Allow,
        Deny
    }
}

internal class AuthorizeCmdHandler(ITelegramBotClient botClient, IChatAuthorization chatAuthorization)
    : IRequestHandler<AuthorizeCmdRequest>, IRequestHandler<AuthorizeReviewCmdRequest>, IRequestHandler<AuthorizeChatCallbackCmdRequest>
{
    public async Task Handle(AuthorizeCmdRequest request, CancellationToken cancellationToken)
    {
        var replyBuilder = new StringBuilder();

        {
            replyBuilder.AppendLine("✔ Allowed chats:");
            replyBuilder.AppendLine();

            IReadOnlyCollection<AllowedChat> allowedChats = await chatAuthorization.GetAllAllowedChats();

            foreach (var allowedChat in allowedChats)
            {
                replyBuilder.AppendLine($"Chat ID: {allowedChat.ChatId}");
                replyBuilder.AppendLine($"Chat Info: {allowedChat.ChatDescription}");
                replyBuilder.AppendLine($"{AuthorizeReviewCmdRequest.BuildData(allowedChat.ChatId)}");
                replyBuilder.AppendLine();
            }
        }

        {
            replyBuilder.AppendLine("🔍 Chat requests:");
            replyBuilder.AppendLine();

            var authorizationRequests = await chatAuthorization.GetAllChatApprovalRequests();

            foreach (ChatApprovalRequest approvalRequest in authorizationRequests.OrderBy(x => x.CreationTime))
            {
                replyBuilder.AppendLine($"Chat ID: {approvalRequest.ChatId}");
                replyBuilder.AppendLine($"Chat Info: {approvalRequest.ChatDescription}");
                replyBuilder.AppendLine($"{AuthorizeReviewCmdRequest.BuildData(approvalRequest.ChatId)}");
                replyBuilder.AppendLine();
            }
        }

        await botClient.SendTextMessageAsync(request.AdminChatId, replyBuilder.ToString(), cancellationToken: cancellationToken);
    }

    public async Task Handle(AuthorizeReviewCmdRequest request, CancellationToken cancellationToken)
    {
        var chatIdToReview = AuthorizeReviewCmdRequest.ParseData(request.CmdText);
        var adminChatId = request.AdminChatId;

        string? chatDescription = null;

        // Try getting description from either approval or already approved chat
        if (await chatAuthorization.GetChatApprovalRequest(chatIdToReview) is { } pendingApproval)
        {
            chatDescription = pendingApproval.ChatDescription;
        }
        else if ((await chatAuthorization.GetAllAllowedChats()).FirstOrDefault(x => x.ChatId == chatIdToReview) is { } approvedChat)
        {
            chatDescription = approvedChat.ChatDescription;
        }

        bool isAuthorized = await chatAuthorization.IsAuthorized(chatIdToReview);

        var approvalMsg = $"""
                           ❓ Please review and user authorization

                           Chat ID: {chatIdToReview}
                           Chat Info: {chatDescription}
                           Is authorized: {isAuthorized}
                           """;

        var replyMarkup = new InlineKeyboardMarkup(
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "Allow",
                    AuthorizeChatCallbackCmdRequest.BuildData(AuthorizeChatCallbackCmdRequest.ApprovalReply.Allow, chatIdToReview, chatDescription)),
                InlineKeyboardButton.WithCallbackData(
                    "Deny",
                    AuthorizeChatCallbackCmdRequest.BuildData(AuthorizeChatCallbackCmdRequest.ApprovalReply.Deny, chatIdToReview, chatDescription)),
            }
        );

        await botClient.SendTextMessageAsync(adminChatId, approvalMsg, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
    }

    public async Task Handle(AuthorizeChatCallbackCmdRequest request, CancellationToken cancellationToken)
    {
        string approvalStatusLine;

        (AuthorizeChatCallbackCmdRequest.ApprovalReply approvalReply, ChatId chatId, string? chatDescription) =
            AuthorizeChatCallbackCmdRequest.ParseData(request.CallbackQuery.Data!);

        if (approvalReply == AuthorizeChatCallbackCmdRequest.ApprovalReply.Allow)
        {
            await chatAuthorization.AuthorizeChat(chatId, chatDescription);

            await botClient.SendTextMessageAsync(chatId, text: "Your bot usage was approved.\nPlease click here: /start", cancellationToken: cancellationToken);

            approvalStatusLine = "✅ Allowed to use bot!";
        }
        else
        {
            await chatAuthorization.RevokeChatAuthorization(chatId);

            approvalStatusLine = "❌ Denied to use bot!";
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
