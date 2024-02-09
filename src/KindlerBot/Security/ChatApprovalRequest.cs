using System;
using Telegram.Bot.Types;

namespace KindlerBot.Security;

public record ChatApprovalRequest(ChatId ChatId, string? ChatDescription, DateTimeOffset CreationTime);
