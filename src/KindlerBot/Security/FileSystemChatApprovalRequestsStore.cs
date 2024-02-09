using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.IO;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace KindlerBot.Security;

internal class FileSystemChatApprovalRequestsStore : FileSystemStoreBase<FileSystemChatApprovalRequestsStore.StoreData>, IChatApprovalRequestsStore
{
    public FileSystemChatApprovalRequestsStore(IOptions<DeploymentConfiguration> deploymentConfig)
        : base("chatApprovalRequests.json", deploymentConfig)
    {
    }

    public async Task<IReadOnlyCollection<ChatApprovalRequest>> GetAllApprovalRequests()
    {
        var storeData = await GetStoreData();
        return storeData.ApprovalRequests.Select(x => x.ToApprovalRequest()).ToArray();
    }

    public async Task<ChatApprovalRequest?> GetApprovalRequest(ChatId chatId)
    {
        var storeData = await GetStoreData();
        return storeData.ApprovalRequests.FirstOrDefault(x => x.ChatId == chatId)?.ToApprovalRequest();
    }

    public async Task AddApprovalRequest(ChatApprovalRequest approvalRequest)
    {
        await UpdateStoreData(storeData =>
        {
            var existing = storeData.ApprovalRequests.FirstOrDefault(x => x.ChatId == approvalRequest.ChatId);
            if (existing != null)
            {
                return;
            }

            storeData.ApprovalRequests.Add(new StoreData.StoreApprovalRequest(
                ChatId: approvalRequest.ChatId.ToString(),
                ChatDescription: approvalRequest.ChatDescription,
                CreationTime: approvalRequest.CreationTime));
        });
    }

    public async Task RemoveRequest(ChatId chatId)
    {
        await UpdateStoreData(storeData =>
        {
            var chatIdStr = chatId.ToString();
            storeData.ApprovalRequests.RemoveAll(x => string.Equals(x.ChatId, chatIdStr));
        });
    }

    public async Task CleanObsoleteRequests(TimeSpan expiration)
    {
        await UpdateStoreData(storeData =>
        {
            var keepAfter = DateTimeOffset.Now - expiration;
            storeData.ApprovalRequests.RemoveAll(x => x.CreationTime < keepAfter);
        });
    }

    internal class StoreData
    {
        public List<StoreApprovalRequest> ApprovalRequests { get; } = new();

        public record StoreApprovalRequest(string ChatId, string? ChatDescription, DateTimeOffset CreationTime)
        {
            public ChatApprovalRequest ToApprovalRequest()
            {
                return new ChatApprovalRequest(new ChatId(ChatId), ChatDescription, CreationTime);
            }
        }
    }
}
