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
    public const string CmdPrefix = "/authorize_review_";

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
    public const string CallbackDataPrefix = "/authorize_callback_";

    public static string BuildData(ApprovalReply reply, ChatId chatId)
    {
        return $"{CallbackDataPrefix}{reply:G}_{chatId}";
    }

    public static (ApprovalReply reply, ChatId chatId) ParseData(string data)
    {
        if (!data.StartsWith(CallbackDataPrefix))
            throw new ArgumentException("Wrong data", nameof(data));

        data = data[CallbackDataPrefix.Length..];

        var parts = data.Split("_");

        return (
            Enum.Parse<ApprovalReply>(parts[0]),
            new ChatId(parts[1])
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

        await botClient.SendMessage(request.AdminChatId, replyBuilder.ToString(), cancellationToken: cancellationToken);
    }

    public async Task Handle(AuthorizeReviewCmdRequest request, CancellationToken cancellationToken)
    {
        var chatIdToReview = AuthorizeReviewCmdRequest.ParseData(request.CmdText);
        var adminChatId = request.AdminChatId;

        string? chatDescription = await TryResolveChatDescription(chatIdToReview);

        bool isAuthorized = await chatAuthorization.IsAuthorized(chatIdToReview);

        var approvalMsg = $"""
                           ❓ Please review and user authorization

                           Chat ID: {chatIdToReview}
                           Chat Info: {chatDescription}
                           Is authorized: {isAuthorized}
                           """;

        var replyMarkup = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData(
                "Allow",
                AuthorizeChatCallbackCmdRequest.BuildData(AuthorizeChatCallbackCmdRequest.ApprovalReply.Allow, chatIdToReview)),
            InlineKeyboardButton.WithCallbackData(
                "Deny",
                AuthorizeChatCallbackCmdRequest.BuildData(AuthorizeChatCallbackCmdRequest.ApprovalReply.Deny, chatIdToReview))
        );

        await botClient.SendMessage(adminChatId, approvalMsg, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
    }

    public async Task Handle(AuthorizeChatCallbackCmdRequest request, CancellationToken cancellationToken)
    {
        string approvalStatusLine;

        (AuthorizeChatCallbackCmdRequest.ApprovalReply approvalReply, ChatId chatId) = AuthorizeChatCallbackCmdRequest.ParseData(request.CallbackQuery.Data!);

        var chatDescription = await TryResolveChatDescription(chatId);

        if (approvalReply == AuthorizeChatCallbackCmdRequest.ApprovalReply.Allow)
        {
            await chatAuthorization.AuthorizeChat(chatId, chatDescription);

            await botClient.SendMessage(chatId, text: "Your bot usage was approved.\nPlease click here: /start", cancellationToken: cancellationToken);

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
            await botClient.EditMessageText(chatId: approveMsg.Chat, messageId: approveMsg.MessageId, text: approvalStatusMsg, cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQuery(request.CallbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task<string?> TryResolveChatDescription(ChatId chatIdToReview)
    {
        // Try getting description from either approval or already approved chat
        if (await chatAuthorization.GetChatApprovalRequest(chatIdToReview) is { } pendingApproval)
        {
            return pendingApproval.ChatDescription;
        }

        if ((await chatAuthorization.GetAllAllowedChats()).FirstOrDefault(x => x.ChatId == chatIdToReview) is { } approvedChat)
        {
            return approvedChat.ChatDescription;
        }

        return null;
    }
}
